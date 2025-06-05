using Backend.CMS.Interfaces.Interfaces;

namespace Backend.CMS.API.Services
{
    public class CustomerTenantService : ITenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public CustomerTenantService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public string? GetCurrentTenantId()
        {
            // Get from subdomain
            var host = _httpContextAccessor.HttpContext?.Request.Host.Host;
            if (!string.IsNullOrEmpty(host))
            {
                var subdomain = host.Split('.').FirstOrDefault();
                if (!string.IsNullOrEmpty(subdomain) && subdomain != "www" && subdomain != "localhost")
                {
                    return subdomain;
                }
            }

            // Get from configuration for development
            return _configuration["DefaultTenant"] ?? "demo";
        }

        public Task<string> GetConnectionStringAsync(string tenantId)
        {
            // In production, this would retrieve from a database or configuration service
            var connectionString = _configuration.GetConnectionString($"Tenant_{tenantId}");

            if (string.IsNullOrEmpty(connectionString))
            {
                // Fallback to default connection
                connectionString = _configuration.GetConnectionString("DefaultConnection");
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string not found for tenant '{tenantId}'");
            }

            return Task.FromResult(connectionString);
        }
    }
}