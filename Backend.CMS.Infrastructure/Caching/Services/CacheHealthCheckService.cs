using Backend.CMS.Infrastructure.Caching.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Infrastructure.Caching.Services
{
    public class CacheHealthCheckService : BackgroundService
    {
        private readonly ICacheMonitoringService _monitoringService;
        private readonly ILogger<CacheHealthCheckService> _logger;
        private readonly TimeSpan _checkInterval;

        public CacheHealthCheckService(
            ICacheMonitoringService monitoringService,
            ILogger<CacheHealthCheckService> logger,
            IConfiguration configuration)
        {
            _monitoringService = monitoringService;
            _logger = logger;
            _checkInterval = TimeSpan.FromMinutes(configuration.GetValue("Cache:HealthCheckIntervalMinutes", 5));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cache health check service started with interval: {Interval}", _checkInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var isHealthy = await _monitoringService.IsHealthyAsync();

                    if (!isHealthy)
                    {
                        _logger.LogWarning("Cache health check failed");
                    }
                    else
                    {
                        _logger.LogDebug("Cache health check passed");
                    }

                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during cache health check");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait before retry
                }
            }

            _logger.LogInformation("Cache health check service stopped");
        }
    }
}
