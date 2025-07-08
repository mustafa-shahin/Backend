using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Data;
using Microsoft.Data.SqlClient;
using Polly;
using Polly.CircuitBreaker;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : BaseEntity
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;
        private readonly ILogger<Repository<T>> _logger;
        private readonly IMemoryCache _cache;
        private readonly RepositoryOptions _options;

        // Advanced concurrency management
        private readonly ReaderWriterLockSlim _readerWriterLock;
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _entityLocks;
        private readonly SemaphoreSlim _globalLock;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _operationLocks;

        // Performance and resilience
        private readonly IAsyncPolicy _retryPolicy;
        private readonly IAsyncPolicy _circuitBreakerPolicy;
        private readonly Timer _lockCleanupTimer;
        private readonly Timer _cacheCleanupTimer;

        // Metrics and monitoring
        private readonly ConcurrentDictionary<string, PerformanceMetrics> _performanceMetrics;
        private readonly ActivitySource _activitySource;

        // Cache management
        private readonly ConcurrentDictionary<string, CacheItem> _localCache;
        private readonly Timer _performanceReportTimer;

        // Configuration
        private readonly string _entityName;
        private bool _disposed = false;

        public Repository(
            ApplicationDbContext context,
            ILogger<Repository<T>> logger,
            IMemoryCache cache = null,
            IOptions<RepositoryOptions> options = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = context.Set<T>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
            _options = options?.Value ?? new RepositoryOptions();

            _entityName = typeof(T).Name;

            // Initialize concurrency controls
            _readerWriterLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _entityLocks = new ConcurrentDictionary<int, SemaphoreSlim>();
            _globalLock = new SemaphoreSlim(_options.MaxConcurrentOperations, _options.MaxConcurrentOperations);
            _operationLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

            // Initialize resilience policies
            _retryPolicy = CreateRetryPolicy();
            _circuitBreakerPolicy = CreateCircuitBreakerPolicy();

            // Initialize monitoring
            _performanceMetrics = new ConcurrentDictionary<string, PerformanceMetrics>();
            _activitySource = new ActivitySource($"Repository.{_entityName}");
            _localCache = new ConcurrentDictionary<string, CacheItem>();

            // Initialize cleanup timers
            _lockCleanupTimer = new Timer(CleanupUnusedLocks, null,
                TimeSpan.FromMinutes(_options.LockCleanupIntervalMinutes),
                TimeSpan.FromMinutes(_options.LockCleanupIntervalMinutes));

            _cacheCleanupTimer = new Timer(CleanupExpiredCache, null,
                TimeSpan.FromMinutes(_options.CacheCleanupIntervalMinutes),
                TimeSpan.FromMinutes(_options.CacheCleanupIntervalMinutes));

            _performanceReportTimer = new Timer(ReportPerformanceMetrics, null,
                TimeSpan.FromMinutes(_options.PerformanceReportIntervalMinutes),
                TimeSpan.FromMinutes(_options.PerformanceReportIntervalMinutes));
        }

        public Repository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IQueryable<T> GetQueryable()
        {
            using var activity = _activitySource.StartActivity("GetQueryable");
            return _dbSet.AsNoTracking().Where(e => !e.IsDeleted);
        }

        #region Enhanced Read Operations with Caching

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            using var activity = _activitySource.StartActivity("GetByIdAsync");
            activity?.SetTag("entity.id", id);

            var cacheKey = GenerateCacheKey("GetById", id);

            // Try cache first
            if (_options.EnableCaching && TryGetFromCache<T>(cacheKey, out var cachedEntity))
            {
                activity?.SetTag("cache.hit", true);
                return cachedEntity;
            }

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var entity = await _dbSet.AsNoTracking()
                        .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

                    RecordPerformanceMetric("GetByIdAsync", stopwatch.Elapsed);

                    if (entity != null && _options.EnableCaching)
                    {
                        AddToCache(cacheKey, entity, _options.DefaultCacheExpiry);
                    }

                    return entity;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual async Task<T?> GetByIdIncludeDeletedAsync(int id)
        {
            using var activity = _activitySource.StartActivity("GetByIdIncludeDeletedAsync");
            activity?.SetTag("entity.id", id);

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var entity = await _context.IncludeDeleted<T>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(e => e.Id == id);

                    RecordPerformanceMetric("GetByIdIncludeDeletedAsync", stopwatch.Elapsed);
                    return entity;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            using var activity = _activitySource.StartActivity("GetAllAsync");

            var cacheKey = GenerateCacheKey("GetAll");

            if (_options.EnableCaching && TryGetFromCache<IEnumerable<T>>(cacheKey, out var cachedEntities))
            {
                activity?.SetTag("cache.hit", true);
                return cachedEntities;
            }

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var entities = await _dbSet.AsNoTracking()
                        .Where(e => !e.IsDeleted)
                        .ToListAsync();

                    RecordPerformanceMetric("GetAllAsync", stopwatch.Elapsed);

                    if (_options.EnableCaching && entities.Count <= _options.MaxCacheableCollectionSize)
                    {
                        AddToCache(cacheKey, entities, _options.DefaultCacheExpiry);
                    }

                    return entities;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            using var activity = _activitySource.StartActivity("FindAsync");
            activity?.SetTag("predicate", predicate.ToString());

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var entities = await _dbSet.AsNoTracking()
                        .Where(e => !e.IsDeleted)
                        .Where(predicate)
                        .ToListAsync();

                    RecordPerformanceMetric("FindAsync", stopwatch.Elapsed);
                    return entities;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            using var activity = _activitySource.StartActivity("FirstOrDefaultAsync");

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var entity = await _dbSet.AsNoTracking()
                        .Where(e => !e.IsDeleted)
                        .FirstOrDefaultAsync(predicate);

                    RecordPerformanceMetric("FirstOrDefaultAsync", stopwatch.Elapsed);
                    return entity;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        #endregion

        #region Enhanced Write Operations with Entity-Level Locking

        public virtual async Task AddAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            using var activity = _activitySource.StartActivity("AddAsync");
            activity?.SetTag("entity.type", _entityName);

            await ExecuteWithCircuitBreakerAsync(async () =>
            {
                await _globalLock.WaitAsync();
                try
                {
                    _readerWriterLock.EnterWriteLock();
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();

                        // Set audit fields
                        entity.CreatedAt = DateTime.UtcNow;
                        entity.UpdatedAt = DateTime.UtcNow;

                        await _dbSet.AddAsync(entity);

                        RecordPerformanceMetric("AddAsync", stopwatch.Elapsed);
                        InvalidateRelevantCache();

                        _logger.LogDebug("Added entity {EntityType} with temporary ID", _entityName);
                    }
                    finally
                    {
                        _readerWriterLock.ExitWriteLock();
                    }
                }
                finally
                {
                    _globalLock.Release();
                }
            });
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var entityList = entities.ToList();
            if (!entityList.Any()) return;

            using var activity = _activitySource.StartActivity("AddRangeAsync");
            activity?.SetTag("entity.count", entityList.Count);

            await ExecuteWithCircuitBreakerAsync(async () =>
            {
                await _globalLock.WaitAsync();
                try
                {
                    _readerWriterLock.EnterWriteLock();
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();

                        var now = DateTime.UtcNow;
                        foreach (var entity in entityList)
                        {
                            entity.CreatedAt = now;
                            entity.UpdatedAt = now;
                        }

                        await _dbSet.AddRangeAsync(entityList);

                        RecordPerformanceMetric("AddRangeAsync", stopwatch.Elapsed);
                        InvalidateRelevantCache();

                        _logger.LogDebug("Added {Count} entities of type {EntityType}", entityList.Count, _entityName);
                    }
                    finally
                    {
                        _readerWriterLock.ExitWriteLock();
                    }
                }
                finally
                {
                    _globalLock.Release();
                }
            });
        }

        public virtual void Update(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            using var activity = _activitySource.StartActivity("Update");
            activity?.SetTag("entity.id", entity.Id);

            var entityLock = GetEntityLock(entity.Id);
            lock (entityLock) // Using synchronous lock for non-async operation
            {
                try
                {
                    var stopwatch = Stopwatch.StartNew();

                    entity.UpdatedAt = DateTime.UtcNow;
                    _context.Entry(entity).State = EntityState.Modified;

                    RecordPerformanceMetric("Update", stopwatch.Elapsed);
                    InvalidateEntityCache(entity.Id);

                    _logger.LogDebug("Updated entity {EntityType} with ID {EntityId}", _entityName, entity.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating entity {EntityType} with ID {EntityId}", _entityName, entity.Id);
                    throw;
                }
            }
        }

        public virtual async Task<bool> SoftDeleteAsync(int id, int? deletedByUserId = null)
        {
            using var activity = _activitySource.StartActivity("SoftDeleteAsync");
            activity?.SetTag("entity.id", id);

            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var entityLock = GetEntityLock(id);
                await entityLock.WaitAsync();
                try
                {
                    var stopwatch = Stopwatch.StartNew();

                    var entity = await _dbSet.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
                    if (entity == null)
                    {
                        _logger.LogWarning("Entity {EntityType} with ID {Id} not found for soft delete", _entityName, id);
                        return false;
                    }

                    entity.IsDeleted = true;
                    entity.DeletedAt = DateTime.UtcNow;
                    entity.DeletedByUserId = deletedByUserId;
                    entity.UpdatedAt = DateTime.UtcNow;
                    entity.UpdatedByUserId = deletedByUserId;

                    _context.Entry(entity).State = EntityState.Modified;

                    RecordPerformanceMetric("SoftDeleteAsync", stopwatch.Elapsed);
                    InvalidateEntityCache(id);

                    _logger.LogInformation("Soft deleted entity {EntityType} with ID {Id}", _entityName, id);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error soft deleting entity {EntityType} with ID {Id}", _entityName, id);
                    throw;
                }
                finally
                {
                    entityLock.Release();
                }
            });
        }

        #endregion

        #region Advanced Query Operations with Optimizations

        public virtual async Task<PagedResult<T>> GetPagedResultAsync(
            int page,
            int pageSize,
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null)
        {
            using var activity = _activitySource.StartActivity("GetPagedResultAsync");
            activity?.SetTag("page", page);
            activity?.SetTag("pageSize", pageSize);

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();

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

                    var items = await query
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    RecordPerformanceMetric("GetPagedResultAsync", stopwatch.Elapsed);

                    return new PagedResult<T>(items, page, pageSize, totalCount);
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            using var activity = _activitySource.StartActivity("AnyAsync");

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var result = await _dbSet.Where(e => !e.IsDeleted).AnyAsync(predicate);
                    RecordPerformanceMetric("AnyAsync", stopwatch.Elapsed);
                    return result;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            using var activity = _activitySource.StartActivity("CountAsync");

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();

                    IQueryable<T> query = _dbSet.Where(e => !e.IsDeleted);
                    if (predicate != null)
                    {
                        query = query.Where(predicate);
                    }

                    var result = await query.CountAsync();
                    RecordPerformanceMetric("CountAsync", stopwatch.Elapsed);
                    return result;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        #endregion

        #region Advanced Transaction and Bulk Operations

        public virtual async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation)
        {
            using var activity = _activitySource.StartActivity("ExecuteInTransactionAsync");

            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                await _globalLock.WaitAsync();
                try
                {
                    using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        var result = await operation();

                        await transaction.CommitAsync();

                        RecordPerformanceMetric("ExecuteInTransactionAsync", stopwatch.Elapsed);
                        InvalidateRelevantCache();

                        _logger.LogDebug("Transaction completed successfully for {EntityType}", _entityName);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Transaction failed for {EntityType}, rolling back", _entityName);
                        throw;
                    }
                }
                finally
                {
                    _globalLock.Release();
                }
            });
        }

        public virtual async Task<bool> BulkSoftDeleteAsync(Expression<Func<T, bool>> predicate, int? deletedByUserId = null)
        {
            using var activity = _activitySource.StartActivity("BulkSoftDeleteAsync");

            return await ExecuteInTransactionAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var entities = await _dbSet.Where(e => !e.IsDeleted).Where(predicate).ToListAsync();
                if (!entities.Any()) return true;

                var now = DateTime.UtcNow;

                // Process in batches to avoid memory issues
                const int batchSize = 1000;
                var batches = entities.Chunk(batchSize);

                foreach (var batch in batches)
                {
                    foreach (var entity in batch)
                    {
                        entity.IsDeleted = true;
                        entity.DeletedAt = now;
                        entity.DeletedByUserId = deletedByUserId;
                        entity.UpdatedAt = now;
                        entity.UpdatedByUserId = deletedByUserId;
                    }

                    _context.UpdateRange(batch);
                    await _context.SaveChangesAsync();
                }

                RecordPerformanceMetric("BulkSoftDeleteAsync", stopwatch.Elapsed);
                InvalidateRelevantCache();

                _logger.LogInformation("Bulk soft deleted {Count} entities of type {EntityType}", entities.Count, _entityName);
                return true;
            });
        }

        public virtual async Task<bool> BulkRestoreAsync(Expression<Func<T, bool>> predicate, int? restoredByUserId = null)
        {
            using var activity = _activitySource.StartActivity("BulkRestoreAsync");

            return await ExecuteInTransactionAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var entities = await _context.IncludeDeleted<T>()
                    .Where(predicate)
                    .Where(e => e.IsDeleted)
                    .ToListAsync();

                if (!entities.Any()) return true;

                var now = DateTime.UtcNow;

                const int batchSize = 1000;
                var batches = entities.Chunk(batchSize);

                foreach (var batch in batches)
                {
                    foreach (var entity in batch)
                    {
                        entity.IsDeleted = false;
                        entity.DeletedAt = null;
                        entity.DeletedByUserId = null;
                        entity.UpdatedAt = now;
                        entity.UpdatedByUserId = restoredByUserId;
                    }

                    _context.UpdateRange(batch);
                    await _context.SaveChangesAsync();
                }

                RecordPerformanceMetric("BulkRestoreAsync", stopwatch.Elapsed);
                InvalidateRelevantCache();

                _logger.LogInformation("Bulk restored {Count} entities of type {EntityType}", entities.Count, _entityName);
                return true;
            });
        }

        #endregion

        #region Resilience and Error Handling

        private async Task<TResult> ExecuteWithRetryAsync<TResult>(Func<Task<TResult>> operation, [CallerMemberName] string operationName = "")
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var activity = _activitySource.StartActivity($"Retry.{operationName}");
                    return await operation();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation {OperationName} failed after retries for {EntityType}", operationName, _entityName);
                throw;
            }
        }

        private async Task<TResult> ExecuteWithCircuitBreakerAsync<TResult>(Func<Task<TResult>> operation, [CallerMemberName] string operationName = "")
        {
            try
            {
                return await _circuitBreakerPolicy.ExecuteAsync(async () =>
                {
                    using var activity = _activitySource.StartActivity($"CircuitBreaker.{operationName}");
                    return await operation();
                });
            }
            catch (CircuitBreakerOpenException)
            {
                _logger.LogWarning("Circuit breaker is open for {EntityType}, operation {OperationName} rejected", _entityName, operationName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation {OperationName} failed for {EntityType}", operationName, _entityName);
                throw;
            }
        }

        private IAsyncPolicy CreateRetryPolicy()
        {
            return Policy
                .Handle<SqlException>(ex => IsTransientError(ex))
                .Or<TimeoutException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    _options.MaxRetryAttempts,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry {RetryCount} for {EntityType} after {Delay}ms due to: {Exception}",
                            retryCount, _entityName, timespan.TotalMilliseconds, outcome.Exception?.Message);
                    });
        }

        private IAsyncPolicy CreateCircuitBreakerPolicy()
        {
            return Policy
                .Handle<SqlException>()
                .Or<TimeoutException>()
                .CircuitBreakerAsync(
                    _options.CircuitBreakerFailureThreshold,
                    _options.CircuitBreakerDuration,
                    onBreak: (exception, timespan) =>
                    {
                        _logger.LogWarning("Circuit breaker opened for {EntityType} for {Duration}s due to: {Exception}",
                            _entityName, timespan.TotalSeconds, exception.Message);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset for {EntityType}", _entityName);
                    });
        }

        private static bool IsTransientError(SqlException ex)
        {
            // SQL Server transient error codes
            var transientErrors = new[] { 2, 53, 121, 233, 10053, 10054, 10060, 40197, 40501, 40613 };
            return transientErrors.Contains(ex.Number);
        }

        #endregion

        #region Cache Management

        private string GenerateCacheKey(string operation, params object[] parameters)
        {
            var key = $"{_entityName}:{operation}";
            if (parameters?.Length > 0)
            {
                key += ":" + string.Join(":", parameters.Select(p => p?.ToString() ?? "null"));
            }
            return key;
        }

        private bool TryGetFromCache<TValue>(string key, out TValue value)
        {
            if (_localCache.TryGetValue(key, out var cacheItem) && !cacheItem.IsExpired)
            {
                value = (TValue)cacheItem.Value;
                return true;
            }

            value = default;
            return false;
        }

        private void AddToCache<TValue>(string key, TValue value, TimeSpan expiry)
        {
            if (_localCache.Count >= _options.MaxCacheSize)
            {
                CleanupExpiredCache(null);
            }

            _localCache.AddOrUpdate(key,
                new CacheItem(value, DateTime.UtcNow.Add(expiry)),
                (k, v) => new CacheItem(value, DateTime.UtcNow.Add(expiry)));
        }

        private void InvalidateEntityCache(int entityId)
        {
            var keysToRemove = _localCache.Keys
                .Where(k => k.Contains($":{entityId}:") || k.EndsWith($":{entityId}"))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _localCache.TryRemove(key, out _);
            }
        }

        private void InvalidateRelevantCache()
        {
            var keysToRemove = _localCache.Keys
                .Where(k => k.StartsWith($"{_entityName}:"))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _localCache.TryRemove(key, out _);
            }
        }

        private void CleanupExpiredCache(object? state)
        {
            try
            {
                var expiredKeys = _localCache
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _localCache.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired cache entries for {EntityType}", expiredKeys.Count, _entityName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during cache cleanup for {EntityType}", _entityName);
            }
        }

        #endregion

        #region Performance Monitoring

        private void RecordPerformanceMetric(string operation, TimeSpan duration)
        {
            _performanceMetrics.AddOrUpdate(operation,
                new PerformanceMetrics(operation, duration),
                (key, existing) => existing.RecordDuration(duration));
        }

        private void ReportPerformanceMetrics(object? state)
        {
            try
            {
                if (!_performanceMetrics.Any()) return;

                var report = _performanceMetrics.Values
                    .Select(metric => new
                    {
                        Operation = metric.OperationName,
                        Count = metric.CallCount,
                        AvgDuration = metric.AverageDuration.TotalMilliseconds,
                        MaxDuration = metric.MaxDuration.TotalMilliseconds,
                        MinDuration = metric.MinDuration.TotalMilliseconds
                    })
                    .OrderByDescending(x => x.AvgDuration);

                _logger.LogInformation("Performance metrics for {EntityType}: {Metrics}",
                    _entityName, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reporting performance metrics for {EntityType}", _entityName);
            }
        }

        #endregion

        #region Resource Management

        private SemaphoreSlim GetEntityLock(int entityId)
        {
            return _entityLocks.GetOrAdd(entityId, _ => new SemaphoreSlim(1, 1));
        }

        private void CleanupUnusedLocks(object? state)
        {
            try
            {
                var locksToRemove = new List<int>();

                foreach (var kvp in _entityLocks.ToList())
                {
                    if (kvp.Value.CurrentCount == 1) // Not currently acquired
                    {
                        locksToRemove.Add(kvp.Key);
                    }
                }

                // Remove up to 100 locks per cleanup cycle to avoid performance impact
                foreach (var entityId in locksToRemove.Take(100))
                {
                    if (_entityLocks.TryRemove(entityId, out var semaphore))
                    {
                        semaphore.Dispose();
                    }
                }

                if (locksToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} unused entity locks for {EntityType}",
                        Math.Min(locksToRemove.Count, 100), _entityName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during lock cleanup for {EntityType}", _entityName);
            }
        }

        #endregion

        #region Save Changes with Enhanced Error Handling

        public virtual async Task<int> SaveChangesAsync()
        {
            using var activity = _activitySource.StartActivity("SaveChangesAsync");

            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    var result = await _context.SaveChangesAsync();

                    RecordPerformanceMetric("SaveChangesAsync", stopwatch.Elapsed);
                    _logger.LogDebug("Saved {ChangeCount} changes for {EntityType}", result, _entityName);

                    return result;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Concurrency conflict detected for {EntityType}", _entityName);
                    throw;
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database update failed for {EntityType}", _entityName);
                    throw;
                }
            });
        }

        #endregion

        #region Remaining Interface Implementations

        public virtual async Task<IEnumerable<T>> GetAllIncludeDeletedAsync()
        {
            using var activity = _activitySource.StartActivity("GetAllIncludeDeletedAsync");

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var entities = await _context.IncludeDeleted<T>().AsNoTracking().ToListAsync();
                    RecordPerformanceMetric("GetAllIncludeDeletedAsync", stopwatch.Elapsed);
                    return entities;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual async Task<IEnumerable<T>> FindIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            using var activity = _activitySource.StartActivity("FindIncludeDeletedAsync");

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var entities = await _context.IncludeDeleted<T>().AsNoTracking().Where(predicate).ToListAsync();
                    RecordPerformanceMetric("FindIncludeDeletedAsync", stopwatch.Elapsed);
                    return entities;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual async Task<T?> FirstOrDefaultIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            using var activity = _activitySource.StartActivity("FirstOrDefaultIncludeDeletedAsync");

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var entity = await _context.IncludeDeleted<T>().AsNoTracking().FirstOrDefaultAsync(predicate);
                    RecordPerformanceMetric("FirstOrDefaultIncludeDeletedAsync", stopwatch.Elapsed);
                    return entity;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual async Task<bool> AnyIncludeDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            using var activity = _activitySource.StartActivity("AnyIncludeDeletedAsync");

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var result = await _context.IncludeDeleted<T>().AnyAsync(predicate);
                    RecordPerformanceMetric("AnyIncludeDeletedAsync", stopwatch.Elapsed);
                    return result;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual async Task<int> CountAsync() => await CountAsync(null);

        public virtual async Task<int> CountIncludeDeletedAsync() => await CountIncludeDeletedAsync(null);

        public virtual async Task<int> CountIncludeDeletedAsync(Expression<Func<T, bool>>? predicate = null)
        {
            using var activity = _activitySource.StartActivity("CountIncludeDeletedAsync");

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();

                    IQueryable<T> query = _context.IncludeDeleted<T>();
                    if (predicate != null)
                    {
                        query = query.Where(predicate);
                    }

                    var result = await query.CountAsync();
                    RecordPerformanceMetric("CountIncludeDeletedAsync", stopwatch.Elapsed);
                    return result;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual async Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize)
        {
            var result = await GetPagedResultAsync(page, pageSize);
            return result.Data;
        }

        public virtual async Task<IEnumerable<T>> GetPagedIncludeDeletedAsync(int page, int pageSize)
        {
            using var activity = _activitySource.StartActivity("GetPagedIncludeDeletedAsync");

            return await ExecuteWithRetryAsync(async () =>
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var entities = await _context.IncludeDeleted<T>()
                        .AsNoTracking()
                        .OrderBy(e => e.Id)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    RecordPerformanceMetric("GetPagedIncludeDeletedAsync", stopwatch.Elapsed);
                    return entities;
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            });
        }

        public virtual void UpdateRange(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var entityList = entities.ToList();
            if (!entityList.Any()) return;

            using var activity = _activitySource.StartActivity("UpdateRange");
            activity?.SetTag("entity.count", entityList.Count);

            var now = DateTime.UtcNow;
            foreach (var entity in entityList)
            {
                entity.UpdatedAt = now;
                _context.Entry(entity).State = EntityState.Modified;
            }

            InvalidateRelevantCache();
            _logger.LogDebug("Updated {Count} entities of type {EntityType}", entityList.Count, _entityName);
        }

        public virtual void Remove(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            using var activity = _activitySource.StartActivity("Remove");
            activity?.SetTag("entity.id", entity.Id);

            _dbSet.Remove(entity);
            InvalidateEntityCache(entity.Id);

            _logger.LogDebug("Removed entity {EntityType} with ID {EntityId}", _entityName, entity.Id);
        }

        public virtual void RemoveRange(IEnumerable<T> entities)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var entityList = entities.ToList();
            if (!entityList.Any()) return;

            using var activity = _activitySource.StartActivity("RemoveRange");
            activity?.SetTag("entity.count", entityList.Count);

            _dbSet.RemoveRange(entityList);

            foreach (var entity in entityList)
            {
                InvalidateEntityCache(entity.Id);
            }

            _logger.LogDebug("Removed {Count} entities of type {EntityType}", entityList.Count, _entityName);
        }

        public virtual async Task<bool> SoftDeleteAsync(T entity, int? deletedByUserId = null)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return await SoftDeleteAsync(entity.Id, deletedByUserId);
        }

        public virtual async Task<bool> SoftDeleteRangeAsync(IEnumerable<T> entities, int? deletedByUserId = null)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var entityList = entities.ToList();
            if (!entityList.Any()) return true;

            var entityIds = entityList.Select(e => e.Id).ToList();
            return await BulkSoftDeleteAsync(e => entityIds.Contains(e.Id), deletedByUserId);
        }

        public virtual async Task<bool> RestoreAsync(int id, int? restoredByUserId = null)
        {
            using var activity = _activitySource.StartActivity("RestoreAsync");
            activity?.SetTag("entity.id", id);

            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var entityLock = GetEntityLock(id);
                await entityLock.WaitAsync();
                try
                {
                    var stopwatch = Stopwatch.StartNew();

                    var entity = await _context.IncludeDeleted<T>().FirstOrDefaultAsync(e => e.Id == id && e.IsDeleted);
                    if (entity == null)
                    {
                        _logger.LogWarning("Entity {EntityType} with ID {Id} not found or not deleted for restore", _entityName, id);
                        return false;
                    }

                    entity.IsDeleted = false;
                    entity.DeletedAt = null;
                    entity.DeletedByUserId = null;
                    entity.UpdatedAt = DateTime.UtcNow;
                    entity.UpdatedByUserId = restoredByUserId;

                    _context.Entry(entity).State = EntityState.Modified;

                    RecordPerformanceMetric("RestoreAsync", stopwatch.Elapsed);
                    InvalidateEntityCache(id);

                    _logger.LogInformation("Restored entity {EntityType} with ID {Id}", _entityName, id);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error restoring entity {EntityType} with ID {Id}", _entityName, id);
                    throw;
                }
                finally
                {
                    entityLock.Release();
                }
            });
        }

        public virtual async Task<bool> RestoreAsync(T entity, int? restoredByUserId = null)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return await RestoreAsync(entity.Id, restoredByUserId);
        }

        #endregion

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Stop timers first
                _lockCleanupTimer?.Dispose();
                _cacheCleanupTimer?.Dispose();
                _performanceReportTimer?.Dispose();

                // Dispose locks
                _readerWriterLock?.Dispose();
                _globalLock?.Dispose();

                // Clean up entity locks
                foreach (var entityLock in _entityLocks.Values)
                {
                    entityLock.Dispose();
                }
                _entityLocks.Clear();

                // Clean up operation locks
                foreach (var operationLock in _operationLocks.Values)
                {
                    operationLock.Dispose();
                }
                _operationLocks.Clear();

                // Dispose activity source
                _activitySource?.Dispose();

                _disposed = true;

                _logger.LogDebug("Repository for {EntityType} disposed", _entityName);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    #region Supporting Classes

    public class RepositoryOptions
    {
        public int MaxConcurrentOperations { get; set; } = Environment.ProcessorCount * 2;
        public int MaxRetryAttempts { get; set; } = 3;
        public int CircuitBreakerFailureThreshold { get; set; } = 5;
        public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromMinutes(1);
        public bool EnableCaching { get; set; } = true;
        public TimeSpan DefaultCacheExpiry { get; set; } = TimeSpan.FromMinutes(15);
        public int MaxCacheSize { get; set; } = 10000;
        public int MaxCacheableCollectionSize { get; set; } = 1000;
        public int LockCleanupIntervalMinutes { get; set; } = 10;
        public int CacheCleanupIntervalMinutes { get; set; } = 5;
        public int PerformanceReportIntervalMinutes { get; set; } = 60;
    }

    internal class CacheItem
    {
        public object Value { get; }
        public DateTime ExpiryTime { get; }
        public bool IsExpired => DateTime.UtcNow > ExpiryTime;

        public CacheItem(object value, DateTime expiryTime)
        {
            Value = value;
            ExpiryTime = expiryTime;
        }
    }

    internal class PerformanceMetrics
    {
        public string OperationName { get; }
        public long CallCount { get; private set; }
        public TimeSpan TotalDuration { get; private set; }
        public TimeSpan MaxDuration { get; private set; }
        public TimeSpan MinDuration { get; private set; }
        public TimeSpan AverageDuration => CallCount > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / CallCount) : TimeSpan.Zero;

        public PerformanceMetrics(string operationName, TimeSpan initialDuration)
        {
            OperationName = operationName;
            CallCount = 1;
            TotalDuration = initialDuration;
            MaxDuration = initialDuration;
            MinDuration = initialDuration;
        }

        public PerformanceMetrics RecordDuration(TimeSpan duration)
        {
            CallCount++;
            TotalDuration = TotalDuration.Add(duration);
            if (duration > MaxDuration) MaxDuration = duration;
            if (duration < MinDuration) MinDuration = duration;
            return this;
        }
    }

    #endregion
}