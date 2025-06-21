using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Common;
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
        private readonly ILogger<CacheEventHandler> _logger;
        private readonly SemaphoreSlim _batchProcessingSemaphore;
        private readonly ConcurrentQueue<(string entityType, int entityId, string operation, DateTime timestamp)> _pendingInvalidations;
        private readonly Timer _batchProcessingTimer;
        private readonly TimeSpan _batchDelay = TimeSpan.FromMilliseconds(100); // Batch invalidations for 100ms
        private bool _disposed = false;

        public CacheEventHandler(
            ICacheInvalidationService cacheInvalidationService,
            ILogger<CacheEventHandler> logger)
        {
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _batchProcessingSemaphore = new SemaphoreSlim(1, 1);
            _pendingInvalidations = new ConcurrentQueue<(string, int, string, DateTime)>();

            // Timer to process batched invalidations
            _batchProcessingTimer = new Timer(ProcessPendingInvalidations, null, _batchDelay, _batchDelay);
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

                // Dequeue all pending invalidations
                while (_pendingInvalidations.TryDequeue(out var invalidation))
                {
                    var key = (invalidation.entityType, invalidation.entityId);

                    // Avoid duplicate invalidations for the same entity in the same batch
                    if (!processedEntities.Contains(key))
                    {
                        invalidationsToProcess.Add((invalidation.entityType, invalidation.entityId, invalidation.operation));
                        processedEntities.Add(key);
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

                    case "componenttemplate":
                        await _cacheInvalidationService.InvalidateComponentCacheAsync();
                        break;

                    case "company":
                        await _cacheInvalidationService.InvalidateCompanyCacheAsync();
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
                        // For permission changes, invalidate all user caches since permissions affect user access
                        await _cacheInvalidationService.InvalidateUserCacheAsync(0);
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

                    default:
                        _logger.LogDebug("No specific cache invalidation handler for entity type: {EntityType}", entityType);
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

            var tasks = userIds.Select(userId => _cacheInvalidationService.InvalidateUserCacheAsync(userId));
            await Task.WhenAll(tasks);
        }

        private async Task ProcessPageInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var pageIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var tasks = pageIds.Select(pageId => _cacheInvalidationService.InvalidatePageCacheAsync(pageId));
            await Task.WhenAll(tasks);
        }

        private async Task ProcessLocationInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var locationIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var tasks = locationIds.Select(locationId => _cacheInvalidationService.InvalidateLocationCacheAsync(locationId));
            await Task.WhenAll(tasks);
        }

        private async Task ProcessFileInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var fileIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var tasks = fileIds.Select(fileId => _cacheInvalidationService.InvalidateFileCacheAsync(fileId));
            await Task.WhenAll(tasks);
        }

        private async Task ProcessFolderInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            var folderIds = invalidations.Select(i => i.entityId).Distinct().ToList();

            var tasks = folderIds.Select(folderId => _cacheInvalidationService.InvalidateFolderCacheAsync(folderId));
            await Task.WhenAll(tasks);
        }

        private async Task ProcessCategoryInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            // For categories,  might want to invalidate product cache as well since they're related
            await Task.WhenAll(
                Task.Run(() => _logger.LogDebug("Category cache invalidation - would invalidate category cache")),
                Task.Run(() => _logger.LogDebug("Category cache invalidation - would invalidate related product cache"))
            );
        }

        private async Task ProcessProductInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            // For products,  might want to invalidate category cache as well
            await Task.Run(() => _logger.LogDebug("Product cache invalidation - would invalidate product and category cache"));
        }

        private async Task ProcessProductVariantInvalidationsAsync(IList<(string entityType, int entityId, string operation)> invalidations)
        {
            // For product variants,  might want to invalidate parent product cache
            await Task.Run(() => _logger.LogDebug("Product variant cache invalidation - would invalidate variant and parent product cache"));
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
            }
        }
    }

    public class ApplicationDbContextWithCacheInvalidation : ApplicationDbContext
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentBag<(string EntityType, int EntityId, string Operation)> _changedEntities;

        public ApplicationDbContextWithCacheInvalidation(
            DbContextOptions options,
            IHttpContextAccessor? httpContextAccessor = null,
            IServiceProvider? serviceProvider = null)
            : base(options, httpContextAccessor)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _changedEntities = new ConcurrentBag<(string, int, string)>();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Capture changed entities before saving
            CaptureChangedEntities();

            // Save changes
            var result = await base.SaveChangesAsync(cancellationToken);

            // Handle cache invalidation after successful save
            await HandleCacheInvalidationAsync();

            return result;
        }

        public override int SaveChanges()
        {
            // Capture changed entities before saving
            CaptureChangedEntities();

            // Save changes
            var result = base.SaveChanges();

            // Handle cache invalidation after successful save (fire and forget)
            _ = Task.Run(async () => await HandleCacheInvalidationAsync());

            return result;
        }

        private void CaptureChangedEntities()
        {
            _changedEntities.Clear();

            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is BaseEntity &&
                           (e.State == EntityState.Added ||
                            e.State == EntityState.Modified ||
                            e.State == EntityState.Deleted))
                .ToList();

            foreach (var entry in entries)
            {
                try
                {
                    var entity = (BaseEntity)entry.Entity;
                    var entityType = entry.Entity.GetType().Name;
                    var operation = entry.State.ToString();

                    _changedEntities.Add((entityType, entity.Id, operation));
                }
                catch (Exception ex)
                {
                    // Log but don't fail the save operation
                    var logger = _serviceProvider.GetService<ILogger<ApplicationDbContextWithCacheInvalidation>>();
                    logger?.LogWarning(ex, "Error capturing entity change for cache invalidation");
                }
            }
        }

        private async Task HandleCacheInvalidationAsync()
        {
            if (!_changedEntities.Any())
                return;

            try
            {
                // Use a new scope to avoid circular dependencies and ensure proper disposal
                using var scope = _serviceProvider.CreateScope();
                var cacheEventHandler = scope.ServiceProvider.GetService<ICacheEventHandler>();

                if (cacheEventHandler != null)
                {
                    var changes = _changedEntities.ToList();
                    await cacheEventHandler.HandleBatchEntityChangesAsync(changes);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - cache invalidation failure shouldn't break the application
                var logger = _serviceProvider.GetService<ILogger<ApplicationDbContextWithCacheInvalidation>>();
                logger?.LogError(ex, "Error during automatic cache invalidation");
            }
            finally
            {
                _changedEntities.Clear();
            }
        }
    }
}