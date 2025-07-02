using Backend.CMS.Application.Common;
using Backend.CMS.Infrastructure.Caching;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class CacheService : ICacheService, ICacheInvalidationService, IDisposable
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CacheService> _logger;
        private readonly TimeSpan _defaultExpiration;
        private readonly TimeSpan _longExpiration;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _invalidationSemaphore;
        private readonly bool _isAdminModeAvailable;

        // Thread-safe semaphore management with automatic cleanup
        private readonly ConcurrentDictionary<string, (SemaphoreSlim Semaphore, DateTime LastUsed)> _keySemaphores;
        private readonly Timer _semaphoreCleanupTimer;
        private readonly TimeSpan _semaphoreTimeout = TimeSpan.FromMinutes(5);
        private readonly int _maxSemaphores;

        private bool _disposed = false;

        public CacheService(
            IDistributedCache distributedCache,
            IConnectionMultiplexer connectionMultiplexer,
            IConfiguration configuration,
            ILogger<CacheService> logger)
        {
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _defaultExpiration = TimeSpan.FromMinutes(GetConfigValue("CacheSettings:DefaultExpirationMinutes", 30));
            _longExpiration = TimeSpan.FromHours(GetConfigValue("CacheSettings:LongExpirationHours", 24));
            _maxSemaphores = GetConfigValue("CacheSettings:MaxSemaphores", 1000);
            _invalidationSemaphore = new SemaphoreSlim(1, 1);
            _keySemaphores = new ConcurrentDictionary<string, (SemaphoreSlim, DateTime)>();

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true
            };

            // Check if admin mode is available
            _isAdminModeAvailable = CheckAdminModeAvailability();

            // Cleanup semaphores every 2 minutes
            _semaphoreCleanupTimer = new Timer(CleanupExpiredSemaphores, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
        }

        #region Basic Cache Operations

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cachedValue = await _distributedCache.GetStringAsync(key);
                if (string.IsNullOrEmpty(cachedValue))
                {
                    _logger.LogDebug("Cache miss for key: {Key}", key);
                    return null;
                }

                var result = JsonSerializer.Deserialize<T>(cachedValue, _jsonOptions);
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached item with key: {Key}", key);
                await RemoveAsync(key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached item with key: {Key}", key);
                return null;
            }
        }

        public async Task<T?> GetAsync<T>(string key, Func<Task<T?>> getItem, bool cacheEmptyCollections = true) where T : class
        {
            var cachedItem = await GetAsync<T>(key);
            if (cachedItem != null)
                return cachedItem;

            // Use semaphore to prevent cache stampede
            var semaphore = GetKeySemaphore(key);
            try
            {
                await semaphore.WaitAsync();

                // Double-check pattern
                cachedItem = await GetAsync<T>(key);
                if (cachedItem != null)
                    return cachedItem;

                var item = await getItem();
                if (item != null)
                {
                    bool shouldCache = true;

                    if (!cacheEmptyCollections && item is IEnumerable enumerable && !enumerable.Cast<object>().Any())
                    {
                        shouldCache = false;
                    }

                    if (shouldCache)
                    {
                        await SetAsync(key, item);
                    }
                }

                return item;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            ArgumentNullException.ThrowIfNull(value);

            try
            {
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration
                };

                await _distributedCache.SetStringAsync(key, serializedValue, options);
                _logger.LogDebug("Cache item set with key: {Key}, expiration: {Expiration}",
                    key, options.AbsoluteExpirationRelativeToNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cached item with key: {Key}", key);
                throw;
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            try
            {
                await _distributedCache.RemoveAsync(key);
                _logger.LogDebug("Cache item removed with key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cached item with key: {Key}", key);
            }
        }

        public async Task RemoveAsync(IEnumerable<string> keys)
        {
            if (keys?.Any() != true) return;

            var keysList = keys.ToList();
            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                var redisKeys = keysList.Select(k => new RedisKey(k)).ToArray();

                if (redisKeys.Length > 0)
                {
                    await database.KeyDeleteAsync(redisKeys);
                    _logger.LogDebug("Removed {Count} cache items", redisKeys.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing multiple cached items");
                // Fallback to individual removal
                var tasks = keysList.Select(RemoveAsync);
                await Task.WhenAll(tasks);
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return;

            await _invalidationSemaphore.WaitAsync();
            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                var endpoints = _connectionMultiplexer.GetEndPoints();

                if (!endpoints.Any())
                {
                    _logger.LogWarning("No Redis endpoints available for pattern deletion");
                    return;
                }

                var server = _connectionMultiplexer.GetServer(endpoints.First());
                const int batchSize = 1000;
                var deletedCount = 0;

                await foreach (var keyBatch in GetKeysBatched(server, pattern, batchSize))
                {
                    if (keyBatch.Any())
                    {
                        await database.KeyDeleteAsync(keyBatch.ToArray());
                        deletedCount += keyBatch.Count();
                    }
                }

                _logger.LogInformation("Removed {Count} cache items matching pattern: {Pattern}",
                    deletedCount, pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cached items by pattern: {Pattern}", pattern);
            }
            finally
            {
                _invalidationSemaphore.Release();
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                return await database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if cached item exists with key: {Key}", key);
                return false;
            }
        }

        public async Task RefreshAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            try
            {
                await _distributedCache.RefreshAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing cached item with key: {Key}", key);
            }
        }

        public string GenerateKey(string prefix, params object[] identifiers)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Prefix cannot be null or empty", nameof(prefix));

            var keyParts = new List<string> { prefix };
            keyParts.AddRange(identifiers.Select(id => id?.ToString() ?? "null"));
            return string.Join(":", keyParts);
        }

        #endregion


        #region  Cache-Aware Operations

        public async Task<T?> GetOrAddAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            // Try to get from cache first
            var cachedItem = await GetAsync<T>(key);
            if (cachedItem != null)
                return cachedItem;

            // Use semaphore to prevent cache stampede
            var semaphore = GetKeySemaphore(key);
            try
            {
                await semaphore.WaitAsync();

                // Double-check pattern
                cachedItem = await GetAsync<T>(key);
                if (cachedItem != null)
                    return cachedItem;

                // Execute factory and cache result
                var item = await factory();
                if (item != null)
                {
                    await SetAsync(key, item, expiration);
                }

                return item;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<T> RefreshCacheAsync<T>(string key, Func<Task<T>> operation, TimeSpan? expiration = null) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                // Execute the operation to get fresh data
                var result = await operation();

                // Update cache with fresh data
                if (result != null)
                {
                    await SetAsync(key, result, expiration);
                    _logger.LogDebug("Cache refreshed for key: {Key}", key);
                }
                else
                {
                    // Remove from cache if result is null
                    await RemoveAsync(key);
                    _logger.LogDebug("Cache removed for key: {Key} (null result)", key);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing cache for key: {Key}", key);

                // Remove potentially stale cache on error
                await RemoveAsync(key);
                throw;
            }
        }

        public async Task<T> InvalidateAndRefreshAsync<T>(Func<Task<T>> operation, params string[] cachePatterns) where T : class
        {
            try
            {
                // Execute the operation first
                var result = await operation();

                // Invalidate cache patterns asynchronously (don't wait)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var invalidationTasks = cachePatterns.Select(RemoveByPatternAsync);
                        await Task.WhenAll(invalidationTasks);

                        _logger.LogDebug("Cache patterns invalidated: {Patterns}", string.Join(", ", cachePatterns));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error invalidating cache patterns: {Patterns}", string.Join(", ", cachePatterns));
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in invalidate and refresh operation");
                throw;
            }
        }

        public async Task InvalidatePatternsAsync(params string[] cachePatterns)
        {
            if (cachePatterns?.Any() != true) return;

            try
            {
                var invalidationTasks = cachePatterns.Select(RemoveByPatternAsync);
                await Task.WhenAll(invalidationTasks);

                _logger.LogDebug("Cache patterns invalidated: {Patterns}", string.Join(", ", cachePatterns));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache patterns: {Patterns}", string.Join(", ", cachePatterns));
            }
        }

        public async Task SetBatchAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiration = null) where T : class
        {
            if (keyValuePairs?.Any() != true) return;

            try
            {
                var tasks = keyValuePairs.Select(async kvp =>
                {
                    try
                    {
                        await SetAsync(kvp.Key, kvp.Value, expiration);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache item with key: {Key}", kvp.Key);
                    }
                });

                await Task.WhenAll(tasks);
                _logger.LogDebug("Batch cached {Count} items", keyValuePairs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch cache operation");
            }
        }

        public async Task<Dictionary<string, T?>> GetBatchAsync<T>(IEnumerable<string> keys) where T : class
        {
            if (keys?.Any() != true)
                return new Dictionary<string, T?>();

            var result = new Dictionary<string, T?>();
            var keysList = keys.ToList();

            try
            {
                var tasks = keysList.Select(async key =>
                {
                    try
                    {
                        var value = await GetAsync<T>(key);
                        return new KeyValuePair<string, T?>(key, value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get cached item with key: {Key}", key);
                        return new KeyValuePair<string, T?>(key, null);
                    }
                });

                var results = await Task.WhenAll(tasks);
                foreach (var kvp in results)
                {
                    result[kvp.Key] = kvp.Value;
                }

                _logger.LogDebug("Batch retrieved {Count} items from cache", keysList.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch get operation");
            }

            return result;
        }

        #endregion

        #region Cache Invalidation Methods
        public async Task InvalidateProductCacheAsync(int? productId = null)
        {
            try
            {
                if (productId.HasValue && productId.Value > 0)
                {
                    var keysToRemove = new List<string>
                    {
                        CacheKeys.ProductById(productId.Value),
                        CacheKeys.ProductBySlug("*"), // Will be handled by pattern matching
                        CacheKeys.ProductDefaultVariant(productId.Value)
                    };

                    await RemoveAsync(keysToRemove.Where(k => !k.Contains("*")));
                    await RemoveByPatternAsync($"product:*:{productId.Value}");
                    await RemoveByPatternAsync($"product-variants:product:{productId.Value}:*");
                    await RemoveByPatternAsync($"products:related:{productId.Value}:*");
                }
                else
                {
                    await RemoveByPatternAsync(CacheKeys.ProductsPattern);
                    await RemoveByPatternAsync("product:*");
                    await RemoveByPatternAsync("products:*");
                    await RemoveByPatternAsync("product-variants:*");
                }

                // Invalidate global product caches
                var globalKeysToRemove = new List<string>
                {
                    CacheKeys.ProductsList(),
                    CacheKeys.ProductStatistics,
                    CacheKeys.ProductPriceRange,
                    CacheKeys.ProductVendors,
                    CacheKeys.ProductTags,
                    CacheKeys.ProductsVariantsList()
                };

                await RemoveAsync(globalKeysToRemove);
                await RemoveByPatternAsync("products:category:*");
                await RemoveByPatternAsync("products:featured:*");
                await RemoveByPatternAsync("products:recent:*");

                _logger.LogInformation("Invalidated product cache {ProductId}",
                    productId?.ToString() ?? "all");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating product cache for product {ProductId}", productId);
            }
        }

        public async Task InvalidateProductVariantCacheAsync(int? variantId = null, int? productId = null)
        {
            try
            {
                if (variantId.HasValue && variantId.Value > 0)
                {
                    await RemoveAsync(CacheKeys.ProductVariantById(variantId.Value));
                    await RemoveByPatternAsync($"product-variant:*:{variantId.Value}");
                }

                if (productId.HasValue && productId.Value > 0)
                {
                    await RemoveAsync(CacheKeys.ProductVariantsByProduct(productId.Value));
                    await RemoveAsync(CacheKeys.ProductDefaultVariant(productId.Value));
                    await RemoveByPatternAsync(CacheKeys.ProductVariantsPattern(productId.Value));
                }

                if (!variantId.HasValue && !productId.HasValue)
                {
                    await RemoveAsync(CacheKeys.ProductsVariantsList());
                    await RemoveByPatternAsync("product-variant:*");
                    await RemoveByPatternAsync("product-variants:*");
                }

                _logger.LogInformation("Invalidated product variant cache - Variant: {VariantId}, Product: {ProductId}",
                    variantId, productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating product variant cache - Variant: {VariantId}, Product: {ProductId}",
                    variantId, productId);
            }
        }

        public async Task InvalidateCategoryCacheAsync(int? categoryId = null)
        {
            try
            {
                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    var keysToRemove = new List<string>
                    {
                        CacheKeys.CategoryById(categoryId.Value),
                        CacheKeys.SubCategories(categoryId.Value)
                    };

                    await RemoveAsync(keysToRemove);
                    await RemoveByPatternAsync($"category:*:{categoryId.Value}");
                    await RemoveByPatternAsync($"products:category:{categoryId.Value}:*");
                }

                // Always invalidate global category caches when any category changes
                var globalKeysToRemove = new List<string>
                {
                    CacheKeys.AllCategories,
                    CacheKeys.CategoryTree,
                    CacheKeys.RootCategories
                };

                await RemoveAsync(globalKeysToRemove);

                if (!categoryId.HasValue)
                {
                    await RemoveByPatternAsync(CacheKeys.CategoriesPattern);
                    await RemoveByPatternAsync("category:*");
                }

                _logger.LogInformation("Invalidated category cache {CategoryId}",
                    categoryId?.ToString() ?? "all");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating category cache for category {CategoryId}", categoryId);
            }
        }

        public async Task InvalidatePermissionCacheAsync(string? role = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    await RemoveAsync(CacheKeys.RolePermissions(role));
                    await RemoveByPatternAsync($"permission:role:{role}:*");
                }
                else
                {
                    await RemoveByPatternAsync(CacheKeys.PermissionsPattern);
                    await RemoveAsync(CacheKeys.AllPermissions);
                }

                // Invalidate user permissions since they depend on role permissions
                await RemoveByPatternAsync($"user:permissions:*");

                _logger.LogInformation("Invalidated permission cache {Role}", role ?? "all");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating permission cache for role {Role}", role);
            }
        }

        public async Task InvalidateSessionCacheAsync(string? sessionId = null, int? userId = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    var keysToRemove = new List<string>
                    {
                        CacheKeys.SessionById(sessionId),
                        CacheKeys.SessionByToken(sessionId) // Assuming sessionId could be token
                    };
                    await RemoveAsync(keysToRemove);
                }

                if (userId.HasValue && userId.Value > 0)
                {
                    await RemoveAsync(CacheKeys.UserSessions(userId.Value));
                    await RemoveByPatternAsync($"session:*:{userId.Value}");
                }

                if (sessionId == null && !userId.HasValue)
                {
                    await RemoveByPatternAsync(CacheKeys.SessionsPattern);
                }

                _logger.LogInformation("Invalidated session cache - Session: {SessionId}, User: {UserId}",
                    sessionId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating session cache - Session: {SessionId}, User: {UserId}",
                    sessionId, userId);
            }
        }

        public async Task InvalidateSearchCacheAsync(string? searchTerm = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    await RemoveByPatternAsync($"search:{searchTerm.ToLowerInvariant()}:*");
                    await RemoveByPatternAsync($"suggestions:{searchTerm.ToLowerInvariant()}:*");
                }
                else
                {
                    await RemoveByPatternAsync(CacheKeys.SearchPattern);
                    await RemoveByPatternAsync("suggestions:*");
                    await RemoveAsync(CacheKeys.IndexingStatus);
                }

                _logger.LogInformation("Invalidated search cache {SearchTerm}", searchTerm ?? "all");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating search cache for term {SearchTerm}", searchTerm);
            }
        }

        public async Task InvalidateComponentCacheAsync(int? componentId = null, int? pageId = null)
        {
            try
            {
                if (componentId.HasValue && componentId.Value > 0)
                {
                    await RemoveByPatternAsync($"component:*:{componentId.Value}");
                    await RemoveByPatternAsync($"{CacheKeys.COMPONENT_PREFIX}:*:{componentId.Value}");
                }

                if (pageId.HasValue && pageId.Value > 0)
                {
                    await RemoveAsync(CacheKeys.PageWithComponents(pageId.Value));
                    await RemoveByPatternAsync($"page:components:{pageId.Value}:*");
                }

                if (!componentId.HasValue && !pageId.HasValue)
                {
                    await RemoveByPatternAsync($"{CacheKeys.COMPONENT_PREFIX}:*");
                    await RemoveByPatternAsync("component:*");
                }

                _logger.LogInformation("Invalidated component cache - Component: {ComponentId}, Page: {PageId}",
                    componentId, pageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating component cache - Component: {ComponentId}, Page: {PageId}",
                    componentId, pageId);
            }
        }

        public async Task InvalidateMultipleAsync(params CacheInvalidationRequest[] requests)
        {
            if (requests?.Any() != true) return;

            try
            {
                var tasks = new List<Task>();

                foreach (var request in requests)
                {
                    var task = request.EntityType switch
                    {
                        CacheEntityType.User => InvalidateUserCacheAsync(request.EntityId ?? 0),
                        CacheEntityType.Page => InvalidatePageCacheAsync(request.EntityId ?? 0),
                        CacheEntityType.File => InvalidateFileCacheAsync(request.EntityId),
                        CacheEntityType.Folder => InvalidateFolderCacheAsync(request.EntityId),
                        CacheEntityType.Product => InvalidateProductCacheAsync(request.EntityId),
                        CacheEntityType.ProductVariant => InvalidateProductVariantCacheAsync(request.EntityId),
                        CacheEntityType.Category => InvalidateCategoryCacheAsync(request.EntityId),
                        CacheEntityType.Permission => InvalidatePermissionCacheAsync(request.EntityKey),
                        CacheEntityType.Session => InvalidateSessionCacheAsync(request.EntityKey, request.EntityId),
                        CacheEntityType.Component => InvalidateComponentCacheAsync(request.EntityId),
                        CacheEntityType.Search => InvalidateSearchCacheAsync(request.EntityKey),
                        CacheEntityType.Company => InvalidateCompanyCacheAsync(),
                        CacheEntityType.Location => InvalidateLocationCacheAsync(request.EntityId),
                        CacheEntityType.All => InvalidateAllCacheAsync(),
                        _ => Task.CompletedTask
                    };

                    tasks.Add(task);

                    // Handle related entity invalidation
                    if (request.InvalidateRelated)
                    {
                        var relatedTasks = await GetRelatedInvalidationTasksAsync(request);
                        tasks.AddRange(relatedTasks);
                    }
                }

                await Task.WhenAll(tasks);

                _logger.LogInformation("Completed multiple cache invalidation for {Count} requests", requests.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during multiple cache invalidation");
            }
        }

        public async Task InvalidateRelatedEntitiesAsync(CacheInvalidationContext context)
        {
            try
            {
                var relatedInvalidations = new List<Task>();

                switch (context.EntityType)
                {
                    case CacheEntityType.User:
                        relatedInvalidations.Add(InvalidateSessionCacheAsync(null, context.EntityId));
                        relatedInvalidations.Add(InvalidatePermissionCacheAsync()); // User role might have changed
                        break;

                    case CacheEntityType.Page:
                        relatedInvalidations.Add(InvalidateComponentCacheAsync(null, context.EntityId));
                        relatedInvalidations.Add(InvalidateSearchCacheAsync()); // Page content affects search
                        if (context.OperationType == CacheOperationType.Delete)
                        {
                            // If page deleted, invalidate child pages
                            await RemoveByPatternAsync($"page:parent:{context.EntityId}:*");
                        }
                        break;

                    case CacheEntityType.Product:
                        relatedInvalidations.Add(InvalidateProductVariantCacheAsync(null, context.EntityId));
                        relatedInvalidations.Add(InvalidateSearchCacheAsync());
                        if (context.Metadata?.ContainsKey("CategoryId") == true)
                        {
                            var categoryId = Convert.ToInt32(context.Metadata["CategoryId"]);
                            relatedInvalidations.Add(InvalidateCategoryCacheAsync(categoryId));
                        }
                        break;

                    case CacheEntityType.Category:
                        relatedInvalidations.Add(InvalidateProductCacheAsync()); // Products in category affected
                        relatedInvalidations.Add(InvalidateSearchCacheAsync());
                        if (context.OperationType == CacheOperationType.Delete)
                        {
                            // If category deleted, invalidate child categories
                            await RemoveByPatternAsync($"category:sub:{context.EntityId}:*");
                        }
                        break;

                    case CacheEntityType.File:
                        if (context.Metadata?.ContainsKey("FolderId") == true)
                        {
                            var folderId = Convert.ToInt32(context.Metadata["FolderId"]);
                            relatedInvalidations.Add(InvalidateFolderCacheAsync(folderId));
                        }
                        break;

                    case CacheEntityType.Folder:
                        relatedInvalidations.Add(InvalidateFileCacheAsync()); // Files in folder affected
                        if (context.OperationType == CacheOperationType.Delete)
                        {
                            // If folder deleted, invalidate child folders
                            await RemoveByPatternAsync($"folder:parent:{context.EntityId}:*");
                        }
                        break;

                    case CacheEntityType.Permission:
                        relatedInvalidations.Add(InvalidateUserCacheAsync(0)); // All user permissions affected
                        break;

                    case CacheEntityType.Company:
                        relatedInvalidations.Add(InvalidateLocationCacheAsync());
                        relatedInvalidations.Add(InvalidateUserCacheAsync(0)); // Company settings might affect users
                        break;
                }

                if (relatedInvalidations.Any())
                {
                    await Task.WhenAll(relatedInvalidations);
                }

                _logger.LogDebug("Invalidated related entities for {EntityType}:{EntityId} operation {OperationType}",
                    context.EntityType, context.EntityId, context.OperationType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating related entities for {EntityType}:{EntityId}",
                    context.EntityType, context.EntityId);
            }
        }

        #endregion

        #region Helper Methods for Related Invalidation

        private async Task<List<Task>> GetRelatedInvalidationTasksAsync(CacheInvalidationRequest request)
        {
            var tasks = new List<Task>();

            try
            {
                switch (request.EntityType)
                {
                    case CacheEntityType.User when request.EntityId.HasValue:
                        tasks.Add(InvalidateSessionCacheAsync(null, request.EntityId.Value));
                        break;

                    case CacheEntityType.Page when request.EntityId.HasValue:
                        tasks.Add(InvalidateComponentCacheAsync(null, request.EntityId.Value));
                        break;

                    case CacheEntityType.Product when request.EntityId.HasValue:
                        tasks.Add(InvalidateProductVariantCacheAsync(null, request.EntityId.Value));
                        break;

                    case CacheEntityType.Category:
                        tasks.Add(InvalidateProductCacheAsync()); // Products in category
                        break;

                    case CacheEntityType.Folder when request.EntityId.HasValue:
                        // Invalidate files in folder
                        await RemoveByPatternAsync($"file:folder:{request.EntityId.Value}:*");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting related invalidation tasks for {EntityType}:{EntityId}",
                    request.EntityType, request.EntityId);
            }

            return tasks;
        }

        public async Task InvalidateUserCacheAsync(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    await RemoveByPatternAsync(CacheKeys.UsersPattern);
                    await RemoveByPatternAsync(CacheKeys.Pattern(CacheKeys.USER_PREFIX));
                }
                else
                {
                    var keysToRemove = new List<string>
                    {
                        CacheKeys.UserById(userId),
                        CacheKeys.UserPermissions(userId),
                        CacheKeys.UserSessions(userId)
                    };

                    await RemoveAsync(keysToRemove);
                    await RemoveByPatternAsync($"user:*:{userId}");
                }

                _logger.LogInformation("Invalidated cache for user {UserId}", userId <= 0 ? "all" : userId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating user cache for user {UserId}", userId);
            }
        }

        public async Task InvalidatePageCacheAsync(int pageId)
        {
            try
            {
                if (pageId <= 0)
                {
                    await RemoveByPatternAsync(CacheKeys.PagesPattern);
                    await RemoveByPatternAsync(CacheKeys.DesignerPattern);
                    await RemoveByPatternAsync("preview:*");
                }
                else
                {
                    var keysToRemove = new List<string>
                    {
                        CacheKeys.PageById(pageId),
                        CacheKeys.PageWithComponents(pageId),
                        CacheKeys.PageVersions(pageId),
                        CacheKeys.DesignerPage(pageId)
                    };

                    await RemoveAsync(keysToRemove);
                    await RemoveByPatternAsync($"page:*:{pageId}");
                    await RemoveByPatternAsync($"designer:*:{pageId}");
                }

                var globalKeysToRemove = new List<string>
                {
                    CacheKeys.PublishedPages,
                    CacheKeys.PageHierarchy
                };
                await RemoveAsync(globalKeysToRemove);

                _logger.LogInformation("Invalidated cache for page {PageId}", pageId <= 0 ? "all" : pageId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating page cache for page {PageId}", pageId);
            }
        }


        public async Task InvalidateCompanyCacheAsync()
        {
            try
            {
                await RemoveByPatternAsync(CacheKeys.CompanyPattern);
                _logger.LogInformation("Invalidated company cache");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating company cache");
            }
        }

        public async Task InvalidateLocationCacheAsync(int? locationId = null)
        {
            try
            {
                if (locationId.HasValue && locationId.Value > 0)
                {
                    await RemoveAsync(CacheKeys.LocationById(locationId.Value));
                }

                var keysToRemove = new List<string>
                {
                    CacheKeys.MainLocation
                };

                await RemoveAsync(keysToRemove);
                await RemoveByPatternAsync(CacheKeys.LocationsPattern);

                _logger.LogInformation("Invalidated location cache {LocationId}",
                    locationId?.ToString() ?? "all");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating location cache");
            }
        }

        public async Task InvalidateFileCacheAsync(int? fileId = null)
        {
            try
            {
                if (fileId.HasValue && fileId.Value > 0)
                {
                    await RemoveAsync(CacheKeys.FileById(fileId.Value));
                }

                var keysToRemove = new List<string>
                {
                    CacheKeys.RecentFiles,
                    CacheKeys.FileStatistics
                };

                await RemoveAsync(keysToRemove);
                await RemoveByPatternAsync(CacheKeys.FilesPattern);

                _logger.LogInformation("Invalidated file cache {FileId}",
                    fileId?.ToString() ?? "all");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating file cache");
            }
        }

        public async Task InvalidateFolderCacheAsync(int? folderId = null)
        {
            try
            {
                if (folderId.HasValue && folderId.Value > 0)
                {
                    await RemoveAsync(CacheKeys.FolderById(folderId.Value));
                }

                var keysToRemove = new List<string>
                {
                    CacheKeys.FolderTree
                };

                await RemoveAsync(keysToRemove);
                await RemoveByPatternAsync(CacheKeys.FoldersPattern);

                _logger.LogInformation("Invalidated folder cache {FolderId}",
                    folderId?.ToString() ?? "all");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating folder cache");
            }
        }

        public async Task InvalidateAllCacheAsync()
        {
            await _invalidationSemaphore.WaitAsync();
            try
            {
                var database = _connectionMultiplexer.GetDatabase();

                if (_isAdminModeAvailable)
                {
                    await database.ExecuteAsync("FLUSHDB");
                    _logger.LogInformation("Invalidated all cache entries using FLUSHDB");
                }
                else
                {
                    // Fallback method when admin mode is not available
                    var patterns = new[]
                    {
                        CacheKeys.UsersPattern,
                        CacheKeys.PagesPattern,
                        CacheKeys.CompanyPattern,
                        CacheKeys.LocationsPattern,
                        CacheKeys.FilesPattern,
                        CacheKeys.FoldersPattern,
                        CacheKeys.SessionsPattern,
                        CacheKeys.SearchPattern,
                        CacheKeys.DesignerPattern
                    };

                    var tasks = patterns.Select(RemoveByPatternAsync);
                    await Task.WhenAll(tasks);

                    _logger.LogInformation("Invalidated all cache entries using pattern-based removal");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating all cache");
                throw;
            }
            finally
            {
                _invalidationSemaphore.Release();
            }
        }

        public async Task WarmupCacheAsync()
        {
            try
            {
                _logger.LogInformation("Starting cache warmup...");

                var tasks = new List<Task>
                {
                    WarmupCompanyDataAsync(),
                    WarmupPublishedPagesAsync(),
                    WarmupComponentLibraryAsync(),
                    WarmupMainLocationAsync(),
                    WarmupPermissionsAsync()
                };

                await Task.WhenAll(tasks);

                _logger.LogInformation("Cache warmup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache warmup");
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetCacheStatisticsAsync()
        {
            try
            {
                var database = _connectionMultiplexer.GetDatabase();
                var endpoints = _connectionMultiplexer.GetEndPoints();

                if (!endpoints.Any())
                {
                    return new Dictionary<string, object> { ["Error"] = "No Redis endpoints available" };
                }

                var server = _connectionMultiplexer.GetServer(endpoints.First());
                var statistics = new Dictionary<string, object>();

                if (_isAdminModeAvailable)
                {
                    try
                    {
                        var info = await server.InfoAsync();

                        // Get basic Redis info
                        var infoDict = info.SelectMany(g => g).ToDictionary(kv => kv.Key, kv => kv.Value);

                        statistics["MemoryUsed"] = infoDict.GetValueOrDefault("used_memory_human", "Unknown");
                        statistics["KeyspaceHits"] = long.Parse(infoDict.GetValueOrDefault("keyspace_hits", "0"));
                        statistics["KeyspaceMisses"] = long.Parse(infoDict.GetValueOrDefault("keyspace_misses", "0"));
                        statistics["ConnectedClients"] = infoDict.GetValueOrDefault("connected_clients", "0");
                        statistics["Uptime"] = infoDict.GetValueOrDefault("uptime_in_seconds", "0");

                        // Calculate hit ratio
                        var hits = (long)statistics["KeyspaceHits"];
                        var misses = (long)statistics["KeyspaceMisses"];
                        var total = hits + misses;
                        statistics["HitRatio"] = total > 0 ? Math.Round((double)hits / total * 100, 2) : 0;
                    }
                    catch (RedisCommandException ex) when (ex.Message.Contains("admin mode"))
                    {
                        _logger.LogWarning("Redis admin mode not available for INFO command, using fallback statistics");
                        statistics = GetFallbackStatistics();
                    }
                }
                else
                {
                    statistics = GetFallbackStatistics();
                }

                // Get key counts by pattern (these don't require admin mode)
                statistics["TotalKeys"] = await GetKeyCountAsync(server, "*");
                statistics["UserCacheKeys"] = await GetKeyCountAsync(server, CacheKeys.UsersPattern);
                statistics["PageCacheKeys"] = await GetKeyCountAsync(server, CacheKeys.PagesPattern);
                statistics["FileCacheKeys"] = await GetKeyCountAsync(server, CacheKeys.FilesPattern);
                statistics["SessionCacheKeys"] = await GetKeyCountAsync(server, CacheKeys.SessionsPattern);
                statistics["ActiveSemaphores"] = _keySemaphores.Count;
                statistics["AdminModeAvailable"] = _isAdminModeAvailable;

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache statistics");
                return new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["AdminModeAvailable"] = _isAdminModeAvailable,
                    ["ActiveSemaphores"] = _keySemaphores.Count
                };
            }
        }

        public async Task<List<string>> GetCacheKeysAsync(string pattern = "*")
        {
            try
            {
                var endpoints = _connectionMultiplexer.GetEndPoints();
                if (!endpoints.Any())
                {
                    _logger.LogWarning("No Redis endpoints available");
                    return new List<string>();
                }

                var server = _connectionMultiplexer.GetServer(endpoints.First());
                var keys = new List<string>();

                await foreach (var keyBatch in GetKeysBatched(server, pattern, 1000))
                {
                    keys.AddRange(keyBatch.Select(k => k.ToString()));
                }

                return keys;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache keys with pattern: {Pattern}", pattern);
                return new List<string>();
            }
        }

        #endregion

        #region Warmup Methods

        private async Task WarmupCompanyDataAsync()
        {
            try
            {
                _logger.LogDebug("Warming up company data cache");
                // Implementation would depend on specific services
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warmup company data cache");
            }
        }

        private async Task WarmupPublishedPagesAsync()
        {
            try
            {
                _logger.LogDebug("Warming up published pages cache");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warmup published pages cache");
            }
        }

        private async Task WarmupComponentLibraryAsync()
        {
            try
            {
                _logger.LogDebug("Warming up component library cache");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warmup component library cache");
            }
        }

        private async Task WarmupMainLocationAsync()
        {
            try
            {
                _logger.LogDebug("Warming up main location cache");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warmup main location cache");
            }
        }

        private async Task WarmupPermissionsAsync()
        {
            try
            {
                _logger.LogDebug("Warming up permissions cache");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warmup permissions cache");
            }
        }

        #endregion

        #region Helper Methods

        private bool CheckAdminModeAvailability()
        {
            try
            {
                var endpoints = _connectionMultiplexer.GetEndPoints();
                if (!endpoints.Any())
                {
                    _logger.LogWarning("No Redis endpoints available to check admin mode");
                    return false;
                }

                var server = _connectionMultiplexer.GetServer(endpoints.First());
                var database = _connectionMultiplexer.GetDatabase();

                // Test if we can execute admin commands
                var testResult = database.Execute("PING");

                // Try to get basic info - this will throw if admin mode is not available
                var info = server.InfoAsync().GetAwaiter().GetResult();

                _logger.LogInformation("Redis admin mode is available");
                return true;
            }
            catch (RedisCommandException ex) when (ex.Message.Contains("admin mode"))
            {
                _logger.LogWarning("Redis admin mode is not available: {Message}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine Redis admin mode availability");
                return false;
            }
        }

        private Dictionary<string, object> GetFallbackStatistics()
        {
            return new Dictionary<string, object>
            {
                ["MemoryUsed"] = "N/A (Admin mode required)",
                ["KeyspaceHits"] = "N/A (Admin mode required)",
                ["KeyspaceMisses"] = "N/A (Admin mode required)",
                ["ConnectedClients"] = "N/A (Admin mode required)",
                ["Uptime"] = "N/A (Admin mode required)",
                ["HitRatio"] = "N/A (Admin mode required)",
                ["AdminModeNote"] = "Redis admin mode is not enabled. Some statistics are unavailable."
            };
        }

        private async IAsyncEnumerable<IEnumerable<RedisKey>> GetKeysBatched(IServer server, string pattern, int batchSize)
        {
            var keys = new List<RedisKey>();

            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                keys.Add(key);

                if (keys.Count >= batchSize)
                {
                    yield return keys.ToArray();
                    keys.Clear();
                }
            }

            if (keys.Any())
            {
                yield return keys.ToArray();
            }
        }

        private async Task<long> GetKeyCountAsync(IServer server, string pattern)
        {
            long count = 0;
            await foreach (var _ in server.KeysAsync(pattern: pattern))
            {
                count++;
            }
            return count;
        }

        private SemaphoreSlim GetKeySemaphore(string key)
        {
            // Clean up if we have too many semaphores
            if (_keySemaphores.Count >= _maxSemaphores)
            {
                CleanupExpiredSemaphores(null);
            }

            var now = DateTime.UtcNow;
            return _keySemaphores.AddOrUpdate(
                key,
                _ => (new SemaphoreSlim(1, 1), now),
                (_, existing) => (existing.Semaphore, now)
            ).Semaphore;
        }

        private void CleanupExpiredSemaphores(object? state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(_semaphoreTimeout);
                var expiredKeys = _keySemaphores
                    .Where(kv => kv.Value.LastUsed < cutoffTime)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    if (_keySemaphores.TryRemove(key, out var entry))
                    {
                        entry.Semaphore.Dispose();
                    }
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired semaphores", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during semaphore cleanup");
            }
        }

        private int GetConfigValue(string key, int defaultValue)
        {
            return int.TryParse(_configuration[key], out var value) ? value : defaultValue;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _semaphoreCleanupTimer?.Dispose();
                _invalidationSemaphore?.Dispose();

                // Cleanup all semaphores
                foreach (var entry in _keySemaphores.Values)
                {
                    entry.Semaphore.Dispose();
                }
                _keySemaphores.Clear();

                _disposed = true;
            }
        }

        #endregion
    }
}