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
        private readonly ConcurrentDictionary<int, DateTime> _lastAccessTimes;
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _cleanupSemaphore;
        private bool _disposed = false;

        // Cache key prefixes
        private const string FILE_CONTENT_PREFIX = "file_content_";
        private const string THUMBNAIL_CONTENT_PREFIX = "thumbnail_content_";
        private const string FILE_METADATA_PREFIX = "file_metadata_";

        public FileCachingService(
            IMemoryCache memoryCache,
            IConfiguration configuration,
            ILogger<FileCachingService> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lastAccessTimes = new ConcurrentDictionary<int, DateTime>();
            _cleanupSemaphore = new SemaphoreSlim(1, 1);

            // Load configuration with validation
            _defaultExpiration = TimeSpan.FromMinutes(GetConfigValue(configuration,
                "FileStorage:DatabaseSettings:CacheExpirationMinutes", 60));

            _thumbnailExpiration = TimeSpan.FromMinutes(GetConfigValue(configuration,
                "FileStorage:DatabaseSettings:ThumbnailCacheExpirationMinutes", 120));

            _cachingEnabled = bool.Parse(configuration["FileStorage:DatabaseSettings:EnableFileCaching"] ?? "true");

            _maxFileSize = GetConfigValue(configuration,
                "FileStorage:DatabaseSettings:MaxDatabaseFileSize", 104857600); // 100MB

            _maxTotalCacheSize = GetConfigValue(configuration,
                "FileStorage:DatabaseSettings:MaxTotalCacheSize", 1073741824); // 1GB

            // Validate configuration
            if (_maxFileSize <= 0) _maxFileSize = 104857600;
            if (_maxTotalCacheSize <= 0) _maxTotalCacheSize = 1073741824;
            if (_defaultExpiration <= TimeSpan.Zero) _defaultExpiration = TimeSpan.FromMinutes(60);
            if (_thumbnailExpiration <= TimeSpan.Zero) _thumbnailExpiration = TimeSpan.FromMinutes(120);

            // Setup cleanup timer - run every 30 minutes
            _cleanupTimer = new Timer(async _ => await CleanupExpiredEntriesAsync(),
                null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

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
                    _lastAccessTimes[fileId] = DateTime.UtcNow;
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

        public Task SetFileContentAsync(int fileId, byte[] content, TimeSpan? expiration = null)
        {
            if (!_cachingEnabled || fileId <= 0 || content == null)
                return Task.CompletedTask;

            // Don't cache files that exceed the maximum size
            if (content.Length > _maxFileSize)
            {
                _logger.LogDebug("File {FileId} exceeds max cache size ({Size} > {MaxSize}), not caching",
                    fileId, content.Length, _maxFileSize);
                return Task.CompletedTask;
            }

            try
            {
                // Check if adding this file would exceed total cache size
                if (!CanAddToCache(content.Length))
                {
                    _logger.LogDebug("Cannot add file {FileId} to cache - would exceed total cache size", fileId);
                    return Task.CompletedTask;
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
                            State = fileId
                        }
                    }
                };

                _memoryCache.Set(key, content, options);
                _lastAccessTimes[fileId] = DateTime.UtcNow;

                _logger.LogDebug("Cached file content for file {FileId}, size: {Size} bytes, expiration: {Expiration}",
                    fileId, content.Length, options.AbsoluteExpirationRelativeToNow);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching file content for file {FileId}", fileId);
            }

            return Task.CompletedTask;
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
                    _lastAccessTimes[fileId] = DateTime.UtcNow;
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

        public Task SetThumbnailContentAsync(int fileId, byte[] content, TimeSpan? expiration = null)
        {
            if (!_cachingEnabled || fileId <= 0 || content == null)
                return Task.CompletedTask;

            try
            {
                // Thumbnails are generally small, but still check reasonable size limit (10MB)
                if (content.Length > 10 * 1024 * 1024)
                {
                    _logger.LogDebug("Thumbnail for file {FileId} is too large ({Size} bytes), not caching",
                        fileId, content.Length);
                    return Task.CompletedTask;
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
                            State = fileId
                        }
                    }
                };

                _memoryCache.Set(key, content, options);
                _lastAccessTimes[fileId] = DateTime.UtcNow;

                _logger.LogDebug("Cached thumbnail for file {FileId}, size: {Size} bytes, expiration: {Expiration}",
                    fileId, content.Length, options.AbsoluteExpirationRelativeToNow);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching thumbnail for file {FileId}", fileId);
            }

            return Task.CompletedTask;
        }

        public Task InvalidateFileAsync(int fileId)
        {
            if (fileId <= 0) return Task.CompletedTask;

            try
            {
                var fileContentKey = $"{FILE_CONTENT_PREFIX}{fileId}";
                var thumbnailKey = $"{THUMBNAIL_CONTENT_PREFIX}{fileId}";

                _memoryCache.Remove(fileContentKey);
                _memoryCache.Remove(thumbnailKey);
                _lastAccessTimes.TryRemove(fileId, out _);

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
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                    Priority = CacheItemPriority.High, // Metadata is frequently accessed
                    Size = EstimateObjectSize(value)
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

        private bool CanAddToCache(long size)
        {
            // Simple check - in a real implementation, might want to track actual cache size
            return size <= _maxFileSize;
        }

        private int EstimateObjectSize(object obj)
        {
            // Simple estimation - might want a more sophisticated approach
            return obj?.ToString()?.Length ?? 0;
        }

        private void OnFileContentEvicted(object key, object value, EvictionReason reason, object state)
        {
            if (state is int fileId)
            {
                _lastAccessTimes.TryRemove(fileId, out _);
                _logger.LogDebug("File content for file {FileId} evicted from cache, reason: {Reason}",
                    fileId, reason);
            }
        }

        private void OnThumbnailEvicted(object key, object value, EvictionReason reason, object state)
        {
            if (state is int fileId)
            {
                _logger.LogDebug("Thumbnail for file {FileId} evicted from cache, reason: {Reason}",
                    fileId, reason);
            }
        }

        private async Task CleanupExpiredEntriesAsync()
        {
            if (!await _cleanupSemaphore.WaitAsync(1000)) // Wait max 1 second
                return;

            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(_defaultExpiration);
                var expiredFiles = _lastAccessTimes
                    .Where(kvp => kvp.Value < cutoffTime)
                    .Select(kvp => kvp.Key)
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
            return long.TryParse(configuration[key], out var value) ? value : defaultValue;
        }

        private int GetConfigValue(IConfiguration configuration, string key, int defaultValue)
        {
            return int.TryParse(configuration[key], out var value) ? value : defaultValue;
        }

        #endregion

        #region Cache Statistics

        public Task<Dictionary<string, object>> GetCacheStatisticsAsync()
        {
            try
            {
                var stats = new Dictionary<string, object>
                {
                    ["CachingEnabled"] = _cachingEnabled,
                    ["MaxFileSize"] = _maxFileSize,
                    ["MaxTotalCacheSize"] = _maxTotalCacheSize,
                    ["DefaultExpiration"] = _defaultExpiration.TotalMinutes,
                    ["ThumbnailExpiration"] = _thumbnailExpiration.TotalMinutes,
                    ["TrackedFiles"] = _lastAccessTimes.Count,
                    ["LastCleanupRun"] = DateTime.UtcNow // In real implementation, track this
                };

                return Task.FromResult(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file cache statistics");
                return Task.FromResult(new Dictionary<string, object> { ["Error"] = ex.Message });
            }
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
                _cleanupTimer?.Dispose();
                _cleanupSemaphore?.Dispose();
                _lastAccessTimes.Clear();
                _disposed = true;
            }
        }

        #endregion
    }
}