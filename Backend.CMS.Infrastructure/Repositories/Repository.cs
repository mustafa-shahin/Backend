using Backend.CMS.Application.DTOs;
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

        public IQueryable<T> GetAllAsQueryable()
        {
            return _dbSet.AsNoTracking();
        }
        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        }

        public virtual async Task<T?> GetByIdIncludeDeletedAsync(int id)
        {
            return await _context.IncludeDeleted<T>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.AsNoTracking().Where(e => !e.IsDeleted).ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetAllIncludeDeletedAsync()
        {
            return await _context.IncludeDeleted<T>().AsNoTracking().ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AsNoTracking()
                              .Where(e => !e.IsDeleted)
                              .Where(predicate)
                              .ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> FindIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.IncludeDeleted<T>()
                                .AsNoTracking()
                                .Where(predicate)
                                .ToListAsync();
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AsNoTracking()
                              .Where(e => !e.IsDeleted)
                              .FirstOrDefaultAsync(predicate);
        }

        public virtual async Task<T?> FirstOrDefaultIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.IncludeDeleted<T>()
                                .AsNoTracking()
                                .FirstOrDefaultAsync(predicate);
        }

        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(e => !e.IsDeleted).AnyAsync(predicate);
        }

        public virtual async Task<bool> AnyIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.IncludeDeleted<T>().AnyAsync(predicate);
        }

        public virtual async Task<int> CountAsync()
        {
            return await _dbSet.Where(e => !e.IsDeleted).CountAsync();
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(e => !e.IsDeleted).CountAsync(predicate);
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
            return await _dbSet.AsNoTracking()
                              .Where(e => !e.IsDeleted)
                              .OrderBy(e => e.Id)
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize)
                              .ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetPagedIncludeDeletedAsync(int page, int pageSize)
        {
            return await _context.IncludeDeleted<T>()
                                .AsNoTracking()
                                .OrderBy(e => e.Id)
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
            _context.Entry(entity).State = EntityState.Modified;
        }

        public virtual void UpdateRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                _context.Entry(entity).State = EntityState.Modified;
            }
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
            var entity = await _dbSet.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
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

        public virtual async Task<bool> SoftDeleteRangeAsync(IEnumerable<T> entities, int? deletedByUserId = null)
        {
            if (!entities.Any()) return true;

            var entityList = entities.ToList();
            var now = DateTime.UtcNow;

            foreach (var entity in entityList)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = now;
                entity.DeletedByUserId = deletedByUserId;
                entity.UpdatedAt = now;
                entity.UpdatedByUserId = deletedByUserId;
            }

            UpdateRange(entityList);
            await SaveChangesAsync();
            return true;
        }

        public virtual async Task<bool> RestoreAsync(int id, int? restoredByUserId = null)
        {
            var entity = await _context.IncludeDeleted<T>()
                                     .FirstOrDefaultAsync(e => e.Id == id && e.IsDeleted);
            if (entity == null) return false;

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
            return _dbSet.AsNoTracking().Where(e => !e.IsDeleted);
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

        public virtual async Task<IEnumerable<T>> FindWithIncludesAsync<TProperty>(
            Expression<Func<T, bool>> predicate,
            params Expression<Func<T, TProperty>>[] includes)
        {
            IQueryable<T> query = _dbSet.AsNoTracking().Where(e => !e.IsDeleted);

            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            return await query.Where(predicate).ToListAsync();
        }

        public virtual async Task<PagedResult<T>> GetPagedResultAsync(
            int page,
            int pageSize,
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null)
        {
            IQueryable<T> query = _dbSet.AsNoTracking().Where(e => !e.IsDeleted);

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            var totalCount = await query.CountAsync();

            if (orderBy != null)
            {
                query = orderBy(query);
            }
            else
            {
                query = query.OrderBy(e => e.Id);
            }

            var items = await query.Skip((page - 1) * pageSize)
                                  .Take(pageSize)
                                  .ToListAsync();

            return new PagedResult<T>
            {
                Data = items,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
    }
}