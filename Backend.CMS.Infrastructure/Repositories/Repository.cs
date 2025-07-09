using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Cache;
using Backend.CMS.Infrastructure.Caching;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Specifications;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Repositories
{
    /// <summary>
    /// Enterprise-grade repository implementation
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public class Repository<T> : IRepository<T> where T : BaseEntity
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;
        private readonly IUserSessionService _userSessionService;
        private readonly IRepositoryCache _cache;
        private readonly ILogger<Repository<T>> _logger;
        private readonly string _entityName;
        private readonly IMapper _mapper;

        public Repository(
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            IRepositoryCache cache,
            ILogger<Repository<T>> logger,
            IMapper mapper)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbSet = context.Set<T>();
            _entityName = typeof(T).Name;
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        #region Query Operations

        public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{_entityName}:Id:{id}";

            return await _cache.GetOrSetAsync(
                cacheKey,
                async () => await _dbSet.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken),
                TimeSpan.FromMinutes(5),
                [_entityName, $"{_entityName}:Id:{id}"],
                cancellationToken);
        }

        public virtual async Task<T?> GetByIdTrackedAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        }

        public virtual async Task<T?> GetAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(specification);

            if (!string.IsNullOrEmpty(specification.CacheKey))
            {
                return await _cache.GetOrSetAsync(
                    specification.CacheKey,
                    async () => await query.FirstOrDefaultAsync(cancellationToken),
                    TimeSpan.FromSeconds(specification.CacheDurationSeconds),
                    specification.CacheTags.Count != 0 ? specification.CacheTags : new[] { _entityName },
                    cancellationToken);
            }

            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        public virtual async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(specification);

            if (!string.IsNullOrEmpty(specification.CacheKey))
            {
                return await _cache.GetOrSetAsync(
                    specification.CacheKey,
                    async () => (IReadOnlyList<T>)await query.ToListAsync(cancellationToken),
                    TimeSpan.FromSeconds(specification.CacheDurationSeconds),
                    specification.CacheTags.Any() ? specification.CacheTags : new[] { _entityName },
                    cancellationToken);
            }

            return await query.ToListAsync(cancellationToken);
        }

        public virtual async Task<IReadOnlyList<T>> ListAllAsync(CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{_entityName}:All";

            return await _cache.GetOrSetAsync(
                cacheKey,
                async () => (IReadOnlyList<T>)await _dbSet.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync(cancellationToken),
                TimeSpan.FromMinutes(5),
                [_entityName],
                cancellationToken);
        }

        public virtual async Task<TResult?> GetProjectedAsync<TResult>(IProjectionSpecification<T, TResult> specification, CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(specification);
            var projectedQuery = query.Select(specification.ProjectTo);

            if (!string.IsNullOrEmpty(specification.CacheKey))
            {
                return await _cache.GetOrSetAsync(
                    specification.CacheKey,
                    async () => await projectedQuery.FirstOrDefaultAsync(cancellationToken),
                    TimeSpan.FromSeconds(specification.CacheDurationSeconds),
                    specification.CacheTags.Count != 0 ? specification.CacheTags : new[] { _entityName },
                    cancellationToken);
            }

            return await projectedQuery.FirstOrDefaultAsync(cancellationToken);
        }

        public virtual async Task<IReadOnlyList<TResult>> ListProjectedAsync<TResult>(IProjectionSpecification<T, TResult> specification, CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(specification);
            var projectedQuery = query.Select(specification.ProjectTo);

            if (!string.IsNullOrEmpty(specification.CacheKey))
            {
                return await _cache.GetOrSetAsync(
                    specification.CacheKey,
                    async () => (IReadOnlyList<TResult>)await projectedQuery.ToListAsync(cancellationToken),
                    TimeSpan.FromSeconds(specification.CacheDurationSeconds),
                    specification.CacheTags.Count != 0 ? specification.CacheTags : new[] { _entityName },
                    cancellationToken);
            }

            return await projectedQuery.ToListAsync(cancellationToken);
        }

        public virtual async Task<PagedResult<T>> GetPagedAsync(ISpecification<T> specification, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(specification);
            var totalCount = await query.CountAsync(cancellationToken);

            var pagedQuery = query.Skip((pageNumber - 1) * pageSize).Take(pageSize);
            var items = await pagedQuery.ToListAsync(cancellationToken);

            return new PagedResult<T>(items, pageNumber, pageSize, totalCount);
        }

        public virtual async Task<PagedResult<TResult>> GetPagedProjectedAsync<TResult>(IProjectionSpecification<T, TResult> specification, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(specification);
            var totalCount = await query.CountAsync(cancellationToken);

            var projectedQuery = query.Select(specification.ProjectTo);
            var pagedQuery = projectedQuery.Skip((pageNumber - 1) * pageSize).Take(pageSize);
            var items = await pagedQuery.ToListAsync(cancellationToken);

            return new PagedResult<TResult>(items, pageNumber, pageSize, totalCount);
        }

        public virtual async Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(specification);

            if (!string.IsNullOrEmpty(specification.CacheKey))
            {
                var cacheKey = $"{specification.CacheKey}:Count";
                return await _cache.GetOrSetAsync(
                    cacheKey,
                    async () => await query.CountAsync(cancellationToken),
                    TimeSpan.FromSeconds(specification.CacheDurationSeconds),
                    specification.CacheTags.Any() ? specification.CacheTags : new[] { _entityName },
                    cancellationToken);
            }

            return await query.CountAsync(cancellationToken);
        }

        public virtual async Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(specification);
            return await query.AnyAsync(cancellationToken);
        }

        #endregion

        #region Command Operations

        public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            SetAuditFields(entity, isUpdate: false);

            await _dbSet.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await InvalidateCacheAsync(_entityName);

            _logger.LogDebug("Added {EntityName} with ID {EntityId}", _entityName, entity.Id);
            return entity;
        }

        public virtual async Task<IReadOnlyList<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entities);

            var entityList = entities.ToList();
            if (entityList.Count == 0) return entityList;

            foreach (var entity in entityList)
            {
                SetAuditFields(entity, isUpdate: false);
            }

            await _dbSet.AddRangeAsync(entityList, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await InvalidateCacheAsync(_entityName);

            _logger.LogDebug("Added {Count} {EntityName} entities", entityList.Count, _entityName);
            return entityList;
        }

        public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            SetAuditFields(entity, isUpdate: true);

            _dbSet.Update(entity);
            await _context.SaveChangesAsync(cancellationToken);

            await InvalidateCacheAsync(_entityName, $"{_entityName}:Id:{entity.Id}");

            _logger.LogDebug("Updated {EntityName} with ID {EntityId}", _entityName, entity.Id);
            return entity;
        }

        public virtual async Task<IReadOnlyList<T>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entities);

            var entityList = entities.ToList();
            if (entityList.Count == 0) return entityList;

            foreach (var entity in entityList)
            {
                SetAuditFields(entity, isUpdate: true);
            }

            _dbSet.UpdateRange(entityList);
            await _context.SaveChangesAsync(cancellationToken);

            await InvalidateCacheAsync(_entityName);

            _logger.LogDebug("Updated {Count} {EntityName} entities", entityList.Count, _entityName);
            return entityList;
        }

        public virtual async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            _dbSet.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);

            await InvalidateCacheAsync(_entityName, $"{_entityName}:Id:{entity.Id}");

            _logger.LogDebug("Deleted {EntityName} with ID {EntityId}", _entityName, entity.Id);
        }

        public virtual async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            if (entity != null)
            {
                await DeleteAsync(entity, cancellationToken);
            }
        }

        public virtual async Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entities);

            var entityList = entities.ToList();
            if (entityList.Count == 0) return;

            _dbSet.RemoveRange(entityList);
            await _context.SaveChangesAsync(cancellationToken);

            await InvalidateCacheAsync(_entityName);

            _logger.LogDebug("Deleted {Count} {EntityName} entities", entityList.Count, _entityName);
        }

        public virtual async Task SoftDeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedByUserId = _userSessionService.GetCurrentUserId();
            SetAuditFields(entity, isUpdate: true);

            await _context.SaveChangesAsync(cancellationToken);

            await InvalidateCacheAsync(_entityName, $"{_entityName}:Id:{entity.Id}");

            _logger.LogDebug("Soft deleted {EntityName} with ID {EntityId}", _entityName, entity.Id);
        }

        public virtual async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);
            if (entity != null)
            {
                await SoftDeleteAsync(entity, cancellationToken);
            }
        }

        public virtual async Task SoftDeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entities);

            var entityList = entities.ToList();
            if (entityList.Count == 0) return;

            var now = DateTime.UtcNow;
            var userId = _userSessionService.GetCurrentUserId();

            foreach (var entity in entityList)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = now;
                entity.DeletedByUserId = userId;
                SetAuditFields(entity, isUpdate: true);
            }

            await _context.SaveChangesAsync(cancellationToken);

            await InvalidateCacheAsync(_entityName);

            _logger.LogDebug("Soft deleted {Count} {EntityName} entities", entityList.Count, _entityName);
        }

        public virtual async Task RestoreAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.DeletedByUserId = null;
            SetAuditFields(entity, isUpdate: true);

            await _context.SaveChangesAsync(cancellationToken);

            await InvalidateCacheAsync(_entityName, $"{_entityName}:Id:{entity.Id}");

            _logger.LogDebug("Restored {EntityName} with ID {EntityId}", _entityName, entity.Id);
        }

        public virtual async Task RestoreAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _dbSet.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity != null && entity.IsDeleted)
            {
                await RestoreAsync(entity, cancellationToken);
            }
        }

        #endregion

        #region Bulk Operations

        public virtual async Task<int> BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entities);

            var entityList = entities.ToList();
            if (entityList.Count == 0) return 0;

            foreach (var entity in entityList)
            {
                SetAuditFields(entity, isUpdate: false);
            }

            await _dbSet.AddRangeAsync(entityList, cancellationToken);
            var result = await _context.SaveChangesAsync(cancellationToken);

            await InvalidateCacheAsync(_entityName);

            _logger.LogDebug("Bulk inserted {Count} {EntityName} entities", entityList.Count, _entityName);
            return result;
        }
        public virtual async Task<int> BulkUpdateAsync(
            ISpecification<T> specification,
            Expression<Func<T, T>> updateExpression,
            CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(specification);

            var entities = await query.ToListAsync(cancellationToken);
            var compiledExpression = updateExpression.Compile();

            foreach (var entity in entities)
            {
                var updatedEntity = compiledExpression(entity);

                _mapper.Map(updatedEntity, entity);

                SetAuditFields(entity, isUpdate: true);
            }

            await _context.BulkUpdateAsync(entities, cancellationToken: cancellationToken);

            await InvalidateCacheAsync(_entityName);

            _logger.LogDebug("Bulk updated {Count} {EntityName} entities using AutoMapper and EFCore.BulkExtensions", entities.Count, _entityName);

            return entities.Count;
        }



        public virtual async Task<int> BulkDeleteAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(specification);
            var result = await query.ExecuteDeleteAsync(cancellationToken);

            await InvalidateCacheAsync(_entityName);

            _logger.LogDebug("Bulk deleted {Count} {EntityName} entities", result, _entityName);
            return result;
        }

        public virtual async Task<int> BulkSoftDeleteAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(specification);
            var now = DateTime.UtcNow;
            var userId = _userSessionService.GetCurrentUserId();

            var result = await query.ExecuteUpdateAsync(s => s
                .SetProperty(e => e.IsDeleted, true)
                .SetProperty(e => e.DeletedAt, now)
                .SetProperty(e => e.DeletedByUserId, userId)
                .SetProperty(e => e.UpdatedAt, now)
                .SetProperty(e => e.UpdatedByUserId, userId), cancellationToken);

            await InvalidateCacheAsync(_entityName);

            _logger.LogDebug("Bulk soft deleted {Count} {EntityName} entities", result, _entityName);
            return result;
        }

        #endregion

        #region Transaction Operations

        public virtual async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var result = await operation();
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        public virtual async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await operation();
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        #endregion

        #region Cache Operations

        public virtual async Task InvalidateCacheAsync(params string[] tags)
        {
            await _cache.RemoveByTagsAsync(tags);
        }

        public virtual async Task InvalidateCacheByPatternAsync(string pattern)
        {
            await _cache.RemoveByPatternAsync(pattern);
        }

        #endregion

        #region Raw SQL Operations

        public virtual async Task<IReadOnlyList<T>> FromSqlAsync(string sql, params object[] parameters)
        {
            return await _dbSet.FromSqlRaw(sql, parameters).AsNoTracking().ToListAsync();
        }

        public virtual async Task<int> ExecuteSqlAsync(string sql, params object[] parameters)
        {
            return await _context.Database.ExecuteSqlRawAsync(sql, parameters);
        }

        #endregion

        #region Private Methods

        private IQueryable<T> ApplySpecification(ISpecification<T> specification)
        {
            return SpecificationEvaluator.GetQuery(_dbSet.AsQueryable(), specification);
        }

        private void SetAuditFields(T entity, bool isUpdate)
        {
            var now = DateTime.UtcNow;
            var userId = _userSessionService.GetCurrentUserId();

            if (isUpdate)
            {
                entity.UpdatedAt = now;
                entity.UpdatedByUserId = userId;
            }
            else
            {
                entity.CreatedAt = now;
                entity.CreatedByUserId = userId;
                entity.UpdatedAt = now;
                entity.UpdatedByUserId = userId;
            }
        }

        #endregion
    }
}