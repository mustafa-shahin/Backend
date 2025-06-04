using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Backend.CMS.Caching.Services;
using Backend.CMS.Infrastructure.Repositories;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.BackgroundServices.Jobs
{
    public class CacheWarmupJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CacheWarmupJob> _logger;
        private readonly TimeSpan _period = TimeSpan.FromHours(6); // Warm cache every 6 hours

        public CacheWarmupJob(IServiceProvider serviceProvider, ILogger<CacheWarmupJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial warmup after 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await WarmupCacheAsync();
                    await Task.Delay(_period, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during cache warmup");
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                }
            }
        }

        private async Task WarmupCacheAsync()
        {
            _logger.LogInformation("Starting cache warmup job");

            using var scope = _serviceProvider.CreateScope();
            var pageRepository = scope.ServiceProvider.GetRequiredService<IPageRepository>();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

            try
            {
                // Warmup published pages
                var publishedPages = await pageRepository.GetPublishedPagesAsync();
                await cacheService.SetAsync("pages:published", publishedPages, TimeSpan.FromHours(1));

                // Warmup individual pages by slug
                foreach (var page in publishedPages.Take(50)) // Limit to top 50 pages
                {
                    var pageWithComponents = await pageRepository.GetBySlugAsync(page.Slug, true);
                    if (pageWithComponents != null)
                    {
                        var cacheKey = $"page:slug:{page.Slug}:components:true";
                        await cacheService.SetAsync(cacheKey, pageWithComponents, TimeSpan.FromMinutes(30));
                    }
                }

                _logger.LogInformation("Cache warmup completed for {Count} pages", publishedPages.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to warmup cache");
                throw;
            }
        }
    }
}
