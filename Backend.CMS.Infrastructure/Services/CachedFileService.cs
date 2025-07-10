//using Backend.CMS.Application.DTOs;
//using Backend.CMS.Domain.Enums;
//using Backend.CMS.Infrastructure.Caching.Interfaces;
//using Backend.CMS.Infrastructure.Interfaces;
//using Microsoft.AspNetCore.Http;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using System.Collections.Concurrent;
//using System.Security.Cryptography;
//using System.Text.Json;

//namespace Backend.CMS.Infrastructure.Services
//{
//    public class CachedFileService : IFileService, IDisposable
//    {
//        private readonly FileService _baseFileService;
//        private readonly ICacheService _cacheService;
//        private readonly ICacheKeyService _cacheKeyService;
//        private readonly ICacheInvalidationService _cacheInvalidationService;
//        private readonly ILogger<CachedFileService> _logger;
//        private readonly IConfiguration _configuration;

//        // Semaphore management for concurrent operations
//        private readonly SemaphoreSlim _streamSemaphore;
//        private readonly ConcurrentDictionary<int, SemaphoreSlim> _fileSemaphores;
//        private readonly Timer _semaphoreCleanupTimer;
//        private readonly Lock _semaphoreLock = new();

//        // Cache TTL configurations
//        private readonly TimeSpan _fileMetadataCacheTTL;
//        private readonly TimeSpan _fileContentCacheTTL;
//        private readonly TimeSpan _fileListCacheTTL;
//        private readonly TimeSpan _searchResultsCacheTTL;
//        private readonly TimeSpan _statisticsCacheTTL;
//        private readonly TimeSpan _existenceCheckCacheTTL;
//        private readonly TimeSpan _previewCacheTTL;
//        private readonly TimeSpan _verificationCacheTTL;

//        // Performance settings
//        private readonly int _maxConcurrentStreams;
//        private readonly long _maxCacheableFileSize;
//        private readonly bool _enableAggressiveCaching;

//        private bool _disposed = false;

//        public CachedFileService(
//            FileService baseFileService,
//            ICacheService cacheService,
//            ICacheKeyService cacheKeyService,
//            ICacheInvalidationService cacheInvalidationService,
//            ILogger<CachedFileService> logger,
//            IConfiguration configuration)
//        {
//            _baseFileService = baseFileService ?? throw new ArgumentNullException(nameof(baseFileService));
//            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
//            _cacheKeyService = cacheKeyService ?? throw new ArgumentNullException(nameof(cacheKeyService));
//            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

//            // Load cache TTL configurations
//            _fileMetadataCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FileStorage:CacheTTL:MetadataMinutes", 30));
//            _fileContentCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FileStorage:CacheTTL:ContentMinutes", 60));
//            _fileListCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FileStorage:CacheTTL:ListMinutes", 10));
//            _searchResultsCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FileStorage:CacheTTL:SearchMinutes", 5));
//            _statisticsCacheTTL = TimeSpan.FromHours(_configuration.GetValue("FileStorage:CacheTTL:StatisticsHours", 1));
//            _existenceCheckCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FileStorage:CacheTTL:ExistenceMinutes", 5));
//            _previewCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FileStorage:CacheTTL:PreviewMinutes", 15));
//            _verificationCacheTTL = TimeSpan.FromHours(_configuration.GetValue("FileStorage:CacheTTL:VerificationHours", 2));

//            // Load performance settings
//            _maxConcurrentStreams = _configuration.GetValue("FileStorage:Performance:MaxConcurrentStreams", Environment.ProcessorCount * 2);
//            _maxCacheableFileSize = _configuration.GetValue("FileStorage:Performance:MaxCacheableFileSizeMB", 50) * 1024 * 1024;
//            _enableAggressiveCaching = _configuration.GetValue("FileStorage:Performance:EnableAggressiveCaching", true);

//            // Initialize semaphores and cleanup
//            _streamSemaphore = new SemaphoreSlim(_maxConcurrentStreams, _maxConcurrentStreams);
//            _fileSemaphores = new ConcurrentDictionary<int, SemaphoreSlim>();

