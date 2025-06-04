using Backend.CMS.Application.Common.Interfaces;
using Backend.CMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Backend.CMS.API.Middleware;
using Backend.CMS.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CMS API", Version = "v1" });
});

// Add CORS - FIXED VERSION
builder.Services.AddCors(options =>
{
    options.AddPolicy("CustomerPolicy", policy =>
    {
        // For development - allow specific origins
        policy.WithOrigins("http://localhost:3000", "http://localhost:5000", "https://localhost:7000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });

    // Add a separate policy for development without credentials
    options.AddPolicy("DevPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
        // Note: No .AllowCredentials() when using AllowAnyOrigin()
    });
});

// Add authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

// Add multi-tenancy
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantService, CustomerTenantService>();
builder.Services.AddScoped<CustomerTenantMiddleware>();

// Add tenant-specific DbContext
builder.Services.AddDbContext<CmsDbContext>((serviceProvider, options) =>
{
    var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    // For design-time scenarios, use default connection
    if (httpContextAccessor?.HttpContext == null)
    {
        var defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("Tenant_demo");
        options.UseNpgsql(defaultConnectionString);
        return;
    }

    var tenantService = serviceProvider.GetRequiredService<ITenantService>();
    var tenantId = tenantService.GetCurrentTenantId();

    if (string.IsNullOrEmpty(tenantId))
    {
        // Use a default connection string when tenant ID is not available
        var defaultConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(defaultConnectionString))
        {
            options.UseNpgsql(defaultConnectionString);
            return;
        }
        throw new InvalidOperationException("Tenant ID is required");
    }

    // Get connection string for the tenant
    var connectionString = configuration.GetConnectionString($"Tenant_{tenantId}");
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    options.UseNpgsql(connectionString);
});

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Backend.CMS.Application.Features.Pages.Commands.CreatePageCommand).Assembly));

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Backend.CMS.Application.Features.Pages.Mappings.PageMappingProfile).Assembly);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use the development CORS policy
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevPolicy");
}
else
{
    app.UseCors("CustomerPolicy");
}

app.UseAuthentication();
app.UseAuthorization();

// Add tenant middleware
app.UseMiddleware<CustomerTenantMiddleware>();

app.MapControllers();

// Create wwwroot directory if it doesn't exist
var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (!Directory.Exists(wwwrootPath))
{
    Directory.CreateDirectory(wwwrootPath);
}

// Serve static files and SPA
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();