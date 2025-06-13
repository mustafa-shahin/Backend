using Backend.CMS.API.Filters;
using Backend.CMS.API.Middleware;
using Backend.CMS.Application.Common;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Events;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Jobs;
using Backend.CMS.Infrastructure.Mapping;
using Backend.CMS.Infrastructure.Repositories;
using Backend.CMS.Infrastructure.Services;
using FluentValidation;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

// Configure logging
ConfigureLogging(builder);

// Configure rate limiting
ConfigureRateLimiting(builder);

// Configure basic services
ConfigureBasicServices(builder);

// Configure databases
ConfigureDatabases(builder);

// Configure Redis caching
ConfigureRedis(builder);

// Configure Hangfire
ConfigureHangfire(builder);

// Configure CORS
ConfigureCors(builder);

// Configure Authentication & Authorization
ConfigureAuthentication(builder);

// Configure validation and mapping
ConfigureValidationAndMapping(builder);

// Register application services
RegisterServices(builder);

// Configure Swagger
ConfigureSwagger(builder);

// BUILD THE APP - Nothing that modifies services can come after this line
var app = builder.Build();

// Configure middleware pipeline
ConfigureMiddleware(app);

// Initialize databases and seed data
await InitializeDatabasesAsync(app);

// Configure HTTP request pipeline
ConfigureRequestPipeline(app);

// Configure endpoints
ConfigureEndpoints(app);

app.Run();

#region Configuration Methods

static void ConfigureLogging(WebApplicationBuilder builder)
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .WriteTo.File(
            new CompactJsonFormatter(),
            "logs/log-.json",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
        .CreateLogger();

    builder.Host.UseSerilog();
}

static void ConfigureRateLimiting(WebApplicationBuilder builder)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("AuthPolicy", configure =>
        {
            configure.PermitLimit = 5;
            configure.Window = TimeSpan.FromMinutes(1);
            configure.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            configure.QueueLimit = 2;
        });

        options.AddFixedWindowLimiter("ApiPolicy", configure =>
        {
            configure.PermitLimit = 100;
            configure.Window = TimeSpan.FromMinutes(1);
        });
    });
}

static void ConfigureBasicServices(WebApplicationBuilder builder)
{
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationActionFilter>();
    }).AddJsonOptions(options =>
    {
        // Configure JSON serialization for enums
        //options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });


    builder.Services.AddHttpContextAccessor();

    // Configure memory cache with size limits for file caching
    builder.Services.AddMemoryCache(options =>
    {
        // Set size limit to 500MB for file caching
        options.SizeLimit = 524288000; // 500MB in bytes
        options.CompactionPercentage = 0.9;
        options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
    });

    // Session configuration
    ConfigureSession(builder);

    builder.Services.AddEndpointsApiExplorer();
}

static void ConfigureDatabases(WebApplicationBuilder builder)
{
    builder.Services.AddDbContext<ApplicationDbContextWithCacheInvalidation>((serviceProvider, options) =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        options.UseNpgsql(connectionString);
    });

    // Register the base ApplicationDbContext to use the enhanced version
    builder.Services.AddScoped<ApplicationDbContext>(provider =>
        provider.GetRequiredService<ApplicationDbContextWithCacheInvalidation>());

    builder.Services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
}


static void ConfigureRedis(WebApplicationBuilder builder)
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        options.InstanceName = "BackendCMS";
    });

    builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    {
        var configuration = provider.GetService<IConfiguration>();
        var connectionString = configuration?.GetConnectionString("Redis") ?? "localhost:6379";
        return ConnectionMultiplexer.Connect(connectionString);
    });
}

static void ConfigureHangfire(WebApplicationBuilder builder)
{
    var hangfireConnectionString = builder.Configuration.GetConnectionString("HangfireConnection")
        ?? builder.Configuration.GetConnectionString("DefaultConnection");

    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(hangfireConnectionString, new PostgreSqlStorageOptions
        {
            QueuePollInterval = TimeSpan.FromSeconds(10),
            JobExpirationCheckInterval = TimeSpan.FromHours(1),
            CountersAggregateInterval = TimeSpan.FromMinutes(5),
            PrepareSchemaIfNecessary = true,
            TransactionSynchronisationTimeout = TimeSpan.FromMinutes(5)
        }));

    builder.Services.AddHangfireServer(options =>
    {
        options.Queues = ["default", "deployment", "template-sync", "maintenance"];
        options.WorkerCount = Environment.ProcessorCount * 2;
    });
}

