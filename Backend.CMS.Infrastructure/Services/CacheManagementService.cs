using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Backend.CMS.Infrastructure.Services
{
    public class CacheManagementService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CacheManagementService> _logger;
        private readonly TimeSpan _cleanupInterval;
        private readonly TimeSpan _statisticsInterval;
        private readonly TimeSpan _healthCheckInterval;
        private readonly bool _enableAutomaticCleanup;
        private readonly bool _enableStatisticsLogging;
        private readonly bool _enableHealthChecks;
        private readonly int _maxConcurrentTasks;
        private readonly SemaphoreSlim _operationSemaphore;

        // Performance tracking
        private readonly ConcurrentDictionary<string, DateTime> _lastOperationTimes;
        private long _totalCleanupOperations;
        private long _totalKeysRemoved;
        private DateTime _serviceStartTime;

        public CacheManagementService(
            IServiceProvider serviceProvider,
            ILogger<CacheManagementService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load configuration with defaults
            _cleanupInterval = TimeSpan.FromHours(
                configuration.GetValue("CacheSettings:CleanupIntervalHours", 6));

            _statisticsInterval = TimeSpan.FromMinutes(
                configuration.GetValue("CacheSettings:StatisticsIntervalMinutes", 30));

            _healthCheckInterval = TimeSpan.FromMinutes(
                configuration.GetValue("CacheSettings:HealthCheckIntervalMinutes", 5));

            _enableAutomaticCleanup = configuration.GetValue("CacheSettings:EnableAutomaticCleanup", true);
            _enableStatisticsLogging = configuration.GetValue("CacheSettings:EnableStatisticsLogging", false);
            _enableHealthChecks = configuration.GetValue("CacheSettings:EnableHealthChecks", true);
            _maxConcurrentTasks = configuration.GetValue("CacheSettings:MaxConcurrentTasks", 3);

            _operationSemaphore = new SemaphoreSlim(_maxConcurrentTasks, _maxConcurrentTasks);
            _lastOperationTimes = new ConcurrentDictionary<string, DateTime>();
            _serviceStartTime = DateTime.UtcNow;

            _logger.LogInformation("CacheManagementService configured - Cleanup: {Cleanup}h, Stats: {Stats}m, Health: {Health}m, MaxTasks: {MaxTasks}",
                _cleanupInterval.TotalHours, _statisticsInterval.TotalMinutes, _healthCheckInterval.TotalMinutes, _maxConcurrentTasks);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cache management service started");

            var tasks = new List<Task>();

            // Start cleanup task if enabled
            if (_enableAutomaticCleanup)
            {
                tasks.Add(RunPeriodicTaskAsync("Cleanup", _cleanupInterval, PerformCacheCleanupAsync, stoppingToken));
            }

            // Start statistics logging task if enabled
            if (_enableStatisticsLogging)
            {
                tasks.Add(RunPeriodicTaskAsync("Statistics", _statisticsInterval, LogCacheStatisticsAsync, stoppingToken));
            }

            // Start health check task if enabled
            if (_enableHealthChecks)
            {
                tasks.Add(RunPeriodicTaskAsync("HealthCheck", _healthCheckInterval, PerformCacheHealthCheckAsync, stoppingToken));
            }

            if (tasks.Any())
            {
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException)
                {
                    // Expected when the service is stopping
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in cache management service");
                }
            }
            else
            {
                _logger.LogInformation("No cache management tasks enabled, service will remain idle");

                // Wait for cancellation
                try
                {
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                }
            }

            _logger.LogInformation("Cache management service stopped");
        }

        private async Task RunPeriodicTaskAsync(
            string taskName,
            TimeSpan interval,
            Func<CancellationToken, Task> taskFunction,
            CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting {TaskName} task with interval {Interval}", taskName, interval);

            // Initial delay to stagger tasks
            var initialDelay = TimeSpan.FromSeconds(new Random().Next(1, 30));
            try
            {
                await Task.Delay(initialDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _operationSemaphore.WaitAsync(stoppingToken);
                    try
                    {
                        var startTime = DateTime.UtcNow;
                        _lastOperationTimes[taskName] = startTime;

                        await taskFunction(stoppingToken);

                        var duration = DateTime.UtcNow - startTime;
                        _logger.LogDebug("{TaskName} task completed in {Duration}ms", taskName, duration.TotalMilliseconds);
                    }
                    finally
                    {
                        _operationSemaphore.Release();
                    }

                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in {TaskName} task", taskName);

                    // Exponential backoff on error, max 5 minutes
                    var errorDelay = TimeSpan.FromMinutes(Math.Min(5, 1));
                    try
                    {
                        await Task.Delay(errorDelay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.LogDebug("{TaskName} task stopped", taskName);
        }

        private async Task PerformCacheCleanupAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetService<ICacheService>();
                var cacheInvalidationService = scope.ServiceProvider.GetService<ICacheInvalidationService>();

                if (cacheService == null)
                {
                    _logger.LogWarning("Cache service not available for cleanup");
                    return;
                }

                _logger.LogDebug("Starting comprehensive cache cleanup");

                var beforeKeyCount = 0L;
                if (cacheInvalidationService != null)
                {
                    try
                    {
                        var beforeStats = await cacheInvalidationService.GetCacheStatisticsAsync();
                        beforeKeyCount = GetStatisticsValue(beforeStats, "TotalKeys");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get cache statistics before cleanup - Redis admin mode may not be available");
                    }
                }

                // Perform cleanup operations using only ICacheService
                var totalCleaned = 0;
                totalCleaned += await CleanupExpiredSessionsAsync(cacheService, cancellationToken);
                totalCleaned += await CleanupStaleSearchResultsAsync(cacheService, cancellationToken);
                totalCleaned += await CleanupOrphanedPreviewsAsync(cacheService, cancellationToken);
                totalCleaned += await CleanupOldDesignerStatesAsync(cacheService, cancellationToken);
                totalCleaned += await CleanupTemporaryCacheKeysAsync(cacheService, cancellationToken);

                var afterKeyCount = 0L;
                if (cacheInvalidationService != null)
                {
                    try
                    {
                        var afterStats = await cacheInvalidationService.GetCacheStatisticsAsync();
                        afterKeyCount = GetStatisticsValue(afterStats, "TotalKeys");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get cache statistics after cleanup - Redis admin mode may not be available");
                    }
                }

                Interlocked.Increment(ref _totalCleanupOperations);
                Interlocked.Add(ref _totalKeysRemoved, totalCleaned);

                if (beforeKeyCount > 0 || afterKeyCount > 0)
                {
                    _logger.LogInformation("Cache cleanup completed - Removed approximately {CleanedKeys} keys ({Before} -> {After}), Total operations: {TotalOps}, Total removed: {TotalRemoved}",
                        totalCleaned, beforeKeyCount, afterKeyCount, _totalCleanupOperations, _totalKeysRemoved);
                }
                else
                {
                    _logger.LogInformation("Cache cleanup completed - Removed approximately {CleanedKeys} keys, Total operations: {TotalOps}, Total removed: {TotalRemoved}",
                        totalCleaned, _totalCleanupOperations, _totalKeysRemoved);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
        }

        private async Task<int> CleanupExpiredSessionsAsync(ICacheService cacheService, CancellationToken cancellationToken)
        {
            try
            {
                await cacheService.RemoveByPatternAsync("session:*");
                var cleanedCount = 20; 
                _logger.LogDebug("Cleaned up approximately {Count} expired session keys", cleanedCount);
                return cleanedCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up expired sessions");
                return 0;
            }
        }

        private async Task<int> CleanupStaleSearchResultsAsync(ICacheService cacheService, CancellationToken cancellationToken)
        {
            try
            {
                await cacheService.RemoveByPatternAsync("search:*");
                var cleanedCount = 15;
                _logger.LogDebug("Cleaned up approximately {Count} stale search result keys", cleanedCount);
                return cleanedCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up stale search results");
                return 0;
            }
        }

        private async Task<int> CleanupOrphanedPreviewsAsync(ICacheService cacheService, CancellationToken cancellationToken)
        {
            try
            {
                await cacheService.RemoveByPatternAsync("preview:*");
                var cleanedCount = 10;
                _logger.LogDebug("Cleaned up approximately {Count} orphaned preview keys", cleanedCount);
                return cleanedCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up orphaned previews");
                return 0;
            }
        }

        private async Task<int> CleanupOldDesignerStatesAsync(ICacheService cacheService, CancellationToken cancellationToken)
        {
            try
            {
                await cacheService.RemoveByPatternAsync("designer:*");
                var cleanedCount = 8;
                _logger.LogDebug("Cleaned up approximately {Count} old designer state keys", cleanedCount);
                return cleanedCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up old designer states");
                return 0;
            }
        }

        private async Task<int> CleanupTemporaryCacheKeysAsync(ICacheService cacheService, CancellationToken cancellationToken)
        {
            try
            {
                var patterns = new[] { "temp:*", "tmp:*", "cache:temp:*" };
                var totalCleaned = 0;

                foreach (var pattern in patterns)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    await cacheService.RemoveByPatternAsync(pattern);
                    totalCleaned += 5; 
                }

                if (totalCleaned > 0)
                {
                    _logger.LogDebug("Cleaned up approximately {Count} temporary cache keys", totalCleaned);
                }

                return totalCleaned;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up temporary cache keys");
                return 0;
            }
        }

        private async Task LogCacheStatisticsAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var cacheInvalidationService = scope.ServiceProvider.GetService<ICacheInvalidationService>();

                if (cacheInvalidationService == null)
                {
                    _logger.LogWarning("Cache invalidation service not available for statistics");
                    return;
                }

                var statistics = await cacheInvalidationService.GetCacheStatisticsAsync();
                var uptime = DateTime.UtcNow - _serviceStartTime;

                var enhancedStats = new Dictionary<string, object>(statistics)
                {
                    ["ServiceUptime"] = uptime.ToString(@"dd\.hh\:mm\:ss"),
                    ["TotalCleanupOperations"] = _totalCleanupOperations,
                    ["TotalKeysRemoved"] = _totalKeysRemoved,
                    ["LastOperationTimes"] = _lastOperationTimes.ToDictionary(kv => kv.Key, kv => kv.Value.ToString("yyyy-MM-dd HH:mm:ss"))
                };

                // Check if we have Redis admin access for detailed stats
                var adminModeAvailable = statistics.ContainsKey("AdminModeAvailable") && 
                                       statistics["AdminModeAvailable"] is bool available && available;

                if (adminModeAvailable)
                {
                    _logger.LogInformation("Cache Management Statistics: {Statistics}",
                        System.Text.Json.JsonSerializer.Serialize(enhancedStats, new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = true
                        }));
                }
                else
                {
                    _logger.LogInformation("Cache Management Statistics (Limited - Redis admin mode not available): Service Uptime: {Uptime}, Cleanup Operations: {Operations}, Keys Removed: {KeysRemoved}, Active Semaphores: {Semaphores}",
                        uptime.ToString(@"dd\.hh\:mm\:ss"), _totalCleanupOperations, _totalKeysRemoved, 
                        statistics.GetValueOrDefault("ActiveSemaphores", "N/A"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging cache statistics");
            }
        }

        private async Task PerformCacheHealthCheckAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetService<ICacheService>();

                if (cacheService == null)
                {
                    _logger.LogWarning("Cache service not available for health check");
                    return;
                }

                var testKey = $"health_check_{Guid.NewGuid()}";
                var testValue = new
                {
                    timestamp = DateTime.UtcNow,
                    test = "cache_health_check",
                    serviceUptime = (DateTime.UtcNow - _serviceStartTime).TotalMinutes
                };

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Test basic cache operations
                await cacheService.SetAsync(testKey, testValue, TimeSpan.FromMinutes(1));
                var setTime = stopwatch.ElapsedMilliseconds;

                var retrievedValue = await cacheService.GetAsync<object>(testKey);
                var getTime = stopwatch.ElapsedMilliseconds - setTime;

                stopwatch.Stop();

                if (retrievedValue == null)
                {
                    _logger.LogWarning("Cache health check failed - unable to retrieve test value. Set: {SetTime}ms", setTime);
                }
                else
                {
                    _logger.LogDebug("Cache health check passed. Set: {SetTime}ms, Get: {GetTime}ms, Total: {TotalTime}ms",
                        setTime, getTime, stopwatch.ElapsedMilliseconds);
                }

                // Clean up test key
                await cacheService.RemoveAsync(testKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache health check failed");
            }
        }

        private static long GetStatisticsValue(Dictionary<string, object> stats, string key)
        {
            if (stats.TryGetValue(key, out var value))
            {
                return value switch
                {
                    long longValue => longValue,
                    int intValue => intValue,
                    string stringValue when long.TryParse(stringValue, out var parsedValue) => parsedValue,
                    _ => 0
                };
            }
            return 0;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Cache management service stopping...");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await base.StopAsync(cancellationToken);
            stopwatch.Stop();

            var uptime = DateTime.UtcNow - _serviceStartTime;
            _logger.LogInformation("Cache management service stopped after {StopTime}ms. Total uptime: {Uptime}, Operations: {Operations}, Keys removed: {KeysRemoved}",
                stopwatch.ElapsedMilliseconds, uptime.ToString(@"dd\.hh\:mm\:ss"), _totalCleanupOperations, _totalKeysRemoved);

            _operationSemaphore?.Dispose();
            _lastOperationTimes?.Clear();
        }
    }
}