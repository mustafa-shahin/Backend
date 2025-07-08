using Backend.CMS.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;
using System.Reflection;

namespace Backend.CMS.Infrastructure.Data
{
    /// <summary>
    /// Extension methods for ApplicationDbContext to support enterprise features
    /// </summary>
    public static class ApplicationDbContextExtensions
    {
        /// <summary>
        /// Gets a DbSet that includes soft-deleted entities
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="context">Database context</param>
        /// <returns>DbSet including soft-deleted entities</returns>
        public static IQueryable<T> IncludeDeleted<T>(this ApplicationDbContext context) where T : BaseEntity
        {
            return context.Set<T>().IgnoreQueryFilters();
        }

        /// <summary>
        /// Gets only soft-deleted entities
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="context">Database context</param>
        /// <returns>DbSet containing only soft-deleted entities</returns>
        public static IQueryable<T> OnlyDeleted<T>(this ApplicationDbContext context) where T : BaseEntity
        {
            return context.Set<T>().IgnoreQueryFilters().Where(e => e.IsDeleted);
        }

        /// <summary>
        /// Gets entities that are not soft-deleted (default behavior)
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="context">Database context</param>
        /// <returns>DbSet containing only non-deleted entities</returns>
        public static IQueryable<T> NotDeleted<T>(this ApplicationDbContext context) where T : BaseEntity
        {
            return context.Set<T>().Where(e => !e.IsDeleted);
        }

        /// <summary>
        /// Performs a batch update operation with optimized SQL
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="context">Database context</param>
        /// <param name="predicate">Filter predicate</param>
        /// <param name="updateExpression">Update expression</param>
        /// <returns>Number of affected rows</returns>
        public static async Task<int> BatchUpdateAsync<T>(
            this ApplicationDbContext context,
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, T>> updateExpression) where T : BaseEntity
        {
            try
            {
                // Get the entities to update
                var entities = await context.Set<T>().Where(predicate).ToListAsync();

                if (!entities.Any())
                    return 0;

                // Apply the update expression to each entity
                var compiledUpdate = updateExpression.Compile();

                foreach (var entity in entities)
                {
                    var updatedEntity = compiledUpdate(entity);

                    // Copy properties from updated entity back to original
                    CopyProperties(updatedEntity, entity);
                    entity.UpdatedAt = DateTime.UtcNow;
                }

                return await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Batch update failed for entity type {typeof(T).Name}", ex);
            }
        }

        /// <summary>
        /// Performs a batch soft delete operation
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="context">Database context</param>
        /// <param name="predicate">Filter predicate</param>
        /// <param name="deletedByUserId">User ID performing the deletion</param>
        /// <returns>Number of affected rows</returns>
        public static async Task<int> BatchSoftDeleteAsync<T>(
            this ApplicationDbContext context,
            Expression<Func<T, bool>> predicate,
            int? deletedByUserId = null) where T : BaseEntity
        {
            try
            {
                var entities = await context.Set<T>()
                    .Where(e => !e.IsDeleted)
                    .Where(predicate)
                    .ToListAsync();

                if (!entities.Any())
                    return 0;

                var now = DateTime.UtcNow;

                foreach (var entity in entities)
                {
                    entity.IsDeleted = true;
                    entity.DeletedAt = now;
                    entity.DeletedByUserId = deletedByUserId;
                    entity.UpdatedAt = now;
                    entity.UpdatedByUserId = deletedByUserId;
                }

                return await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Batch soft delete failed for entity type {typeof(T).Name}", ex);
            }
        }

        /// <summary>
        /// Performs a batch restore operation
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="context">Database context</param>
        /// <param name="predicate">Filter predicate</param>
        /// <param name="restoredByUserId">User ID performing the restoration</param>
        /// <returns>Number of affected rows</returns>
        public static async Task<int> BatchRestoreAsync<T>(
            this ApplicationDbContext context,
            Expression<Func<T, bool>> predicate,
            int? restoredByUserId = null) where T : BaseEntity
        {
            try
            {
                var entities = await context.IncludeDeleted<T>()
                    .Where(predicate)
                    .Where(e => e.IsDeleted)
                    .ToListAsync();

                if (!entities.Any())
                    return 0;

                var now = DateTime.UtcNow;

                foreach (var entity in entities)
                {
                    entity.IsDeleted = false;
                    entity.DeletedAt = null;
                    entity.DeletedByUserId = null;
                    entity.UpdatedAt = now;
                    entity.UpdatedByUserId = restoredByUserId;
                }

                return await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Batch restore failed for entity type {typeof(T).Name}", ex);
            }
        }

