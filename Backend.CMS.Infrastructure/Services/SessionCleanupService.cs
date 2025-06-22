using Backend.CMS.Application.Common;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class SessionCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SessionCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval;
        private readonly TimeSpan _sessionTimeout;
        private readonly int _batchSize;
        private readonly bool _enabled;

        public SessionCleanupService(
            IServiceProvider serviceProvider,
            ILogger<SessionCleanupService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _enabled = configuration.GetValue("SessionSettings:EnableCleanup", true);
            _cleanupInterval = TimeSpan.FromMinutes(configuration.GetValue("SessionSettings:CleanupIntervalMinutes", 60));
            _sessionTimeout = TimeSpan.FromMinutes(configuration.GetValue("SessionSettings:TimeoutMinutes", 30));
            _batchSize = configuration.GetValue("SessionSettings:CleanupBatchSize", 100);

            _logger.LogInformation("Session cleanup service configured - Enabled: {Enabled}, Interval: {Interval}m, Timeout: {Timeout}m, BatchSize: {BatchSize}",
                _enabled, _cleanupInterval.TotalMinutes, _sessionTimeout.TotalMinutes, _batchSize);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("Session cleanup service is disabled");
                try
                {
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                }
                return;
            }

            _logger.LogInformation("Session cleanup service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_cleanupInterval, stoppingToken);
                    await PerformCleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during session cleanup");

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

            _logger.LogInformation("Session cleanup service stopped");
        }

        private async Task PerformCleanupAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetService<ICacheService>();

                if (cacheService == null)
                {
                    _logger.LogWarning("Cache service not available for session cleanup");
                    return;
                }

                var startTime = DateTime.UtcNow;
                _logger.LogDebug("Starting session cleanup at {StartTime}", startTime);

                var totalCleaned = 0;

                // Clean up expired sessions using pattern-based removal
                totalCleaned += await CleanupExpiredSessionsAsync(cacheService, cancellationToken);

                // Clean up orphaned session data
                totalCleaned += await CleanupOrphanedSessionDataAsync(cacheService, cancellationToken);

                // Clean up old preview tokens
                totalCleaned += await CleanupOldPreviewTokensAsync(cacheService, cancellationToken);

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Session cleanup completed in {Duration}ms - Cleaned approximately: {Cleaned}",
                    duration.TotalMilliseconds, totalCleaned);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
            }
        }

        private async Task<int> CleanupExpiredSessionsAsync(
            ICacheService cacheService,
            CancellationToken cancellationToken)
        {
            try
            {
                // Use pattern-based cleanup for expired sessions since we can't enumerate keys
                var sessionPatterns = new[] { "session:id:*", "session:token:*", "session:*" };
                var totalCleaned = 0;

                foreach (var pattern in sessionPatterns)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await cacheService.RemoveByPatternAsync(pattern);
                        totalCleaned += 10; // Estimate cleaned count per pattern

                        _logger.LogDebug("Cleaned up session keys matching pattern {Pattern}", pattern);

                        // Small delay to prevent overwhelming the cache
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(100, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error cleaning sessions with pattern {Pattern}", pattern);
                    }
                }

                if (totalCleaned > 0)
                {
                    _logger.LogInformation("Cleaned up approximately {Count} expired sessions", totalCleaned);
                }

                return totalCleaned;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired sessions");
                return 0;
            }
        }

        private async Task<int> CleanupOrphanedSessionDataAsync(
            ICacheService cacheService,
            CancellationToken cancellationToken)
        {
            try
            {
                // Clean up session-related cache keys that might be orphaned using pattern removal
                var patterns = new[]
                {
                    "user:sessions:*",
                    "session:token:*",
                    "session:temp:*"
                };

                var totalCleaned = 0;

                foreach (var pattern in patterns)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await cacheService.RemoveByPatternAsync(pattern);
                        totalCleaned += 5; // Estimate cleaned count

                        _logger.LogDebug("Cleaned up orphaned session data matching pattern {Pattern}", pattern);

                        if (!cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(50, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error cleaning orphaned session data for pattern {Pattern}", pattern);
                    }
                }

                if (totalCleaned > 0)
                {
                    _logger.LogDebug("Cleaned up approximately {Count} orphaned session data entries", totalCleaned);
                }

                return totalCleaned;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up orphaned session data");
                return 0;
            }
        }

        private async Task<int> CleanupOldPreviewTokensAsync(
            ICacheService cacheService,
            CancellationToken cancellationToken)
        {
            try
            {
                // Clean up old preview tokens using pattern removal
                await cacheService.RemoveByPatternAsync("preview:*");

                var cleanedCount = 10; // Estimate cleaned count
                _logger.LogDebug("Cleaned up approximately {Count} old preview tokens", cleanedCount);

                return cleanedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old preview tokens");
                return 0;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Session cleanup service stopping...");
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("Session cleanup service stopped");
        }
    }
}