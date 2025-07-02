using Backend.CMS.Infrastructure.Caching.Abstractions;
using Backend.CMS.Infrastructure.Caching.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.CMS.Infrastructure.Caching.Services
{
    /// <summary>
    /// Background service for monitoring cache health
    /// </summary>
    public class CacheHealthCheckService : BackgroundService
    {
        private readonly ICacheMonitoringService _monitoringService;
        private readonly ILogger<CacheHealthCheckService> _logger;
        private readonly TimeSpan _checkInterval;
        private readonly bool _enableHealthChecks;

        public CacheHealthCheckService(
            ICacheMonitoringService monitoringService,
            ILogger<CacheHealthCheckService> logger,
            IConfiguration configuration)
        {
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _checkInterval = TimeSpan.FromMinutes(configuration.GetValue("Cache:HealthCheckIntervalMinutes", 5));
            _enableHealthChecks = configuration.GetValue("Cache:EnableHealthChecks", true);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enableHealthChecks)
            {
                _logger.LogInformation("Cache health checks are disabled");
                return;
            }

            _logger.LogInformation("Cache health check service started with interval: {Interval}", _checkInterval);

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Initial delay

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var healthCheckStart = DateTime.UtcNow;
                    var isHealthy = await _monitoringService.IsHealthyAsync();
                    var healthCheckDuration = DateTime.UtcNow - healthCheckStart;

                    if (!isHealthy)
                    {
                        _logger.LogWarning("Cache health check failed (Duration: {Duration}ms)",
                            healthCheckDuration.TotalMilliseconds);

                        // Optionally perform additional diagnostics
                        await PerformDetailedHealthCheckAsync();
                    }
                    else
                    {
                        _logger.LogDebug("Cache health check passed (Duration: {Duration}ms)",
                            healthCheckDuration.TotalMilliseconds);
                    }

                    // Log performance metrics periodically
                    await LogPerformanceMetricsAsync();

                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during cache health check");

                    // Use exponential backoff on errors
                    var retryDelay = TimeSpan.FromMinutes(Math.Min(5, 1 * Math.Pow(2, 1)));
                    await Task.Delay(retryDelay, stoppingToken);
                }
            }

            _logger.LogInformation("Cache health check service stopped");
        }

        private async Task PerformDetailedHealthCheckAsync()
        {
            try
            {
                var cacheInfo = await _monitoringService.GetCacheInfoAsync();

                if (cacheInfo.ContainsKey("error"))
                {
                    _logger.LogError("Cache diagnostics failed: {Error}", cacheInfo["error"]);
                }
                else
                {
                    // Log important Redis metrics
                    LogRedisDiagnostics(cacheInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform detailed health check");
            }
        }

        private void LogRedisDiagnostics(Dictionary<string, object> cacheInfo)
        {
            var importantMetrics = new[]
            {
                "server_connected_clients",
                "memory_used_memory",
                "memory_used_memory_human",
                "stats_total_commands_processed",
                "stats_instantaneous_ops_per_sec",
                "replication_role"
            };

            var metrics = new List<string>();
            foreach (var metric in importantMetrics)
            {
                if (cacheInfo.TryGetValue(metric, out var value))
                {
                    metrics.Add($"{metric}={value}");
                }
            }

            if (metrics.Any())
            {
                _logger.LogInformation("Redis diagnostics: {Metrics}", string.Join(", ", metrics));
            }
        }

        private async Task LogPerformanceMetricsAsync()
        {
            try
            {
                var statistics = await _monitoringService.GetStatisticsAsync();

                // Log performance metrics every 10 health checks (to avoid spam)
                var shouldLog = DateTime.UtcNow.Minute % 10 == 0;

                if (shouldLog)
                {
                    _logger.LogInformation(
                        "Cache performance - Hits: {Hits}, Misses: {Misses}, Hit Ratio: {HitRatio:P2}, " +
                        "Errors: {Errors}, Total Operations: {TotalOps}",
                        statistics.HitCount,
                        statistics.MissCount,
                        statistics.HitRatio,
                        statistics.ErrorCount,
                        statistics.TotalOperations);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to log performance metrics");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Cache health check service is stopping...");
            await base.StopAsync(cancellationToken);
        }
    }
}