static void ConfigureCors(WebApplicationBuilder builder)
{
    builder.Services.AddCors(options =>
    {
        // Default policy for development
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(
                    "http://localhost:3000",
                    "https://localhost:3000",
                    "http://localhost:3001",
                    "https://localhost:3001",
                    "http://127.0.0.1:3000",
                    "https://127.0.0.1:3000"
                  )
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });

        // Named policy for development with credentials support
        options.AddPolicy("Development", policy =>
        {
            policy.WithOrigins(
                    "http://localhost:3000",
                    "https://localhost:3000",
                    "http://localhost:3001",
                    "https://localhost:3001",
                    "http://127.0.0.1:3000",
                    "https://127.0.0.1:3000"
                  )
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });

        // Production policy
        options.AddPolicy("Production", policy =>
        {
            policy.WithOrigins("https://yourdomain.com")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });
}

static void ConfigureAuthentication(WebApplicationBuilder builder)
{
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"];

    if (string.IsNullOrEmpty(secretKey))
    {
        throw new InvalidOperationException("JWT SecretKey is not configured");
    }

    // Configure authentication with multiple schemes
    var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    });

    // Add JWT Bearer authentication
    authBuilder.AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = false; // Set to true in production
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };

        // Add event handlers for debugging
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine($"Token validated for: {context.Principal?.Identity?.Name}");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"Authentication challenge: {context.Error}, {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

    // Add Google OAuth if configured
    var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
    var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

    if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
    {
        authBuilder.AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            options.CallbackPath = "/api/auth/google/callback";
            options.SaveTokens = true;

            // Request additional scopes
            options.Scope.Add("profile");
            options.Scope.Add("email");

            // Map claims
            options.ClaimActions.MapJsonKey("picture", "picture");
            options.ClaimActions.MapJsonKey("verified_email", "verified_email");

            options.Events.OnCreatingTicket = async context =>
            {
                try
                {
                    // Get additional user info from Google API
                    var request = new HttpRequestMessage(HttpMethod.Get,
                        "https://www.googleapis.com/oauth2/v2/userinfo");
                    request.Headers.Authorization = new System.Net.Http.Headers
                        .AuthenticationHeaderValue("Bearer", context.AccessToken);

                    var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();

                    var userInfo = await response.Content.ReadAsStringAsync();
                    var user = System.Text.Json.JsonDocument.Parse(userInfo);

                    context.RunClaimActions(user.RootElement);
                }
                catch (Exception ex)
                {
                    // Log but don't fail the authentication
                    Console.WriteLine($"Error getting Google user info: {ex.Message}");
                }
            };
        });
    }

    // Add Facebook OAuth if configured
    var facebookAppId = builder.Configuration["Authentication:Facebook:AppId"];
    var facebookAppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];

    if (!string.IsNullOrEmpty(facebookAppId) && !string.IsNullOrEmpty(facebookAppSecret))
    {
        authBuilder.AddFacebook(options =>
        {
            options.AppId = facebookAppId;
            options.AppSecret = facebookAppSecret;
            options.CallbackPath = "/api/auth/facebook/callback";
            options.SaveTokens = true;

            // Request additional permissions
            options.Scope.Add("email");
            options.Scope.Add("public_profile");

            options.Fields.Add("name");
            options.Fields.Add("email");
            options.Fields.Add("picture");

            options.Events.OnCreatingTicket = async context =>
            {
                try
                {
                    // Get additional user info from Facebook Graph API
                    var request = new HttpRequestMessage(HttpMethod.Get,
                        "https://graph.facebook.com/me?fields=id,name,email,picture.type(large)");
                    request.Headers.Authorization = new System.Net.Http.Headers
                        .AuthenticationHeaderValue("Bearer", context.AccessToken);

                    var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();

                    var userInfo = await response.Content.ReadAsStringAsync();
                    var user = System.Text.Json.JsonDocument.Parse(userInfo);

                    context.RunClaimActions(user.RootElement);
                }
                catch (Exception ex)
                {
                    // Log but don't fail the authentication
                    Console.WriteLine($"Error getting Facebook user info: {ex.Message}");
                }
            };
        });
    }

    builder.Services.AddAuthorization();
}

