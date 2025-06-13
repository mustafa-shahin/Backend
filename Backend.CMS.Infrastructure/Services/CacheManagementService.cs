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

        public CacheManagementService(
            IServiceProvider serviceProvider,
            ILogger<CacheManagementService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cleanupInterval = TimeSpan.FromHours(
                configuration.GetValue("CacheSettings:CleanupIntervalHours", 6));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cache management service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_cleanupInterval, stoppingToken);

                    using var scope = _serviceProvider.CreateScope();
                    var cacheService = scope.ServiceProvider.GetService<ICacheInvalidationService>();

                    if (cacheService != null)
                    {
                        // Get cache statistics
                        var stats = await cacheService.GetCacheStatisticsAsync();
                        _logger.LogInformation("Cache statistics: {Stats}",
                            System.Text.Json.JsonSerializer.Serialize(stats));

                        // Optionally clean up expired or old cache entries
                        // This depends on your Redis configuration and requirements
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in cache management service");
                }
            }

            _logger.LogInformation("Cache management service stopped");
        }
    }
}
