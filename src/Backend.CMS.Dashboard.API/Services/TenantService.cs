using Microsoft.EntityFrameworkCore;
using Backend.CMS.Application.Common.Interfaces;
using Backend.CMS.Infrastructure.Data;

namespace Backend.CMS.Dashboard.API.Services
{
    public class TenantService : ITenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IServiceProvider _serviceProvider;

        public TenantService(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
        {
            _httpContextAccessor = httpContextAccessor;
            _serviceProvider = serviceProvider;
        }

        public string? GetCurrentTenantId()
        {
            var tenantId = _httpContextAccessor.HttpContext?.Items["TenantId"]?.ToString();

            if (string.IsNullOrEmpty(tenantId))
            {
                // Try to get from claims
                var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("TenantId");
                tenantId = claim?.Value;
            }

            // Fallback for development/design-time
            if (string.IsNullOrEmpty(tenantId))
            {
                tenantId = "demo";
            }

            return tenantId;
        }

        public async Task<string> GetConnectionStringAsync(string tenantId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var masterContext = scope.ServiceProvider.GetService<MasterDbContext>();

                if (masterContext != null)
                {
                    var tenant = await masterContext.Tenants
                        .FirstOrDefaultAsync(t => t.Identifier == tenantId && t.IsActive);

                    if (tenant != null)
                    {
                        return tenant.ConnectionString;
                    }
                }
            }
            catch
            {
                // If we can't connect to master DB, fall back to configuration
            }

            // Fallback to configuration-based connection string
            var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString($"Tenant_{tenantId}");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string not found for tenant '{tenantId}'");
            }

            return connectionString;
        }
    }
}