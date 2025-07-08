using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces; 
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T>, IDisposable where T : BaseEntity
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;
        private readonly SemaphoreSlim _semaphore;
        private readonly ThreadLocal<bool> _semaphoreAcquired;
        private bool _disposed = false;
        private readonly IUserSessionService _userSessionService;

        public Repository(ApplicationDbContext context, IUserSessionService userSessionService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = context.Set<T>();
            _semaphore = new SemaphoreSlim(1, 1);
            _semaphoreAcquired = new ThreadLocal<bool>(() => false);
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
        }

        /// <summary>
        /// Provides an IQueryable for building complex queries.
        /// Filters out deleted entities by default.
        /// </summary>
        /// <returns>An IQueryable of entities.</returns>
        public IQueryable<T> GetQueryable()
        {
            return _dbSet.AsNoTracking().Where(e => !e.IsDeleted);
        }

        #region Public Methods with Semaphore Management

        // These public methods act as wrappers, acquiring the semaphore before calling internal methods.
        // This ensures that only one operation using this repository instance runs at a time.

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await ExecuteWithSemaphoreAsync(async () => await GetByIdInternalAsync(id));
        }

        public virtual async Task<T?> GetByIdIncludeDeletedAsync(int id)
        {
            return await ExecuteWithSemaphoreAsync(async () => await GetByIdIncludeDeletedInternalAsync(id));
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await ExecuteWithSemaphoreAsync(GetAllInternalAsync);
        }

        public virtual async Task<IEnumerable<T>> GetAllIncludeDeletedAsync()
        {
            return await ExecuteWithSemaphoreAsync(GetAllIncludeDeletedInternalAsync);
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return predicate == null
                ? throw new ArgumentNullException(nameof(predicate))
                : await ExecuteWithSemaphoreAsync(async () => await FindInternalAsync(predicate));
        }

        public virtual async Task<IEnumerable<T>> FindIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return predicate == null
                ? throw new ArgumentNullException(nameof(predicate))
                : await ExecuteWithSemaphoreAsync(async () => await FindIncludeDeletedInternalAsync(predicate));
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return predicate == null
                ? throw new ArgumentNullException(nameof(predicate))
                : await ExecuteWithSemaphoreAsync(async () => await FirstOrDefaultInternalAsync(predicate));
        }

        public virtual async Task<T?> FirstOrDefaultIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return predicate == null
                ? throw new ArgumentNullException(nameof(predicate))
                : await ExecuteWithSemaphoreAsync(async () => await FirstOrDefaultIncludeDeletedInternalAsync(predicate));
        }

        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return predicate == null
                ? throw new ArgumentNullException(nameof(predicate))
                : await ExecuteWithSemaphoreAsync(async () => await AnyInternalAsync(predicate));
        }

        public virtual async Task<bool> AnyIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return predicate == null
                ? throw new ArgumentNullException(nameof(predicate))
                : await ExecuteWithSemaphoreAsync(async () => await AnyIncludeDeletedInternalAsync(predicate));
        }

        public virtual async Task<int> CountAsync()
        {
            return await ExecuteWithSemaphoreAsync(async () => await CountInternalAsync());
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            return predicate == null
                ? throw new ArgumentNullException(nameof(predicate))
                : await ExecuteWithSemaphoreAsync(async () => await CountInternalAsync(predicate));
        }

        public virtual async Task<int> CountIncludeDeletedAsync()
        {
            return await ExecuteWithSemaphoreAsync(async () => await CountIncludeDeletedInternalAsync());
        }

        public virtual async Task<int> CountIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return predicate == null
                ? throw new ArgumentNullException(nameof(predicate))
                : await ExecuteWithSemaphoreAsync(async () => await CountIncludeDeletedInternalAsync(predicate));
        }

        public virtual async Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize)
        {
            return await ExecuteWithSemaphoreAsync(async () => await GetPagedInternalAsync(page, pageSize));
        }

        public virtual async Task<IEnumerable<T>> GetPagedIncludeDeletedAsync(int page, int pageSize)
        {
            return await ExecuteWithSemaphoreAsync(async () => await GetPagedIncludeDeletedInternalAsync(page, pageSize));
        }

        public virtual async Task AddAsync(T entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            await ExecuteWithSemaphoreAsync(async () => await AddInternalAsync(entity));
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
        {
            ArgumentNullException.ThrowIfNull(entities);
            await ExecuteWithSemaphoreAsync(async () => await AddRangeInternalAsync(entities));
        }

        public virtual void Update(T entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            // Ensure the entity is tracked before setting state to Modified.
            // If the entity is detached, attach it first.
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
            }
            _context.Entry(entity).State = EntityState.Modified;
        }

        public virtual void UpdateRange(IEnumerable<T> entities)
        {
            ArgumentNullException.ThrowIfNull(entities);

            foreach (var entity in entities)
            {
                if (_context.Entry(entity).State == EntityState.Detached)
                {
                    _dbSet.Attach(entity);
                }
                _context.Entry(entity).State = EntityState.Modified;
            }
        }

        public virtual void Remove(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _dbSet.Remove(entity);
        }

        public virtual void RemoveRange(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            _dbSet.RemoveRange(entities);
        }

        public virtual async Task<bool> SoftDeleteAsync(int id, int? deletedByUserId = null)
        {
            return await ExecuteWithSemaphoreAsync(async () =>
            {
                try
                {
                    // Retrieve with tracking if it might be updated
                    var entity = await GetByIdInternalTrackedAsync(id);
                    if (entity == null)
                    {
                        return false;
                    }
                    return await SoftDeleteInternalAsync(entity, deletedByUserId);
                }
                catch (Exception ex)
                {
                    throw; 
                }
            });
        }

        public virtual async Task<bool> SoftDeleteAsync(T entity, int? deletedByUserId = null)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return await ExecuteWithSemaphoreAsync(async () =>
            {
                try
                {
                    // If the entity is passed in, ensure it's tracked if it's not already.
                    // This is crucial if the entity comes from outside the current context.
                    var trackedEntity = _context.Entry(entity).State == EntityState.Detached ?
                                        _dbSet.Attach(entity).Entity :
                                        entity;
                    return await SoftDeleteInternalAsync(trackedEntity, deletedByUserId);
                }
                catch (Exception ex)
                {
                    throw;
                }
            });
        }

        public virtual async Task<bool> SoftDeleteRangeAsync(IEnumerable<T> entities, int? deletedByUserId = null)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            return await ExecuteWithSemaphoreAsync(async () =>
            {
                try
                {
                    return await SoftDeleteRangeInternalAsync(entities, deletedByUserId);
                }
                catch (Exception ex)
                {
                    throw;
                }
            });
        }

        public virtual async Task<bool> RestoreAsync(int id, int? restoredByUserId = null)
        {
            return await ExecuteWithSemaphoreAsync(async () =>
            {
                try
                {
                    // Retrieve with tracking if it might be updated
                    var entity = await GetByIdIncludeDeletedInternalTrackedAsync(id);
                    if (entity == null)
                    {
                        return false;
                    }
                    if (!entity.IsDeleted)
                    {
                        return false;
                    }
                    return await RestoreInternalAsync(entity, restoredByUserId);
                }
                catch (Exception ex)
                {
                    throw;
                }
            });
        }

        public virtual async Task<bool> RestoreAsync(T entity, int? restoredByUserId = null)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return await ExecuteWithSemaphoreAsync(async () =>
            {
                try
                {
                    // If the entity is passed in, ensure it's tracked if it's not already.
                    var trackedEntity = _context.Entry(entity).State == EntityState.Detached ?
                                        _dbSet.Attach(entity).Entity :
                                        entity;
                    return await RestoreInternalAsync(trackedEntity, restoredByUserId);
                }
                catch (Exception ex)
                {
                    throw;
                }
            });
        }

        public virtual async Task<int> SaveChangesAsync()
        {
            return await ExecuteWithSemaphoreAsync(SaveChangesInternalAsync);
        }

        public virtual async Task<PagedResult<T>> GetPagedResultAsync(
            int page,
            int pageSize,
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null)
        {
            return await ExecuteWithSemaphoreAsync(async () => await GetPagedResultInternalAsync(page, pageSize, predicate, orderBy));
        }

        #endregion

        #region Internal Methods (No Semaphore Acquisition)

        // These methods are intended to be called by public methods after semaphore acquisition,
        // or within a larger transactional scope already managed by the semaphore.

        protected virtual async Task<T?> GetByIdInternalAsync(int id)
        {
            return await _dbSet.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        }

        // Added for internal operations that require a tracked entity for updates
        protected virtual async Task<T?> GetByIdInternalTrackedAsync(int id)
        {
            return await _dbSet.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        }

        protected virtual async Task<T?> GetByIdIncludeDeletedInternalAsync(int id)
        {
            return await _context.Set<T>().IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        }

        // Added for internal operations that require a tracked entity for updates (even if deleted)
        protected virtual async Task<T?> GetByIdIncludeDeletedInternalTrackedAsync(int id)
        {
            return await _context.Set<T>().IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id);
        }

        protected virtual async Task<IEnumerable<T>> GetAllInternalAsync()
        {
            return await _dbSet.AsNoTracking().Where(e => !e.IsDeleted).ToListAsync();
        }

        protected virtual async Task<IEnumerable<T>> GetAllIncludeDeletedInternalAsync()
        {
            return await _context.Set<T>().IgnoreQueryFilters().AsNoTracking().ToListAsync();
        }

        protected virtual async Task<IEnumerable<T>> FindInternalAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AsNoTracking()
                               .Where(e => !e.IsDeleted)
                               .Where(predicate)
                               .ToListAsync();
        }

        protected virtual async Task<IEnumerable<T>> FindIncludeDeletedInternalAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.Set<T>().IgnoreQueryFilters()
                                 .AsNoTracking()
                                 .Where(predicate)
                                 .ToListAsync();
        }

        protected virtual async Task<T?> FirstOrDefaultInternalAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AsNoTracking()
                               .Where(e => !e.IsDeleted)
                               .FirstOrDefaultAsync(predicate);
        }

        protected virtual async Task<T?> FirstOrDefaultIncludeDeletedInternalAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.Set<T>().IgnoreQueryFilters()
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(predicate);
        }

        protected virtual async Task<bool> AnyInternalAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(e => !e.IsDeleted).AnyAsync(predicate);
        }

        protected virtual async Task<bool> AnyIncludeDeletedInternalAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.Set<T>().IgnoreQueryFilters().AnyAsync(predicate);
        }

        protected virtual async Task<int> CountInternalAsync()
        {
            return await _dbSet.Where(e => !e.IsDeleted).CountAsync();
        }

        protected virtual async Task<int> CountInternalAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(e => !e.IsDeleted).CountAsync(predicate);
        }

        protected virtual async Task<int> CountIncludeDeletedInternalAsync()
        {
            return await _context.Set<T>().IgnoreQueryFilters().CountAsync();
        }

        protected virtual async Task<int> CountIncludeDeletedInternalAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.Set<T>().IgnoreQueryFilters().CountAsync(predicate);
        }

        protected virtual async Task<IEnumerable<T>> GetPagedInternalAsync(int page, int pageSize)
        {
            return await _dbSet.AsNoTracking()
                               .Where(e => !e.IsDeleted)
                               .OrderBy(e => e.Id) // Default order for pagination
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
        }

        protected virtual async Task<IEnumerable<T>> GetPagedIncludeDeletedInternalAsync(int page, int pageSize)
        {
            return await _context.Set<T>().IgnoreQueryFilters()
                                 .AsNoTracking()
                                 .OrderBy(e => e.Id) // Default order for pagination
                                 .Skip((page - 1) * pageSize)
                                 .Take(pageSize)
                                 .ToListAsync();
        }

        protected virtual async Task AddInternalAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        protected virtual async Task AddRangeInternalAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        protected virtual async Task<bool> SoftDeleteInternalAsync(T entity, int? deletedByUserId = null)
        {
            // The entity should already be tracked by the calling public method or explicitly attached.
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
            }

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedByUserId = deletedByUserId ?? _userSessionService.GetCurrentUserId(); 
            entity.UpdatedAt = DateTime.UtcNow; // Soft delete is also an update
            entity.UpdatedByUserId = deletedByUserId ?? _userSessionService.GetCurrentUserId(); 

            _context.Entry(entity).State = EntityState.Modified; // Ensure it's marked as modified
            await SaveChangesInternalAsync(); // Save changes immediately for this single entity operation

            return true;
        }

        protected virtual async Task<bool> SoftDeleteRangeInternalAsync(IEnumerable<T> entities, int? deletedByUserId = null)
        {
            var entityList = entities.ToList();
            if (!entityList.Any()) return true;

            var now = DateTime.UtcNow;
            var userId = deletedByUserId ?? _userSessionService.GetCurrentUserId();

            foreach (var entity in entityList)
            {
                if (_context.Entry(entity).State == EntityState.Detached)
                {
                    _dbSet.Attach(entity);
                }
                entity.IsDeleted = true;
                entity.DeletedAt = now;
                entity.DeletedByUserId = userId;
                entity.UpdatedAt = now;
                entity.UpdatedByUserId = userId;
                _context.Entry(entity).State = EntityState.Modified; // Ensure it's marked as modified
            }

            await SaveChangesInternalAsync(); // Save all changes at once for the range operation

            return true;
        }

        protected virtual async Task<bool> RestoreInternalAsync(T entity, int? restoredByUserId = null)
        {
            // The entity should already be tracked by the calling public method or explicitly attached.
            if (_context.Entry(entity).State == EntityState.Detached)
            {
                _dbSet.Attach(entity);
            }

            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.DeletedByUserId = null;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedByUserId = restoredByUserId ?? _userSessionService.GetCurrentUserId();

            _context.Entry(entity).State = EntityState.Modified; 
            await SaveChangesInternalAsync(); 

            return true;
        }

        protected virtual async Task<int> SaveChangesInternalAsync()
        {
            return await _context.SaveChangesAsync();
        }

        protected virtual async Task<PagedResult<T>> GetPagedResultInternalAsync(
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
                query = query.OrderBy(e => e.Id); // Default ordering
            }

            var items = await query.Skip((page - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

            return new PagedResult<T>(items, page, pageSize, totalCount);
        }

        #endregion

        #region Semaphore Management

        private async Task<TResult> ExecuteWithSemaphoreAsync<TResult>(Func<Task<TResult>> operation)
        {
            if (_semaphoreAcquired.Value)
            {
                // Already holding semaphore in this thread, execute directly
                return await operation();
            }

            await _semaphore.WaitAsync();
            _semaphoreAcquired.Value = true;
            try
            {
                return await operation();
            }
            finally
            {
                _semaphoreAcquired.Value = false;
                _semaphore.Release();
            }
        }

        private async Task ExecuteWithSemaphoreAsync(Func<Task> operation)
        {
            if (_semaphoreAcquired.Value)
            {
                // Already holding semaphore in this thread, execute directly
                await operation();
                return;
            }

            await _semaphore.WaitAsync();
            _semaphoreAcquired.Value = true;
            try
            {
                await operation();
            }
            finally
            {
                _semaphoreAcquired.Value = false;
                _semaphore.Release();
            }
        }

        #endregion

        #region Transaction Support

        public virtual async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation)
        {
            return await ExecuteWithSemaphoreAsync(async () =>
            {
                // Use a transaction scope across operations if necessary
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var result = await operation();
                    await transaction.CommitAsync();
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public virtual async Task ExecuteInTransactionAsync(Func<Task> operation)
        {
            await ExecuteWithSemaphoreAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    await operation();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        #endregion

        #region Batch Operations (using EF Core 7+ ExecuteUpdate/ExecuteDelete)

        public virtual async Task<bool> BatchSoftDeleteAsync(Expression<Func<T, bool>> predicate, int? deletedByUserId = null)
        {
            return await ExecuteWithSemaphoreAsync(async () =>
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var userId = deletedByUserId ?? _userSessionService.GetCurrentUserId();

                    // Using IgnoreQueryFilters to ensure we can target entities that might already be marked as deleted in other contexts
                    var affectedRows = await _dbSet.Where(e => !e.IsDeleted) // Only soft-delete if not already deleted
                                                   .Where(predicate)
                                                   .ExecuteUpdateAsync(s => s
                                                       .SetProperty(e => e.IsDeleted, true)
                                                       .SetProperty(e => e.DeletedAt, now)
                                                       .SetProperty(e => e.DeletedByUserId, userId)
                                                       .SetProperty(e => e.UpdatedAt, now)
                                                       .SetProperty(e => e.UpdatedByUserId, userId));

    
                    return affectedRows > 0;
                }
                catch (Exception ex)
                {
                    throw;
                }
            });
        }

        public virtual async Task<bool> BatchRestoreAsync(Expression<Func<T, bool>> predicate, int? restoredByUserId = null)
        {
            return await ExecuteWithSemaphoreAsync(async () =>
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var userId = restoredByUserId ?? _userSessionService.GetCurrentUserId();

                    var affectedRows = await _dbSet.Where(e => e.IsDeleted) // Only restore if already deleted
                                                   .Where(predicate)
                                                   .ExecuteUpdateAsync(s => s
                                                       .SetProperty(e => e.IsDeleted, false)
                                                       .SetProperty(e => e.DeletedAt, (DateTime?)null)
                                                       .SetProperty(e => e.DeletedByUserId, (int?)null)
                                                       .SetProperty(e => e.UpdatedAt, now)
                                                       .SetProperty(e => e.UpdatedByUserId, userId));

                    return affectedRows > 0;
                }
                catch (Exception ex)
                {
                    throw;
                }
            });
        }

        #endregion

        #region Performance Methods

        public virtual async Task<bool> ExistsAsync(int id)
        {
            return await ExecuteWithSemaphoreAsync(async () => await _dbSet.AnyAsync(e => e.Id == id && !e.IsDeleted));
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return await ExecuteWithSemaphoreAsync(async () => await _dbSet.Where(e => !e.IsDeleted).AnyAsync(predicate));
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Dispose managed state (managed objects)
                _semaphore?.Dispose();
                _semaphoreAcquired?.Dispose();
                // The _context should typically be managed by dependency injection container
                // and its disposal handled by the container. Disposing it here might lead to
                // issues if other components are sharing the same context instance.
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}