using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Backend.CMS.Audit.Services;

namespace Backend.CMS.BackgroundServices.Jobs
{
    public class HealthCheckJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HealthCheckJob> _logger;
        private readonly TimeSpan _period = TimeSpan.FromMinutes(5);

        public HealthCheckJob(IServiceProvider serviceProvider, ILogger<HealthCheckJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformHealthChecksAsync();
                    await Task.Delay(_period, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during health check monitoring");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task PerformHealthChecksAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

            var result = await healthCheckService.CheckHealthAsync();

            if (result.Status != HealthStatus.Healthy)
            {
                _logger.LogWarning("Health check failed with status {Status}", result.Status);

                var failedChecks = result.Entries
                    .Where(e => e.Value.Status != HealthStatus.Healthy)
                    .Select(e => new { Name = e.Key, Status = e.Value.Status.ToString(), Exception = e.Value.Exception?.Message })
                    .ToList();

                await auditService.LogSecurityEventAsync(
                    "health_check_failed",
                    $"Health check failed: {string.Join(", ", failedChecks.Select(c => $"{c.Name}:{c.Status}"))}");

                // Could implement alerting here (email, Slack, etc.)
            }
            else
            {
                _logger.LogDebug("All health checks passed");
            }
        }
    }
}