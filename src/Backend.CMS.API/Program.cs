using Backend.CMS.API.HealthChecks;
using Backend.CMS.API.Middleware;
using Backend.CMS.API.Services;
using Backend.CMS.Application.Common.Interfaces;
using Backend.CMS.Caching.Extensions;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Repositories;
using Backend.CMS.Security.Extensions;
using Backend.CMS.Security.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CMS API",
        Version = "v1",
        Description = "Production CMS API for multi-tenant content management"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
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

// Add CORS with environment-specific configuration
builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("Development", policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://localhost:5000", "https://localhost:7000")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    }
    else
    {
        options.AddPolicy("Production", policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                               ?? Array.Empty<string>();
            policy.WithOrigins(allowedOrigins)
                  .WithHeaders("Authorization", "Content-Type", "X-Tenant-Id")
                  .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                  .AllowCredentials();
        });
    }
});

// Add authentication with enhanced configuration
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                {
                    context.Response.Headers.Add("Token-Expired", "true");
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Add caching
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
builder.Services.AddCaching(redisConnectionString);

// Add security services
builder.Services.AddSecurityServices();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!)
    .AddCheck("redis", () =>
    {
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            try
            {
                using var connection = StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString);
                return HealthCheckResult.Healthy("Redis is accessible");
            }
            catch
            {
                return HealthCheckResult.Unhealthy("Redis is not accessible");
            }
        }
        return HealthCheckResult.Healthy("Redis not configured");
    });

// Add multi-tenancy
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantService, CustomerTenantService>();
builder.Services.AddScoped<CustomerTenantMiddleware>();

// Add repositories
builder.Services.AddScoped<IPageRepository, PageRepository>();

// Add tenant-specific DbContext with retry policy
builder.Services.AddDbContext<CmsDbContext>((serviceProvider, options) =>
{
    var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    // For design-time scenarios, use default connection
    if (httpContextAccessor?.HttpContext == null)
    {
        var defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("Tenant_demo");
        options.UseNpgsql(defaultConnectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        });
        return;
    }

    var tenantService = serviceProvider.GetRequiredService<ITenantService>();
    var tenantId = tenantService.GetCurrentTenantId();

    if (string.IsNullOrEmpty(tenantId))
    {
        var defaultConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(defaultConnectionString))
        {
            options.UseNpgsql(defaultConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(30), null);
            });
            return;
        }
        throw new InvalidOperationException("Tenant ID is required");
    }

    var connectionString = configuration.GetConnectionString($"Tenant_{tenantId}");
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(30), null);
    });
});

// Add MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Backend.CMS.Application.Features.Pages.Commands.CreatePageCommand).Assembly));

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
    });
}
else
{
    app.UseHsts();
}

// Add security headers
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseHttpsRedirection();

// Add CORS
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}
else
{
    app.UseCors("Production");
}

// Add global exception handling
app.UseMiddleware<GlobalExceptionMiddleware>();

// Add rate limiting (commented out until we fix the reference issue)
// app.UseMiddleware<RateLimitingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Add tenant middleware
app.UseMiddleware<CustomerTenantMiddleware>();

// Add health checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                exception = x.Value.Exception?.Message,
                duration = x.Value.Duration.ToString()
            }),
            duration = report.TotalDuration.ToString()
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

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