//            // Cleanup unused semaphores every 5 minutes
//            _semaphoreCleanupTimer = new Timer(CleanupUnusedSemaphores, null,
//                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
//        }

//        #region Upload Operations (Invalidate cache after upload)

//        public async Task<FileDto> UploadFileAsync(FileUploadDto uploadDto)
//        {
//            var result = await _baseFileService.UploadFileAsync(uploadDto);
//            await InvalidateFileRelatedCacheAsync(result.Id, result.FolderId);
//            return result;
//        }

//        public async Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto)
//        {
//            var results = await _baseFileService.UploadMultipleFilesAsync(uploadDto);

//            // Invalidate cache for all uploaded files
//            var invalidationTasks = results.Select(file => InvalidateFileRelatedCacheAsync(file.Id, file.FolderId));
//            await Task.WhenAll(invalidationTasks);

//            return results;
//        }

//        public async Task<FileDto> UploadFileFromUrlAsync(string url, int? folderId = null, string? description = null)
//        {
//            var result = await _baseFileService.UploadFileFromUrlAsync(url, folderId, description);
//            await InvalidateFileRelatedCacheAsync(result.Id, result.FolderId);
//            return result;
//        }

//        #endregion

//        #region Cached File Retrieval Operations

//        public async Task<FileDto> GetFileByIdAsync(int fileId)
//        {
//            if (fileId <= 0)
//                throw new ArgumentException("File ID must be greater than 0", nameof(fileId));

//            var cacheKey = _cacheKeyService.GetEntityKey<FileDto>(fileId);

//            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
//            {
//                _logger.LogDebug("Cache miss for file metadata: {FileId}", fileId);
//                return await _baseFileService.GetFileByIdAsync(fileId);
//            }, _fileMetadataCacheTTL) ?? throw new InvalidOperationException($"File with ID {fileId} could not be retrieved.");
//        }

//        public async Task<List<FileDto>> GetFilesAsync(int page = 1, int pageSize = 20)
//        {
//            var cacheKey = _cacheKeyService.GetCollectionKey<FileDto>("paged", page, pageSize);

//            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
//            {
//                _logger.LogDebug("Cache miss for file list: page {Page}, size {PageSize}", page, pageSize);
//                var files = await _baseFileService.GetFilesAsync(page, pageSize);

//                // Cache individual file metadata for future single file requests
//                if (_enableAggressiveCaching)
//                {
//                    await CacheFileMetadataListAsync(files);
//                }

//                return files;
//            }, _fileListCacheTTL) ?? new List<FileDto>();
//        }

//        public async Task<List<FileDto>> GetFilesByFolderAsync(int? folderId, int page = 1, int pageSize = 20)
//        {
//            var cacheKey = _cacheKeyService.GetCollectionKey<FileDto>("folder", folderId ?? 0, page, pageSize);

//            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
//            {
//                _logger.LogDebug("Cache miss for folder file list: folder {FolderId}, page {Page}, size {PageSize}",
//                    folderId, page, pageSize);

//                var files = await _baseFileService.GetFilesByFolderAsync(folderId, page, pageSize);

//                // Cache individual file metadata
//                if (_enableAggressiveCaching && files.Any())
//                {
//                    await CacheFileMetadataListAsync(files);
//                }

//                return files;
//            }, _fileListCacheTTL) ?? new List<FileDto>();
//        }

//        public async Task<List<FileDto>> SearchFilesAsync(FileSearchDto searchDto)
//        {
//            // Create deterministic cache key based on search parameters
//            var searchHash = GenerateSearchHash(searchDto);
//            var cacheKey = _cacheKeyService.GetQueryKey<FileDto>("search", new { Hash = searchHash });

//            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
//            {
//                _logger.LogDebug("Cache miss for file search: {SearchHash}", searchHash);
//                var files = await _baseFileService.SearchFilesAsync(searchDto);

//                // Cache individual file metadata for shorter TTL during search
//                if (_enableAggressiveCaching && files.Any())
//                {
//                    await CacheFileMetadataListAsync(files, TimeSpan.FromMinutes(10));
//                }

//                return files;
//            }, _searchResultsCacheTTL) ?? new List<FileDto>();
//        }

