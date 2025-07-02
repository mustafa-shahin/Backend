using Backend.CMS.Infrastructure.Caching.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Caching.Services
{
    /// <summary>
    /// Background service for collecting and logging cache statistics
    /// </summary>
    public class CacheStatisticsService : BackgroundService
    {
        private readonly ICacheMonitoringService _monitoringService;
        private readonly ILogger<CacheStatisticsService> _logger;
        private readonly TimeSpan _logInterval;
        private readonly bool _enableLogging;
        private readonly bool _enableDetailedLogging;
        private readonly TimeSpan _resetInterval;
        private DateTime _lastResetTime;

        public CacheStatisticsService(
            ICacheMonitoringService monitoringService,
            ILogger<CacheStatisticsService> logger,
            IConfiguration configuration)
        {
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logInterval = TimeSpan.FromMinutes(configuration.GetValue("Cache:StatisticsLogIntervalMinutes", 30));
            _enableLogging = configuration.GetValue("Cache:EnableStatisticsLogging", true);
            _enableDetailedLogging = configuration.GetValue("Cache:EnableDetailedStatisticsLogging", false);
            _resetInterval = TimeSpan.FromHours(configuration.GetValue("Cache:StatisticsResetIntervalHours", 24));
            _lastResetTime = DateTime.UtcNow;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enableLogging)
            {
                _logger.LogInformation("Cache statistics logging is disabled");
                return;
            }

            _logger.LogInformation("Cache statistics service started with log interval: {Interval}, reset interval: {ResetInterval}",
                _logInterval, _resetInterval);

            // Initial delay to let the system stabilize
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await LogStatisticsAsync();
                    await CheckForStatisticsResetAsync();

                    await Task.Delay(_logInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting cache statistics");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait before retry
                }
            }

            _logger.LogInformation("Cache statistics service stopped");
        }

        private async Task LogStatisticsAsync()
        {
            try
            {
                var statistics = await _monitoringService.GetStatisticsAsync();

                // Basic statistics
                _logger.LogInformation(
                    "Cache Statistics - Hits: {Hits}, Misses: {Misses}, Hit Ratio: {HitRatio:P2}, " +
                    "Errors: {Errors}, Total Operations: {TotalOps}, Last Reset: {LastReset}",
                    statistics.HitCount,
                    statistics.MissCount,
                    statistics.HitRatio,
                    statistics.ErrorCount,
                    statistics.TotalOperations,
                    statistics.LastResetTime);

                // Detailed operation breakdown
                if (_enableDetailedLogging && statistics.OperationCounts.Any())
                {
                    var operationStats = statistics.OperationCounts
                        .OrderByDescending(kv => kv.Value)
                        .Take(10) // Top 10 operations
                        .Select(kv => $"{kv.Key}: {kv.Value}")
                        .ToList();

                    if (operationStats.Any())
                    {
                        _logger.LogInformation("Top Cache Operations - {Operations}",
                            string.Join(", ", operationStats));
                    }
                }

                // Performance alerts
                await CheckPerformanceAlertsAsync(statistics);

                // System health metrics
                if (_enableDetailedLogging)
                {
                    await LogSystemHealthMetricsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log cache statistics");
            }
        }

        private async Task CheckPerformanceAlertsAsync(CacheStatistics statistics)
        {
            try
            {
                // Alert on low hit ratio
                if (statistics.TotalOperations > 1000 && statistics.HitRatio < 0.5)
                {
                    _logger.LogWarning("Low cache hit ratio detected: {HitRatio:P2} (Total Operations: {TotalOps})",
                        statistics.HitRatio, statistics.TotalOperations);
                }

                // Alert on high error rate
                if (statistics.TotalOperations > 100 && statistics.ErrorCount > statistics.TotalOperations * 0.1)
                {
                    _logger.LogWarning("High cache error rate detected: {ErrorCount}/{TotalOps} ({ErrorRate:P2})",
                        statistics.ErrorCount, statistics.TotalOperations,
                        (double)statistics.ErrorCount / statistics.TotalOperations);
                }

                // Alert on cache health
                var isHealthy = await _monitoringService.IsHealthyAsync();
                if (!isHealthy)
                {
                    _logger.LogWarning("Cache health check failed during statistics collection");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to check performance alerts");
            }
        }

        private async Task LogSystemHealthMetricsAsync()
        {
            try
            {
                var cacheInfo = await _monitoringService.GetCacheInfoAsync();

                if (cacheInfo.ContainsKey("error"))
                {
                    _logger.LogWarning("Could not retrieve cache system metrics: {Error}", cacheInfo["error"]);
                    return;
                }

                // Log memory usage
                if (cacheInfo.TryGetValue("memory_used_memory_human", out var memoryUsage))
                {
                    _logger.LogDebug("Redis Memory Usage: {MemoryUsage}", memoryUsage);
                }

                // Log operations per second
                if (cacheInfo.TryGetValue("stats_instantaneous_ops_per_sec", out var opsPerSec))
                {
                    _logger.LogDebug("Redis Operations/sec: {OpsPerSec}", opsPerSec);
                }

                // Log connected clients
                if (cacheInfo.TryGetValue("server_connected_clients", out var connectedClients))
                {
                    _logger.LogDebug("Redis Connected Clients: {ConnectedClients}", connectedClients);
                }

                // Check for concerning metrics
                if (cacheInfo.TryGetValue("memory_used_memory_rss", out var rssMemory) &&
                    long.TryParse(rssMemory.ToString(), out var rssBytes))
                {
                    var rssMB = rssBytes / (1024 * 1024);
                    if (rssMB > 1000) // More than 1GB
                    {
                        _logger.LogWarning("High Redis memory usage detected: {MemoryMB}MB", rssMB);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to log system health metrics");
            }
        }

        private async Task CheckForStatisticsResetAsync()
        {
            try
            {
                if (DateTime.UtcNow - _lastResetTime > _resetInterval)
                {
                    _logger.LogInformation("Resetting cache statistics after {Interval}", _resetInterval);

                    await _monitoringService.ResetStatisticsAsync();
                    _lastResetTime = DateTime.UtcNow;

                    _logger.LogInformation("Cache statistics reset completed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset cache statistics");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Cache statistics service is stopping...");

            try
            {
                // Log final statistics before stopping
                await LogStatisticsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log final statistics");
            }

            await base.StopAsync(cancellationToken);
        }
    }
}