static void ConfigureValidationAndMapping(WebApplicationBuilder builder)
{
    // Register AutoMapper
    builder.Services.AddAutoMapper(typeof(MappingProfile));

    // Register FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // Register MediatR
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
}

static void ConfigureSession(WebApplicationBuilder builder)
{
    // Configure distributed cache for session storage
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        options.InstanceName = "BackendCMS";
    });

    // Configure session with Redis backing store
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(int.Parse(builder.Configuration["SessionSettings:TimeoutMinutes"] ?? "30"));
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Name = "BackendCMS.SessionId";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Use Secure in production

        // Set a longer cookie timeout than session timeout to allow session refresh
        options.Cookie.MaxAge = TimeSpan.FromDays(7);
    });

    // Add background service for session cleanup
    builder.Services.AddHostedService<SessionCleanupService>();
}


static void RegisterServices(WebApplicationBuilder builder)
{
    RegisterRepositories(builder);
    RegisterCoreServices(builder);
    RegisterBusinessServices(builder);
    RegisterCachingServices(builder);
    RegisterSearchServices(builder);
    RegisterFileServices(builder);
    RegisterBackgroundJobs(builder);
    RegisterSocialAuthServices(builder);
    RegisterProductServices(builder);
}

static void RegisterRepositories(WebApplicationBuilder builder)
{
    // Generic repository
    builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

    // Specialized repositories
    builder.Services.AddScoped<IPageRepository, PageRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<ILocationRepository, LocationRepository>();

    // Entity repositories
    builder.Services.AddScoped<IRepository<UserSession>, Repository<UserSession>>();
    builder.Services.AddScoped<IRepository<PasswordResetToken>, Repository<PasswordResetToken>>();
    builder.Services.AddScoped<IRepository<Company>, Repository<Company>>();
    builder.Services.AddScoped<IRepository<Location>, Repository<Location>>();
    builder.Services.AddScoped<IRepository<LocationOpeningHour>, Repository<LocationOpeningHour>>();
    builder.Services.AddScoped<IRepository<Address>, Repository<Address>>();
    builder.Services.AddScoped<IRepository<ContactDetails>, Repository<ContactDetails>>();
    builder.Services.AddScoped<IRepository<ComponentTemplate>, Repository<ComponentTemplate>>();
    builder.Services.AddScoped<IRepository<PageComponent>, Repository<PageComponent>>();
    builder.Services.AddScoped<IRepository<PageVersion>, Repository<PageVersion>>();
    builder.Services.AddScoped<IRepository<FileEntity>, Repository<FileEntity>>();
    builder.Services.AddScoped<IRepository<Folder>, Repository<Folder>>();
    builder.Services.AddScoped<IRepository<Backend.CMS.Domain.Entities.FileAccess>, Repository<Backend.CMS.Domain.Entities.FileAccess>>();
    builder.Services.AddScoped<IRepository<Permission>, Repository<Permission>>();
    builder.Services.AddScoped<IRepository<RolePermission>, Repository<RolePermission>>();
    builder.Services.AddScoped<IRepository<UserPermission>, Repository<UserPermission>>();
    builder.Services.AddScoped<IRepository<SearchIndex>, Repository<SearchIndex>>();
    builder.Services.AddScoped<IRepository<IndexingJob>, Repository<IndexingJob>>();
    builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
}

static void RegisterSocialAuthServices(WebApplicationBuilder builder)
{
    builder.Services.AddScoped<IRepository<UserExternalLogin>, Repository<UserExternalLogin>>();
    builder.Services.AddScoped<ISocialAuthService, SocialAuthService>();
    builder.Services.AddHttpClient<SocialAuthService>();
}

