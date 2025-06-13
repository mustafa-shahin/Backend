using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Events
{
    public interface ICacheEventHandler
    {
        Task HandleEntityChangedAsync(string entityType, int entityId, string operation);
    }

    public class CacheEventHandler : ICacheEventHandler
    {
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly ILogger<CacheEventHandler> _logger;

        public CacheEventHandler(
            ICacheInvalidationService cacheInvalidationService,
            ILogger<CacheEventHandler> logger)
        {
            _cacheInvalidationService = cacheInvalidationService;
            _logger = logger;
        }

        public async Task HandleEntityChangedAsync(string entityType, int entityId, string operation)
        {
            try
            {
                _logger.LogDebug("Handling cache invalidation for {EntityType} {EntityId} - {Operation}",
                    entityType, entityId, operation);

                switch (entityType.ToLowerInvariant())
                {
                    case "user":
                        await _cacheInvalidationService.InvalidateUserCacheAsync(entityId);
                        break;

                    case "page":
                        await _cacheInvalidationService.InvalidatePageCacheAsync(entityId);
                        break;

                    case "componenttemplate":
                        await _cacheInvalidationService.InvalidateComponentCacheAsync();
                        break;

                    case "company":
                        await _cacheInvalidationService.InvalidateCompanyCacheAsync();
                        break;

                    case "location":
                        await _cacheInvalidationService.InvalidateLocationCacheAsync(entityId);
                        break;

                    case "fileentity":
                        await _cacheInvalidationService.InvalidateFileCacheAsync(entityId);
                        break;

                    case "folder":
                        await _cacheInvalidationService.InvalidateFolderCacheAsync(entityId);
                        break;

                    case "permission":
                    case "rolepermission":
                    case "userpermission":
                        // For permission changes, we might want to invalidate all user caches
                        // since permissions affect user access
                        await _cacheInvalidationService.InvalidateUserCacheAsync(0); // Clear all user caches
                        break;

                    default:
                        _logger.LogDebug("No cache invalidation handler for entity type: {EntityType}", entityType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling cache invalidation for {EntityType} {EntityId}",
                    entityType, entityId);
            }
        }
    }

    // ApplicationDbContext with automatic cache invalidation
    public class ApplicationDbContextWithCacheInvalidation : ApplicationDbContext
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly List<(string EntityType, int EntityId, string Operation)> _changedEntities;

        public ApplicationDbContextWithCacheInvalidation(
            DbContextOptions options,
            IHttpContextAccessor? httpContextAccessor = null,
            IServiceProvider? serviceProvider = null)
            : base(options, httpContextAccessor)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _changedEntities = new List<(string, int, string)>();
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

            // Handle cache invalidation after successful save
            Task.Run(async () => await HandleCacheInvalidationAsync());

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
                var entity = (BaseEntity)entry.Entity;
                var entityType = entry.Entity.GetType().Name;
                var operation = entry.State.ToString();

                _changedEntities.Add((entityType, entity.Id, operation));
            }
        }

        private async Task HandleCacheInvalidationAsync()
        {
            if (!_changedEntities.Any())
                return;

            try
            {
                // Use a new scope to avoid circular dependencies
                using var scope = _serviceProvider.CreateScope();
                var cacheEventHandler = scope.ServiceProvider.GetService<ICacheEventHandler>();

                if (cacheEventHandler != null)
                {
                    var tasks = _changedEntities
                        .Select(e => cacheEventHandler.HandleEntityChangedAsync(e.EntityType, e.EntityId, e.Operation))
                        .ToArray();

                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - cache invalidation failure shouldn't break the application
                var logger = _serviceProvider.GetService<ILogger<ApplicationDbContextWithCacheInvalidation>>();
                logger?.LogError(ex, "Error during automatic cache invalidation");
            }
        }
    }
}