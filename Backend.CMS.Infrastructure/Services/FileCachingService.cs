using Backend.CMS.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Backend.CMS.Infrastructure.Services
{
    public class FileCachingService : IFileCachingService, IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<FileCachingService> _logger;
        private readonly TimeSpan _defaultExpiration;
        private readonly TimeSpan _thumbnailExpiration;
        private readonly bool _cachingEnabled;
        private readonly long _maxFileSize;
        private readonly long _maxTotalCacheSize;
        private readonly ConcurrentDictionary<int, CacheMetadata> _accessTimes;
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _cleanupSemaphore;
        private readonly object _cacheSizeLock = new();
        private long _currentCacheSize = 0;
        private bool _disposed = false;

        // Cache key prefixes
        private const string FILE_CONTENT_PREFIX = "file_content_";
        private const string THUMBNAIL_CONTENT_PREFIX = "thumbnail_content_";
        private const string FILE_METADATA_PREFIX = "file_metadata_";

        private class CacheMetadata
        {
            public DateTime LastAccessed { get; set; }
            public long Size { get; set; }
            public string Type { get; set; } = string.Empty;
        }

        public FileCachingService(
            IMemoryCache memoryCache,
            IConfiguration configuration,
            ILogger<FileCachingService> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _accessTimes = new ConcurrentDictionary<int, CacheMetadata>();
            _cleanupSemaphore = new SemaphoreSlim(1, 1);

            // Load configuration with validation
            _defaultExpiration = TimeSpan.FromMinutes(GetConfigValue(configuration,
                "FileStorage:DatabaseSettings:CacheExpirationMinutes", 60));

            _thumbnailExpiration = TimeSpan.FromMinutes(GetConfigValue(configuration,
                "FileStorage:DatabaseSettings:ThumbnailCacheExpirationMinutes", 120));

            _cachingEnabled = bool.Parse(configuration["FileStorage:DatabaseSettings:EnableFileCaching"] ?? "true");

            _maxFileSize = GetConfigValue(configuration,
                "FileStorage:DatabaseSettings:MaxDatabaseFileSize", 104857600L); // 100MB

            _maxTotalCacheSize = GetConfigValue(configuration,
                "FileStorage:DatabaseSettings:MaxTotalCacheSize", 1073741824L); // 1GB

            // Validate configuration
            if (_maxFileSize <= 0) _maxFileSize = 104857600L;
            if (_maxTotalCacheSize <= 0) _maxTotalCacheSize = 1073741824L;
            if (_defaultExpiration <= TimeSpan.Zero) _defaultExpiration = TimeSpan.FromMinutes(60);
            if (_thumbnailExpiration <= TimeSpan.Zero) _thumbnailExpiration = TimeSpan.FromMinutes(120);

            // Setup cleanup timer - run every 10 minutes with staggered start
            var initialDelay = TimeSpan.FromMinutes(new Random().Next(1, 5));
            _cleanupTimer = new Timer(async _ => await CleanupExpiredEntriesAsync(),
                null, initialDelay, TimeSpan.FromMinutes(10));

            _logger.LogInformation("FileCachingService initialized - Enabled: {Enabled}, MaxFileSize: {MaxFileSize}MB, MaxTotalSize: {MaxTotalSize}MB",
                _cachingEnabled, _maxFileSize / 1024 / 1024, _maxTotalCacheSize / 1024 / 1024);
        }

        public Task<byte[]?> GetFileContentAsync(int fileId)
        {
            if (!_cachingEnabled || fileId <= 0)
                return Task.FromResult<byte[]?>(null);

            try
            {
                var key = $"{FILE_CONTENT_PREFIX}{fileId}";
                var content = _memoryCache.Get<byte[]>(key);

                if (content != null)
                {
                    UpdateAccessTime(fileId, content.Length, "content");
                    _logger.LogDebug("File content cache hit for file {FileId}, size: {Size} bytes",
                        fileId, content.Length);
                }
                else
                {
                    _logger.LogDebug("File content cache miss for file {FileId}", fileId);
                }

                return Task.FromResult(content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving file content from cache for file {FileId}", fileId);
                return Task.FromResult<byte[]?>(null);
            }
        }

        public async Task SetFileContentAsync(int fileId, byte[] content, TimeSpan? expiration = null)
        {
            if (!_cachingEnabled || fileId <= 0 || content == null)
                return;

            // Don't cache files that exceed the maximum size
            if (content.Length > _maxFileSize)
            {
                _logger.LogDebug("File {FileId} exceeds max cache size ({Size} > {MaxSize}), not caching",
                    fileId, content.Length, _maxFileSize);
                return;
            }

            try
            {
                // Check if adding this file would exceed total cache size
                if (!await CanAddToCacheAsync(content.Length))
                {
                    _logger.LogDebug("Cannot add file {FileId} to cache - would exceed total cache size", fileId);
                    return;
                }

                var key = $"{FILE_CONTENT_PREFIX}{fileId}";
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                    Size = content.Length,
                    Priority = CacheItemPriority.Low, // Files can be evicted to free memory
                    PostEvictionCallbacks =
                    {
                        new PostEvictionCallbackRegistration
                        {
                            EvictionCallback = OnFileContentEvicted,
                            State = new EvictionState { FileId = fileId, Size = content.Length, Type = "content" }
                        }
                    }
                };

                _memoryCache.Set(key, content, options);
                UpdateCacheSize(content.Length, true);
                UpdateAccessTime(fileId, content.Length, "content");

                _logger.LogDebug("Cached file content for file {FileId}, size: {Size} bytes, expiration: {Expiration}",
                    fileId, content.Length, options.AbsoluteExpirationRelativeToNow);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching file content for file {FileId}", fileId);
            }
        }

        public Task<byte[]?> GetThumbnailContentAsync(int fileId)
        {
            if (!_cachingEnabled || fileId <= 0)
                return Task.FromResult<byte[]?>(null);

            try
            {
                var key = $"{THUMBNAIL_CONTENT_PREFIX}{fileId}";
                var content = _memoryCache.Get<byte[]>(key);

                if (content != null)
                {
                    UpdateAccessTime(fileId, content.Length, "thumbnail");
                    _logger.LogDebug("Thumbnail cache hit for file {FileId}, size: {Size} bytes",
                        fileId, content.Length);
                }
                else
                {
                    _logger.LogDebug("Thumbnail cache miss for file {FileId}", fileId);
                }

                return Task.FromResult(content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving thumbnail from cache for file {FileId}", fileId);
                return Task.FromResult<byte[]?>(null);
            }
        }

        public async Task SetThumbnailContentAsync(int fileId, byte[] content, TimeSpan? expiration = null)
        {
            if (!_cachingEnabled || fileId <= 0 || content == null)
                return;

            try
            {
                // Thumbnails are generally small, but still check reasonable size limit (10MB)
                if (content.Length > 10 * 1024 * 1024)
                {
                    _logger.LogDebug("Thumbnail for file {FileId} is too large ({Size} bytes), not caching",
                        fileId, content.Length);
                    return;
                }

                if (!await CanAddToCacheAsync(content.Length))
                {
                    _logger.LogDebug("Cannot add thumbnail for file {FileId} to cache - would exceed total cache size", fileId);
                    return;
                }

                var key = $"{THUMBNAIL_CONTENT_PREFIX}{fileId}";
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _thumbnailExpiration,
                    Size = content.Length,
                    Priority = CacheItemPriority.Normal, // Thumbnails are more important than full files
                    PostEvictionCallbacks =
                    {
                        new PostEvictionCallbackRegistration
                        {
                            EvictionCallback = OnThumbnailEvicted,
                            State = new EvictionState { FileId = fileId, Size = content.Length, Type = "thumbnail" }
                        }
                    }
                };

                _memoryCache.Set(key, content, options);
                UpdateCacheSize(content.Length, true);
                UpdateAccessTime(fileId, content.Length, "thumbnail");

                _logger.LogDebug("Cached thumbnail for file {FileId}, size: {Size} bytes, expiration: {Expiration}",
                    fileId, content.Length, options.AbsoluteExpirationRelativeToNow);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching thumbnail for file {FileId}", fileId);
            }
        }

        public Task InvalidateFileAsync(int fileId)
        {
            if (fileId <= 0) return Task.CompletedTask;

            try
            {
                var fileContentKey = $"{FILE_CONTENT_PREFIX}{fileId}";
                var thumbnailKey = $"{THUMBNAIL_CONTENT_PREFIX}{fileId}";

                // Get sizes before removal for cache size tracking
                var fileContent = _memoryCache.Get<byte[]>(fileContentKey);
                var thumbnailContent = _memoryCache.Get<byte[]>(thumbnailKey);

                _memoryCache.Remove(fileContentKey);
                _memoryCache.Remove(thumbnailKey);

                // Update cache size
                if (fileContent != null)
                    UpdateCacheSize(fileContent.Length, false);
                if (thumbnailContent != null)
                    UpdateCacheSize(thumbnailContent.Length, false);

                _accessTimes.TryRemove(fileId, out _);

                _logger.LogDebug("Invalidated cache for file {FileId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error invalidating cache for file {FileId}", fileId);
            }

            return Task.CompletedTask;
        }

        public Task<T?> GetFileMetadataAsync<T>(string key) where T : class
        {
            if (!_cachingEnabled || string.IsNullOrWhiteSpace(key))
                return Task.FromResult<T?>(null);

            try
            {
                var cacheKey = $"{FILE_METADATA_PREFIX}{key}";
                var metadata = _memoryCache.Get<T>(cacheKey);

                if (metadata != null)
                {
                    _logger.LogDebug("Metadata cache hit for key {Key}", key);
                }
                else
                {
                    _logger.LogDebug("Metadata cache miss for key {Key}", key);
                }

                return Task.FromResult(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving metadata from cache for key {Key}", key);
                return Task.FromResult<T?>(null);
            }
        }

        public Task SetFileMetadataAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            if (!_cachingEnabled || string.IsNullOrWhiteSpace(key) || value == null)
                return Task.CompletedTask;

            try
            {
                var cacheKey = $"{FILE_METADATA_PREFIX}{key}";
                var estimatedSize = EstimateObjectSize(value);

                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                    Priority = CacheItemPriority.High, // Metadata is frequently accessed
                    Size = estimatedSize
                };

                _memoryCache.Set(cacheKey, value, options);

                _logger.LogDebug("Cached metadata for key {Key}, expiration: {Expiration}",
                    key, options.AbsoluteExpirationRelativeToNow);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching metadata for key {Key}", key);
            }

            return Task.CompletedTask;
        }

        public Task InvalidateFileMetadataAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return Task.CompletedTask;

            try
            {
                var cacheKey = $"{FILE_METADATA_PREFIX}{key}";
                _memoryCache.Remove(cacheKey);

                _logger.LogDebug("Invalidated metadata cache for key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error invalidating metadata cache for key {Key}", key);
            }

            return Task.CompletedTask;
        }

        #region Helper Methods

        private async Task<bool> CanAddToCacheAsync(long size)
        {
            if (size > _maxFileSize)
                return false;

            lock (_cacheSizeLock)
            {
                if (_currentCacheSize + size > _maxTotalCacheSize)
                {
                    // Try to free up space by removing least recently used items
                    return TryFreeUpSpace(size);
                }
                return true;
            }
        }

        private bool TryFreeUpSpace(long requiredSize)
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-30); // Remove items not accessed in 30 minutes
            var itemsToRemove = _accessTimes
                .Where(kvp => kvp.Value.LastAccessed < cutoffTime)
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .Take(100) // Limit to prevent excessive removal
                .ToList();

            long freedSpace = 0;
            foreach (var item in itemsToRemove)
            {
                try
                {
                    var fileContentKey = $"{FILE_CONTENT_PREFIX}{item.Key}";
                    var thumbnailKey = $"{THUMBNAIL_CONTENT_PREFIX}{item.Key}";

                    var fileContent = _memoryCache.Get<byte[]>(fileContentKey);
                    var thumbnailContent = _memoryCache.Get<byte[]>(thumbnailKey);

                    if (fileContent != null)
                    {
                        _memoryCache.Remove(fileContentKey);
                        freedSpace += fileContent.Length;
                    }

                    if (thumbnailContent != null)
                    {
                        _memoryCache.Remove(thumbnailKey);
                        freedSpace += thumbnailContent.Length;
                    }

                    _accessTimes.TryRemove(item.Key, out _);

                    if (freedSpace >= requiredSize)
                        break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error removing cache item {FileId}", item.Key);
                }
            }

            _currentCacheSize -= freedSpace;

            if (freedSpace > 0)
            {
                _logger.LogDebug("Freed {FreedSpace} bytes from cache, removed {Count} items",
                    freedSpace, itemsToRemove.Count);
            }

            return _currentCacheSize + requiredSize <= _maxTotalCacheSize;
        }

        private void UpdateCacheSize(long size, bool isAddition)
        {
            lock (_cacheSizeLock)
            {
                if (isAddition)
                    _currentCacheSize += size;
                else
                    _currentCacheSize = Math.Max(0, _currentCacheSize - size);
            }
        }

        private void UpdateAccessTime(int fileId, long size, string type)
        {
            _accessTimes.AddOrUpdate(fileId,
                new CacheMetadata { LastAccessed = DateTime.UtcNow, Size = size, Type = type },
                (_, existing) =>
                {
                    existing.LastAccessed = DateTime.UtcNow;
                    return existing;
                });
        }

        private int EstimateObjectSize(object obj)
        {
            if (obj == null) return 0;

            return obj switch
            {
                string str => str.Length * 2, // Unicode characters
                byte[] bytes => bytes.Length,
                _ => obj.ToString()?.Length ?? 0
            };
        }

        private void OnFileContentEvicted(object key, object value, EvictionReason reason, object state)
        {
            if (state is EvictionState evictionState)
            {
                _accessTimes.TryRemove(evictionState.FileId, out _);
                UpdateCacheSize(evictionState.Size, false);
                _logger.LogDebug("File content for file {FileId} evicted from cache, reason: {Reason}, size: {Size}",
                    evictionState.FileId, reason, evictionState.Size);
            }
        }

        private void OnThumbnailEvicted(object key, object value, EvictionReason reason, object state)
        {
            if (state is EvictionState evictionState)
            {
                UpdateCacheSize(evictionState.Size, false);
                _logger.LogDebug("Thumbnail for file {FileId} evicted from cache, reason: {Reason}, size: {Size}",
                    evictionState.FileId, reason, evictionState.Size);
            }
        }

        private async Task CleanupExpiredEntriesAsync()
        {
            if (!await _cleanupSemaphore.WaitAsync(1000)) // Wait max 1 second
                return;

            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(_defaultExpiration.Add(TimeSpan.FromMinutes(30)));
                var expiredFiles = _accessTimes
                    .Where(kvp => kvp.Value.LastAccessed < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .Take(50) // Limit cleanup to prevent blocking
                    .ToList();

                foreach (var fileId in expiredFiles)
                {
                    await InvalidateFileAsync(fileId);
                }

                if (expiredFiles.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired file cache entries", expiredFiles.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during cache cleanup");
            }
            finally
            {
                _cleanupSemaphore.Release();
            }
        }

        private long GetConfigValue(IConfiguration configuration, string key, long defaultValue)
        {
            var value = configuration[key];
            return !string.IsNullOrEmpty(value) && long.TryParse(value, out var result) ? result : defaultValue;
        }

        private int GetConfigValue(IConfiguration configuration, string key, int defaultValue)
        {
            var value = configuration[key];
            return !string.IsNullOrEmpty(value) && int.TryParse(value, out var result) ? result : defaultValue;
        }

        #endregion

        #region Cache Statistics

        public Task<Dictionary<string, object>> GetCacheStatisticsAsync()
        {
            try
            {
                lock (_cacheSizeLock)
                {
                    var stats = new Dictionary<string, object>
                    {
                        ["CachingEnabled"] = _cachingEnabled,
                        ["MaxFileSize"] = _maxFileSize,
                        ["MaxTotalCacheSize"] = _maxTotalCacheSize,
                        ["CurrentCacheSize"] = _currentCacheSize,
                        ["CurrentCacheSizeFormatted"] = FormatBytes(_currentCacheSize),
                        ["DefaultExpiration"] = _defaultExpiration.TotalMinutes,
                        ["ThumbnailExpiration"] = _thumbnailExpiration.TotalMinutes,
                        ["TrackedFiles"] = _accessTimes.Count,
                        ["CacheUtilization"] = _maxTotalCacheSize > 0 ? (double)_currentCacheSize / _maxTotalCacheSize : 0
                    };

                    return Task.FromResult(stats);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file cache statistics");
                return Task.FromResult(new Dictionary<string, object> { ["Error"] = ex.Message });
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        private class EvictionState
        {
            public int FileId { get; set; }
            public long Size { get; set; }
            public string Type { get; set; } = string.Empty;
        }

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
                _cleanupTimer?.Dispose();
                _cleanupSemaphore?.Dispose();
                _accessTimes.Clear();
                _disposed = true;
            }
        }

        #endregion
    }
}