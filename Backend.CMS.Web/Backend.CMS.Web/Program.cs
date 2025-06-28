using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Blazor.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Mapping;
using Backend.CMS.Infrastructure.Repositories;
using Backend.CMS.Infrastructure.Services;
using Backend.CMS.Web.Components;
using Backend.CMS.Web.Services;
using Blazored.LocalStorage;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/blazor-log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add Blazor components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure AutoMapper - using the assembly that contains MappingProfile
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

// Configure FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(MappingProfile).Assembly);

// Configure MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(MappingProfile).Assembly);
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
});

// Add authentication services
ConfigureAuthentication(builder);

// Add authorization
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("AdminOrDev", policy =>
        policy.RequireRole("Admin", "Dev"));

    options.AddPolicy("DevOnly", policy =>
        policy.RequireRole("Dev"));
});

// Register repositories
RegisterRepositories(builder.Services);

// Register application services
RegisterApplicationServices(builder.Services);

// Register Blazor-specific services
RegisterBlazorServices(builder.Services);

// Add HTTP client for API calls
builder.Services.AddHttpClient("API", client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7001";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add authentication endpoints
app.MapPost("/api/auth/login", async (LoginRequest request, IAuthService authService) =>
{
    try
    {
        var loginDto = new Backend.CMS.Application.DTOs.LoginDto
        {
            Email = request.Email,
            Password = request.Password,
            RememberMe = request.RememberMe
        };

        var result = await authService.LoginAsync(loginDto);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Login error for {Email}", request.Email);
        return Results.BadRequest(new { Message = "Authentication failed" });
    }
});

app.MapPost("/api/auth/logout", async (IAuthService authService, HttpContext context) =>
{
    try
    {
        // Get user ID from claims if available
        var userIdClaim = context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
        {
            await authService.LogoutAsync(userId);
        }
        return Results.Ok(new { Message = "Logged out successfully" });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Logout error");
        return Results.BadRequest(new { Message = "Logout failed" });
    }
});

// Initialize database
await InitializeDatabase(app);

app.Run();

static void ConfigureAuthentication(WebApplicationBuilder builder)
{
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"];

    if (string.IsNullOrEmpty(secretKey))
    {
        throw new InvalidOperationException("JWT SecretKey is not configured");
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();
}

static void RegisterRepositories(IServiceCollection services)
{
    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    services.AddScoped<IPageRepository, PageRepository>();
    services.AddScoped<IUserRepository, UserRepository>();
    services.AddScoped<ILocationRepository, LocationRepository>();
    services.AddScoped<ICompanyRepository, CompanyRepository>();
    services.AddScoped<ICategoryRepository, CategoryRepository>();
    services.AddScoped<IProductRepository, ProductRepository>();
    services.AddScoped<IProductVariantRepository, ProductVariantRepository>();

    // Additional entity repositories
    services.AddScoped<IRepository<UserSession>, Repository<UserSession>>();
    services.AddScoped<IRepository<PasswordResetToken>, Repository<PasswordResetToken>>();
    services.AddScoped<IRepository<Company>, Repository<Company>>();
    services.AddScoped<IRepository<Location>, Repository<Location>>();
    services.AddScoped<IRepository<Address>, Repository<Address>>();
    services.AddScoped<IRepository<ContactDetails>, Repository<ContactDetails>>();
    services.AddScoped<IRepository<FileEntity>, Repository<FileEntity>>();
    services.AddScoped<IRepository<Permission>, Repository<Permission>>();
    services.AddScoped<IRepository<RolePermission>, Repository<RolePermission>>();
    services.AddScoped<IRepository<UserPermission>, Repository<UserPermission>>();
}

static void RegisterApplicationServices(IServiceCollection services)
{
    // Core services
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<IUserService, UserService>();
    services.AddScoped<IPageService, PageService>();
    services.AddScoped<IUserSessionService, UserSessionService>();
    services.AddScoped<IEmailService, EmailService>();
    services.AddScoped<IAddressService, AddressService>();
    services.AddScoped<IContactDetailsService, ContactDetailsService>();
    services.AddScoped<ICompanyService, CompanyService>();
    services.AddScoped<ILocationService, LocationService>();
    services.AddScoped<IPermissionService, PermissionService>();
    services.AddScoped<IPermissionResolver, PermissionResolver>();

    // Product services
    services.AddScoped<ICategoryService, CategoryService>();
    services.AddScoped<IProductService, ProductService>();
    services.AddScoped<IProductVariantService, ProductVariantService>();

    // Cache services
    services.AddScoped<CacheService>();
    services.AddScoped<ICacheService>(provider => provider.GetRequiredService<CacheService>());
    services.AddScoped<ICacheInvalidationService>(provider => provider.GetRequiredService<CacheService>());

    // File services
    services.AddScoped<FileService>();
    services.AddScoped<IImageProcessingService, ImageProcessingService>();
    services.AddScoped<IFileValidationService, FileValidationService>();
    services.AddScoped<IDownloadTokenService, DownloadTokenService>();
    services.AddScoped<IFileService>(provider => provider.GetRequiredService<FileService>());

    // Search services
    services.AddScoped<IIndexingService, IndexingService>();
    services.AddScoped<ISearchService, SearchService>();
}

static void RegisterBlazorServices(IServiceCollection services)
{
    services.AddBlazoredLocalStorage();
    services.AddScoped<IThemeService, ThemeService>();
    services.AddScoped<INotificationService, NotificationService>();
    services.AddScoped<IDialogService, DialogService>();

    // Add memory cache
    services.AddMemoryCache();

    // Add distributed cache (in-memory for now)
    services.AddDistributedMemoryCache();
}

static async Task InitializeDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        Log.Information("Database initialized successfully");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "An error occurred while initializing the database");
        throw;
    }
}

public record LoginRequest(string Email, string Password, bool RememberMe);