//        public async Task<List<FileDto>> GetRecentFilesAsync(int count = 10)
//        {
//            var cacheKey = _cacheKeyService.GetCollectionKey<FileDto>("recent", count);

//            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
//            {
//                _logger.LogDebug("Cache miss for recent files: count {Count}", count);
//                var files = await _baseFileService.GetRecentFilesAsync(count);

//                if (_enableAggressiveCaching && files.Any())
//                {
//                    await CacheFileMetadataListAsync(files);
//                }

//                return files;
//            }, _fileListCacheTTL) ?? new List<FileDto>();
//        }

//        public async Task<FilePreviewDto> GetFilePreviewAsync(int fileId)
//        {
//            if (fileId <= 0)
//                throw new ArgumentException("File ID must be greater than 0", nameof(fileId));

//            var cacheKey = _cacheKeyService.GetCustomKey("file_preview", fileId);

//            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
//            {
//                _logger.LogDebug("Cache miss for file preview: {FileId}", fileId);
//                return await _baseFileService.GetFilePreviewAsync(fileId);
//            }, _previewCacheTTL) ?? throw new InvalidOperationException($"Preview for file {fileId} could not be generated.");
//        }

//        public async Task<Dictionary<string, object>> GetFileStatisticsAsync()
//        {
//            var cacheKey = _cacheKeyService.GetCustomKey("file_statistics");

//            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
//            {
//                _logger.LogDebug("Cache miss for file statistics");
//                return await _baseFileService.GetFileStatisticsAsync();
//            }, _statisticsCacheTTL) ?? new Dictionary<string, object>();
//        }

//        public async Task<bool> FileExistsAsync(int fileId)
//        {
//            if (fileId <= 0)
//                return false;

//            var cacheKey = _cacheKeyService.GetCustomKey("file_exists", fileId);

//            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
//            {
//                var exists = await _baseFileService.FileExistsAsync(fileId);
//                return new { Exists = exists };
//            }, _existenceCheckCacheTTL);

//            var exists = result?.Exists ?? false;
//            _logger.LogDebug("File existence check for {FileId}: {Exists} (cached: {IsCached})",
//                fileId, exists, result != null);

//            return exists;
//        }

//        public async Task<long> GetTotalFileSizeAsync(int? folderId = null)
//        {
//            var cacheKey = _cacheKeyService.GetCustomKey("file_total_size", folderId ?? 0);

//            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
//            {
//                var totalSize = await _baseFileService.GetTotalFileSizeAsync(folderId);
//                return new { TotalSize = totalSize };
//            }, _fileListCacheTTL);

//            var size = result?.TotalSize ?? 0;
//            _logger.LogDebug("Total file size for folder {FolderId}: {Size} bytes", folderId, size);

//            return size;
//        }

//        #endregion

//        #region Cached Stream Operations

//        public async Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int fileId)
//        {
//            if (fileId <= 0)
//                throw new ArgumentException("File ID must be greater than 0", nameof(fileId));

//            var fileSemaphore = GetOrCreateFileSemaphore(fileId);
//            await _streamSemaphore.WaitAsync();

//            try
//            {
//                await fileSemaphore.WaitAsync();
//                try
//                {
//                    // Check if we should cache file content
//                    var fileInfo = await GetFileByIdAsync(fileId);
//                    var shouldCacheContent = fileInfo.FileSize <= _maxCacheableFileSize;

//                    if (shouldCacheContent)
//                    {
//                        var contentCacheKey = _cacheKeyService.GetCustomKey("file_content", fileId);

//                        // Try to get cached content
//                        var cachedContent = await _cacheService.GetAsync<FileContentCache>(contentCacheKey);
//                        if (cachedContent != null)
//                        {
//                            _logger.LogDebug("File {FileId} served from cache, size: {Size} bytes",
//                                fileId, cachedContent.Content.Length);

//                            var cachedStream = new MemoryStream(cachedContent.Content, false);
//                            return (cachedStream, cachedContent.ContentType, cachedContent.FileName);
//                        }
//                    }

//                    // Get from database and optionally cache
//                    var (stream, contentType, fileName) = await _baseFileService.GetFileStreamAsync(fileId);

