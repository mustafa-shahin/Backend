using Microsoft.EntityFrameworkCore;
using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.Data.Seeders
{
    public static class MasterDataSeeder
    {
        public static async Task SeedAsync(MasterDbContext context)
        {
            // Seed default tenant
            if (!await context.Tenants.AnyAsync())
            {
                var defaultTenant = new Tenant
                {
                    Identifier = "demo",
                    Name = "Demo Tenant",
                    Domain = "demo.localhost",
                    ConnectionString = "Host=localhost;Database=cms_tenant_demo;Username=postgres;Password=yourpassword",
                    IsActive = true,
                    Theme = "default"
                };

                context.Tenants.Add(defaultTenant);
                await context.SaveChangesAsync();
            }
        }
    }
}