static void RegisterCoreServices(WebApplicationBuilder builder)
{
    // Core services (order matters for dependencies)
    builder.Services.AddScoped<IUserSessionService, UserSessionService>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IAddressService, AddressService>();
    builder.Services.AddScoped<IContactDetailsService, ContactDetailsService>();
}

static void RegisterBusinessServices(WebApplicationBuilder builder)
{
    // Business services
    builder.Services.AddScoped<IComponentService, ComponentService>();
    builder.Services.AddScoped<ICompanyService, CompanyService>();
    builder.Services.AddScoped<ILocationService, LocationService>();
    builder.Services.AddScoped<IPageService, PageService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>();
    builder.Services.AddScoped<IPermissionResolver, PermissionResolver>();
}

static void RegisterCachingServices(WebApplicationBuilder builder)
{
    builder.Services.AddScoped<ICacheService, CacheService>();

    builder.Services.AddScoped<ICacheInvalidationService, CacheService>();

    builder.Services.AddScoped<CacheService>();
    builder.Services.AddScoped<ICacheService>(provider => provider.GetService<CacheService>());
    builder.Services.AddScoped<ICacheInvalidationService>(provider => provider.GetService<CacheService>());
}

static void RegisterSearchServices(WebApplicationBuilder builder)
{
    builder.Services.AddScoped<IIndexingService, IndexingService>();
    builder.Services.AddScoped<ISearchService, SearchService>();
}

static void RegisterFileServices(WebApplicationBuilder builder)
{
    builder.Services.AddScoped<FileService>();
    builder.Services.AddScoped<IFileCachingService, FileCachingService>();
    builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
    builder.Services.AddScoped<IFileValidationService, FileValidationService>();
    builder.Services.AddScoped<IFolderService, FolderService>();
    builder.Services.AddScoped<DatabaseFilePerformanceService>();
    builder.Services.AddScoped<IDownloadTokenService, DownloadTokenService>();
    builder.Services.AddScoped<IFileService>(provider =>
    {
        var baseService = provider.GetRequiredService<FileService>();
        var cachingService = provider.GetRequiredService<IFileCachingService>();
        var logger = provider.GetRequiredService<ILogger<CachedFileService>>();

        return new CachedFileService(baseService, cachingService, logger);
    });
}

static void RegisterBackgroundJobs(WebApplicationBuilder builder)
{
    builder.Services.AddScoped<IndexingBackgroundJob>();
}

static void RegisterProductServices(WebApplicationBuilder builder)
{
    // Product catalog repositories
    builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
    builder.Services.AddScoped<IProductRepository, ProductRepository>();
    builder.Services.AddScoped<IProductVariantRepository, ProductVariantRepository>();

    // Product catalog services
    builder.Services.AddScoped<ICategoryService, CategoryService>();
    builder.Services.AddScoped<IProductService, ProductService>();
    builder.Services.AddScoped<IProductVariantService, ProductVariantService>();

    // Additional entity repositories
    builder.Services.AddScoped<IRepository<ProductCategory>, Repository<ProductCategory>>();
    builder.Services.AddScoped<IRepository<ProductImage>, Repository<ProductImage>>();
    builder.Services.AddScoped<IRepository<ProductOption>, Repository<ProductOption>>();
    builder.Services.AddScoped<IRepository<ProductOptionValue>, Repository<ProductOptionValue>>();
}

static void ConfigureSwagger(WebApplicationBuilder builder)
{
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Backend CMS API",
            Version = "v1",
            Description = "Multi-tenant CMS API with page builder functionality and job management"
        });

        // Add JWT authentication to Swagger
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });
}
#endregion

#region Middleware Configuration

