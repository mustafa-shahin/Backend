using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : BaseEntity
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;
        private readonly SemaphoreSlim _semaphore;
        private readonly ILogger<Repository<T>> _logger;
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _entityLocks;
        private readonly ThreadLocal<bool> _semaphoreAcquired;
        private bool _disposed = false;

        public Repository(ApplicationDbContext context, ILogger<Repository<T>> logger = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = context.Set<T>();
            _semaphore = new SemaphoreSlim(1, 1);
            _logger = logger;
            _entityLocks = new ConcurrentDictionary<int, SemaphoreSlim>();
            _semaphoreAcquired = new ThreadLocal<bool>(() => false);
        }

        public IQueryable<T> GetQueryable()
        {
            return _dbSet.AsNoTracking().Where(e => !e.IsDeleted);
        }

        #region Public Methods with Semaphore Management

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
            return await ExecuteWithSemaphoreAsync(async () => await GetAllInternalAsync());
        }

        public virtual async Task<IEnumerable<T>> GetAllIncludeDeletedAsync()
        {
            return await ExecuteWithSemaphoreAsync(async () => await GetAllIncludeDeletedInternalAsync());
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await ExecuteWithSemaphoreAsync(async () => await FindInternalAsync(predicate));
        }

        public virtual async Task<IEnumerable<T>> FindIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return await ExecuteWithSemaphoreAsync(async () => await FindIncludeDeletedInternalAsync(predicate));
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await ExecuteWithSemaphoreAsync(async () => await FirstOrDefaultInternalAsync(predicate));
        }

        public virtual async Task<T?> FirstOrDefaultIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return await ExecuteWithSemaphoreAsync(async () => await FirstOrDefaultIncludeDeletedInternalAsync(predicate));
        }

        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await ExecuteWithSemaphoreAsync(async () => await AnyInternalAsync(predicate));
        }

        public virtual async Task<bool> AnyIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return await ExecuteWithSemaphoreAsync(async () => await AnyIncludeDeletedInternalAsync(predicate));
        }

        public virtual async Task<int> CountAsync()
        {
            return await ExecuteWithSemaphoreAsync(async () => await CountInternalAsync());
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            return await ExecuteWithSemaphoreAsync(async () => await CountInternalAsync(predicate));
        }

        public virtual async Task<int> CountIncludeDeletedAsync()
        {
            return await ExecuteWithSemaphoreAsync(async () => await CountIncludeDeletedInternalAsync());
        }

        public virtual async Task<int> CountIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            return await ExecuteWithSemaphoreAsync(async () => await CountIncludeDeletedInternalAsync(predicate));
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
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            await ExecuteWithSemaphoreAsync(async () => await AddInternalAsync(entity));
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            await ExecuteWithSemaphoreAsync(async () => await AddRangeInternalAsync(entities));
        }

        public virtual void Update(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _context.Entry(entity).State = EntityState.Modified;
        }

        public virtual void UpdateRange(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            foreach (var entity in entities)
            {
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
                    var entity = await GetByIdInternalAsync(id);
                    if (entity == null)
                    {
                        _logger?.LogWarning("Entity {EntityType} with ID {Id} not found for soft delete", typeof(T).Name, id);
                        return false;
                    }

                    return await SoftDeleteInternalAsync(entity, deletedByUserId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during soft delete of entity {EntityType} with ID {Id}", typeof(T).Name, id);
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
                    return await SoftDeleteInternalAsync(entity, deletedByUserId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during soft delete of entity {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
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
                    _logger?.LogError(ex, "Error during bulk soft delete of entities {EntityType}", typeof(T).Name);
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
                    var entity = await GetByIdIncludeDeletedInternalAsync(id);
                    if (entity == null || !entity.IsDeleted)
                    {
                        _logger?.LogWarning("Entity {EntityType} with ID {Id} not found or not deleted for restore", typeof(T).Name, id);
                        return false;
                    }

                    return await RestoreInternalAsync(entity, restoredByUserId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during restore of entity {EntityType} with ID {Id}", typeof(T).Name, id);
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
                    return await RestoreInternalAsync(entity, restoredByUserId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during restore of entity {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
                    throw;
                }
            });
        }

        public virtual async Task<int> SaveChangesAsync()
        {
            return await ExecuteWithSemaphoreAsync(async () => await SaveChangesInternalAsync());
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

        protected virtual async Task<T?> GetByIdInternalAsync(int id)
        {
            return await _dbSet.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        }

        protected virtual async Task<T?> GetByIdIncludeDeletedInternalAsync(int id)
        {
            return await _context.IncludeDeleted<T>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        }

        protected virtual async Task<IEnumerable<T>> GetAllInternalAsync()
        {
            return await _dbSet.AsNoTracking().Where(e => !e.IsDeleted).ToListAsync();
        }

        protected virtual async Task<IEnumerable<T>> GetAllIncludeDeletedInternalAsync()
        {
            return await _context.IncludeDeleted<T>().AsNoTracking().ToListAsync();
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
            return await _context.IncludeDeleted<T>()
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
            return await _context.IncludeDeleted<T>()
                                .AsNoTracking()
                                .FirstOrDefaultAsync(predicate);
        }

        protected virtual async Task<bool> AnyInternalAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(e => !e.IsDeleted).AnyAsync(predicate);
        }

        protected virtual async Task<bool> AnyIncludeDeletedInternalAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.IncludeDeleted<T>().AnyAsync(predicate);
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
            return await _context.IncludeDeleted<T>().CountAsync();
        }

        protected virtual async Task<int> CountIncludeDeletedInternalAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.IncludeDeleted<T>().CountAsync(predicate);
        }

        protected virtual async Task<IEnumerable<T>> GetPagedInternalAsync(int page, int pageSize)
        {
            return await _dbSet.AsNoTracking()
                              .Where(e => !e.IsDeleted)
                              .OrderBy(e => e.Id)
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize)
                              .ToListAsync();
        }

        protected virtual async Task<IEnumerable<T>> GetPagedIncludeDeletedInternalAsync(int page, int pageSize)
        {
            return await _context.IncludeDeleted<T>()
                                .AsNoTracking()
                                .OrderBy(e => e.Id)
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
            try
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                entity.DeletedByUserId = deletedByUserId;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedByUserId = deletedByUserId;

                Update(entity);
                await SaveChangesInternalAsync();

                _logger?.LogInformation("Successfully soft deleted entity {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to soft delete entity {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
                return false;
            }
        }

        protected virtual async Task<bool> SoftDeleteRangeInternalAsync(IEnumerable<T> entities, int? deletedByUserId = null)
        {
            try
            {
                var entityList = entities.ToList();
                if (!entityList.Any()) return true;

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
                await SaveChangesInternalAsync();

                _logger?.LogInformation("Successfully soft deleted {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to soft delete entity range {EntityType}", typeof(T).Name);
                return false;
            }
        }

        protected virtual async Task<bool> RestoreInternalAsync(T entity, int? restoredByUserId = null)
        {
            try
            {
                entity.IsDeleted = false;
                entity.DeletedAt = null;
                entity.DeletedByUserId = null;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedByUserId = restoredByUserId;

                Update(entity);
                await SaveChangesInternalAsync();

                _logger?.LogInformation("Successfully restored entity {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to restore entity {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
                return false;
            }
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
                query = query.OrderBy(e => e.Id);
            }

            var items = await query.Skip((page - 1) * pageSize)
                                  .Take(pageSize)
                                  .ToListAsync();

            return new PagedResult<T>(items, page, pageSize, totalCount);
        }

        #endregion

        #region Semaphore Management

        private async Task<T> ExecuteWithSemaphoreAsync<T>(Func<Task<T>> operation)
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
                using var transaction = await _context.Database.BeginTransactionAsync();
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
                using var transaction = await _context.Database.BeginTransactionAsync();
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

        #region Batch Operations

        public virtual async Task<bool> BatchSoftDeleteAsync(Expression<Func<T, bool>> predicate, int? deletedByUserId = null)
        {
            return await ExecuteWithSemaphoreAsync(async () =>
            {
                try
                {
                    var entities = await FindInternalAsync(predicate);
                    if (!entities.Any()) return true;

                    return await SoftDeleteRangeInternalAsync(entities, deletedByUserId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during batch soft delete of entities {EntityType}", typeof(T).Name);
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
                    var entities = await FindIncludeDeletedInternalAsync(predicate);
                    var deletedEntities = entities.Where(e => e.IsDeleted).ToList();

                    if (!deletedEntities.Any()) return true;

                    var now = DateTime.UtcNow;
                    foreach (var entity in deletedEntities)
                    {
                        entity.IsDeleted = false;
                        entity.DeletedAt = null;
                        entity.DeletedByUserId = null;
                        entity.UpdatedAt = now;
                        entity.UpdatedByUserId = restoredByUserId;
                    }

                    UpdateRange(deletedEntities);
                    await SaveChangesInternalAsync();

                    _logger?.LogInformation("Successfully restored {Count} entities of type {EntityType}", deletedEntities.Count, typeof(T).Name);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during batch restore of entities {EntityType}", typeof(T).Name);
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
                _semaphore?.Dispose();
                _semaphoreAcquired?.Dispose();

                foreach (var entityLock in _entityLocks.Values)
                {
                    entityLock.Dispose();
                }
                _entityLocks.Clear();

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}