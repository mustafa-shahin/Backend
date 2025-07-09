using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace Backend.CMS.Infrastructure.Interfaces
{
    /// <summary>
    /// Unit of Work pattern implementation
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        #region Repository Properties

        /// <summary>
        /// User repository
        /// </summary>
        IUserRepository Users { get; }

        /// <summary>
        /// Page repository
        /// </summary>
        IPageRepository Pages { get; }

        /// <summary>
        /// File repository
        /// </summary>
        IFileRepository Files { get; }

        /// <summary>
        /// Folder repository
        /// </summary>
        IFolderRepository Folders { get; }

        /// <summary>
        /// Category repository
        /// </summary>
        ICategoryRepository Categories { get; }

        /// <summary>
        /// Product repository
        /// </summary>
        IProductRepository Products { get; }

        /// <summary>
        /// Product variant repository
        /// </summary>
        IProductVariantRepository ProductVariants { get; }

        /// <summary>
        /// Location repository
        /// </summary>
        ILocationRepository Locations { get; }

        /// <summary>
        /// Company repository
        /// </summary>
        ICompanyRepository Companies { get; }

        #endregion

        #region Transaction Management

        /// <summary>
        /// Begin a new transaction
        /// </summary>
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Commit the current transaction
        /// </summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rollback the current transaction
        /// </summary>
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute operation in transaction
        /// </summary>
        Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute operation in transaction
        /// </summary>
        Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);

        #endregion

        #region Save Operations

        /// <summary>
        /// Save all changes
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Save changes with acceptance flag
        /// </summary>
        Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Bulk insert entities
        /// </summary>
        Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Bulk update entities
        /// </summary>
        Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Bulk delete entities
        /// </summary>
        Task<int> BulkDeleteAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class;

        #endregion

        #region Raw SQL Operations

        /// <summary>
        /// Execute raw SQL command
        /// </summary>
        Task<int> ExecuteSqlRawAsync(string sql, params object[] parameters);

        /// <summary>
        /// Execute raw SQL command with cancellation token
        /// </summary>
        Task<int> ExecuteSqlRawAsync(string sql, CancellationToken cancellationToken, params object[] parameters);

        #endregion

        #region State Management

        /// <summary>
        /// Detach all entities from change tracker
        /// </summary>
        void DetachAllEntities();

        /// <summary>
        /// Reload entity from database
        /// </summary>
        Task ReloadEntityAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Clear change tracker
        /// </summary>
        Task ClearChangeTrackerAsync();

        #endregion

        #region Audit Operations

        /// <summary>
        /// Get pending audit changes
        /// </summary>
        Task<List<string>> GetPendingAuditChangesAsync();

        /// <summary>
        /// Get change tracking entries
        /// </summary>
        Task<List<ChangeTrackingEntry>> GetChangeTrackingEntriesAsync();

        #endregion

        #region Cache Operations

        /// <summary>
        /// Invalidate cache by tags
        /// </summary>
        Task InvalidateCacheAsync(params string[] tags);

        /// <summary>
        /// Invalidate cache by pattern
        /// </summary>
        Task InvalidateCacheByPatternAsync(string pattern);

        /// <summary>
        /// Clear all cache
        /// </summary>
        Task ClearAllCacheAsync();

        #endregion

        #region Performance Monitoring

        /// <summary>
        /// Get performance metrics
        /// </summary>
        Task<PerformanceMetrics> GetPerformanceMetricsAsync();

        #endregion
    }

    /// <summary>
    /// Change tracking entry
    /// </summary>
    public class ChangeTrackingEntry
    {
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public Dictionary<string, object> Changes { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Performance metrics
    /// </summary>
    public class PerformanceMetrics
    {
        public int TotalQueries { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public double CacheHitRate { get; set; }
        public int PendingChanges { get; set; }
        public long MemoryUsage { get; set; }
        public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;
    }
}