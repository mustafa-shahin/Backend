// src/Backend.CMS.API/Program.cs
using Backend.CMS.API.HealthChecks;
using Backend.CMS.API.Middleware;
using Backend.CMS.API.Services;
using Backend.CMS.Application.Common.Interfaces;
using Backend.CMS.Audit.Services;
using Backend.CMS.Caching.Extensions;
using Backend.CMS.Caching.Services;
using Backend.CMS.Infrastructure.Configuration;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Repositories;
using Backend.CMS.Security.Extensions;
using Backend.CMS.Security.Middleware;
using Backend.CMS.Security.Policies;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using System.Text;
using System.Text.Json;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .WriteTo.File("Logs/cms-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog
builder.Host.UseSerilog();

// Bind configuration
var appSettings = new AppSettings();
builder.Configuration.Bind(appSettings);
builder.Services.Configure<AppSettings>(builder.Configuration);

try
{
    Log.Information("Starting CMS API application");

    // Add services
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.WriteIndented = false;
        });

    builder.Services.AddEndpointsApiExplorer();

    // Configure Swagger with comprehensive documentation
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "CMS API",
            Version = "v1",
            Description = "Production-ready CMS API for multi-tenant content management",
            Contact = new OpenApiContact
            {
                Name = "CMS Team",
                Email = "support@cms.com"
            },
            License = new OpenApiLicense
            {
                Name = "MIT License"
            }
        });

        // JWT Authentication
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        // API Key Authentication
        c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Description = "API Key authorization header. Example: \"X-API-Key: {apikey}\"",
            Name = "X-API-Key",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey
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

        // Include XML comments if available
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    });

    // Configure CORS with environment-specific settings
    builder.Services.AddCors(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.AddPolicy("Development", policy =>
            {
                policy.WithOrigins("http://localhost:3000", "http://localhost:5000", "https://localhost:7000")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .WithExposedHeaders("X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset");
            });
        }
        else
        {
            options.AddPolicy("Production", policy =>
            {
                policy.WithOrigins(appSettings.Security.AllowedOrigins)
                      .WithHeaders("Authorization", "Content-Type", "X-Tenant-Id", "X-API-Key")
                      .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                      .AllowCredentials()
                      .WithExposedHeaders("X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset");
            });
        }
    });

    // Configure Authentication with enhanced settings
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = appSettings.Security.JwtSecret,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.Security.JwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(5),
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("Authentication failed: {Error}", context.Exception.Message);
                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                {
                    context.Response.Headers.Append("Token-Expired", "true");
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Debug("Token validated for user {UserId}",
                    context.Principal?.Identity?.Name);
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                // Support token from query string for SignalR connections
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

    // Configure Authorization with custom policies
    builder.Services.AddAuthorization(options =>
    {
        // Tenant-based policies
        options.AddPolicy("TenantAccess", policy =>
            policy.Requirements.Add(new TenantRequirement("current")));

        // Permission-based policies
        options.AddPolicy("CanViewPages", policy =>
            policy.Requirements.Add(new PermissionRequirement("Pages", "View")));

        options.AddPolicy("CanEditPages", policy =>
            policy.Requirements.Add(new PermissionRequirement("Pages", "Edit")));

        options.AddPolicy("CanDeletePages", policy =>
            policy.Requirements.Add(new PermissionRequirement("Pages", "Delete")));

        options.AddPolicy("CanPublishPages", policy =>
            policy.Requirements.Add(new PermissionRequirement("Pages", "Publish")));

        // Admin-only policies
        options.AddPolicy("AdminOnly", policy =>
            policy.RequireRole("Admin"));

        // API Key policies
        options.AddPolicy("ApiKeyAccess", policy =>
            policy.RequireAuthenticatedUser()
                  .RequireClaim("type", "api_key"));
    });

    // Register authorization handlers
    builder.Services.AddScoped<IAuthorizationHandler, TenantAuthorizationHandler>();
    builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

    // Add caching with production configuration
    var cacheSettings = appSettings.Cache;
    builder.Services.AddCaching(cacheSettings.RedisConnectionString);

    // Add security services
    builder.Services.AddSecurityServices();

    // Add audit services
    builder.Services.AddScoped<IAuditService, AuditService>();

    // Add comprehensive health checks
    var healthChecksBuilder = builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready", "database" })
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "postgresql",
            timeout: TimeSpan.FromSeconds(10),
            tags: new[] { "ready", "database" });

    // Add Redis health check if configured
    if (!string.IsNullOrEmpty(cacheSettings.RedisConnectionString))
    {
        healthChecksBuilder.AddRedis(
            cacheSettings.RedisConnectionString,
            name: "redis",
            timeout: TimeSpan.FromSeconds(5),
            tags: new[] { "ready", "cache" });
    }

    // Add custom health checks
    healthChecksBuilder
        .AddCheck("memory", () =>
        {
            var allocatedMB = GC.GetTotalMemory(false) / 1024 / 1024;
            return allocatedMB < 500 ? HealthCheckResult.Healthy($"Memory usage: {allocatedMB}MB")
                                     : HealthCheckResult.Degraded($"High memory usage: {allocatedMB}MB");
        }, tags: new[] { "ready" })
        .AddCheck("disk_space", () =>
        {
            var drive = new DriveInfo(Path.GetPathRoot(Directory.GetCurrentDirectory())!);
            var freeSpaceGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
            return freeSpaceGB > 1 ? HealthCheckResult.Healthy($"Free disk space: {freeSpaceGB}GB")
                                   : HealthCheckResult.Unhealthy($"Low disk space: {freeSpaceGB}GB");
        }, tags: new[] { "ready" });

    // Add multi-tenancy services
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ITenantService, CustomerTenantService>();
    builder.Services.AddScoped<CustomerTenantMiddleware>();

    // Add middleware services
    builder.Services.AddScoped<SecurityAuditMiddleware>();
    builder.Services.AddScoped<TenantSecurityMiddleware>();
    builder.Services.AddScoped<ApiKeyAuthenticationMiddleware>();

    // Add repositories with caching decorators
    builder.Services.AddScoped<PageRepository>();
    builder.Services.AddScoped<IPageRepository>(provider =>
    {
        var inner = provider.GetRequiredService<PageRepository>();
        var cache = provider.GetRequiredService<ICacheService>();
        return new CachedPageRepository(inner, cache);
    });

    // Configure tenant-specific DbContext with advanced options
    builder.Services.AddDbContext<CmsDbContext>((serviceProvider, options) =>
    {
        var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        // For design-time scenarios, use default connection
        if (httpContextAccessor?.HttpContext == null)
        {
            var defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration.GetConnectionString("Tenant_demo");
            ConfigureNpgsql(options, defaultConnectionString!);
            return;
        }

        var tenantService = serviceProvider.GetRequiredService<ITenantService>();
        var tenantId = tenantService.GetCurrentTenantId();

        if (string.IsNullOrEmpty(tenantId))
        {
            var defaultConnectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(defaultConnectionString))
            {
                ConfigureNpgsql(options, defaultConnectionString);
                return;
            }
            throw new InvalidOperationException("Tenant ID is required");
        }

        var connectionString = configuration.GetConnectionString($"Tenant_{tenantId}")
                            ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"Connection string not found for tenant '{tenantId}'");
        }

        ConfigureNpgsql(options, connectionString);
    });

    // Configure master database context
    builder.Services.AddDbContext<MasterDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("MasterDatabase");
        ConfigureNpgsql(options, connectionString!);
    });

    // Add MediatR with enhanced configuration
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(Backend.CMS.Application.Features.Pages.Commands.CreatePageCommand).Assembly);
        // Add pipeline behaviors for validation, logging, caching, etc.
    });

    // Add AutoMapper
    builder.Services.AddAutoMapper(typeof(Backend.CMS.Application.Features.Pages.Mappings.PageMappingProfile).Assembly);

    // Add FluentValidation
    builder.Services.AddValidatorsFromAssembly(typeof(Backend.CMS.Application.Features.Pages.Validators.CreatePageValidator).Assembly);

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API V1");
            c.RoutePrefix = "swagger";
            c.DisplayRequestDuration();
            c.EnableDeepLinking();
            c.EnableValidator();
        });
    }
    else
    {
        app.UseHsts();
    }

    // Security headers (should be first)
    app.UseMiddleware<SecurityHeadersMiddleware>();

    app.UseHttpsRedirection();

    // CORS
    app.UseCors(builder.Environment.IsDevelopment() ? "Development" : "Production");

    // Custom middleware pipeline
    app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    app.UseMiddleware<Backend.CMS.API.Middleware.RateLimitingMiddleware>();
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<SecurityAuditMiddleware>();

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Tenant middleware (after auth)
    app.UseMiddleware<CustomerTenantMiddleware>();
    app.UseMiddleware<TenantSecurityMiddleware>();

    // Health checks with detailed responses
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                environment = app.Environment.EnvironmentName,
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                timestamp = DateTimeOffset.UtcNow,
                checks = report.Entries.Select(x => new
                {
                    name = x.Key,
                    status = x.Value.Status.ToString(),
                    description = x.Value.Description,
                    duration = x.Value.Duration.ToString(),
                    exception = x.Value.Exception?.Message,
                    data = x.Value.Data,
                    tags = x.Value.Tags
                }),
                totalDuration = report.TotalDuration.ToString()
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
        }
    });

    // Readiness check (subset of health checks)
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { status = report.Status.ToString() }));
        }
    });

    // Liveness check (basic check)
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { status = "Healthy" }));
        }
    });

    app.MapControllers();

    // Ensure wwwroot directory exists for static files
    var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    if (!Directory.Exists(wwwrootPath))
    {
        Directory.CreateDirectory(wwwrootPath);
    }

    // Serve static files and SPA fallback
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");

    Log.Information("CMS API started successfully on {Environment}", app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CMS API failed to start");
}
finally
{
    Log.CloseAndFlush();
}

// Helper method for PostgreSQL configuration
static void ConfigureNpgsql(DbContextOptionsBuilder options, string connectionString)
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(30);
    });

    // Configure for production optimization
    options.EnableSensitiveDataLogging(false);
    options.EnableDetailedErrors(false);
    options.EnableServiceProviderCaching();
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
}