using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Specifications
{
    /// <summary>
    /// Base specification implementation
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public abstract class BaseSpecification<T> : ISpecification<T>
    {
        protected BaseSpecification()
        {
            Includes = new List<Expression<Func<T, object>>>();
            IncludeStrings = new List<string>();
            ThenBy = new List<Expression<Func<T, object>>>();
            ThenByDescending = new List<Expression<Func<T, object>>>();
            CacheTags = new List<string>();
            AsNoTracking = true; // Default to no tracking for performance
            CacheDurationSeconds = 300; // Default 5 minutes
        }

        protected BaseSpecification(Expression<Func<T, bool>> criteria) : this()
        {
            Criteria = criteria;
        }

        public Expression<Func<T, bool>>? Criteria { get; private set; }
        public List<Expression<Func<T, object>>> Includes { get; }
        public List<string> IncludeStrings { get; }
        public Expression<Func<T, object>>? OrderBy { get; private set; }
        public Expression<Func<T, object>>? OrderByDescending { get; private set; }
        public List<Expression<Func<T, object>>> ThenBy { get; }
        public List<Expression<Func<T, object>>> ThenByDescending { get; }
        public Expression<Func<T, object>>? GroupBy { get; private set; }
        public int Take { get; private set; }
        public int Skip { get; private set; }
        public bool AsNoTracking { get; private set; } = true;
        public bool IgnoreQueryFilters { get; private set; }
        public bool AsSplitQuery { get; private set; }
        public string? CacheKey { get; private set; }
        public int CacheDurationSeconds { get; private set; }
        public List<string> CacheTags { get; }

        /// <summary>
        /// Add criteria expression
        /// </summary>
        protected virtual void AddCriteria(Expression<Func<T, bool>> criteriaExpression)
        {
            Criteria = Criteria == null ? criteriaExpression : CombineExpressions(Criteria, criteriaExpression);
        }

        /// <summary>
        /// Add include expression
        /// </summary>
        protected virtual void AddInclude(Expression<Func<T, object>> includeExpression)
        {
            Includes.Add(includeExpression);
        }

        /// <summary>
        /// Add include string
        /// </summary>
        protected virtual void AddInclude(string includeString)
        {
            IncludeStrings.Add(includeString);
        }

        /// <summary>
        /// Add order by expression
        /// </summary>
        protected virtual void AddOrderBy(Expression<Func<T, object>> orderByExpression)
        {
            OrderBy = orderByExpression;
        }

        /// <summary>
        /// Add order by descending expression
        /// </summary>
        protected virtual void AddOrderByDescending(Expression<Func<T, object>> orderByDescExpression)
        {
            OrderByDescending = orderByDescExpression;
        }

        /// <summary>
        /// Add then by expression
        /// </summary>
        protected virtual void AddThenBy(Expression<Func<T, object>> thenByExpression)
        {
            ThenBy.Add(thenByExpression);
        }

        /// <summary>
        /// Add then by descending expression
        /// </summary>
        protected virtual void AddThenByDescending(Expression<Func<T, object>> thenByDescExpression)
        {
            ThenByDescending.Add(thenByDescExpression);
        }

        /// <summary>
        /// Add group by expression
        /// </summary>
        protected virtual void AddGroupBy(Expression<Func<T, object>> groupByExpression)
        {
            GroupBy = groupByExpression;
        }

        /// <summary>
        /// Apply paging
        /// </summary>
        protected virtual void ApplyPaging(int skip, int take)
        {
            Skip = skip;
            Take = take;
        }

        /// <summary>
        /// Enable change tracking
        /// </summary>
        protected virtual void EnableTracking()
        {
            AsNoTracking = false;
        }

        /// <summary>
        /// Disable change tracking
        /// </summary>
        protected virtual void DisableTracking()
        {
            AsNoTracking = true;
        }

        /// <summary>
        /// Ignore query filters
        /// </summary>
        protected virtual void IgnoreFilters()
        {
            IgnoreQueryFilters = true;
        }

        /// <summary>
        /// Use split query for multiple includes
        /// </summary>
        protected virtual void UseSplitQuery()
        {
            AsSplitQuery = true;
        }

        /// <summary>
        /// Set cache configuration
        /// </summary>
        protected virtual void SetCache(string cacheKey, int durationSeconds = 300)
        {
            CacheKey = cacheKey;
            CacheDurationSeconds = durationSeconds;
        }

        /// <summary>
        /// Add cache tag
        /// </summary>
        protected virtual void AddCacheTag(string tag)
        {
            CacheTags.Add(tag);
        }

        /// <summary>
        /// Combine two expressions with AND
        /// </summary>
        private static Expression<Func<T, bool>> CombineExpressions(
            Expression<Func<T, bool>> expr1,
            Expression<Func<T, bool>> expr2)
        {
            var parameter = Expression.Parameter(typeof(T));
            var body = Expression.AndAlso(
                Expression.Invoke(expr1, parameter),
                Expression.Invoke(expr2, parameter));
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }
    }

    /// <summary>
    /// Base projection specification
    /// </summary>
    /// <typeparam name="T">Source entity type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    public abstract class BaseProjectionSpecification<T, TResult> : BaseSpecification<T>, IProjectionSpecification<T, TResult>
    {
        protected BaseProjectionSpecification(Expression<Func<T, TResult>> projectTo) : base()
        {
            ProjectTo = projectTo;
        }

        protected BaseProjectionSpecification(Expression<Func<T, bool>> criteria, Expression<Func<T, TResult>> projectTo) : base(criteria)
        {
            ProjectTo = projectTo;
        }

        public Expression<Func<T, TResult>> ProjectTo { get; }
    }
}