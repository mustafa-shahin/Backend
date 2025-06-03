using Backend.CMS.Application.Common.Interfaces;
using Backend.CMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CustomerPolicy", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "*" })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
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
    var tenantService = serviceProvider.GetRequiredService<ITenantService>();
    var tenantId = tenantService.GetCurrentTenantId();

    if (string.IsNullOrEmpty(tenantId))
    {
        throw new InvalidOperationException("Tenant ID is required");
    }

    // In production, get connection string from configuration or database
    var connectionString = builder.Configuration.GetConnectionString($"Tenant_{tenantId}");
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
app.UseCors("CustomerPolicy");
app.UseAuthentication();
app.UseAuthorization();

// Add tenant middleware
app.UseMiddleware<CustomerTenantMiddleware>();

app.MapControllers();

// Serve static files and SPA
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();