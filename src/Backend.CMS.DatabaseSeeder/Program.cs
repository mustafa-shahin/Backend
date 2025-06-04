using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Data.Seeders;
using Backend.CMS.Application.Common.Interfaces;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false);

// Add database contexts
builder.Services.AddDbContext<MasterDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MasterDatabase")));

// Add a simple tenant service for seeding
builder.Services.AddSingleton<ITenantService, SeedingTenantService>();

builder.Services.AddDbContext<CmsDbContext>((serviceProvider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("Tenant_demo");
    options.UseNpgsql(connectionString);
});

var app = builder.Build();

// Seed databases
using (var scope = app.Services.CreateScope())
{
    try
    {
        Console.WriteLine("Starting database seeding...");

        // Seed master database
        var masterContext = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        await MasterDataSeeder.SeedAsync(masterContext);
        Console.WriteLine("✅ Master database seeded successfully.");

        // Seed tenant database
        var tenantContext = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        await TenantDataSeeder.SeedAsync(tenantContext, "demo");
        Console.WriteLine("✅ Demo tenant database seeded successfully.");

        Console.WriteLine("🎉 Database seeding completed successfully!");
        Console.WriteLine();
        Console.WriteLine("Default login credentials:");
        Console.WriteLine("Email: admin@demo.com");
        Console.WriteLine("Password: Admin123!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error seeding database: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();

// Simple tenant service for seeding purposes
public class SeedingTenantService : ITenantService
{
    public string? GetCurrentTenantId() => "demo";

    public Task<string> GetConnectionStringAsync(string tenantId)
    {
        return Task.FromResult("Host=localhost;Database=cms_tenant_demo;Username=postgres;Password=23041988");
    }
}