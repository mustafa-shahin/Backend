using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class FileCachingService : IFileCachingService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<FileCachingService> _logger;
        private readonly TimeSpan _defaultExpiration;
        private readonly bool _cachingEnabled;
        private readonly long _maxCacheSize;

        // Cache key prefixes
        private const string FILE_CONTENT_PREFIX = "file_content_";
        private const string THUMBNAIL_CONTENT_PREFIX = "thumbnail_content_";
        private const string FILE_METADATA_PREFIX = "file_metadata_";

        public FileCachingService(
            IMemoryCache memoryCache,
            IConfiguration configuration,
            ILogger<FileCachingService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;

            _defaultExpiration = TimeSpan.FromMinutes(
                int.Parse(configuration["FileStorage:DatabaseSettings:CacheExpirationMinutes"] ?? "60"));

            _cachingEnabled = bool.Parse(
                configuration["FileStorage:DatabaseSettings:EnableFileCaching"] ?? "true");

            _maxCacheSize = long.Parse(
                configuration["FileStorage:DatabaseSettings:MaxDatabaseFileSize"] ?? "104857600"); // 100MB
        }

        public async Task<byte[]?> GetFileContentAsync(int fileId)
        {
            if (!_cachingEnabled) return null;

            try
            {
                var key = $"{FILE_CONTENT_PREFIX}{fileId}";
                return _memoryCache.Get<byte[]>(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving file content from cache for file {FileId}", fileId);
                return null;
            }
        }

        public async Task SetFileContentAsync(int fileId, byte[] content, TimeSpan? expiration = null)
        {
            if (!_cachingEnabled || content.Length > _maxCacheSize) return;

            try
            {
                var key = $"{FILE_CONTENT_PREFIX}{fileId}";
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                    Size = content.Length,
                    Priority = CacheItemPriority.Low // Files can be evicted to free memory
                };

                _memoryCache.Set(key, content, options);

                _logger.LogDebug("Cached file content for file {FileId}, size: {Size} bytes",
                    fileId, content.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching file content for file {FileId}", fileId);
            }
        }

        public async Task<byte[]?> GetThumbnailContentAsync(int fileId)
        {
            if (!_cachingEnabled) return null;

            try
            {
                var key = $"{THUMBNAIL_CONTENT_PREFIX}{fileId}";
                return _memoryCache.Get<byte[]>(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving thumbnail from cache for file {FileId}", fileId);
                return null;
            }
        }

        public async Task SetThumbnailContentAsync(int fileId, byte[] content, TimeSpan? expiration = null)
        {
            if (!_cachingEnabled) return;

            try
            {
                var key = $"{THUMBNAIL_CONTENT_PREFIX}{fileId}";
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                    Size = content.Length,
                    Priority = CacheItemPriority.Normal 
                };

                _memoryCache.Set(key, content, options);

                _logger.LogDebug("Cached thumbnail for file {FileId}, size: {Size} bytes",
                    fileId, content.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching thumbnail for file {FileId}", fileId);
            }
        }

        public async Task InvalidateFileAsync(int fileId)
        {
            try
            {
                var fileContentKey = $"{FILE_CONTENT_PREFIX}{fileId}";
                var thumbnailKey = $"{THUMBNAIL_CONTENT_PREFIX}{fileId}";

                _memoryCache.Remove(fileContentKey);
                _memoryCache.Remove(thumbnailKey);

                _logger.LogDebug("Invalidated cache for file {FileId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error invalidating cache for file {FileId}", fileId);
            }
        }

        public async Task<T?> GetFileMetadataAsync<T>(string key) where T : class
        {
            if (!_cachingEnabled) return null;

            try
            {
                var cacheKey = $"{FILE_METADATA_PREFIX}{key}";
                return _memoryCache.Get<T>(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving metadata from cache for key {Key}", key);
                return null;
            }
        }

        public async Task SetFileMetadataAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            if (!_cachingEnabled) return;

            try
            {
                var cacheKey = $"{FILE_METADATA_PREFIX}{key}";
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration,
                    Priority = CacheItemPriority.High // Metadata is frequently accessed
                };

                _memoryCache.Set(cacheKey, value, options);

                _logger.LogDebug("Cached metadata for key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching metadata for key {Key}", key);
            }
        }

        public async Task InvalidateFileMetadataAsync(string key)
        {
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
        }
    }
   
}