static void ConfigureMiddleware(WebApplication app)
{
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseRateLimiter();
}
static void ConfigureRequestPipeline(WebApplication app)
{
    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Backend CMS API V1");
            c.RoutePrefix = "swagger";
        });
    }

    // Configure Hangfire Dashboard
    app.UseHangfireDashboard("/jobs", new DashboardOptions
    {
        Authorization = [new HangfireAuthorizationFilter()],
        DisplayStorageConnectionString = false,
        DashboardTitle = "Backend CMS Jobs"
    });

    // CRITICAL: Proper middleware order
    // 1. CORS must come before Authentication and Authorization
    if (app.Environment.IsDevelopment())
    {
        app.UseCors("Development");
    }
    else
    {
        app.UseCors("Production");
    }

    // 2. Session MUST come after CORS
    app.UseSession();

    // 3. Authentication and Authorization AFTER CORS and Session
    app.UseAuthentication();
    app.UseAuthorization();

    // Debug middleware (only in development)
    if (app.Environment.IsDevelopment())
    {
        app.Use(async (context, next) =>
        {
            Console.WriteLine($"=== REQUEST: {context.Request.Method} {context.Request.Path} ===");
            Console.WriteLine($"Host: {context.Request.Host}");
            Console.WriteLine($"Origin: {context.Request.Headers.Origin}");
            Console.WriteLine($"Session ID: {context.Session.Id}");
            Console.WriteLine($"Is Authenticated: {context.User?.Identity?.IsAuthenticated}");

            // Check if this is a preflight request
            if (context.Request.Method == "OPTIONS")
            {
                Console.WriteLine("=== PREFLIGHT REQUEST DETECTED ===");
                Console.WriteLine($"Access-Control-Request-Method: {context.Request.Headers["Access-Control-Request-Method"]}");
                Console.WriteLine($"Access-Control-Request-Headers: {context.Request.Headers["Access-Control-Request-Headers"]}");
            }

            await next();

            Console.WriteLine($"=== RESPONSE: {context.Response.StatusCode} ===");
        });
    }
}

static void ConfigureEndpoints(WebApplication app)
{
    app.MapControllers();

    // Admin-controlled job management endpoints
    app.MapPost("/admin/jobs/emergency-stop-all", async (IServiceProvider serviceProvider) =>
    {
        // Emergency stop all running jobs (admin only)
        using var scope = serviceProvider.CreateScope();
        var hangfireClient = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();

        // This would require additional implementation to stop running jobs
        return Results.Ok("Emergency stop initiated - check Hangfire dashboard");
    }).RequireAuthorization();

    // Initialize Hangfire database
    try
    {
        using var scope = app.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var hangfireConn = configuration.GetConnectionString("HangfireConnection")
            ?? configuration.GetConnectionString("DefaultConnection")?.Replace("{TENANT_ID}", "hangfire");

        // Ensure Hangfire database exists
        GlobalConfiguration.Configuration.UsePostgreSqlStorage(hangfireConn);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to initialize Hangfire database");
    }
}

#endregion

#region Database Initialization

static async Task InitializeDatabasesAsync(WebApplication app)
{
    await InitializeDatabaseConnectionsAsync(app);
    await InitializeDatabaseSchemaAsync(app);
    await SeedDefaultDataAsync(app);
    await SeedPermissionsAsync(app);
}

static async Task InitializeDatabaseConnectionsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var databaseInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();

    try
    {
        // Ensure main database exists
        var defaultConnectionString = app.Configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(defaultConnectionString))
        {
            var defaultBuilder = new Npgsql.NpgsqlConnectionStringBuilder(defaultConnectionString);
            await databaseInitializer.EnsureDatabaseExistsAsync(defaultConnectionString,
                defaultBuilder.Database ?? "backend_cms");
        }

        // Ensure Hangfire database exists
        await databaseInitializer.EnsureHangfireDatabaseExistsAsync();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Failed to initialize databases");
        throw;
    }
}

static async Task InitializeDatabaseSchemaAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "An error occurred while migrating the database");
        throw;
    }
}

static async Task SeedDefaultDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedDatabase(context);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "An error occurred while seeding the database");
        throw;
    }
}

static async Task SeedPermissionsAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var permissionService = scope.ServiceProvider.GetService<IPermissionService>();
        if (permissionService != null)
        {
            await permissionService.SeedDefaultPermissionsAsync();
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to seed permissions - they may already exist");
    }
}

#endregion

#region Database Seeding Methods

