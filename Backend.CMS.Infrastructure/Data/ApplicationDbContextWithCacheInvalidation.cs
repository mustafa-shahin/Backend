using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;


namespace Backend.CMS.Infrastructure.Data
{
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
