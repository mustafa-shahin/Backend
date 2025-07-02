using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    /// <summary>
    /// Base service class providing cache-aware operations for all entity services
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <typeparam name="TDto">DTO type</typeparam>
    public abstract class BaseCacheAwareService<TEntity, TDto>
        where TEntity : BaseEntity
        where TDto : class
    {
        protected readonly IRepository<TEntity> _repository;
        protected readonly ICacheService _cacheService;
        protected readonly ILogger _logger;

        // Cache configuration
        protected virtual TimeSpan DefaultCacheExpiration => TimeSpan.FromMinutes(30);
        protected virtual TimeSpan ListCacheExpiration => TimeSpan.FromMinutes(10);
        protected virtual TimeSpan SearchCacheExpiration => TimeSpan.FromMinutes(5);

        protected BaseCacheAwareService(
            IRepository<TEntity> repository,
            ICacheService cacheService,
            ILogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Abstract Methods (Must be implemented by derived classes)

        /// <summary>
        /// Generate cache key for a single entity by ID
        /// </summary>
        protected abstract string GetEntityCacheKey(int id);

        /// <summary>
        /// Generate cache key patterns for invalidation when entity changes
        /// </summary>
        protected abstract string[] GetEntityCachePatterns(int id);

        /// <summary>
        /// Generate cache key patterns for invalidation when any entity of this type changes
        /// </summary>
        protected abstract string[] GetAllEntitiesCachePatterns();

        /// <summary>
        /// Map entity to DTO
        /// </summary>
        protected abstract Task<TDto> MapToDto(TEntity entity);

        /// <summary>
        /// Map entities to DTOs
        /// </summary>
        protected abstract Task<List<TDto>> MapToDtos(IEnumerable<TEntity> entities);

        #endregion

        #region Cache-Aware Read Operations

        /// <summary>
        /// Get entity by ID with caching
        /// </summary>
        public virtual async Task<TDto?> GetByIdAsync(int id)
        {
            if (id <= 0) return null;

            var cacheKey = GetEntityCacheKey(id);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for {EntityType} ID: {Id}", typeof(TEntity).Name, id);
                var entity = await _repository.GetByIdAsync(id);
                return entity != null ? await MapToDto(entity) : null;
            }, DefaultCacheExpiration);
        }

        /// <summary>
        /// Get all entities with caching
        /// </summary>
        public virtual async Task<List<TDto>> GetAllAsync()
        {
            var cacheKey = _cacheService.GenerateKey($"{typeof(TEntity).Name.ToLower()}", "all");

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for all {EntityType}", typeof(TEntity).Name);
                var entities = await _repository.GetAllAsync();
                return await MapToDtos(entities);
            }, ListCacheExpiration) ?? new List<TDto>();
        }

        /// <summary>
        /// Get paged entities with caching
        /// </summary>
        public virtual async Task<List<TDto>> GetPagedAsync(int page, int pageSize)
        {
            var cacheKey = _cacheService.GenerateKey($"{typeof(TEntity).Name.ToLower()}", "paged", page, pageSize);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for paged {EntityType} - Page: {Page}, Size: {Size}",
                    typeof(TEntity).Name, page, pageSize);
                var entities = await _repository.GetPagedAsync(page, pageSize);
                return await MapToDtos(entities);
            }, ListCacheExpiration) ?? new List<TDto>();
        }

        /// <summary>
        /// Get entity count with caching
        /// </summary>
        public virtual async Task<int> GetCountAsync()
        {
            var cacheKey = _cacheService.GenerateKey($"{typeof(TEntity).Name.ToLower()}", "count");

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for {EntityType} count", typeof(TEntity).Name);
                var count = await _repository.CountAsync();
                return new CountWrapper { Value = count };
            }, ListCacheExpiration);

            return result?.Value ?? 0;
        }

        #endregion

        #region Cache-Aware Write Operations

        /// <summary>
        /// Create entity and refresh cache
        /// </summary>
        protected virtual async Task<TDto> CreateAsync(TEntity entity)
        {
            // Add to repository
            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();

            // Get fresh data and update specific cache
            var cacheKey = GetEntityCacheKey(entity.Id);
            var dto = await _cacheService.RefreshCacheAsync(cacheKey, async () =>
            {
                var freshEntity = await _repository.GetByIdAsync(entity.Id);
                return freshEntity != null ? await MapToDto(freshEntity) : throw new InvalidOperationException("Entity not found after creation");
            }, DefaultCacheExpiration);

            // Invalidate related caches
            await InvalidateRelatedCaches();

            _logger.LogInformation("Created {EntityType} with ID: {Id}", typeof(TEntity).Name, entity.Id);
            return dto;
        }

        /// <summary>
        /// Update entity and refresh cache
        /// </summary>
        protected virtual async Task<TDto> UpdateAsync(TEntity entity)
        {
            // Update in repository
            _repository.Update(entity);
            await _repository.SaveChangesAsync();

            // Refresh specific entity cache
            var cacheKey = GetEntityCacheKey(entity.Id);
            var dto = await _cacheService.RefreshCacheAsync(cacheKey, async () =>
            {
                var freshEntity = await _repository.GetByIdAsync(entity.Id);
                return freshEntity != null ? await MapToDto(freshEntity) : null;
            }, DefaultCacheExpiration);

            // Invalidate related caches
            await InvalidateEntityCaches(entity.Id);

            _logger.LogInformation("Updated {EntityType} with ID: {Id}", typeof(TEntity).Name, entity.Id);
            return dto ?? throw new InvalidOperationException("Entity not found after update");
        }

        /// <summary>
        /// Delete entity and invalidate cache
        /// </summary>
        protected virtual async Task<bool> DeleteAsync(int id, int? deletedByUserId = null)
        {
            var result = await _repository.SoftDeleteAsync(id, deletedByUserId);

            if (result)
            {
                // Invalidate all related caches
                await InvalidateEntityCaches(id);
                _logger.LogInformation("Deleted {EntityType} with ID: {Id}", typeof(TEntity).Name, id);
            }

            return result;
        }

        /// <summary>
        /// Restore entity and refresh cache
        /// </summary>
        protected virtual async Task<bool> RestoreAsync(int id, int? restoredByUserId = null)
        {
            var result = await _repository.RestoreAsync(id, restoredByUserId);

            if (result)
            {
                // Refresh the entity cache
                var cacheKey = GetEntityCacheKey(id);
                await _cacheService.RefreshCacheAsync(cacheKey, async () =>
                {
                    var entity = await _repository.GetByIdAsync(id);
                    return entity != null ? await MapToDto(entity) : null;
                }, DefaultCacheExpiration);

                // Invalidate related caches
                await InvalidateRelatedCaches();

                _logger.LogInformation("Restored {EntityType} with ID: {Id}", typeof(TEntity).Name, id);
            }

            return result;
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Invalidate cache for a specific entity
        /// </summary>
        protected virtual async Task InvalidateEntityCaches(int id)
        {
            try
            {
                var patterns = GetEntityCachePatterns(id);
                await _cacheService.InvalidatePatternsAsync(patterns);

                _logger.LogDebug("Invalidated cache for {EntityType} ID: {Id}", typeof(TEntity).Name, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for {EntityType} ID: {Id}", typeof(TEntity).Name, id);
            }
        }

        /// <summary>
        /// Invalidate all related caches (lists, counts, etc.)
        /// </summary>
        protected virtual async Task InvalidateRelatedCaches()
        {
            try
            {
                var patterns = GetAllEntitiesCachePatterns();
                await _cacheService.InvalidatePatternsAsync(patterns);

                _logger.LogDebug("Invalidated related caches for {EntityType}", typeof(TEntity).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating related caches for {EntityType}", typeof(TEntity).Name);
            }
        }

        /// <summary>
        /// Refresh cache for a specific entity
        /// </summary>
        protected virtual async Task<TDto?> RefreshEntityCache(int id)
        {
            var cacheKey = GetEntityCacheKey(id);
            return await _cacheService.RefreshCacheAsync(cacheKey, async () =>
            {
                var entity = await _repository.GetByIdAsync(id);
                return entity != null ? await MapToDto(entity) : null;
            }, DefaultCacheExpiration);
        }

        /// <summary>
        /// Batch refresh cache for multiple entities
        /// </summary>
        protected virtual async Task<Dictionary<int, TDto?>> RefreshEntitiesCache(IEnumerable<int> ids)
        {
            var result = new Dictionary<int, TDto?>();
            var cacheOperations = new Dictionary<string, TDto>();

            foreach (var id in ids)
            {
                try
                {
                    var entity = await _repository.GetByIdAsync(id);
                    if (entity != null)
                    {
                        var dto = await MapToDto(entity);
                        var cacheKey = GetEntityCacheKey(id);
                        cacheOperations[cacheKey] = dto;
                        result[id] = dto;
                    }
                    else
                    {
                        result[id] = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing cache for {EntityType} ID: {Id}", typeof(TEntity).Name, id);
                    result[id] = null;
                }
            }

            // Batch update cache
            if (cacheOperations.Any())
            {
                await _cacheService.SetBatchAsync(cacheOperations, DefaultCacheExpiration);
            }

            return result;
        }

        #endregion

        #region Helper Classes

        protected class CountWrapper
        {
            public int Value { get; set; }
        }

        #endregion
    }
}