//                    // Cache content if it's small enough and stream supports seeking
//                    if (shouldCacheContent && stream.CanSeek && stream.CanRead)
//                    {
//                        var originalPosition = stream.Position;
//                        stream.Position = 0;

//                        using var memoryStream = new MemoryStream();
//                        await stream.CopyToAsync(memoryStream);
//                        var content = memoryStream.ToArray();

//                        // Dispose the original stream
//                        await stream.DisposeAsync();

//                        // Cache content asynchronously
//                        _ = CacheFileContentAsync(fileId, content, contentType, fileName);

//                        // Return new stream with cached content
//                        return (new MemoryStream(content, false), contentType, fileName);
//                    }

//                    _logger.LogDebug("File {FileId} served directly (not cached), size: {Size} bytes",
//                        fileId, fileInfo.FileSize);
//                    return (stream, contentType, fileName);
//                }
//                finally
//                {
//                    fileSemaphore.Release();
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error getting file stream for file {FileId}", fileId);
//                throw;
//            }
//            finally
//            {
//                _streamSemaphore.Release();
//            }
//        }

//        public async Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int fileId)
//        {
//            if (fileId <= 0)
//                throw new ArgumentException("File ID must be greater than 0", nameof(fileId));

//            var fileSemaphore = GetOrCreateFileSemaphore(fileId);
//            await _streamSemaphore.WaitAsync();

//            try
//            {
//                await fileSemaphore.WaitAsync();
//                try
//                {
//                    var thumbnailCacheKey = _cacheKeyService.GetCustomKey("file_thumbnail", fileId);

//                    // Try to get cached thumbnail
//                    var cachedThumbnail = await _cacheService.GetAsync<FileContentCache>(thumbnailCacheKey);
//                    if (cachedThumbnail != null)
//                    {
//                        _logger.LogDebug("Thumbnail for file {FileId} served from cache, size: {Size} bytes",
//                            fileId, cachedThumbnail.Content.Length);

//                        var cachedStream = new MemoryStream(cachedThumbnail.Content, false);
//                        return (cachedStream, cachedThumbnail.ContentType, cachedThumbnail.FileName);
//                    }

//                    // Get from database
//                    var (stream, contentType, fileName) = await _baseFileService.GetThumbnailStreamAsync(fileId);

//                    // Cache thumbnail if stream supports seeking
//                    if (stream.CanSeek && stream.CanRead)
//                    {
//                        var originalPosition = stream.Position;
//                        stream.Position = 0;

//                        using var memoryStream = new MemoryStream();
//                        await stream.CopyToAsync(memoryStream);
//                        var content = memoryStream.ToArray();

//                        // Dispose the original stream
//                        await stream.DisposeAsync();

//                        // Cache thumbnail asynchronously
//                        _ = CacheThumbnailAsync(fileId, content, contentType, fileName);

//                        // Return new stream with cached content
//                        return (new MemoryStream(content, false), contentType, fileName);
//                    }

//                    return (stream, contentType, fileName);
//                }
//                finally
//                {
//                    fileSemaphore.Release();
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error getting thumbnail stream for file {FileId}", fileId);
//                throw;
//            }
//            finally
//            {
//                _streamSemaphore.Release();
//            }
//        }

//        #endregion

//        #region Update/Delete Operations (With cache invalidation)

//        public async Task<FileDto> UpdateFileAsync(int fileId, UpdateFileDto updateDto)
//        {
//            var result = await _baseFileService.UpdateFileAsync(fileId, updateDto);
//            await InvalidateFileRelatedCacheAsync(fileId, updateDto.FolderId);
//            return result;
//        }

//        public async Task<bool> DeleteFileAsync(int fileId)
//        {
//            // Get file info before deletion for cache invalidation
//            FileDto? fileInfo = null;
//            try
//            {
//                fileInfo = await GetFileByIdAsync(fileId);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Could not get file info before deletion for cache invalidation: {FileId}", fileId);
//            }

//            var result = await _baseFileService.DeleteFileAsync(fileId);
//            if (result)
//            {
//                await InvalidateFileRelatedCacheAsync(fileId, fileInfo?.FolderId);
//                await InvalidateFileVerificationCacheAsync(fileId);
//            }
//            return result;
//        }

