using Backend.CMS.Infrastructure.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Jobs
{
    public class IndexingBackgroundJob
    {
        private readonly IIndexingService _indexingService;
        private readonly ILogger<IndexingBackgroundJob> _logger;

        public IndexingBackgroundJob(IIndexingService indexingService, ILogger<IndexingBackgroundJob> logger)
        {
            _indexingService = indexingService;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task RunFullIndexAsync()
        {
            _logger.LogInformation("Starting scheduled full index job");

            try
            {
                var success = await _indexingService.FullReindexAsync();
                if (success)
                {
                    _logger.LogInformation("Scheduled full index completed successfully");
                }
                else
                {
                    _logger.LogWarning("Scheduled full index completed with errors");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled full index");
                throw;
            }
        }

        [AutomaticRetry(Attempts = 5)]
        public async Task RunIncrementalIndexAsync()
        {
            _logger.LogInformation("Starting scheduled incremental index job");

            try
            {
                var success = await _indexingService.IncrementalIndexAsync();
                if (success)
                {
                    _logger.LogInformation("Scheduled incremental index completed successfully");
                }
                else
                {
                    _logger.LogWarning("Scheduled incremental index completed with errors");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled incremental index");
                throw;
            }
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task IndexEntityAsync(string entityType, int entityId)
        {
            _logger.LogInformation("Indexing {EntityType} {EntityId}", entityType, entityId);

            try
            {
                var success = entityType.ToLowerInvariant() switch
                {
                    "page" => await _indexingService.IndexPagesAsync(new[] { entityId }),
                    "file" => await _indexingService.IndexFilesAsync(new[] { entityId }),
                    "user" => await _indexingService.IndexUsersAsync(new[] { entityId }),
                    _ => false
                };

                if (success)
                {
                    _logger.LogInformation("Successfully indexed {EntityType} {EntityId}", entityType, entityId);
                }
                else
                {
                    _logger.LogWarning("Failed to index {EntityType} {EntityId}", entityType, entityId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }
    }
}