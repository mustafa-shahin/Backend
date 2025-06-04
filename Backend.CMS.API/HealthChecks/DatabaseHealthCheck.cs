using Backend.CMS.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Backend.CMS.API.HealthChecks
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly CmsDbContext _context;

        public DatabaseHealthCheck(CmsDbContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.Database.CanConnectAsync(cancellationToken);
                return HealthCheckResult.Healthy("Database is accessible");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database is not accessible", ex);
            }
        }
    }
}