//        public async Task<bool> DeleteMultipleFilesAsync(List<int> fileIds)
//        {
//            if (fileIds?.Any() != true)
//                return false;

//            var result = await _baseFileService.DeleteMultipleFilesAsync(fileIds);
//            if (result)
//            {
//                var invalidationTasks = fileIds.Select(fileId =>
//                    Task.WhenAll(
//                        InvalidateFileRelatedCacheAsync(fileId, null),
//                        InvalidateFileVerificationCacheAsync(fileId)
//                    ));
//                await Task.WhenAll(invalidationTasks);
//            }
//            return result;
//        }

//        public async Task<FileDto> MoveFileAsync(MoveFileDto moveDto)
//        {
//            // Get original folder info for cache invalidation
//            FileDto? originalFile = null;
//            try
//            {
//                originalFile = await GetFileByIdAsync(moveDto.FileId);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Could not get original file info before move: {FileId}", moveDto.FileId);
//            }

//            var result = await _baseFileService.MoveFileAsync(moveDto);

//            // Invalidate cache for both old and new folders
//            await Task.WhenAll(
//                InvalidateFileRelatedCacheAsync(moveDto.FileId, originalFile?.FolderId),
//                InvalidateFileRelatedCacheAsync(moveDto.FileId, moveDto.NewFolderId),
//                InvalidateFileVerificationCacheAsync(moveDto.FileId)
//            );

//            return result;
//        }

//        public async Task<FileDto> CopyFileAsync(CopyFileDto copyDto)
//        {
//            var result = await _baseFileService.CopyFileAsync(copyDto);
//            await InvalidateFileRelatedCacheAsync(result.Id, result.FolderId);
//            return result;
//        }

//        public async Task<bool> RenameFileAsync(int fileId, string newName)
//        {
//            var result = await _baseFileService.RenameFileAsync(fileId, newName);
//            if (result)
//            {
//                await Task.WhenAll(
//                    InvalidateFileMetadataCacheAsync(fileId),
//                    InvalidateFileVerificationCacheAsync(fileId)
//                );
//            }
//            return result;
//        }

//        public async Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto)
//        {
//            if (fileIds?.Any() != true)
//                return false;

//            var result = await _baseFileService.BulkUpdateFilesAsync(fileIds, updateDto);
//            if (result)
//            {
//                var invalidationTasks = fileIds.Select(fileId =>
//                    Task.WhenAll(
//                        InvalidateFileRelatedCacheAsync(fileId, updateDto.FolderId),
//                        InvalidateFileVerificationCacheAsync(fileId)
//                    ));
//                await Task.WhenAll(invalidationTasks);
//            }
//            return result;
//        }

//        public async Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId)
//        {
//            if (fileIds?.Any() != true)
//                return false;

//            var result = await _baseFileService.BulkMoveFilesAsync(fileIds, destinationFolderId);
//            if (result)
//            {
//                var invalidationTasks = fileIds.Select(fileId =>
//                    Task.WhenAll(
//                        InvalidateFileRelatedCacheAsync(fileId, destinationFolderId),
//                        InvalidateFileVerificationCacheAsync(fileId)
//                    ));
//                await Task.WhenAll(invalidationTasks);
//            }
//            return result;
//        }

//        public async Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId)
//        {
//            var results = await _baseFileService.BulkCopyFilesAsync(fileIds, destinationFolderId);

//            // Invalidate cache for copied files
//            var invalidationTasks = results.Select(file => InvalidateFileRelatedCacheAsync(file.Id, file.FolderId));
//            await Task.WhenAll(invalidationTasks);

//            return results;
//        }

//        public async Task<bool> GenerateThumbnailAsync(int fileId)
//        {
//            var result = await _baseFileService.GenerateThumbnailAsync(fileId);
//            if (result)
//            {
//                await Task.WhenAll(
//                    InvalidateFileRelatedCacheAsync(fileId, null),
//                    InvalidateFileVerificationCacheAsync(fileId)
//                );
//            }
//            return result;
//        }

//        public async Task<bool> ProcessFileAsync(int fileId)
//        {
//            var result = await _baseFileService.ProcessFileAsync(fileId);
//            if (result)
//            {
//                await Task.WhenAll(
//                    InvalidateFileRelatedCacheAsync(fileId, null),
//                    InvalidateFileVerificationCacheAsync(fileId)
//                );
//            }
//            return result;
//        }