        /// <summary>
        /// Gets pending changes with detailed information
        /// </summary>
        /// <param name="context">Database context</param>
        /// <returns>Collection of pending change information</returns>
        public static List<EntityChangeInfo> GetPendingChanges(this ApplicationDbContext context)
        {
            var changes = new List<EntityChangeInfo>();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Unchanged)
                    continue;

                var changeInfo = new EntityChangeInfo
                {
                    EntityType = entry.Entity.GetType().Name,
                    State = entry.State,
                    Entity = entry.Entity
                };

                // Get property changes
                foreach (var property in entry.Properties)
                {
                    if (property.IsModified || entry.State == EntityState.Added || entry.State == EntityState.Deleted)
                    {
                        changeInfo.PropertyChanges[property.Metadata.Name] = new PropertyChangeInfo
                        {
                            PropertyName = property.Metadata.Name,
                            OldValue = entry.State == EntityState.Added ? null : property.OriginalValue,
                            NewValue = entry.State == EntityState.Deleted ? null : property.CurrentValue,
                            IsModified = property.IsModified
                        };
                    }
                }

                changes.Add(changeInfo);
            }

            return changes;
        }

        /// <summary>
        /// Executes a raw SQL command with parameters
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="sql">SQL command</param>
        /// <param name="parameters">Parameters</param>
        /// <returns>Number of affected rows</returns>
        public static async Task<int> ExecuteSqlCommandAsync(
            this ApplicationDbContext context,
            string sql,
            params object[] parameters)
        {
            try
            {
                return await context.Database.ExecuteSqlRawAsync(sql, parameters);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"SQL command execution failed: {sql}", ex);
            }
        }

        /// <summary>
        /// Executes a raw SQL query and returns results
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="context">Database context</param>
        /// <param name="sql">SQL query</param>
        /// <param name="parameters">Parameters</param>
        /// <returns>Query results</returns>
        public static async Task<List<T>> ExecuteSqlQueryAsync<T>(
            this ApplicationDbContext context,
            string sql,
            params object[] parameters) where T : BaseEntity
        {
            try
            {
                return await context.Set<T>().FromSqlRaw(sql, parameters).ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"SQL query execution failed: {sql}", ex);
            }
        }

        /// <summary>
        /// Applies global query filters for soft delete
        /// </summary>
        /// <param name="modelBuilder">Model builder</param>
        public static void ApplyGlobalQueryFilters(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                    var filter = Expression.Lambda(Expression.Not(property), parameter);

                    entityType.SetQueryFilter(filter);
                }
            }
        }

        /// <summary>
        /// Configures automatic audit fields for entities
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="currentUserId">Current user ID for audit</param>
        public static void SetAuditFields(this ApplicationDbContext context, int? currentUserId = null)
        {
            var entries = context.ChangeTracker.Entries<BaseEntity>();
            var now = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = now;
                        entry.Entity.UpdatedAt = now;
                        entry.Entity.CreatedByUserId = currentUserId;
                        entry.Entity.UpdatedByUserId = currentUserId;
                        break;

                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = now;
                        entry.Entity.UpdatedByUserId = currentUserId;
                        // Prevent modification of creation audit fields
                        entry.Property(e => e.CreatedAt).IsModified = false;
                        entry.Property(e => e.CreatedByUserId).IsModified = false;
                        break;
                }
            }
        }

        /// <summary>
        /// Detaches all entities from the context
        /// </summary>
        /// <param name="context">Database context</param>
        public static void DetachAllEntities(this ApplicationDbContext context)
        {
            var entries = context.ChangeTracker.Entries().ToList();
            foreach (var entry in entries)
            {
                entry.State = EntityState.Detached;
            }
        }

        /// <summary>
        /// Reloads an entity from the database
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="context">Database context</param>
        /// <param name="entity">Entity to reload</param>
        /// <returns>Reloaded entity</returns>
        public static async Task<T?> ReloadEntityAsync<T>(this ApplicationDbContext context, T entity)
            where T : BaseEntity
        {
            try
            {
                var entry = context.Entry(entity);
                await entry.ReloadAsync();
                return entity;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to reload entity {typeof(T).Name} with ID {entity.Id}", ex);
            }
        }

        /// <summary>
        /// Gets entity change history (requires change tracking to be enabled)
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="context">Database context</param>
        /// <param name="entityId">Entity ID</param>
        /// <returns>Change history</returns>
        public static async Task<List<EntityAuditRecord>> GetEntityHistoryAsync<T>(
            this ApplicationDbContext context,
            int entityId) where T : BaseEntity
        {
            // This would typically query an audit table
            // For now, returning empty list as audit implementation depends on specific requirements
            return new List<EntityAuditRecord>();
        }

        /// <summary>
        /// Performs database health check
        /// </summary>
        /// <param name="context">Database context</param>
        /// <returns>Health check result</returns>
        public static async Task<DatabaseHealthCheck> PerformHealthCheckAsync(this ApplicationDbContext context)
        {
            var healthCheck = new DatabaseHealthCheck
            {
                CheckedAt = DateTime.UtcNow
            };

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Test basic connectivity
                await context.Database.CanConnectAsync();

                stopwatch.Stop();
                healthCheck.ConnectionTime = stopwatch.Elapsed;
                healthCheck.IsHealthy = true;
                healthCheck.Status = "Healthy";

                // Additional checks
                healthCheck.Details["CanConnect"] = true;
                healthCheck.Details["ConnectionTime"] = stopwatch.Elapsed.TotalMilliseconds;

                // Check pending migrations
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                healthCheck.Details["PendingMigrations"] = pendingMigrations.Count();
                healthCheck.Details["HasPendingMigrations"] = pendingMigrations.Any();

                // Check applied migrations
                var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
                healthCheck.Details["AppliedMigrations"] = appliedMigrations.Count();

            }
            catch (Exception ex)
            {
                healthCheck.IsHealthy = false;
                healthCheck.Status = "Unhealthy";
                healthCheck.Details["Error"] = ex.Message;
                healthCheck.Details["Exception"] = ex.GetType().Name;
            }

            return healthCheck;
        }

        #region Private Helper Methods

        /// <summary>
        /// Copies properties from source to destination entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="source">Source entity</param>
        /// <param name="destination">Destination entity</param>
        private static void CopyProperties<T>(T source, T destination) where T : BaseEntity
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => p.Name != nameof(BaseEntity.Id)) // Don't copy ID
                .Where(p => p.Name != nameof(BaseEntity.CreatedAt)) // Don't copy creation audit fields
                .Where(p => p.Name != nameof(BaseEntity.CreatedByUserId));

            foreach (var property in properties)
            {
                try
                {
                    var value = property.GetValue(source);
                    property.SetValue(destination, value);
                }
                catch (Exception)
                {
                    // Skip properties that can't be copied
                    continue;
                }
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Information about entity changes
    /// </summary>
    public class EntityChangeInfo
    {
        public string EntityType { get; set; } = string.Empty;
        public EntityState State { get; set; }
        public object Entity { get; set; } = null!;
        public Dictionary<string, PropertyChangeInfo> PropertyChanges { get; set; } = new();
    }

    /// <summary>
    /// Information about property changes
    /// </summary>
    public class PropertyChangeInfo
    {
        public string PropertyName { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
        public bool IsModified { get; set; }
    }

    /// <summary>
    /// Entity audit record
    /// </summary>
    public class EntityAuditRecord
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string Action { get; set; } = string.Empty; // Insert, Update, Delete
        public Dictionary<string, object> Changes { get; set; } = new();
        public int? UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Database health check result
    /// </summary>
    public class DatabaseHealthCheck
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public TimeSpan ConnectionTime { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public DateTime CheckedAt { get; set; }
    }

    #endregion
}