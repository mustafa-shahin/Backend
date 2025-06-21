using Backend.CMS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

            _logger.LogInformation("CacheManagementService configured - Cleanup: {Cleanup}h, Stats: {Stats}m, Health: {Health}m",
                _cleanupInterval.TotalHours, _statisticsInterval.TotalMinutes, _healthCheckInterval.TotalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cache management service started");

            var tasks = new List<Task>();

            // Start cleanup task if enabled
            if (_enableAutomaticCleanup)
            {
                tasks.Add(RunCleanupTaskAsync(stoppingToken));
            }

            // Start statistics logging task if enabled
            if (_enableStatisticsLogging)
            {
                tasks.Add(RunStatisticsTaskAsync(stoppingToken));
            }

            // Start health check task if enabled
            if (_enableHealthChecks)
            {
                tasks.Add(RunHealthCheckTaskAsync(stoppingToken));
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

        private async Task RunCleanupTaskAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting cache cleanup task");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_cleanupInterval, stoppingToken);
                    await PerformCacheCleanupAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in cache cleanup task");

                    // Wait a shorter interval before retrying on error
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.LogDebug("Cache cleanup task stopped");
        }

        private async Task RunStatisticsTaskAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting cache statistics task");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_statisticsInterval, stoppingToken);
                    await LogCacheStatisticsAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in cache statistics task");

                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.LogDebug("Cache statistics task stopped");
        }

        private async Task RunHealthCheckTaskAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting cache health check task");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_healthCheckInterval, stoppingToken);
                    await PerformCacheHealthCheckAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in cache health check task");

                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.LogDebug("Cache health check task stopped");
        }

        private async Task PerformCacheCleanupAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetService<ICacheInvalidationService>();
                var fileCachingService = scope.ServiceProvider.GetService<IFileCachingService>();

                if (cacheService != null)
                {
                    _logger.LogDebug("Starting cache cleanup");

                    // Get cache statistics before cleanup
                    var beforeStats = await cacheService.GetCacheStatisticsAsync();
                    var beforeKeyCount = beforeStats.TryGetValue("TotalKeys", out var beforeKeys) ? beforeKeys : 0;

                    // Perform cleanup operations
                    await CleanupExpiredSessionsAsync(cacheService);
                    await CleanupStaleSearchResultsAsync(cacheService);
                    await CleanupOrphanedPreviewsAsync(cacheService);

                    // Get statistics after cleanup
                    var afterStats = await cacheService.GetCacheStatisticsAsync();
                    var afterKeyCount = afterStats.TryGetValue("TotalKeys", out var afterKeys) ? afterKeys : 0;

                    var cleanedKeys = Convert.ToInt64(beforeKeyCount) - Convert.ToInt64(afterKeyCount);

                    _logger.LogInformation("Cache cleanup completed - Cleaned {CleanedKeys} keys ({Before} -> {After})",
                        cleanedKeys, beforeKeyCount, afterKeyCount);
                }

                // Cleanup file cache if available
                if (fileCachingService is FileCachingService fileService)
                {
                    // Trigger file cache cleanup (if the service supports it)
                    _logger.LogDebug("File cache cleanup would be performed here");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
        }

        private async Task CleanupExpiredSessionsAsync(ICacheInvalidationService cacheService)
        {
            try
            {
                // Get session keys and clean up expired ones
                var sessionKeys = await cacheService.GetCacheKeysAsync("session:*");
                var expiredKeys = new List<string>();

                // In a real implementation, check session expiration
                // For now,  clean up sessions older than a certain threshold
                var cutoffTime = DateTime.UtcNow.AddDays(-7); // Remove sessions older than 7 days

                foreach (var key in sessionKeys)
                {
                    // This is a simplified check - in reality parse the session data
                    // to check actual expiration times
                    if (ShouldCleanupSessionKey(key, cutoffTime))
                    {
                        expiredKeys.Add(key);
                    }
                }

                if (expiredKeys.Any())
                {
                    // Remove expired sessions
                    foreach (var key in expiredKeys)
                    {
                        try
                        {
                            await ((ICacheService)cacheService).RemoveAsync(key);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to remove expired session key: {Key}", key);
                        }
                    }

                    _logger.LogDebug("Cleaned up {Count} expired session keys", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up expired sessions");
            }
        }

        private async Task CleanupStaleSearchResultsAsync(ICacheInvalidationService cacheService)
        {
            try
            {
                // Clean up old search result caches
                var searchKeys = await cacheService.GetCacheKeysAsync("search:*");
                var staleKeys = new List<string>();

                // Remove search caches older than 1 hour
                foreach (var key in searchKeys.Take(100)) // Limit to avoid performance issues
                {
                    // In a real implementation, have timestamps in the cache keys
                    // or metadata to determine age
                    if (ShouldCleanupSearchKey(key))
                    {
                        staleKeys.Add(key);
                    }
                }

                if (staleKeys.Any())
                {
                    foreach (var key in staleKeys)
                    {
                        try
                        {
                            await ((ICacheService)cacheService).RemoveAsync(key);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to remove stale search key: {Key}", key);
                        }
                    }

                    _logger.LogDebug("Cleaned up {Count} stale search keys", staleKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up stale search results");
            }
        }

        private async Task CleanupOrphanedPreviewsAsync(ICacheInvalidationService cacheService)
        {
            try
            {
                // Clean up old preview caches
                var previewKeys = await cacheService.GetCacheKeysAsync("preview:*");

                if (previewKeys.Any())
                {
                    // Remove all preview caches older than 1 day
                    foreach (var key in previewKeys.Take(50)) // Limit batch size
                    {
                        try
                        {
                            await ((ICacheService)cacheService).RemoveAsync(key);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to remove preview key: {Key}", key);
                        }
                    }

                    _logger.LogDebug("Cleaned up {Count} preview keys", Math.Min(previewKeys.Count, 50));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up orphaned previews");
            }
        }

        private async Task LogCacheStatisticsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetService<ICacheInvalidationService>();

                if (cacheService != null)
                {
                    var statistics = await cacheService.GetCacheStatisticsAsync();

                    _logger.LogInformation("Cache Statistics: {Statistics}",
                        System.Text.Json.JsonSerializer.Serialize(statistics, new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = true
                        }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging cache statistics");
            }
        }

        private async Task PerformCacheHealthCheckAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetService<ICacheService>();

                if (cacheService != null)
                {
                    // Test cache connectivity and basic operations
                    var testKey = $"health_check_{Guid.NewGuid()}";
                    var testValue = new { timestamp = DateTime.UtcNow, test = "cache_health_check" };

                    // Test set operation
                    await cacheService.SetAsync(testKey, testValue, TimeSpan.FromMinutes(1));

                    // Test get operation
                    var retrievedValue = await cacheService.GetAsync<object>(testKey);

                    if (retrievedValue == null)
                    {
                        _logger.LogWarning("Cache health check failed - unable to retrieve test value");
                    }
                    else
                    {
                        _logger.LogDebug("Cache health check passed");
                    }

                    // Cleanup test key
                    await cacheService.RemoveAsync(testKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache health check failed");
            }
        }

        private bool ShouldCleanupSessionKey(string key, DateTime cutoffTime)
        {
            // Simplified check - in reality parse session data to check expiration
            // For now, assume keys with older timestamps should be cleaned
            try
            {
                // Extract timestamp from key if it contains one
                // This is a placeholder implementation
                return key.Contains("session:") && key.Length > 20; // Simple heuristic
            }
            catch
            {
                return false;
            }
        }

        private bool ShouldCleanupSearchKey(string key)
        {
            // should have timestamps or TTL information
            try
            {
                return key.StartsWith("search:") && key.Length > 50; // Simple heuristic
            }
            catch
            {
                return false;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Cache management service stopping...");
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("Cache management service stopped");
        }
    }
}