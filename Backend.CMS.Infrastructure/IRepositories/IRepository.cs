using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Common;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.IRepositories
{
    /// <summary>
    /// Enterprise-grade repository interface with advanced features
    /// </summary>
    /// <typeparam name="T">Entity type that inherits from BaseEntity</typeparam>
    public interface IRepository<T> where T : BaseEntity
    {
        #region Basic CRUD Operations

        /// <summary>
        /// Gets an entity by its ID (excludes soft-deleted entities)
        /// </summary>
        /// <param name="id">Entity ID</param>
        /// <returns>Entity or null if not found</returns>
        Task<T?> GetByIdAsync(int id);

        /// <summary>
        /// Gets an entity by its ID (includes soft-deleted entities)
        /// </summary>
        /// <param name="id">Entity ID</param>
        /// <returns>Entity or null if not found</returns>
        Task<T?> GetByIdIncludeDeletedAsync(int id);

        /// <summary>
        /// Gets all entities (excludes soft-deleted entities)
        /// </summary>
        /// <returns>Collection of entities</returns>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// Gets all entities (includes soft-deleted entities)
        /// </summary>
        /// <returns>Collection of entities</returns>
        Task<IEnumerable<T>> GetAllIncludeDeletedAsync();

        /// <summary>
        /// Finds entities based on a predicate (excludes soft-deleted entities)
        /// </summary>
        /// <param name="predicate">Filter predicate</param>
        /// <returns>Collection of matching entities</returns>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Finds entities based on a predicate (includes soft-deleted entities)
        /// </summary>
        /// <param name="predicate">Filter predicate</param>
        /// <returns>Collection of matching entities</returns>
        Task<IEnumerable<T>> FindIncludeDeletedAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Gets the first entity matching the predicate or null (excludes soft-deleted entities)
        /// </summary>
        /// <param name="predicate">Filter predicate</param>
        /// <returns>Entity or null if not found</returns>
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Gets the first entity matching the predicate or null (includes soft-deleted entities)
        /// </summary>
        /// <param name="predicate">Filter predicate</param>
        /// <returns>Entity or null if not found</returns>
        Task<T?> FirstOrDefaultIncludeDeletedAsync(Expression<Func<T, bool>> predicate);

        #endregion

        #region Query Operations

        /// <summary>
        /// Checks if any entity matches the predicate (excludes soft-deleted entities)
        /// </summary>
        /// <param name="predicate">Filter predicate</param>
        /// <returns>True if any entity matches</returns>
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Checks if any entity matches the predicate (includes soft-deleted entities)
        /// </summary>
        /// <param name="predicate">Filter predicate</param>
        /// <returns>True if any entity matches</returns>
        Task<bool> AnyIncludeDeletedAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Counts entities (excludes soft-deleted entities)
        /// </summary>
        /// <returns>Number of entities</returns>
        Task<int> CountAsync();

        /// <summary>
        /// Counts entities matching the predicate (excludes soft-deleted entities)
        /// </summary>
        /// <param name="predicate">Filter predicate</param>
        /// <returns>Number of matching entities</returns>
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Counts entities (includes soft-deleted entities)
        /// </summary>
        /// <returns>Number of entities</returns>
        Task<int> CountIncludeDeletedAsync();

        /// <summary>
        /// Counts entities matching the predicate (includes soft-deleted entities)
        /// </summary>
        /// <param name="predicate">Filter predicate</param>
        /// <returns>Number of matching entities</returns>
        Task<int> CountIncludeDeletedAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Gets a queryable for advanced querying (excludes soft-deleted entities)
        /// </summary>
        /// <returns>IQueryable for the entity type</returns>
        IQueryable<T> GetQueryable();

        #endregion

        #region Pagination

        /// <summary>
        /// Gets entities with pagination (excludes soft-deleted entities)
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <returns>Collection of entities for the specified page</returns>
        Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize);

        /// <summary>
        /// Gets entities with pagination (includes soft-deleted entities)
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <returns>Collection of entities for the specified page</returns>
        Task<IEnumerable<T>> GetPagedIncludeDeletedAsync(int page, int pageSize);

        /// <summary>
        /// Gets a paginated result with metadata
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="predicate">Optional filter predicate</param>
        /// <param name="orderBy">Optional ordering function</param>
        /// <returns>Paginated result with metadata</returns>
        Task<PagedResult<T>> GetPagedResultAsync(
            int page,
            int pageSize,
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null);

        #endregion

        #region Write Operations

        /// <summary>
        /// Adds an entity to the context
        /// </summary>
        /// <param name="entity">Entity to add</param>
        Task AddAsync(T entity);

        /// <summary>
        /// Adds multiple entities to the context
        /// </summary>
        /// <param name="entities">Entities to add</param>
        Task AddRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// Updates an entity in the context
        /// </summary>
        /// <param name="entity">Entity to update</param>
        void Update(T entity);

        /// <summary>
        /// Updates multiple entities in the context
        /// </summary>
        /// <param name="entities">Entities to update</param>
        void UpdateRange(IEnumerable<T> entities);

        /// <summary>
        /// Removes an entity from the context (hard delete)
        /// </summary>
        /// <param name="entity">Entity to remove</param>
        void Remove(T entity);

        /// <summary>
        /// Removes multiple entities from the context (hard delete)
        /// </summary>
        /// <param name="entities">Entities to remove</param>
        void RemoveRange(IEnumerable<T> entities);

        #endregion

        #region Soft Delete Operations

        /// <summary>
        /// Soft deletes an entity by ID
        /// </summary>
        /// <param name="id">Entity ID</param>
        /// <param name="deletedByUserId">ID of user performing the deletion</param>
        /// <returns>True if the entity was successfully soft deleted</returns>
        Task<bool> SoftDeleteAsync(int id, int? deletedByUserId = null);

        /// <summary>
        /// Soft deletes an entity
        /// </summary>
        /// <param name="entity">Entity to soft delete</param>
        /// <param name="deletedByUserId">ID of user performing the deletion</param>
        /// <returns>True if the entity was successfully soft deleted</returns>
        Task<bool> SoftDeleteAsync(T entity, int? deletedByUserId = null);

        /// <summary>
        /// Soft deletes multiple entities
        /// </summary>
        /// <param name="entities">Entities to soft delete</param>
        /// <param name="deletedByUserId">ID of user performing the deletion</param>
        /// <returns>True if all entities were successfully soft deleted</returns>
        Task<bool> SoftDeleteRangeAsync(IEnumerable<T> entities, int? deletedByUserId = null);

        /// <summary>
        /// Restores a soft-deleted entity by ID
        /// </summary>
        /// <param name="id">Entity ID</param>
        /// <param name="restoredByUserId">ID of user performing the restoration</param>
        /// <returns>True if the entity was successfully restored</returns>
        Task<bool> RestoreAsync(int id, int? restoredByUserId = null);

        /// <summary>
        /// Restores a soft-deleted entity
        /// </summary>
        /// <param name="entity">Entity to restore</param>
        /// <param name="restoredByUserId">ID of user performing the restoration</param>
        /// <returns>True if the entity was successfully restored</returns>
        Task<bool> RestoreAsync(T entity, int? restoredByUserId = null);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Performs bulk soft delete based on a predicate
        /// </summary>
        /// <param name="predicate">Filter predicate for entities to delete</param>
        /// <param name="deletedByUserId">ID of user performing the deletion</param>
        /// <returns>True if the operation was successful</returns>
        Task<bool> BulkSoftDeleteAsync(Expression<Func<T, bool>> predicate, int? deletedByUserId = null);

        /// <summary>
        /// Performs bulk restore based on a predicate
        /// </summary>
        /// <param name="predicate">Filter predicate for entities to restore</param>
        /// <param name="restoredByUserId">ID of user performing the restoration</param>
        /// <returns>True if the operation was successful</returns>
        Task<bool> BulkRestoreAsync(Expression<Func<T, bool>> predicate, int? restoredByUserId = null);

        #endregion

        #region Transaction Support

        /// <summary>
        /// Executes an operation within a transaction
        /// </summary>
        /// <typeparam name="TResult">Return type of the operation</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <returns>Result of the operation</returns>
        Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation);

        /// <summary>
        /// Saves all changes to the context
        /// </summary>
        /// <returns>Number of entities affected</returns>
        Task<int> SaveChangesAsync();

        #endregion
    }

    /// <summary>
    /// Extended repository interface for advanced enterprise features
    /// </summary>
    /// <typeparam name="T">Entity type that inherits from BaseEntity</typeparam>
    public interface IAdvancedRepository<T> : IRepository<T> where T : BaseEntity
    {
        #region Performance and Monitoring

        /// <summary>
        /// Gets performance metrics for repository operations
        /// </summary>
        /// <returns>Dictionary of operation names and their metrics</returns>
        Task<Dictionary<string, object>> GetPerformanceMetricsAsync();

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        /// <returns>Cache usage statistics</returns>
        Task<Dictionary<string, object>> GetCacheStatisticsAsync();

        /// <summary>
        /// Clears the repository cache
        /// </summary>
        Task ClearCacheAsync();

        #endregion

        #region Health Checks

        /// <summary>
        /// Performs a health check on the repository
        /// </summary>
        /// <returns>Health check result</returns>
        Task<RepositoryHealthCheck> HealthCheckAsync();

        #endregion

        #region Advanced Querying

        /// <summary>
        /// Executes a raw SQL query and returns entities
        /// </summary>
        /// <param name="sql">SQL query</param>
        /// <param name="parameters">Query parameters</param>
        /// <returns>Collection of entities</returns>
        Task<IEnumerable<T>> ExecuteRawSqlAsync(string sql, params object[] parameters);

        /// <summary>
        /// Executes a compiled query for better performance
        /// </summary>
        /// <typeparam name="TResult">Result type</typeparam>
        /// <param name="compiledQuery">Compiled query function</param>
        /// <returns>Query result</returns>
        Task<TResult> ExecuteCompiledQueryAsync<TResult>(Func<IQueryable<T>, TResult> compiledQuery);

        #endregion

        #region Audit and Change Tracking

        /// <summary>
        /// Gets the change history for an entity
        /// </summary>
        /// <param name="entityId">Entity ID</param>
        /// <returns>Collection of change records</returns>
        Task<IEnumerable<EntityChangeRecord>> GetEntityChangeHistoryAsync(int entityId);

        /// <summary>
        /// Gets pending changes in the context
        /// </summary>
        /// <returns>Collection of pending changes</returns>
        Task<IEnumerable<PendingChange>> GetPendingChangesAsync();

        #endregion

        #region Concurrency Management

        /// <summary>
        /// Attempts to acquire an exclusive lock on an entity
        /// </summary>
        /// <param name="entityId">Entity ID</param>
        /// <param name="timeout">Lock timeout</param>
        /// <returns>Lock handle or null if failed</returns>
        Task<IEntityLock?> TryAcquireEntityLockAsync(int entityId, TimeSpan timeout);

        /// <summary>
        /// Resolves optimistic concurrency conflicts
        /// </summary>
        /// <param name="entity">Entity with conflict</param>
        /// <param name="resolutionStrategy">Resolution strategy</param>
        /// <returns>True if conflict was resolved</returns>
        Task<bool> ResolveConcurrencyConflictAsync(T entity, ConcurrencyResolutionStrategy resolutionStrategy);

        #endregion

        #region Data Validation

        /// <summary>
        /// Validates an entity before persistence
        /// </summary>
        /// <param name="entity">Entity to validate</param>
        /// <returns>Validation result</returns>
        Task<ValidationResult> ValidateEntityAsync(T entity);

        /// <summary>
        /// Validates multiple entities before persistence
        /// </summary>
        /// <param name="entities">Entities to validate</param>
        /// <returns>Collection of validation results</returns>
        Task<IEnumerable<ValidationResult>> ValidateEntitiesAsync(IEnumerable<T> entities);

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Repository health check result
    /// </summary>
    public class RepositoryHealthCheck
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new();
        public TimeSpan ResponseTime { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Entity change record for audit purposes
    /// </summary>
    public class EntityChangeRecord
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty; // Insert, Update, Delete
        public Dictionary<string, object> ChangedProperties { get; set; } = new();
        public Dictionary<string, object> OldValues { get; set; } = new();
        public Dictionary<string, object> NewValues { get; set; } = new();
        public int? ChangedByUserId { get; set; }
        public DateTime ChangedAt { get; set; }
        public string? ChangeReason { get; set; }
    }

    /// <summary>
    /// Pending change in the context
    /// </summary>
    public class PendingChange
    {
        public string EntityType { get; set; } = string.Empty;
        public object? EntityId { get; set; }
        public string ChangeType { get; set; } = string.Empty; // Added, Modified, Deleted
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    /// <summary>
    /// Entity lock handle for concurrency control
    /// </summary>
    public interface IEntityLock : IDisposable
    {
        int EntityId { get; }
        DateTime AcquiredAt { get; }
        DateTime ExpiresAt { get; }
        bool IsExpired { get; }
        Task RenewAsync(TimeSpan extension);
    }

    /// <summary>
    /// Concurrency conflict resolution strategies
    /// </summary>
    public enum ConcurrencyResolutionStrategy
    {
        ClientWins,
        StoreWins,
        Merge,
        Reject
    }

    /// <summary>
    /// Validation result
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = new();
        public List<ValidationWarning> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Validation error
    /// </summary>
    public class ValidationError
    {
        public string PropertyName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public object? AttemptedValue { get; set; }
    }

    /// <summary>
    /// Validation warning
    /// </summary>
    public class ValidationWarning
    {
        public string PropertyName { get; set; } = string.Empty;
        public string WarningMessage { get; set; } = string.Empty;
        public string WarningCode { get; set; } = string.Empty;
    }

    #endregion
}