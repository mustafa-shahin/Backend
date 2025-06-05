using FluentValidation;
using Backend.CMS.Dashboard.API.Middleware;
using Backend.CMS.Dashboard.API.Services;
using Backend.CMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Backend.CMS.Interfaces.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CMS Dashboard API", Version = "v1" });
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

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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

// Add HTTP context accessor
builder.Services.AddHttpContextAccessor();

// Add database contexts
builder.Services.AddDbContext<MasterDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MasterDatabase")));

// Add multi-tenancy
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<TenantMiddleware>();

// Add tenant-specific DbContext
builder.Services.AddDbContext<CmsDbContext>((serviceProvider, options) =>
{
    var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    // For design-time scenarios, use default connection
    if (httpContextAccessor?.HttpContext == null)
    {
        var defaultConnectionString = configuration.GetConnectionString("Tenant_demo");
        options.UseNpgsql(defaultConnectionString);
        return;
    }

    var tenantService = serviceProvider.GetRequiredService<ITenantService>();
    var tenantId = tenantService.GetCurrentTenantId() ?? "demo";

    try
    {
        var connectionString = tenantService.GetConnectionStringAsync(tenantId).Result;
        options.UseNpgsql(connectionString);
    }
    catch
    {
        // Fallback for cases where master DB is not available
        var fallbackConnectionString = configuration.GetConnectionString("Tenant_demo");
        options.UseNpgsql(fallbackConnectionString);
    }
});

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Backend.CMS.Application.Features.Pages.Commands.CreatePageCommand).Assembly));

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Backend.CMS.Application.Features.Pages.Mappings.PageMappingProfile).Assembly);

// Add FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(Backend.CMS.Application.Features.Pages.Validators.CreatePageValidator).Assembly);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("DashboardPolicy");
app.UseAuthentication();
app.UseAuthorization();

// Add tenant middleware
app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

app.Run();