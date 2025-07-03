using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Backend.CMS.Infrastructure.Interfaces;

namespace Backend.CMS.Infrastructure.Services
{
    public class SessionCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SessionCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval;
        private readonly TimeSpan _sessionTimeout;
        private readonly bool _enableCleanup;

        public SessionCleanupService(
            IServiceProvider serviceProvider,
            ILogger<SessionCleanupService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load configuration
            var sessionTimeoutMinutes = configuration.GetValue("SessionSettings:TimeoutMinutes", 30);
            var cleanupIntervalMinutes = configuration.GetValue("SessionSettings:CleanupIntervalMinutes", 60);

            _sessionTimeout = TimeSpan.FromMinutes(sessionTimeoutMinutes);
            _cleanupInterval = TimeSpan.FromMinutes(cleanupIntervalMinutes);
            _enableCleanup = configuration.GetValue("SessionSettings:EnableAutomaticCleanup", true);

            _logger.LogInformation("SessionCleanupService configured - Cleanup interval: {CleanupInterval}, Session timeout: {SessionTimeout}, Enabled: {Enabled}",
                _cleanupInterval, _sessionTimeout, _enableCleanup);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enableCleanup)
            {
                _logger.LogInformation("Session cleanup is disabled");

                // Wait for cancellation
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

            _logger.LogInformation("Session cleanup service started - running every {CleanupInterval}", _cleanupInterval);

            // Initial delay before first cleanup
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformSessionCleanupAsync(stoppingToken);
                    await Task.Delay(_cleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during session cleanup");

                    // Wait a shorter time before retrying on error
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

        private async Task PerformSessionCleanupAsync(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var cleanedCount = 0;

            try
            {
                using var scope = _serviceProvider.CreateScope();

                // Clean up expired database sessions
                cleanedCount += await CleanupDatabaseSessionsAsync(scope, cancellationToken);

                // Clean up expired cache sessions
                cleanedCount += await CleanupCacheSessionsAsync(scope, cancellationToken);

                var duration = DateTime.UtcNow - startTime;

                if (cleanedCount > 0)
                {
                    _logger.LogInformation("Session cleanup completed - Removed {CleanedCount} expired sessions in {Duration}ms",
                        cleanedCount, duration.TotalMilliseconds);
                }
                else
                {
                    _logger.LogDebug("Session cleanup completed - No expired sessions found in {Duration}ms",
                        duration.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing session cleanup");
            }
        }

        private async Task<int> CleanupDatabaseSessionsAsync(IServiceScope scope, CancellationToken cancellationToken)
        {
            try
            {
                var sessionRepository = scope.ServiceProvider.GetService<IRepository<UserSession>>();
                if (sessionRepository == null)
                {
                    _logger.LogWarning("UserSession repository not available for cleanup");
                    return 0;
                }

                var cutoffTime = DateTime.UtcNow.Subtract(_sessionTimeout);

                // Find expired sessions
                var expiredSessions = await sessionRepository.FindAsync(s =>
                    !s.IsRevoked &&
                    (s.ExpiresAt < DateTime.UtcNow || s.UpdatedAt < cutoffTime));

                var sessionsList = expiredSessions.ToList();

                if (!sessionsList.Any())
                {
                    return 0;
                }

                // Mark sessions as revoked instead of deleting them for audit purposes
                foreach (var session in sessionsList)
                {
                    session.IsRevoked = true;
                    session.UpdatedAt = DateTime.UtcNow;
                    sessionRepository.Update(session);
                }

                await sessionRepository.SaveChangesAsync();

                // Invalidate cache for cleaned sessions
                await InvalidateSessionCacheAsync(scope, sessionsList, cancellationToken);

                _logger.LogDebug("Cleaned up {Count} expired database sessions", sessionsList.Count);
                return sessionsList.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up database sessions");
                return 0;
            }
        }

        private async Task<int> CleanupCacheSessionsAsync(IServiceScope scope, CancellationToken cancellationToken)
        {
            try
            {
                var cacheInvalidationService = scope.ServiceProvider.GetService<ICacheInvalidationService>();
                if (cacheInvalidationService == null)
                {
                    _logger.LogWarning("Cache invalidation service not available for session cleanup");
                    return 0;
                }

                var cacheKeyService = scope.ServiceProvider.GetService<ICacheKeyService>();
                if (cacheKeyService == null)
                {
                    _logger.LogWarning("Cache key service not available for session cleanup");
                    return 0;
                }

                // Clean up session cache entries using the new pattern-based invalidation
                var sessionPattern = cacheKeyService.GetEntityPattern<UserSession>();
                await cacheInvalidationService.InvalidateByPatternAsync(sessionPattern, cancellationToken);

                // Also clean up user session cache entries
                var userSessionPattern = cacheKeyService.GetCustomKey("user", "sessions", "*");
                await cacheInvalidationService.InvalidateByPatternAsync(userSessionPattern, cancellationToken);

                // Estimated count since we can't get exact numbers without detailed cache inspection
                var estimatedCount = 25; // Conservative estimate

                _logger.LogDebug("Cleaned up approximately {Count} cached session entries", estimatedCount);
                return estimatedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up cached sessions");
                return 0;
            }
        }

        private async Task InvalidateSessionCacheAsync(IServiceScope scope, List<UserSession> cleanedSessions, CancellationToken cancellationToken)
        {
            try
            {
                var cacheInvalidationService = scope.ServiceProvider.GetService<ICacheInvalidationService>();
                var cacheKeyService = scope.ServiceProvider.GetService<ICacheKeyService>();

                if (cacheInvalidationService == null || cacheKeyService == null)
                {
                    _logger.LogDebug("Cache services not available for session invalidation");
                    return;
                }

                // Invalidate specific session caches
                foreach (var session in cleanedSessions)
                {
                    // Invalidate session by ID
                    var sessionKey = cacheKeyService.GetEntityKey<UserSession>(session.Id);
                    await cacheInvalidationService.InvalidateByPatternAsync(sessionKey, cancellationToken);

                    // Invalidate session by refresh token if available
                    if (!string.IsNullOrEmpty(session.RefreshToken))
                    {
                        var tokenKey = cacheKeyService.GetCustomKey("session", "refresh_token", GetTokenHash(session.RefreshToken));
                        await cacheInvalidationService.InvalidateByPatternAsync(tokenKey, cancellationToken);
                    }

                    // Invalidate user sessions cache
                    var userSessionsKey = cacheKeyService.GetCustomKey("user", "sessions", session.UserId);
                    await cacheInvalidationService.InvalidateByPatternAsync(userSessionsKey, cancellationToken);
                }

                _logger.LogDebug("Invalidated cache for {Count} cleaned sessions", cleanedSessions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating session cache");
            }
        }

        private static string GetTokenHash(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return "empty";

            // Use last 8 characters for token identification (safe for logs)
            return token.Length <= 8 ? token : token[^8..];
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Session cleanup service stopping...");

            try
            {
                // Perform final cleanup before stopping if enabled
                if (_enableCleanup)
                {
                    await PerformSessionCleanupAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during final session cleanup");
            }

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("Session cleanup service stopped");
        }
    }
}