// Database seeding method
static async Task SeedDatabase(ApplicationDbContext context)
{
    try
    {
        // First, seed default admin user WITHOUT audit trail dependencies
        if (!context.Users.Any())
        {
            var adminUser = new User
            {
                Email = "mustafashahin988@gmail.com",
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("@Admin12345678"),
                FirstName = "Mustafa",
                LastName = "Shahin",
                IsActive = true,
                EmailVerifiedAt = DateTime.UtcNow,
                Role = UserRole.Dev,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                // Set audit fields to NULL for initial seed
                CreatedByUserId = null,
                UpdatedByUserId = null
            };

            context.Users.Add(adminUser);
            await context.SaveChangesAsync();

            var adminUserId = adminUser.Id;

            // using the admin user ID for subsequent entities
            await SeedCompanyData(context, adminUserId);
            await SeedComponentTemplates(context, adminUserId);
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error occurred while seeding database");
        throw;
    }
}

static async Task SeedCompanyData(ApplicationDbContext context, int adminUserId)
{
    // Seed default company
    if (!context.Companies.Any())
    {
        var company = new Company
        {
            Name = "Default Company",
            Description = "Default company",
            IsActive = true,
            Currency = "EUR",
            Language = "en",
            Timezone = "UTC",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUserId,
            UpdatedByUserId = adminUserId
        };

        context.Companies.Add(company);
        await context.SaveChangesAsync();

        // Add default address for company
        var companyAddress = new Address
        {
            Street = "123 Business Street",
            City = "Business City",
            State = "Business State",
            Country = "Business Country",
            PostalCode = "12345",
            AddressType = "Main",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUserId,
            UpdatedByUserId = adminUserId
        };

        context.Entry(companyAddress).Property("CompanyId").CurrentValue = company.Id;
        context.Addresses.Add(companyAddress);

        // Add default contact details for company
        var companyContact = new ContactDetails
        {
            PrimaryPhone = "+1234567890",
            Email = "info@company.com",
            Website = "https://company.com",
            ContactType = "Business",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUserId,
            UpdatedByUserId = adminUserId
        };

        context.Entry(companyContact).Property("CompanyId").CurrentValue = company.Id;
        context.ContactDetails.Add(companyContact);

        var mainLocation = new Location
        {
            CompanyId = company.Id,
            Name = "Main Office",
            Description = "Main company office",
            LocationCode = "MAIN001",
            LocationType = "Headquarters",
            IsMainLocation = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUserId,
            UpdatedByUserId = adminUserId
        };
        context.Locations.Add(mainLocation);

        await context.SaveChangesAsync();

        var locationAddress = new Address
        {
            Street = "456 Office Avenue",
            City = "Office City",
            State = "Office State",
            Country = "Office Country",
            PostalCode = "67890",
            AddressType = "Office",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUserId,
            UpdatedByUserId = adminUserId
        };

        context.Entry(locationAddress).Property("LocationId").CurrentValue = mainLocation.Id;
        context.Addresses.Add(locationAddress);

        // Add contact details for main location
        var locationContact = new ContactDetails
        {
            PrimaryPhone = "+1987654321",
            Email = "office@company.com",
            ContactType = "Office",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = adminUserId,
            UpdatedByUserId = adminUserId
        };

        context.Entry(locationContact).Property("LocationId").CurrentValue = mainLocation.Id;
        context.ContactDetails.Add(locationContact);

        // Add opening hours for main location
        for (int day = 0; day < 7; day++)
        {
            var openingHour = new LocationOpeningHour
            {
                LocationId = mainLocation.Id,
                DayOfWeek = (DayOfWeek)day,
                OpenTime = new TimeOnly(9, 0),
                CloseTime = new TimeOnly(17, 0),
                IsClosed = day == 0 || day == 6, // Closed on Sunday (0) and Saturday (6)
                IsOpen24Hours = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = adminUserId,
                UpdatedByUserId = adminUserId
            };
            context.LocationOpeningHours.Add(openingHour);
        }

        await context.SaveChangesAsync();
    }
}

static async Task SeedComponentTemplates(ApplicationDbContext context, int adminUserId)
{
    // Seed component templates
    if (!context.ComponentTemplates.Any())
    {
        var componentTemplates = new[]
        {
            new ComponentTemplate
            {
                Name = "text-block",
                DisplayName = "Text Block",
                Description = "Simple text content block",
                Type = ComponentType.Text,
                Category = "Content",
                Icon = "type",
                IsSystemTemplate = true,
                IsActive = true,
                SortOrder = 1,
                DefaultProperties = new Dictionary<string, object>
                {
                    { "text", "Enter your text here..." },
                    { "fontSize", "16px" },
                    { "fontWeight", "normal" },
                    { "textAlign", "left" }
                },
                DefaultStyles = new Dictionary<string, object>
                {
                    { "margin", "0" },
                    { "padding", "16px" }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = adminUserId,
                UpdatedByUserId = adminUserId
            },
            new ComponentTemplate
            {
                Name = "image-block",
                DisplayName = "Image",
                Description = "Image display component",
                Type = ComponentType.Image,
                Category = "Media",
                Icon = "image",
                IsSystemTemplate = true,
                IsActive = true,
                SortOrder = 2,
                DefaultProperties = new Dictionary<string, object>
                {
                    { "src", "" },
                    { "alt", "Image" },
                    { "width", "100%" },
                    { "height", "auto" }
                },
                DefaultStyles = new Dictionary<string, object>
                {
                    { "display", "block" },
                    { "maxWidth", "100%" }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = adminUserId,
                UpdatedByUserId = adminUserId
            },
            new ComponentTemplate
            {
                Name = "button",
                DisplayName = "Button",
                Description = "Clickable button component",
                Type = ComponentType.Button,
                Category = "Interactive",
                Icon = "hand-pointer",
                IsSystemTemplate = true,
                IsActive = true,
                SortOrder = 3,
                DefaultProperties = new Dictionary<string, object>
                {
                    { "text", "Click Me" },
                    { "type", "button" },
                    { "variant", "primary" },
                    { "size", "medium" }
                },
                DefaultStyles = new Dictionary<string, object>
                {
                    { "padding", "12px 24px" },
                    { "border", "none" },
                    { "borderRadius", "4px" },
                    { "cursor", "pointer" }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = adminUserId,
                UpdatedByUserId = adminUserId
            },
            new ComponentTemplate
            {
                Name = "container",
                DisplayName = "Container",
                Description = "Layout container for other components",
                Type = ComponentType.Container,
                Category = "Layout",
                Icon = "square",
                IsSystemTemplate = true,
                IsActive = true,
                SortOrder = 4,
                DefaultProperties = new Dictionary<string, object>
                {
                    { "maxWidth", "1200px" },
                    { "centered", true }
                },
                DefaultStyles = new Dictionary<string, object>
                {
                    { "margin", "0 auto" },
                    { "padding", "20px" }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = adminUserId,
                UpdatedByUserId = adminUserId
            }
        };

        context.ComponentTemplates.AddRange(componentTemplates);
        await context.SaveChangesAsync();
    }
}

#endregion

#region Support Classes

// Hangfire authorization filter - Simplified implementation
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // For development, allow all access
        // In production, proper authorization should be implemented 
        return true; // This must be changed to implement proper security in production
    }
}

#endregion

public class SessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1); // Run cleanup every hour

    public SessionCleanupService(IServiceProvider serviceProvider, ILogger<SessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredSessions();
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during session cleanup");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait 5 minutes before retrying
            }
        }
    }

    private async Task CleanupExpiredSessions()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            // Change ICacheService to ICacheInvalidationService
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheInvalidationService>();

            // Get all session keys
            var sessionKeys = await cacheService.GetCacheKeysAsync("session:*");
            var expiredKeys = new List<string>();

            foreach (var key in sessionKeys)
            {
                try
                {
                    var session = await ((ICacheService)cacheService).GetAsync<UserSessionContext>(key);
                    if (session != null && session.IsSessionExpired(TimeSpan.FromMinutes(30)))
                    {
                        expiredKeys.Add(key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking session {Key} for expiration", key);
                    expiredKeys.Add(key); // Remove invalid sessions
                }
            }

            if (expiredKeys.Count > 0)
            {
                await ((ICacheService)cacheService).RemoveAsync(expiredKeys);
                _logger.LogInformation("Cleaned up {Count} expired sessions", expiredKeys.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session cleanup");
        }
    }
}