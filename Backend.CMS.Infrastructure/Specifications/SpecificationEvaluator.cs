using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Specifications
{
    /// <summary>
    /// Evaluates specifications against IQueryable
    /// </summary>
    public static class SpecificationEvaluator
    {
        /// <summary>
        /// Apply specification to queryable
        /// </summary>
        public static IQueryable<T> GetQuery<T>(IQueryable<T> inputQuery, ISpecification<T> specification) where T : class
        {
            var query = inputQuery;

            // Apply ignore query filters if specified
            if (specification.IgnoreQueryFilters)
            {
                query = query.IgnoreQueryFilters();
            }

            // Apply criteria
            if (specification.Criteria != null)
            {
                query = query.Where(specification.Criteria);
            }

            // Apply includes
            query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));
            query = specification.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

            // Apply ordering
            if (specification.OrderBy != null)
            {
                query = query.OrderBy(specification.OrderBy);
            }
            else if (specification.OrderByDescending != null)
            {
                query = query.OrderByDescending(specification.OrderByDescending);
            }

            // Apply additional ordering
            if (specification.OrderBy != null || specification.OrderByDescending != null)
            {
                var orderedQuery = (IOrderedQueryable<T>)query;

                foreach (var thenBy in specification.ThenBy)
                {
                    orderedQuery = orderedQuery.ThenBy(thenBy);
                }

                foreach (var thenByDesc in specification.ThenByDescending)
                {
                    orderedQuery = orderedQuery.ThenByDescending(thenByDesc);
                }

                query = orderedQuery;
            }

            // Apply grouping
            if (specification.GroupBy != null)
            {
                query = query.GroupBy(specification.GroupBy).SelectMany(x => x);
            }

            // Apply split query if specified
            if (specification.AsSplitQuery)
            {
                query = query.AsSplitQuery();
            }

            // Apply no tracking if specified
            if (specification.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            // Apply paging
            if (specification.Skip > 0)
            {
                query = query.Skip(specification.Skip);
            }

            if (specification.Take > 0)
            {
                query = query.Take(specification.Take);
            }

            return query;
        }

        /// <summary>
        /// Apply projection specification to queryable
        /// </summary>
        public static IQueryable<TResult> GetQuery<T, TResult>(IQueryable<T> inputQuery, IProjectionSpecification<T, TResult> specification) where T : class
        {
            var query = GetQuery(inputQuery, (ISpecification<T>)specification);
            return query.Select(specification.ProjectTo);
        }
    }
}