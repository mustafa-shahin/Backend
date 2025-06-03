using Microsoft.EntityFrameworkCore;
using Backend.CMS.Application.Common.Interfaces;
using Backend.CMS.Infrastructure.Data;

namespace Backend.CMS.Dashboard.API.Services
{
    public class TenantService : ITenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly MasterDbContext _masterContext;

        public TenantService(IHttpContextAccessor httpContextAccessor, MasterDbContext masterContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _masterContext = masterContext;
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

            return tenantId;
        }

        public async Task<string> GetConnectionStringAsync(string tenantId)
        {
            var tenant = await _masterContext.Tenants
                .FirstOrDefaultAsync(t => t.Identifier == tenantId && t.IsActive);

            if (tenant == null)
            {
                throw new InvalidOperationException($"Tenant '{tenantId}' not found or inactive.");
            }

            return tenant.ConnectionString;
        }
    }
}
