using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Specifications;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.IRepositories
{
    /// <summary>
    /// repository interface
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public interface IRepository<T> where T : BaseEntity
    {
        #region Query Operations

        /// <summary>
        /// Get entity by ID
        /// </summary>
        Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get entity by ID with change tracking
        /// </summary>
        Task<T?> GetByIdTrackedAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get entity by specification
        /// </summary>
        Task<T?> GetAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get entities by specification
        /// </summary>
        Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all entities
        /// </summary>
        Task<IReadOnlyList<T>> ListAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get projected result by specification
        /// </summary>
        Task<TResult?> GetProjectedAsync<TResult>(IProjectionSpecification<T, TResult> specification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get projected results by specification
        /// </summary>
        Task<IReadOnlyList<TResult>> ListProjectedAsync<TResult>(IProjectionSpecification<T, TResult> specification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get paged result by specification
        /// </summary>
        Task<PagedResult<T>> GetPagedAsync(ISpecification<T> specification, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get paged projected result by specification
        /// </summary>
        Task<PagedResult<TResult>> GetPagedProjectedAsync<TResult>(IProjectionSpecification<T, TResult> specification, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Count entities by specification
        /// </summary>
        Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if any entity exists by specification
        /// </summary>
        Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        #endregion

        #region Command Operations

        /// <summary>
        /// Add entity
        /// </summary>
        Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add multiple entities
        /// </summary>
        Task<IReadOnlyList<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update entity
        /// </summary>
        Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update multiple entities
        /// </summary>
        Task<IReadOnlyList<T>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete entity
        /// </summary>
        Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete entity by ID
        /// </summary>
        Task DeleteAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete multiple entities
        /// </summary>
        Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft delete entity
        /// </summary>
        Task SoftDeleteAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft delete entity by ID
        /// </summary>
        Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft delete multiple entities
        /// </summary>
        Task SoftDeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restore soft deleted entity
        /// </summary>
        Task RestoreAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restore soft deleted entity by ID
        /// </summary>
        Task RestoreAsync(int id, CancellationToken cancellationToken = default);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Bulk insert entities
        /// </summary>
        Task<int> BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk update entities by specification
        /// </summary>
        Task<int> BulkUpdateAsync(ISpecification<T> specification, Expression<Func<T, T>> updateExpression, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk delete entities by specification
        /// </summary>
        Task<int> BulkDeleteAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk soft delete entities by specification
        /// </summary>
        Task<int> BulkSoftDeleteAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        #endregion

        #region Transaction Operations

        /// <summary>
        /// Execute operation in transaction
        /// </summary>
        Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute operation in transaction
        /// </summary>
        Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);

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

        #endregion

        #region Raw SQL Operations

        /// <summary>
        /// Execute raw SQL query
        /// </summary>
        Task<IReadOnlyList<T>> FromSqlAsync(string sql, params object[] parameters);

        /// <summary>
        /// Execute raw SQL command
        /// </summary>
        Task<int> ExecuteSqlAsync(string sql, params object[] parameters);

        #endregion
    }
}