using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Domain.Common;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Extensions
{
    public static class RepositoryExtensions
    {
        public static async Task<List<T>> GetPagedAsync<T>(
            this IRepository<T> repository,
            IQueryable<T> query,
            int page,
            int pageSize) where T : BaseEntity 
        {
            var skip = (page - 1) * pageSize;
            return await query.Skip(skip).Take(pageSize).ToListAsync();
        }

        public static async Task<int> CountAsync<T>(
            this IRepository<T> repository,
            IQueryable<T> query) where T : BaseEntity  
        {
            return await query.CountAsync();
        }

        public static async Task<List<T>> FindAsync<T>(
            this IRepository<T> repository,
            Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            int? take = null) where T : BaseEntity 
        {
            var query = repository.GetQueryable().Where(predicate);

            if (orderBy != null)
            {
                query = orderBy(query);
            }

            if (take.HasValue)
            {
                query = query.Take(take.Value);
            }

            return await query.ToListAsync();
        }
    }
}