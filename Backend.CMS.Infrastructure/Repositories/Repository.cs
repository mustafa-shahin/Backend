using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : BaseEntity
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;
        protected readonly ILogger<Repository<T>> _logger;

        public Repository(ApplicationDbContext context, ILogger<Repository<T>> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = context.Set<T>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Repository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = context.Set<T>();
            _logger = new LoggerFactory().CreateLogger<Repository<T>>();
        }

        public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _dbSet.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
                
                _logger.LogDebug("Retrieved entity {EntityType} with ID {Id}: {Found}", 
                    typeof(T).Name, id, entity != null ? "Found" : "Not found");
                
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entity {EntityType} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        public virtual async Task<T?> GetByIdIncludeDeletedAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _context.IncludeDeleted<T>().AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
                
                _logger.LogDebug("Retrieved entity {EntityType} with ID {Id} (including deleted): {Found}", 
                    typeof(T).Name, id, entity != null ? "Found" : "Not found");
                
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entity {EntityType} with ID {Id} (including deleted)", typeof(T).Name, id);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var entities = await _dbSet.AsNoTracking()
                    .Where(e => !e.IsDeleted)
                    .ToListAsync(cancellationToken);
                
                _logger.LogDebug("Retrieved {Count} entities of type {EntityType}", entities.Count, typeof(T).Name);
                
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> GetAllIncludeDeletedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var entities = await _context.IncludeDeleted<T>().AsNoTracking()
                    .ToListAsync(cancellationToken);
                
                _logger.LogDebug("Retrieved {Count} entities of type {EntityType} (including deleted)", entities.Count, typeof(T).Name);
                
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all entities of type {EntityType} (including deleted)", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                var entities = await _dbSet.AsNoTracking()
                    .Where(e => !e.IsDeleted)
                    .Where(predicate)
                    .ToListAsync(cancellationToken);
                
                _logger.LogDebug("Found {Count} entities of type {EntityType} matching predicate", entities.Count, typeof(T).Name);
                
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding entities of type {EntityType} with predicate", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> FindIncludeDeletedAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                var entities = await _context.IncludeDeleted<T>().AsNoTracking()
                    .Where(predicate)
                    .ToListAsync(cancellationToken);
                
                _logger.LogDebug("Found {Count} entities of type {EntityType} matching predicate (including deleted)", entities.Count, typeof(T).Name);
                
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding entities of type {EntityType} with predicate (including deleted)", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _dbSet.AsNoTracking()
                    .Where(e => !e.IsDeleted)
                    .FirstOrDefaultAsync(predicate, cancellationToken);
                
                _logger.LogDebug("Retrieved first entity {EntityType} matching predicate: {Found}", 
                    typeof(T).Name, entity != null ? "Found" : "Not found");
                
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving first entity of type {EntityType} with predicate", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<T?> FirstOrDefaultIncludeDeletedAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _context.IncludeDeleted<T>().AsNoTracking()
                    .FirstOrDefaultAsync(predicate, cancellationToken);
                
                _logger.LogDebug("Retrieved first entity {EntityType} matching predicate (including deleted): {Found}", 
                    typeof(T).Name, entity != null ? "Found" : "Not found");
                
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving first entity of type {EntityType} with predicate (including deleted)", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                var exists = await _dbSet.Where(e => !e.IsDeleted)
                    .AnyAsync(predicate, cancellationToken);
                
                _logger.LogDebug("Entity {EntityType} exists with predicate: {Exists}", typeof(T).Name, exists);
                
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if entity of type {EntityType} exists with predicate", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<bool> AnyIncludeDeletedAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                var exists = await _context.IncludeDeleted<T>()
                    .AnyAsync(predicate, cancellationToken);
                
                _logger.LogDebug("Entity {EntityType} exists with predicate (including deleted): {Exists}", typeof(T).Name, exists);
                
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if entity of type {EntityType} exists with predicate (including deleted)", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var count = await _dbSet.Where(e => !e.IsDeleted)
                    .CountAsync(cancellationToken);
                
                _logger.LogDebug("Count of entities {EntityType}: {Count}", typeof(T).Name, count);
                
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                var count = await _dbSet.Where(e => !e.IsDeleted)
                    .CountAsync(predicate, cancellationToken);
                
                _logger.LogDebug("Count of entities {EntityType} with predicate: {Count}", typeof(T).Name, count);
                
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting entities of type {EntityType} with predicate", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<int> CountIncludeDeletedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var count = await _context.IncludeDeleted<T>()
                    .CountAsync(cancellationToken);
                
                _logger.LogDebug("Count of entities {EntityType} (including deleted): {Count}", typeof(T).Name, count);
                
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting entities of type {EntityType} (including deleted)", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<int> CountIncludeDeletedAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                var count = await _context.IncludeDeleted<T>()
                    .CountAsync(predicate, cancellationToken);
                
                _logger.LogDebug("Count of entities {EntityType} with predicate (including deleted): {Count}", typeof(T).Name, count);
                
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting entities of type {EntityType} with predicate (including deleted)", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidatePagination(page, pageSize);
                
                var entities = await _dbSet.AsNoTracking()
                    .Where(e => !e.IsDeleted)
                    .OrderBy(e => e.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
                
                _logger.LogDebug("Retrieved page {Page} of {EntityType} entities: {Count} items", page, typeof(T).Name, entities.Count);
                
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged entities of type {EntityType}, page {Page}, size {PageSize}", typeof(T).Name, page, pageSize);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> GetPagedIncludeDeletedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidatePagination(page, pageSize);
                
                var entities = await _context.IncludeDeleted<T>().AsNoTracking()
                    .OrderBy(e => e.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
                
                _logger.LogDebug("Retrieved page {Page} of {EntityType} entities (including deleted): {Count} items", page, typeof(T).Name, entities.Count);
                
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged entities of type {EntityType} (including deleted), page {Page}, size {PageSize}", typeof(T).Name, page, pageSize);
                throw;
            }
        }

        public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                await _dbSet.AddAsync(entity, cancellationToken);
                _logger.LogDebug("Added entity {EntityType} with ID {Id} to context", typeof(T).Name, entity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding entity of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            try
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));

                var entityList = entities.ToList();
                if (entityList.Count == 0)
                    return;

                await _dbSet.AddRangeAsync(entityList, cancellationToken);
                _logger.LogDebug("Added {Count} entities of type {EntityType} to context", entityList.Count, typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding range of entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual void Update(T entity)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                _context.Entry(entity).State = EntityState.Modified;
                _logger.LogDebug("Updated entity {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity of type {EntityType} with ID {Id}", typeof(T).Name, entity?.Id);
                throw;
            }
        }

        public virtual void UpdateRange(IEnumerable<T> entities)
        {
            try
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));

                var entityList = entities.ToList();
                if (entityList.Count == 0)
                    return;

                foreach (var entity in entityList)
                {
                    _context.Entry(entity).State = EntityState.Modified;
                }
                
                _logger.LogDebug("Updated {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating range of entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual void Remove(T entity)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                _dbSet.Remove(entity);
                _logger.LogDebug("Removed entity {EntityType} with ID {Id} from context", typeof(T).Name, entity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing entity of type {EntityType} with ID {Id}", typeof(T).Name, entity?.Id);
                throw;
            }
        }

        public virtual void RemoveRange(IEnumerable<T> entities)
        {
            try
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));

                var entityList = entities.ToList();
                if (entityList.Count == 0)
                    return;

                _dbSet.RemoveRange(entityList);
                _logger.LogDebug("Removed {Count} entities of type {EntityType} from context", entityList.Count, typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing range of entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<bool> SoftDeleteAsync(int id, int? deletedByUserId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _dbSet.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
                if (entity == null)
                {
                    _logger.LogWarning("Entity {EntityType} with ID {Id} not found for soft delete", typeof(T).Name, id);
                    return false;
                }

                return await SoftDeleteAsync(entity, deletedByUserId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft deleting entity of type {EntityType} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        public virtual async Task<bool> SoftDeleteAsync(T entity, int? deletedByUserId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                entity.DeletedByUserId = deletedByUserId;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedByUserId = deletedByUserId;

                Update(entity);
                await SaveChangesAsync(cancellationToken);
                
                _logger.LogDebug("Soft deleted entity {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft deleting entity of type {EntityType} with ID {Id}", typeof(T).Name, entity?.Id);
                throw;
            }
        }

        public virtual async Task<bool> SoftDeleteRangeAsync(IEnumerable<T> entities, int? deletedByUserId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));

                var entityList = entities.ToList();
                if (entityList.Count == 0)
                    return true;

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
                await SaveChangesAsync(cancellationToken);
                
                _logger.LogDebug("Soft deleted {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft deleting range of entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<bool> RestoreAsync(int id, int? restoredByUserId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = await _context.IncludeDeleted<T>()
                    .FirstOrDefaultAsync(e => e.Id == id && e.IsDeleted, cancellationToken);
                
                if (entity == null)
                {
                    _logger.LogWarning("Deleted entity {EntityType} with ID {Id} not found for restore", typeof(T).Name, id);
                    return false;
                }

                return await RestoreAsync(entity, restoredByUserId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring entity of type {EntityType} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        public virtual async Task<bool> RestoreAsync(T entity, int? restoredByUserId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                entity.IsDeleted = false;
                entity.DeletedAt = null;
                entity.DeletedByUserId = null;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedByUserId = restoredByUserId;

                Update(entity);
                await SaveChangesAsync(cancellationToken);
                
                _logger.LogDebug("Restored entity {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring entity of type {EntityType} with ID {Id}", typeof(T).Name, entity?.Id);
                throw;
            }
        }

        public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _context.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("SaveChanges completed for {EntityType}: {Count} entities affected", typeof(T).Name, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving changes for {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual IQueryable<T> GetQueryable()
        {
            return _dbSet.AsNoTracking().Where(e => !e.IsDeleted);
        }

        public virtual async Task<PagedResult<T>> GetPagedResultAsync(
            int page,
            int pageSize,
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidatePagination(page, pageSize);
                
                IQueryable<T> query = _dbSet.AsNoTracking().Where(e => !e.IsDeleted);

                if (predicate != null)
                {
                    query = query.Where(predicate);
                }

                var totalCount = await query.CountAsync(cancellationToken);

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
                    .ToListAsync(cancellationToken);

                var result = new PagedResult<T>
                {
                    Data = items,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };

                _logger.LogDebug("Retrieved paged result for {EntityType}: Page {Page}, Size {PageSize}, Total {TotalCount}", 
                    typeof(T).Name, page, pageSize, totalCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged result for {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> FindWithIncludesAsync(
            Expression<Func<T, bool>>? predicate = null,
            params Expression<Func<T, object>>[] includes)
        {
            try
            {
                IQueryable<T> query = _dbSet.AsNoTracking().Where(e => !e.IsDeleted);

                if (includes != null)
                {
                    foreach (var include in includes)
                    {
                        query = query.Include(include);
                    }
                }

                if (predicate != null)
                {
                    query = query.Where(predicate);
                }

                var entities = await query.ToListAsync();
                
                _logger.LogDebug("Found {Count} entities of type {EntityType} with includes", entities.Count, typeof(T).Name);
                
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding entities of type {EntityType} with includes", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<IEnumerable<T>> BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            try
            {
                if (entities == null)
                    throw new ArgumentNullException(nameof(entities));

                var entityList = entities.ToList();
                if (entityList.Count == 0)
                    return entityList;

                await _dbSet.AddRangeAsync(entityList, cancellationToken);
                await SaveChangesAsync(cancellationToken);
                
                _logger.LogDebug("Bulk inserted {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);
                
                return entityList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk inserting entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<int> BulkUpdateAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateExpression, CancellationToken cancellationToken = default)
        {
            try
            {
                // Note: This is a simplified implementation. For production, consider using libraries like EF Core Bulk Extensions
                var entities = await _dbSet.Where(e => !e.IsDeleted).Where(predicate).ToListAsync(cancellationToken);
                
                foreach (var entity in entities)
                {
                    var updatedEntity = updateExpression.Compile()(entity);
                    _context.Entry(entity).CurrentValues.SetValues(updatedEntity);
                }

                var result = await SaveChangesAsync(cancellationToken);
                
                _logger.LogDebug("Bulk updated {Count} entities of type {EntityType}", entities.Count, typeof(T).Name);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk updating entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<int> BulkDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                var entities = await _dbSet.Where(e => !e.IsDeleted).Where(predicate).ToListAsync(cancellationToken);
                
                _dbSet.RemoveRange(entities);
                var result = await SaveChangesAsync(cancellationToken);
                
                _logger.LogDebug("Bulk deleted {Count} entities of type {EntityType}", entities.Count, typeof(T).Name);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var exists = await _dbSet.Where(e => !e.IsDeleted)
                    .AnyAsync(e => e.Id == id, cancellationToken);
                
                _logger.LogDebug("Entity {EntityType} with ID {Id} exists: {Exists}", typeof(T).Name, id, exists);
                
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if entity {EntityType} with ID {Id} exists", typeof(T).Name, id);
                throw;
            }
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            try
            {
                var exists = await _dbSet.Where(e => !e.IsDeleted)
                    .AnyAsync(predicate, cancellationToken);
                
                _logger.LogDebug("Entity {EntityType} exists with predicate: {Exists}", typeof(T).Name, exists);
                
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if entity {EntityType} exists with predicate", typeof(T).Name);
                throw;
            }
        }

        public virtual void Attach(T entity)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                _context.Entry(entity).State = EntityState.Unchanged;
                _logger.LogDebug("Attached entity {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error attaching entity of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual void Detach(T entity)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                _context.Entry(entity).State = EntityState.Detached;
                _logger.LogDebug("Detached entity {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detaching entity of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        public virtual void SetEntityState(T entity, EntityState state)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                _context.Entry(entity).State = state;
                _logger.LogDebug("Set entity {EntityType} with ID {Id} state to {State}", typeof(T).Name, entity.Id, state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting entity state for {EntityType}", typeof(T).Name);
                throw;
            }
        }

        private static void ValidatePagination(int page, int pageSize)
        {
            if (page < 1)
                throw new ArgumentException("Page number must be greater than 0", nameof(page));
            
            if (pageSize < 1)
                throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));
            
            if (pageSize > 1000)
                throw new ArgumentException("Page size cannot exceed 1000", nameof(pageSize));
        }
    }
}