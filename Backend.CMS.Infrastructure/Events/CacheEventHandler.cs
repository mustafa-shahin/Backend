using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Backend.CMS.Infrastructure.Events
{
    public interface ICacheEventHandler
    {
        Task HandleEntityChangedAsync(string entityType, int entityId, string operation);
        Task HandleBatchEntityChangesAsync(IEnumerable<(string entityType, int entityId, string operation)> changes);
    }

    public class CacheEventHandler : ICacheEventHandler, IDisposable
    {
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<CacheEventHandler> _logger;
        private readonly SemaphoreSlim _batchProcessingSemaphore;
        private readonly ConcurrentQueue<(string entityType, int entityId, string operation, DateTime timestamp)> _pendingInvalidations;
        private readonly Timer _batchProcessingTimer;
        private readonly TimeSpan _batchDelay = TimeSpan.FromMilliseconds(100); // Batch invalidations for 100ms
        private readonly int _maxBatchSize = 500; // Maximum items to process in one batch
        private bool _disposed = false;

        public CacheEventHandler(
            ICacheInvalidationService cacheInvalidationService,
            ICacheKeyService cacheKeyService,
            ICacheService cacheService,
            ILogger<CacheEventHandler> logger)
        {
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
            _cacheKeyService = cacheKeyService ?? throw new ArgumentNullException(nameof(cacheKeyService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _batchProcessingSemaphore = new SemaphoreSlim(1, 1);
            _pendingInvalidations = new ConcurrentQueue<(string, int, string, DateTime)>();

            // Timer to process batched invalidations
            _batchProcessingTimer = new Timer(ProcessPendingInvalidations, null, _batchDelay, _batchDelay);

            _logger.LogInformation("CacheEventHandler initialized with batch delay: {BatchDelay}ms, max batch size: {MaxBatchSize}",
                _batchDelay.TotalMilliseconds, _maxBatchSize);
        }

        public async Task HandleEntityChangedAsync(string entityType, int entityId, string operation)
        {
            if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0)
            {
                _logger.LogWarning("Invalid entity change notification: {EntityType} {EntityId}", entityType, entityId);
                return;
            }

            try
            {
                _logger.LogDebug("Handling cache invalidation for {EntityType} {EntityId} - {Operation}",
                    entityType, entityId, operation);

                // Add to batch processing queue
                _pendingInvalidations.Enqueue((entityType, entityId, operation, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing cache invalidation for {EntityType} {EntityId}",
                    entityType, entityId);
            }
        }

        public async Task HandleBatchEntityChangesAsync(IEnumerable<(string entityType, int entityId, string operation)> changes)
        {
            if (changes?.Any() != true) return;

            try
            {
                var timestamp = DateTime.UtcNow;
                foreach (var (entityType, entityId, operation) in changes)
                {
                    _pendingInvalidations.Enqueue((entityType, entityId, operation, timestamp));
                }

                _logger.LogDebug("Queued {Count} entity changes for batch cache invalidation", changes.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing batch cache invalidations");
            }
        }

        private async void ProcessPendingInvalidations(object? state)
        {
            if (_disposed || !await _batchProcessingSemaphore.WaitAsync(50)) // Don't wait long
                return;

            try
            {
                var invalidationsToProcess = new List<(string entityType, int entityId, string operation)>();
                var processedEntities = new HashSet<(string, int)>();

                // Dequeue pending invalidations up to max batch size
                var count = 0;
                while (_pendingInvalidations.TryDequeue(out var invalidation) && count < _maxBatchSize)
                {
                    var key = (invalidation.entityType, invalidation.entityId);

                    // Avoid duplicate invalidations for the same entity in the same batch
                    if (!processedEntities.Contains(key))
                    {
                        invalidationsToProcess.Add((invalidation.entityType, invalidation.entityId, invalidation.operation));
                        processedEntities.Add(key);
                        count++;
                    }
                }

                if (invalidationsToProcess.Any())
                {
                    await ProcessInvalidationsBatchAsync(invalidationsToProcess);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending cache invalidations");
            }
            finally
            {
                _batchProcessingSemaphore.Release();
            }
        }

        private async Task ProcessInvalidationsBatchAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            try
            {
                // Group invalidations by entity type for more efficient processing
                var groupedInvalidations = invalidations
                    .GroupBy(i => i.entityType.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.ToList());

                var tasks = new List<Task>();

                foreach (var (entityType, entityInvalidations) in groupedInvalidations)
                {
                    tasks.Add(ProcessEntityTypeInvalidationsAsync(entityType, entityInvalidations));
                }

                await Task.WhenAll(tasks);

                _logger.LogDebug("Processed {Count} cache invalidations across {Types} entity types",
                    invalidations.Count, groupedInvalidations.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing invalidations batch");
            }
        }

        private async Task ProcessEntityTypeInvalidationsAsync(string entityType,
            IList<(string entityType, int entityId, string operation)> invalidations)
        {
            try
            {
                switch (entityType)
                {
                    case "user":
                        await ProcessUserInvalidationsAsync(invalidations);
                        break;

                    case "page":
                        await ProcessPageInvalidationsAsync(invalidations);
                        break;

                    case "company":
                        await ProcessCompanyInvalidationsAsync(invalidations);
                        break;

                    case "location":
                        await ProcessLocationInvalidationsAsync(invalidations);
                        break;

                    case "fileentity":
                        await ProcessFileInvalidationsAsync(invalidations);
                        break;

                    case "folder":
                        await ProcessFolderInvalidationsAsync(invalidations);
                        break;

                    case "permission":
                    case "rolepermission":
                    case "userpermission":
                        await ProcessPermissionInvalidationsAsync(invalidations);
                        break;

                    case "category":
                        await ProcessCategoryInvalidationsAsync(invalidations);
                        break;

                    case "product":
                        await ProcessProductInvalidationsAsync(invalidations);
                        break;

                    case "productvariant":
                        await ProcessProductVariantInvalidationsAsync(invalidations);
                        break;

                    case "usersession":
                        await ProcessSessionInvalidationsAsync(invalidations);
                        break;

                    default:
                        _logger.LogDebug("No specific cache invalidation handler for entity type: {EntityType}", entityType);
                        await ProcessGenericEntityInvalidationsAsync(entityType, invalidations);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing {EntityType} invalidations", entityType);
            }
        }

        private async Task ProcessUserInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var userIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var invalidationTasks = userIds.Select(async userId =>
            {
                try
                {
                    // Invalidate user-specific cache patterns
                    await Task.WhenAll(
                        _cacheService.RemoveAsync(_cacheKeyService.GetEntityKey<User>(userId)),
                        _cacheService.RemoveByPatternAsync($"user:*:{userId}:*"),
                        _cacheService.RemoveByPatternAsync($"user:permissions:{userId}*"),
                        _cacheService.RemoveByPatternAsync($"user:sessions:{userId}*")
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invalidating cache for user {UserId}", userId);
                }
            });

            await Task.WhenAll(invalidationTasks);

            // Invalidate global user cache
            await _cacheService.RemoveByPatternAsync("user:list:*");
        }

        private async Task ProcessPageInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var pageIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var invalidationTasks = pageIds.Select(async pageId =>
            {
                try
                {
                    // Invalidate page-specific cache patterns
                    await Task.WhenAll(
                        _cacheService.RemoveAsync(_cacheKeyService.GetEntityKey<Page>(pageId)),
                        _cacheService.RemoveByPatternAsync($"page:*:{pageId}:*"),
                        _cacheService.RemoveByPatternAsync($"page:components:{pageId}*"),
                        _cacheService.RemoveByPatternAsync($"page:versions:{pageId}*"),
                        _cacheService.RemoveByPatternAsync($"designer:*:{pageId}*")
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invalidating cache for page {PageId}", pageId);
                }
            });

            await Task.WhenAll(invalidationTasks);

            // Invalidate global page cache
            await Task.WhenAll(
                _cacheService.RemoveByPatternAsync("page:list:*"),
                _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("page_hierarchy")),
                _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("published_pages"))
            );
        }

        private async Task ProcessCompanyInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            // For company changes, invalidate all company-related cache
            await Task.WhenAll(
                _cacheService.RemoveByPatternAsync("company:*"),
                _cacheService.RemoveByPatternAsync("location:*") // Locations are company-dependent
            );
        }

        private async Task ProcessLocationInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var locationIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var invalidationTasks = locationIds.Select(async locationId =>
            {
                try
                {
                    await Task.WhenAll(
                        _cacheService.RemoveAsync(_cacheKeyService.GetEntityKey<Location>(locationId)),
                        _cacheService.RemoveByPatternAsync($"location:*:{locationId}:*")
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invalidating cache for location {LocationId}", locationId);
                }
            });

            await Task.WhenAll(invalidationTasks);

            // Invalidate global location cache
            await _cacheService.RemoveByPatternAsync("location:list:*");
        }

        private async Task ProcessFileInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var fileIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var invalidationTasks = fileIds.Select(async fileId =>
            {
                try
                {
                    // Invalidate file-specific cache patterns
                    await Task.WhenAll(
                        _cacheService.RemoveAsync(_cacheKeyService.GetEntityKey<FileEntity>(fileId)),
                        _cacheService.RemoveByPatternAsync($"file:*:{fileId}:*"),
                        _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("file_content", fileId)),
                        _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("file_thumbnail", fileId)),
                        _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("file_preview", fileId)),
                        _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("file_exists", fileId)),
                        _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("file_integrity", fileId))
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invalidating cache for file {FileId}", fileId);
                }
            });

            await Task.WhenAll(invalidationTasks);

            // Invalidate global file cache
            await Task.WhenAll(
                _cacheService.RemoveByPatternAsync("file:list:*"),
                _cacheService.RemoveByPatternAsync("file:recent:*"),
                _cacheService.RemoveByPatternAsync("file:query:*"),
                _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("file_statistics"))
            );
        }

        private async Task ProcessFolderInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var folderIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var invalidationTasks = folderIds.Select(async folderId =>
            {
                try
                {
                    await Task.WhenAll(
                        _cacheService.RemoveAsync(_cacheKeyService.GetEntityKey<Folder>(folderId)),
                        _cacheService.RemoveByPatternAsync($"folder:*:{folderId}:*"),
                        _cacheService.RemoveByPatternAsync($"file:*:folder:{folderId}:*")
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invalidating cache for folder {FolderId}", folderId);
                }
            });

            await Task.WhenAll(invalidationTasks);

            // Invalidate global folder cache
            await Task.WhenAll(
                _cacheService.RemoveByPatternAsync("folder:list:*"),
                _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("folder_tree"))
            );
        }

        private async Task ProcessPermissionInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            // For permission changes, invalidate all permission and user-related cache
            await Task.WhenAll(
                _cacheService.RemoveByPatternAsync("permission:*"),
                _cacheService.RemoveByPatternAsync("user:permissions:*"),
                _cacheService.RemoveByPatternAsync("role:permissions:*")
            );
        }

        private async Task ProcessCategoryInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var categoryIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var invalidationTasks = categoryIds.Select(async categoryId =>
            {
                try
                {
                    await Task.WhenAll(
                        _cacheService.RemoveAsync(_cacheKeyService.GetEntityKey<Category>(categoryId)),
                        _cacheService.RemoveByPatternAsync($"category:*:{categoryId}:*"),
                        _cacheService.RemoveByPatternAsync($"product:*:category:{categoryId}:*")
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invalidating cache for category {CategoryId}", categoryId);
                }
            });

            await Task.WhenAll(invalidationTasks);

            // Invalidate global category cache
            await Task.WhenAll(
                _cacheService.RemoveByPatternAsync("category:list:*"),
                _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("category_tree")),
                _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("all_categories"))
            );
        }

        private async Task ProcessProductInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var productIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var invalidationTasks = productIds.Select(async productId =>
            {
                try
                {
                    await Task.WhenAll(
                        _cacheService.RemoveAsync(_cacheKeyService.GetEntityKey<Product>(productId)),
                        _cacheService.RemoveByPatternAsync($"product:*:{productId}:*"),
                        _cacheService.RemoveByPatternAsync($"product-variant:*:product:{productId}:*")
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invalidating cache for product {ProductId}", productId);
                }
            });

            await Task.WhenAll(invalidationTasks);

            // Invalidate global product cache
            await Task.WhenAll(
                _cacheService.RemoveByPatternAsync("product:list:*"),
                _cacheService.RemoveByPatternAsync("product:featured:*"),
                _cacheService.RemoveByPatternAsync("product:recent:*"),
                _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("product_statistics"))
            );
        }

        private async Task ProcessProductVariantInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var variantIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var invalidationTasks = variantIds.Select(async variantId =>
            {
                try
                {
                    await Task.WhenAll(
                        _cacheService.RemoveAsync(_cacheKeyService.GetEntityKey<ProductVariant>(variantId)),
                        _cacheService.RemoveByPatternAsync($"product-variant:*:{variantId}:*")
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invalidating cache for product variant {VariantId}", variantId);
                }
            });

            await Task.WhenAll(invalidationTasks);

            // Invalidate variant list cache
            await _cacheService.RemoveByPatternAsync("product-variant:list:*");
        }

        private async Task ProcessSessionInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var sessionIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var invalidationTasks = sessionIds.Select(async sessionId =>
            {
                try
                {
                    await _cacheService.RemoveByPatternAsync($"session:*:{sessionId}:*");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invalidating cache for session {SessionId}", sessionId);
                }
            });

            await Task.WhenAll(invalidationTasks);

            // Invalidate session list cache
            await _cacheService.RemoveByPatternAsync("session:list:*");
        }

        private async Task ProcessGenericEntityInvalidationsAsync(string entityType,
            IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var entityIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var invalidationTasks = entityIds.Select(async entityId =>
            {
                try
                {
                    // Invalidate basic entity patterns
                    await Task.WhenAll(
                        _cacheService.RemoveByPatternAsync($"{entityType.ToLowerInvariant()}:*:{entityId}:*"),
                        _cacheService.RemoveByPatternAsync($"{entityType.ToLowerInvariant()}:id:{entityId}*")
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invalidating cache for {EntityType} {EntityId}", entityType, entityId);
                }
            });

            await Task.WhenAll(invalidationTasks);

            // Invalidate entity type list cache
            await _cacheService.RemoveByPatternAsync($"{entityType.ToLowerInvariant()}:list:*");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _disposed = true;
                _batchProcessingTimer?.Dispose();
                _batchProcessingSemaphore?.Dispose();

                _logger.LogInformation("CacheEventHandler disposed");
            }
        }
    }
}