//        #endregion

//        #region Cached Verification

//        public async Task<bool> VerifyFileIntegrityAsync(int fileId)
//        {
//            if (fileId <= 0)
//                throw new ArgumentException("File ID must be greater than 0", nameof(fileId));

//            var fileSemaphore = GetOrCreateFileSemaphore(fileId);
//            await fileSemaphore.WaitAsync();

//            try
//            {
//                var cacheKey = _cacheKeyService.GetCustomKey("file_integrity", fileId);

//                var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
//                {
//                    _logger.LogDebug("Performing file integrity verification for file {FileId}", fileId);

//                    // Get file info to check if it was modified
//                    var fileInfo = await GetFileByIdAsync(fileId);
//                    var isValid = await _baseFileService.VerifyFileIntegrityAsync(fileId);

//                    return new FileIntegrityResult
//                    {
//                        FileId = fileId,
//                        IsValid = isValid,
//                        VerifiedAt = DateTime.UtcNow,
//                        LastModified = fileInfo.UpdatedAt
//                    };
//                }, _verificationCacheTTL);

//                if (result != null)
//                {
//                    // Check if file was modified after verification
//                    var currentFileInfo = await GetFileByIdAsync(fileId);
//                    if (currentFileInfo.UpdatedAt > result.LastModified)
//                    {
//                        _logger.LogDebug("File {FileId} was modified after verification, re-verifying", fileId);

//                        // Invalidate cache and re-verify
//                        await _cacheService.RemoveAsync(cacheKey);
//                        return await VerifyFileIntegrityAsync(fileId);
//                    }

//                    _logger.LogDebug("File integrity verification served from cache for file {FileId}: {IsValid}",
//                        fileId, result.IsValid);
//                    return result.IsValid;
//                }

//                // Fallback
//                return await _baseFileService.VerifyFileIntegrityAsync(fileId);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error during cached file integrity verification for file {FileId}", fileId);
//                return await _baseFileService.VerifyFileIntegrityAsync(fileId);
//            }
//            finally
//            {
//                fileSemaphore.Release();
//            }
//        }

//        #endregion

//        #region Delegated Methods (No caching needed)

//        public Task<string> GenerateFileUrlAsync(int fileId, bool thumbnail = false) =>
//            _baseFileService.GenerateFileUrlAsync(fileId, thumbnail);

//        public Task RecordFileAccessAsync(int fileId, FileAccessType accessType) =>
//            _baseFileService.RecordFileAccessAsync(fileId, accessType);

//        public Task<bool> ValidateFileAsync(IFormFile file) =>
//            _baseFileService.ValidateFileAsync(file);

//        public Task<string> GetFileHashAsync(Stream stream) =>
//            _baseFileService.GetFileHashAsync(stream);

//        #endregion

//        #region Cache Helper Methods

//        private async Task CacheFileMetadataListAsync(IEnumerable<FileDto> files, TimeSpan? customTtl = null)
//        {
//            if (!_enableAggressiveCaching || !files.Any())
//                return;

//            var ttl = customTtl ?? _fileMetadataCacheTTL;
//            var cachingTasks = files.Select(async file =>
//            {
//                try
//                {
//                    var cacheKey = _cacheKeyService.GetEntityKey<FileDto>(file.Id);
//                    await _cacheService.SetAsync(cacheKey, file, ttl);
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogWarning(ex, "Failed to cache metadata for file {FileId}", file.Id);
//                }
//            });

//            await Task.WhenAll(cachingTasks);
//            _logger.LogDebug("Cached metadata for {Count} files", files.Count());
//        }

//        private string GenerateSearchHash(FileSearchDto searchDto)
//        {
//            // Create a consistent hash based on search parameters
//            var searchData = new
//            {
//                SearchTerm = searchDto.SearchTerm?.ToLowerInvariant(), 
//                searchDto.FileType,
//                searchDto.FolderId,
//                searchDto.IsPublic,
//                CreatedFrom = searchDto.CreatedFrom?.ToString("yyyyMMdd"),
//                CreatedTo = searchDto.CreatedTo?.ToString("yyyyMMdd"),
//                searchDto.MinSize,
//                searchDto.MaxSize,
//                SortBy = searchDto.SortBy?.ToLowerInvariant(), 
//                SortDirection = searchDto.SortDirection?.ToLowerInvariant(),
//                searchDto.Page,
//                searchDto.PageSize
//            };

