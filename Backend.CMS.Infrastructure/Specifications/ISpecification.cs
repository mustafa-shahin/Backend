using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Specifications
{
    /// <summary>
    /// Specification pattern interface for building complex queries
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public interface ISpecification<T>
    {
        /// <summary>
        /// Criteria expression for filtering
        /// </summary>
        Expression<Func<T, bool>>? Criteria { get; }

        /// <summary>
        /// Include expressions for eager loading
        /// </summary>
        List<Expression<Func<T, object>>> Includes { get; }

        /// <summary>
        /// Include string expressions for eager loading
        /// </summary>
        List<string> IncludeStrings { get; }

        /// <summary>
        /// Order by expressions
        /// </summary>
        Expression<Func<T, object>>? OrderBy { get; }

        /// <summary>
        /// Order by descending expressions
        /// </summary>
        Expression<Func<T, object>>? OrderByDescending { get; }

        /// <summary>
        /// Then by expressions for additional ordering
        /// </summary>
        List<Expression<Func<T, object>>> ThenBy { get; }

        /// <summary>
        /// Then by descending expressions for additional ordering
        /// </summary>
        List<Expression<Func<T, object>>> ThenByDescending { get; }

        /// <summary>
        /// Group by expression
        /// </summary>
        Expression<Func<T, object>>? GroupBy { get; }

        /// <summary>
        /// Take count for pagination
        /// </summary>
        int Take { get; }

        /// <summary>
        /// Skip count for pagination
        /// </summary>
        int Skip { get; }

        /// <summary>
        /// Whether to enable change tracking
        /// </summary>
        bool AsNoTracking { get; }

        /// <summary>
        /// Whether to ignore query filters (for soft delete)
        /// </summary>
        bool IgnoreQueryFilters { get; }

        /// <summary>
        /// Whether to use split query for multiple includes
        /// </summary>
        bool AsSplitQuery { get; }

        /// <summary>
        /// Cache key for query caching
        /// </summary>
        string? CacheKey { get; }

        /// <summary>
        /// Cache duration in seconds
        /// </summary>
        int CacheDurationSeconds { get; }

        /// <summary>
        /// Cache tags for invalidation
        /// </summary>
        List<string> CacheTags { get; }
    }

    /// <summary>
    /// Specification for projection queries
    /// </summary>
    /// <typeparam name="T">Source entity type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    public interface IProjectionSpecification<T, TResult> : ISpecification<T>
    {
        /// <summary>
        /// Projection expression
        /// </summary>
        Expression<Func<T, TResult>> ProjectTo { get; }
    }
}