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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

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
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7001");
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
        return Results.BadRequest(new { Message = ex.Message });
    }
});

app.MapPost("/api/auth/logout", async (IAuthService authService) =>
{
    // Implementation for logout
    return Results.Ok();
});

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
}

static void RegisterApplicationServices(IServiceCollection services)
{
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<IUserService, UserService>();
    services.AddScoped<IPageService, PageService>();
    services.AddScoped<IUserSessionService, UserSessionService>();
    services.AddScoped<ICacheService, CacheService>();
    services.AddScoped<ICacheInvalidationService, CacheService>();
}

static void RegisterBlazorServices(IServiceCollection services)
{
    services.AddBlazoredLocalStorage();
    services.AddScoped<IThemeService, ThemeService>();
    services.AddScoped<INotificationService, NotificationService>();
    services.AddScoped<IDialogService, DialogService>();
}

public record LoginRequest(string Email, string Password, bool RememberMe);