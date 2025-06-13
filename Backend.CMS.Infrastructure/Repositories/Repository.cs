using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : BaseEntity
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<T?> GetByIdIncludeDeletedAsync(int id)
        {
            return await _context.IncludeDeleted<T>().FirstOrDefaultAsync(e => e.Id == id);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetAllIncludeDeletedAsync()
        {
            return await _context.IncludeDeleted<T>().ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> FindIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.IncludeDeleted<T>().Where(predicate).ToListAsync();
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.FirstOrDefaultAsync(predicate);
        }

        public virtual async Task<T?> FirstOrDefaultIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.IncludeDeleted<T>().FirstOrDefaultAsync(predicate);
        }

        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }

        public virtual async Task<bool> AnyIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.IncludeDeleted<T>().AnyAsync(predicate);
        }

        public virtual async Task<int> CountAsync()
        {
            return await _dbSet.CountAsync();
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.CountAsync(predicate);
        }

        public virtual async Task<int> CountIncludeDeletedAsync()
        {
            return await _context.IncludeDeleted<T>().CountAsync();
        }

        public virtual async Task<int> CountIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.IncludeDeleted<T>().CountAsync(predicate);
        }

        public virtual async Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize)
        {
            return await _dbSet
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetPagedIncludeDeletedAsync(int page, int pageSize)
        {
            return await _context.IncludeDeleted<T>()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public virtual async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public virtual void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public virtual void UpdateRange(IEnumerable<T> entities)
        {
            _dbSet.UpdateRange(entities);
        }

        public virtual void Remove(T entity)
        {
            _dbSet.Remove(entity);
        }

        public virtual void RemoveRange(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        public virtual async Task<bool> SoftDeleteAsync(int id, int? deletedByUserId = null)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null) return false;

            return await SoftDeleteAsync(entity, deletedByUserId);
        }

        public virtual async Task<bool> SoftDeleteAsync(T entity, int? deletedByUserId = null)
        {
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedByUserId = deletedByUserId;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedByUserId = deletedByUserId;

            Update(entity);
            await SaveChangesAsync();
            return true;
        }

        public virtual async Task<bool> RestoreAsync(int id, int? restoredByUserId = null)
        {
            var entity = await GetByIdIncludeDeletedAsync(id);
            if (entity == null || !entity.IsDeleted) return false;

            return await RestoreAsync(entity, restoredByUserId);
        }

        public virtual async Task<bool> RestoreAsync(T entity, int? restoredByUserId = null)
        {
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.DeletedByUserId = null;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedByUserId = restoredByUserId;

            Update(entity);
            await SaveChangesAsync();
            return true;
        }

        public virtual async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
        public IQueryable<T> GetQueryable()
        {
            return _dbSet.Where(e => !e.IsDeleted);
        }
        public async Task<IEnumerable<T>> FindWithOrderingAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int? skip = null,
        int? take = null)
        {
            IQueryable<T> query = GetQueryable();

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            if (orderBy != null)
            {
                query = orderBy(query);
            }

            if (skip.HasValue)
            {
                query = query.Skip(skip.Value);
            }

            if (take.HasValue)
            {
                query = query.Take(take.Value);
            }

            return await query.ToListAsync();
        }
    }
}