//            var json = JsonSerializer.Serialize(searchData);
//            using var sha256 = SHA256.Create();
//            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
//            return Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-").TrimEnd('=')[..16];
//        }

//        private async Task InvalidateFileRelatedCacheAsync(int fileId, int? folderId)
//        {
//            try
//            {
//                var invalidationTasks = new List<Task>
//                {
//                    InvalidateFileMetadataCacheAsync(fileId),
//                    InvalidateFileContentCacheAsync(fileId),
//                    InvalidateFileExistenceCacheAsync(fileId),
//                    InvalidateGlobalFileCacheAsync(),
//                    InvalidateSearchCacheAsync()
//                };

//                if (folderId.HasValue)
//                {
//                    invalidationTasks.Add(InvalidateFolderRelatedCacheAsync(folderId.Value));
//                }

//                await Task.WhenAll(invalidationTasks);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error invalidating cache for file {FileId}", fileId);
//            }
//        }

//        private async Task InvalidateFileMetadataCacheAsync(int fileId)
//        {
//            try
//            {
//                var cacheKeys = new[]
//                {
//                    _cacheKeyService.GetEntityKey<FileDto>(fileId),
//                    _cacheKeyService.GetCustomKey("file_preview", fileId)
//                };

//                await _cacheService.RemoveAsync(cacheKeys);
//                _logger.LogDebug("Invalidated metadata cache for file {FileId}", fileId);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Failed to invalidate metadata cache for file {FileId}", fileId);
//            }
//        }

//        private async Task InvalidateFileContentCacheAsync(int fileId)
//        {
//            try
//            {
//                var cacheKeys = new[]
//                {
//                    _cacheKeyService.GetCustomKey("file_content", fileId),
//                    _cacheKeyService.GetCustomKey("file_thumbnail", fileId)
//                };

//                await _cacheService.RemoveAsync(cacheKeys);
//                _logger.LogDebug("Invalidated content cache for file {FileId}", fileId);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Failed to invalidate content cache for file {FileId}", fileId);
//            }
//        }

//        private async Task InvalidateFileExistenceCacheAsync(int fileId)
//        {
//            try
//            {
//                var cacheKey = _cacheKeyService.GetCustomKey("file_exists", fileId);
//                await _cacheService.RemoveAsync(cacheKey);
//                _logger.LogDebug("Invalidated existence cache for file {FileId}", fileId);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Failed to invalidate existence cache for file {FileId}", fileId);
//            }
//        }

//        private async Task InvalidateFileVerificationCacheAsync(int fileId)
//        {
//            try
//            {
//                var cacheKey = _cacheKeyService.GetCustomKey("file_integrity", fileId);
//                await _cacheService.RemoveAsync(cacheKey);
//                _logger.LogDebug("Verification cache invalidated for file {FileId}", fileId);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Failed to invalidate verification cache for file {FileId}", fileId);
//            }
//        }

//        private async Task InvalidateGlobalFileCacheAsync()
//        {
//            try
//            {
//                var patterns = new[]
//                {
//                    "file:list:*",
//                    "file:recent:*",
//                    "file_statistics",
//                    "file_total_size:*"
//                };

//                var invalidationTasks = patterns.Select(pattern => _cacheService.RemoveByPatternAsync(pattern));
//                await Task.WhenAll(invalidationTasks);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Failed to invalidate global file cache");
//            }
//        }

//        private async Task InvalidateSearchCacheAsync()
//        {
//            try
//            {
//                await _cacheService.RemoveByPatternAsync("file:query:search:*");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Failed to invalidate search cache");
//            }
//        }

//        private async Task InvalidateFolderRelatedCacheAsync(int folderId)
//        {
//            try
//            {
//                await _cacheService.RemoveByPatternAsync($"file:*:folder:{folderId}:*");
//                await _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("file_total_size", folderId));
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Failed to invalidate folder-related cache for folder {FolderId}", folderId);
//            }
//        }

