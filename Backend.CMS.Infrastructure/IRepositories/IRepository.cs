using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Common;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IRepository<T> where T : BaseEntity
    {
        Task<T?> GetByIdAsync(int id);
        Task<T?> GetByIdIncludeDeletedAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> GetAllIncludeDeletedAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<IEnumerable<T>> FindIncludeDeletedAsync(Expression<Func<T, bool>> predicate);
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
        Task<T?> FirstOrDefaultIncludeDeletedAsync(Expression<Func<T, bool>> predicate);
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
        Task<bool> AnyIncludeDeletedAsync(Expression<Func<T, bool>> predicate);
        Task<int> CountAsync();
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);
        Task<int> CountIncludeDeletedAsync();
        Task<int> CountIncludeDeletedAsync(Expression<Func<T, bool>> predicate);
        Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize);
        Task<IEnumerable<T>> GetPagedIncludeDeletedAsync(int page, int pageSize);
        Task AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);
        void Update(T entity);
        void UpdateRange(IEnumerable<T> entities);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
        Task<bool> SoftDeleteAsync(int id, int? deletedByUserId = null);
        Task<bool> SoftDeleteAsync(T entity, int? deletedByUserId = null);
        Task<bool> SoftDeleteRangeAsync(IEnumerable<T> entities, int? deletedByUserId = null);
        Task<bool> RestoreAsync(int id, int? restoredByUserId = null);
        Task<bool> RestoreAsync(T entity, int? restoredByUserId = null);
        Task<int> SaveChangesAsync();
        IQueryable<T> GetQueryable();
        Task<PagedResult<T>> GetPagedResultAsync(
            int page,
            int pageSize,
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null);
        Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation);
        Task ExecuteInTransactionAsync(Func<Task> operation);
        Task<bool> BatchSoftDeleteAsync(Expression<Func<T, bool>> predicate, int? deletedByUserId = null);
        Task<bool> BatchRestoreAsync(Expression<Func<T, bool>> predicate, int? restoredByUserId = null);
        Task<bool> ExistsAsync(int id);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
    }
}