//        private SemaphoreSlim GetOrCreateFileSemaphore(int fileId)
//        {
//            return _fileSemaphores.GetOrAdd(fileId, _ => new SemaphoreSlim(1, 1));
//        }

//        private async Task CacheFileContentAsync(int fileId, byte[] content, string contentType, string fileName)
//        {
//            try
//            {
//                var cacheKey = _cacheKeyService.GetCustomKey("file_content", fileId);
//                var cacheData = new FileContentCache
//                {
//                    Content = content,
//                    ContentType = contentType,
//                    FileName = fileName,
//                    CachedAt = DateTime.UtcNow
//                };

//                await _cacheService.SetAsync(cacheKey, cacheData, _fileContentCacheTTL);
//                _logger.LogDebug("Cached file content for file {FileId}, size: {Size} bytes", fileId, content.Length);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Failed to cache file content for file {FileId}", fileId);
//            }
//        }

//        private async Task CacheThumbnailAsync(int fileId, byte[] content, string contentType, string fileName)
//        {
//            try
//            {
//                var cacheKey = _cacheKeyService.GetCustomKey("file_thumbnail", fileId);
//                var cacheData = new FileContentCache
//                {
//                    Content = content,
//                    ContentType = contentType,
//                    FileName = fileName,
//                    CachedAt = DateTime.UtcNow
//                };

//                await _cacheService.SetAsync(cacheKey, cacheData, _fileContentCacheTTL);
//                _logger.LogDebug("Cached thumbnail for file {FileId}, size: {Size} bytes", fileId, content.Length);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Failed to cache thumbnail for file {FileId}", fileId);
//            }
//        }

//        private void CleanupUnusedSemaphores(object? state)
//        {
//            lock (_semaphoreLock)
//            {
//                try
//                {
//                    var keysToRemove = new List<int>();

//                    foreach (var kvp in _fileSemaphores)
//                    {
//                        var semaphore = kvp.Value;
//                        // If semaphore is not being used, remove it
//                        if (semaphore.CurrentCount == 1) // No one is waiting
//                        {
//                            keysToRemove.Add(kvp.Key);
//                        }
//                    }

//                    foreach (var key in keysToRemove.Take(100)) // Limit cleanup
//                    {
//                        if (_fileSemaphores.TryRemove(key, out var semaphore))
//                        {
//                            semaphore.Dispose();
//                        }
//                    }

//                    if (keysToRemove.Count > 0)
//                    {
//                        _logger.LogDebug("Cleaned up {Count} unused file semaphores",
//                            Math.Min(keysToRemove.Count, 100));
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogWarning(ex, "Error during semaphore cleanup");
//                }
//            }
//        }

//        #endregion

//        #region Supporting Classes

//        private class FileContentCache
//        {
//            public byte[] Content { get; set; } = Array.Empty<byte>();
//            public string ContentType { get; set; } = string.Empty;
//            public string FileName { get; set; } = string.Empty;
//            public DateTime CachedAt { get; set; }
//        }

//        private class FileIntegrityResult
//        {
//            public int FileId { get; set; }
//            public bool IsValid { get; set; }
//            public DateTime VerifiedAt { get; set; }
//            public DateTime LastModified { get; set; }
//        }

//        #endregion

//        #region IDisposable

//        public void Dispose()
//        {
//            Dispose(true);
//            GC.SuppressFinalize(this);
//        }

//        protected virtual void Dispose(bool disposing)
//        {
//            if (!_disposed && disposing)
//            {
//                _semaphoreCleanupTimer?.Dispose();
//                _streamSemaphore?.Dispose();

//                // Dispose all file semaphores
//                foreach (var semaphore in _fileSemaphores.Values)
//                {
//                    semaphore.Dispose();
//                }
//                _fileSemaphores.Clear();

//                // Dispose base service if it implements IDisposable
//                if (_baseFileService is IDisposable disposableService)
//                {
//                    disposableService.Dispose();
//                }

//                _disposed = true;
//            }
//        }

//        #endregion
//    }
//}