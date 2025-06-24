using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Backend.CMS.Infrastructure.Services
{
    public class CachedFileService : IFileService, IDisposable
    {
        private readonly IFileService _baseFileService;
        private readonly IFileCachingService _cachingService;
        private readonly ILogger<CachedFileService> _logger;
        private readonly SemaphoreSlim _streamSemaphore;
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _fileSemaphores;
        private readonly Timer _semaphoreCleanupTimer;
        private readonly Lock _semaphoreLock = new();
        private bool _disposed = false;

        // Cache TTL configurations for metadata caching using IFileCachingService
        private static readonly TimeSpan FileMetadataCacheTTL = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan FileListCacheTTL = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan SearchResultsCacheTTL = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan StatisticsCacheTTL = TimeSpan.FromHours(1);
        private static readonly TimeSpan ExistenceCheckCacheTTL = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan PreviewCacheTTL = TimeSpan.FromMinutes(15);

        // Internal classes for caching non-binary data
        private class FileIntegrityResult
        {
            public int FileId { get; set; }
            public bool IsValid { get; set; }
            public DateTime VerifiedAt { get; set; }
        }

        private class BooleanCacheWrapper
        {
            public bool Value { get; set; }
        }

        private class LongCacheWrapper
        {
            public long Value { get; set; }
        }

        public CachedFileService(
            IFileService baseFileService,
            IFileCachingService cachingService,
            ILogger<CachedFileService> logger)
        {
            _baseFileService = baseFileService ?? throw new ArgumentNullException(nameof(baseFileService));
            _cachingService = cachingService ?? throw new ArgumentNullException(nameof(cachingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamSemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);
            _fileSemaphores = new ConcurrentDictionary<int, SemaphoreSlim>();

            // Cleanup unused semaphores every 5 minutes
            _semaphoreCleanupTimer = new Timer(CleanupUnusedSemaphores, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        #region Upload Operations (No caching needed, but invalidate cache)

        public async Task<FileDto> UploadFileAsync(FileUploadDto uploadDto)
        {
            var result = await _baseFileService.UploadFileAsync(uploadDto);
            await InvalidateRelevantCacheAsync(result.Id);
            return result;
        }

        public async Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto)
        {
            var results = await _baseFileService.UploadMultipleFilesAsync(uploadDto);

            // Invalidate metadata cache for uploaded files
            var tasks = results.Select(file => InvalidateFileMetadataCacheAsync(file.Id));
            await Task.WhenAll(tasks);

            return results;
        }

        public async Task<FileDto> UploadFileFromUrlAsync(string url, int? folderId = null, string? description = null)
        {
            var result = await _baseFileService.UploadFileFromUrlAsync(url, folderId, description);
            await InvalidateRelevantCacheAsync(result.Id);
            return result;
        }

        #endregion

        #region File Retrieval Operations (With caching)

        public async Task<FileDto> GetFileByIdAsync(int fileId)
        {

            if (fileId <= 0)
                throw new ArgumentException("File ID must be greater than 0", nameof(fileId));

            var cacheKey = $"file_metadata_{fileId}";

            var cachedFile = await _cachingService.GetFileMetadataAsync<FileDto>(cacheKey);

            if (cachedFile is not null)
            {

                return cachedFile;

            }

            var file = await CacheAndReturn(cacheKey, async () =>
            {

                _logger.LogDebug("Cache miss for file metadata: {FileId}", fileId);

                return await _baseFileService.GetFileByIdAsync(fileId);

            }, FileMetadataCacheTTL);

            return file ?? throw new InvalidOperationException($"File with ID {fileId} could not be retrieved.");
        }

        public async Task<List<FileDto>> GetFilesAsync(int page = 1, int pageSize = 20)
        {
            var cacheKey = $"file_list_{page}_{pageSize}";

            var cachedFiles = await _cachingService.GetFileMetadataAsync<List<FileDto>>(cacheKey);
            if (cachedFiles is not null)
            {
                return cachedFiles;
            }

            var files = await CacheAndReturn(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for file list: page {Page}, size {PageSize}", page, pageSize);
                var retrievedFiles = await _baseFileService.GetFilesAsync(page, pageSize);

                // Also cache individual file metadata
                await CacheFileMetadataAsync(retrievedFiles);

                return retrievedFiles;
            }, FileListCacheTTL);

            return files ?? [];
        }

        public async Task<List<FileDto>> GetFilesByFolderAsync(int? folderId, int page = 1, int pageSize = 20)
        {
            var cacheKey = $"file_folder_list_{folderId ?? 0}_{page}_{pageSize}";

            var cachedFiles = await _cachingService.GetFileMetadataAsync<List<FileDto>>(cacheKey);
            if (cachedFiles is not null)
            {
                return cachedFiles;
            }

            _logger.LogDebug("Cache miss for folder file list: folder {FolderId}, page {Page}, size {PageSize}",
                folderId, page, pageSize);

            var files = await _baseFileService.GetFilesByFolderAsync(folderId, page, pageSize) ?? new List<FileDto>();

            // Also cache individual file metadata
            await CacheFileMetadataAsync(files);

            await CacheAndReturn(cacheKey, () => Task.FromResult(files), FileListCacheTTL);

            return files;
        }


        public async Task<List<FileDto>> SearchFilesAsync(FileSearchDto searchDto)
        {
            // Create a cache key based on search parameters
            var searchKey = CreateSearchCacheKey(searchDto);
            var cacheKey = $"file_search_{searchKey}";

            return await _cachingService.GetFileMetadataAsync<List<FileDto>>(cacheKey) ??
                   await CacheAndReturn(cacheKey, async () =>
                   {
                       _logger.LogDebug("Cache miss for file search: {SearchKey}", searchKey);
                       var files = await _baseFileService.SearchFilesAsync(searchDto);

                       // Cache individual file metadata for short term
                       await CacheFileMetadataAsync(files, TimeSpan.FromMinutes(10));

                       return files;
                   }, SearchResultsCacheTTL);
        }

        public async Task<List<FileDto>> GetRecentFilesAsync(int count = 10)
        {
            var cacheKey = $"file_recent_{count}";

            return await _cachingService.GetFileMetadataAsync<List<FileDto>>(cacheKey) ??
                   await CacheAndReturn(cacheKey, async () =>
                   {
                       _logger.LogDebug("Cache miss for recent files: count {Count}", count);
                       var files = await _baseFileService.GetRecentFilesAsync(count);

                       // Also cache individual file metadata
                       await CacheFileMetadataAsync(files);

                       return files;
                   }, FileListCacheTTL);
        }

        public async Task<FilePreviewDto> GetFilePreviewAsync(int fileId)
        {
            if (fileId <= 0)
                throw new ArgumentException("File ID must be greater than 0", nameof(fileId));

            var cacheKey = $"file_preview_{fileId}";

            return await _cachingService.GetFileMetadataAsync<FilePreviewDto>(cacheKey) ??
                   await CacheAndReturn(cacheKey, async () =>
                   {
                       _logger.LogDebug("Cache miss for file preview: {FileId}", fileId);
                       return await _baseFileService.GetFilePreviewAsync(fileId);
                   }, PreviewCacheTTL);
        }

        public async Task<Dictionary<string, object>> GetFileStatisticsAsync()
        {
            var cacheKey = "file_statistics";

            return await _cachingService.GetFileMetadataAsync<Dictionary<string, object>>(cacheKey) ??
                   await CacheAndReturn(cacheKey, async () =>
                   {
                       _logger.LogDebug("Cache miss for file statistics");
                       return await _baseFileService.GetFileStatisticsAsync();
                   }, StatisticsCacheTTL);
        }

        public async Task<bool> FileExistsAsync(int fileId)
        {
            if (fileId <= 0)
                return false;

            var cacheKey = $"file_exists_{fileId}";

            var cachedResult = await _cachingService.GetFileMetadataAsync<BooleanCacheWrapper>(cacheKey);
            if (cachedResult != null)
            {
                _logger.LogDebug("Cache hit for file existence: {FileId} = {Exists}", fileId, cachedResult.Value);
                return cachedResult.Value;
            }

            var exists = await _baseFileService.FileExistsAsync(fileId);
            await _cachingService.SetFileMetadataAsync(cacheKey, new BooleanCacheWrapper { Value = exists }, ExistenceCheckCacheTTL);

            _logger.LogDebug("Cache miss for file existence: {FileId} = {Exists}", fileId, exists);
            return exists;
        }

        public async Task<long> GetTotalFileSizeAsync(int? folderId = null)
        {
            var cacheKey = $"file_total_size_{folderId ?? 0}";

            var cachedResult = await _cachingService.GetFileMetadataAsync<LongCacheWrapper>(cacheKey);
            if (cachedResult != null)
            {
                _logger.LogDebug("Cache hit for total file size: folder {FolderId} = {Size}", folderId, cachedResult.Value);
                return cachedResult.Value;
            }

            var totalSize = await _baseFileService.GetTotalFileSizeAsync(folderId);
            await _cachingService.SetFileMetadataAsync(cacheKey, new LongCacheWrapper { Value = totalSize }, FileListCacheTTL);

            _logger.LogDebug("Cache miss for total file size: folder {FolderId} = {Size}", folderId, totalSize);
            return totalSize;
        }

        #endregion

        #region Stream Operations (With content caching)

        public async Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int fileId)
        {
            if (fileId <= 0)
                throw new ArgumentException("File ID must be greater than 0", nameof(fileId));

            var fileSemaphore = GetOrCreateFileSemaphore(fileId);
            await _streamSemaphore.WaitAsync();

            try
            {
                await fileSemaphore.WaitAsync();
                try
                {
                    // Try to get from cache first
                    var cachedContent = await _cachingService.GetFileContentAsync(fileId);
                    if (cachedContent != null)
                    {
                        _logger.LogDebug("File {FileId} served from cache, size: {Size} bytes", fileId, cachedContent.Length);

                        // Get file metadata to return correct content type and filename
                        var fileInfo = await GetFileByIdAsync(fileId);
                        var cachedStream = new MemoryStream(cachedContent, false); // Read-only stream
                        return (cachedStream, fileInfo.ContentType, fileInfo.OriginalFileName);
                    }

                    // Get from database
                    var (stream, contentType, fileName) = await _baseFileService.GetFileStreamAsync(fileId);

                    // Read content from stream for caching (only if stream supports seeking)
                    if (stream.CanSeek && stream.CanRead)
                    {
                        var originalPosition = stream.Position;
                        stream.Position = 0;

                        using var memoryStream = new MemoryStream();
                        await stream.CopyToAsync(memoryStream);
                        var content = memoryStream.ToArray();

                        // Dispose the original stream since we have the content
                        await stream.DisposeAsync();

                        // Cache content asynchronously (don't wait)
                        _ = CacheContentAsync(fileId, content, "file content");

                        // Return new stream with cached content
                        return (new MemoryStream(content, false), contentType, fileName);
                    }
                    else
                    {
                        // Stream doesn't support seeking, return as-is without caching
                        _logger.LogDebug("File {FileId} stream doesn't support seeking, not caching", fileId);
                        return (stream, contentType, fileName);
                    }
                }
                finally
                {
                    fileSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file stream for file {FileId}", fileId);
                throw;
            }
            finally
            {
                _streamSemaphore.Release();
            }
        }

        public async Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int fileId)
        {
            if (fileId <= 0)
                throw new ArgumentException("File ID must be greater than 0", nameof(fileId));

            var fileSemaphore = GetOrCreateFileSemaphore(fileId);
            await _streamSemaphore.WaitAsync();

            try
            {
                await fileSemaphore.WaitAsync();
                try
                {
                    // Try to get from cache first
                    var cachedThumbnail = await _cachingService.GetThumbnailContentAsync(fileId);
                    if (cachedThumbnail != null)
                    {
                        _logger.LogDebug("Thumbnail for file {FileId} served from cache, size: {Size} bytes",
                            fileId, cachedThumbnail.Length);

                        var cachedStream = new MemoryStream(cachedThumbnail, false);
                        return (cachedStream, "image/jpeg", $"thumb_file_{fileId}.jpg");
                    }

                    // Get from database
                    var (stream, contentType, fileName) = await _baseFileService.GetThumbnailStreamAsync(fileId);

                    // Read content from stream for caching
                    if (stream.CanSeek && stream.CanRead)
                    {
                        var originalPosition = stream.Position;
                        stream.Position = 0;

                        using var memoryStream = new MemoryStream();
                        await stream.CopyToAsync(memoryStream);
                        var content = memoryStream.ToArray();

                        // Dispose the original stream
                        await stream.DisposeAsync();

                        // Cache thumbnail asynchronously
                        _ = CacheThumbnailAsync(fileId, content);

                        // Return new stream with cached content
                        return (new MemoryStream(content, false), contentType, fileName);
                    }
                    else
                    {
                        _logger.LogDebug("Thumbnail stream for file {FileId} doesn't support seeking, not caching", fileId);
                        return (stream, contentType, fileName);
                    }
                }
                finally
                {
                    fileSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting thumbnail stream for file {FileId}", fileId);
                throw;
            }
            finally
            {
                _streamSemaphore.Release();
            }
        }

        #endregion

        #region Update/Delete Operations (With cache invalidation)

        public async Task<FileDto> UpdateFileAsync(int fileId, UpdateFileDto updateDto)
        {
            var result = await _baseFileService.UpdateFileAsync(fileId, updateDto);
            await InvalidateRelevantCacheAsync(fileId);
            return result;
        }

        public async Task<bool> DeleteFileAsync(int fileId)
        {
            var result = await _baseFileService.DeleteFileAsync(fileId);
            if (result)
            {
                await InvalidateRelevantCacheAsync(fileId);
                await InvalidateVerificationCacheAsync(fileId);
            }
            return result;
        }

        public async Task<bool> DeleteMultipleFilesAsync(List<int> fileIds)
        {
            if (fileIds?.Any() != true)
                return false;

            var result = await _baseFileService.DeleteMultipleFilesAsync(fileIds);
            if (result)
            {
                await InvalidateRelevantCacheAsync(fileIds);
                await InvalidateVerificationCacheAsync(fileIds);
            }
            return result;
        }

        public async Task<FileDto> MoveFileAsync(MoveFileDto moveDto)
        {
            var result = await _baseFileService.MoveFileAsync(moveDto);
            await InvalidateRelevantCacheAsync(moveDto.FileId);
            await InvalidateVerificationCacheAsync(moveDto.FileId);
            return result;
        }

        public async Task<FileDto> CopyFileAsync(CopyFileDto copyDto)
        {
            var result = await _baseFileService.CopyFileAsync(copyDto);
            await InvalidateRelevantCacheAsync(result.Id);
            return result;
        }

        public async Task<bool> RenameFileAsync(int fileId, string newName)
        {
            var result = await _baseFileService.RenameFileAsync(fileId, newName);
            if (result)
            {
                await InvalidateFileMetadataCacheAsync(fileId);
                await InvalidateVerificationCacheAsync(fileId);
            }
            return result;
        }

        public async Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto)
        {
            if (fileIds?.Any() != true)
                return false;

            var result = await _baseFileService.BulkUpdateFilesAsync(fileIds, updateDto);
            if (result)
            {
                await InvalidateRelevantCacheAsync(fileIds);
                await InvalidateVerificationCacheAsync(fileIds);
            }
            return result;
        }

        public async Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId)
        {
            if (fileIds?.Any() != true)
                return false;

            var result = await _baseFileService.BulkMoveFilesAsync(fileIds, destinationFolderId);
            if (result)
            {
                await InvalidateRelevantCacheAsync(fileIds);
                await InvalidateVerificationCacheAsync(fileIds);
            }
            return result;
        }

        public async Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId)
        {
            var results = await _baseFileService.BulkCopyFilesAsync(fileIds, destinationFolderId);

            // Invalidate cache for copied files
            var tasks = results.Select(file => InvalidateFileMetadataCacheAsync(file.Id));
            await Task.WhenAll(tasks);

            return results;
        }

        public async Task<bool> GenerateThumbnailAsync(int fileId)
        {
            var result = await _baseFileService.GenerateThumbnailAsync(fileId);
            if (result)
            {
                await InvalidateRelevantCacheAsync(fileId);
                await InvalidateVerificationCacheAsync(fileId);
            }
            return result;
        }

        public async Task<bool> ProcessFileAsync(int fileId)
        {
            var result = await _baseFileService.ProcessFileAsync(fileId);
            if (result)
            {
                await InvalidateRelevantCacheAsync(fileId);
                await InvalidateVerificationCacheAsync(fileId);
            }
            return result;
        }

        #endregion

        #region Verification (With caching)

        public async Task<bool> VerifyFileIntegrityAsync(int fileId)
        {
            if (fileId <= 0)
                throw new ArgumentException("File ID must be greater than 0", nameof(fileId));

            var fileSemaphore = GetOrCreateFileSemaphore(fileId);
            await fileSemaphore.WaitAsync();

            try
            {
                // Create cache key for verification result
                var cacheKey = $"file_integrity_{fileId}";

                // Try to get cached verification result
                var cachedResult = await _cachingService.GetFileMetadataAsync<FileIntegrityResult>(cacheKey);

                if (cachedResult != null)
                {
                    // Get file info to check if it was modified after verification
                    var fileInfo = await GetFileByIdAsync(fileId);

                    // If file hasn't been modified since verification, return cached result
                    if (fileInfo.UpdatedAt <= cachedResult.VerifiedAt)
                    {
                        _logger.LogDebug("File integrity verification served from cache for file {FileId}: {IsValid}",
                            fileId, cachedResult.IsValid);
                        return cachedResult.IsValid;
                    }

                    _logger.LogDebug("File {FileId} was modified after verification, re-verifying", fileId);
                }

                // Perform actual verification
                _logger.LogDebug("Performing file integrity verification for file {FileId}", fileId);
                var isValid = await _baseFileService.VerifyFileIntegrityAsync(fileId);

                // Cache the result with timestamp
                var verificationResult = new FileIntegrityResult
                {
                    FileId = fileId,
                    IsValid = isValid,
                    VerifiedAt = DateTime.UtcNow
                };

                // Cache for 1 hour - verification is expensive but files don't change often
                await _cachingService.SetFileMetadataAsync(cacheKey, verificationResult, TimeSpan.FromHours(1));

                _logger.LogDebug("File integrity verification completed for file {FileId}: {IsValid} (cached)",
                    fileId, isValid);

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cached file integrity verification for file {FileId}", fileId);

                // Fall back to base service on error
                return await _baseFileService.VerifyFileIntegrityAsync(fileId);
            }
            finally
            {
                fileSemaphore.Release();
            }
        }

        #endregion

        #region Delegated Methods (No caching needed)

        public Task<string> GenerateFileUrlAsync(int fileId, bool thumbnail = false) =>
            _baseFileService.GenerateFileUrlAsync(fileId, thumbnail);

        public Task RecordFileAccessAsync(int fileId, FileAccessType accessType) =>
            _baseFileService.RecordFileAccessAsync(fileId, accessType);

        public Task<bool> ValidateFileAsync(IFormFile file) =>
            _baseFileService.ValidateFileAsync(file);

        public Task<string> GetFileHashAsync(Stream stream) =>
            _baseFileService.GetFileHashAsync(stream);

        #endregion

        #region Cache Helper Methods

        private async Task<T?> CacheAndReturn<T>(string cacheKey, Func<Task<T>> getItem, TimeSpan? expiration = null) where T : class
        {
            var item = await getItem();
            if (item != null)
            {
                await _cachingService.SetFileMetadataAsync(cacheKey, item, expiration);
            }
            return item;
        }

        private async Task CacheFileMetadataAsync(IEnumerable<FileDto> files, TimeSpan? customTtl = null)
        {
            var ttl = customTtl ?? FileMetadataCacheTTL;
            var tasks = files.Select(async file =>
            {
                try
                {
                    var cacheKey = $"file_metadata_{file.Id}";
                    await _cachingService.SetFileMetadataAsync(cacheKey, file, ttl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache metadata for file {FileId}", file.Id);
                }
            });

            await Task.WhenAll(tasks);
            _logger.LogDebug("Cached metadata for {Count} files", files.Count());
        }

        private string CreateSearchCacheKey(FileSearchDto searchDto)
        {
            var keyComponents = new List<string>
            {
                searchDto.SearchTerm?.ToLowerInvariant() ?? "null",
                searchDto.FileType?.ToString() ?? "null",
                searchDto.FolderId?.ToString() ?? "null",
                searchDto.IsPublic?.ToString() ?? "null",
                searchDto.CreatedFrom?.ToString("yyyyMMdd") ?? "null",
                searchDto.CreatedTo?.ToString("yyyyMMdd") ?? "null",
                searchDto.MinSize?.ToString() ?? "null",
                searchDto.MaxSize?.ToString() ?? "null",
                searchDto.SortBy ?? "null",
                searchDto.SortDirection ?? "null",
                searchDto.Page.ToString(),
                searchDto.PageSize.ToString()
            };

            return string.Join("_", keyComponents);
        }

        private async Task InvalidateRelevantCacheAsync(int fileId)
        {
            await Task.WhenAll(
                InvalidateFileMetadataCacheAsync(fileId),
                InvalidateFileExistenceCacheAsync(fileId),
                _cachingService.InvalidateFileAsync(fileId)
            );
        }

        private async Task InvalidateRelevantCacheAsync(List<int> fileIds)
        {
            var tasks = new List<Task>();

            foreach (var fileId in fileIds)
            {
                tasks.Add(InvalidateFileMetadataCacheAsync(fileId));
                tasks.Add(InvalidateFileExistenceCacheAsync(fileId));
                tasks.Add(_cachingService.InvalidateFileAsync(fileId));
            }

            await Task.WhenAll(tasks);
        }

        private async Task InvalidateFileMetadataCacheAsync(int fileId)
        {
            try
            {
                var cacheKeys = new[]
                {
                    $"file_metadata_{fileId}",
                    $"file_preview_{fileId}"
                };

                var tasks = cacheKeys.Select(key => _cachingService.InvalidateFileMetadataAsync(key));
                await Task.WhenAll(tasks);

                _logger.LogDebug("Invalidated metadata cache for file {FileId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate metadata cache for file {FileId}", fileId);
            }
        }

        private async Task InvalidateFileExistenceCacheAsync(int fileId)
        {
            try
            {
                await _cachingService.InvalidateFileMetadataAsync($"file_exists_{fileId}");
                _logger.LogDebug("Invalidated existence cache for file {FileId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate existence cache for file {FileId}", fileId);
            }
        }

        private async Task InvalidateVerificationCacheAsync(int fileId)
        {
            try
            {
                await _cachingService.InvalidateFileMetadataAsync($"file_integrity_{fileId}");
                _logger.LogDebug("Verification cache invalidated for file {FileId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate verification cache for file {FileId}", fileId);
            }
        }

        private async Task InvalidateVerificationCacheAsync(List<int> fileIds)
        {
            try
            {
                var tasks = fileIds.Select(InvalidateVerificationCacheAsync);
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate verification cache for some files");
            }
        }

        private SemaphoreSlim GetOrCreateFileSemaphore(int fileId)
        {
            return _fileSemaphores.GetOrAdd(fileId, _ => new SemaphoreSlim(1, 1));
        }

        private async Task CacheContentAsync(int fileId, byte[] content, string contentType)
        {
            try
            {
                await _cachingService.SetFileContentAsync(fileId, content);
                _logger.LogDebug("Cached {ContentType} for file {FileId}, size: {Size} bytes",
                    contentType, fileId, content.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache {ContentType} for file {FileId}", contentType, fileId);
            }
        }

        private async Task CacheThumbnailAsync(int fileId, byte[] content)
        {
            try
            {
                await _cachingService.SetThumbnailContentAsync(fileId, content);
                _logger.LogDebug("Cached thumbnail for file {FileId}, size: {Size} bytes", fileId, content.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache thumbnail for file {FileId}", fileId);
            }
        }

        private void CleanupUnusedSemaphores(object? state)
        {
            lock (_semaphoreLock)
            {
                try
                {
                    var keysToRemove = new List<int>();

                    foreach (var kvp in _fileSemaphores)
                    {
                        var semaphore = kvp.Value;
                        // If semaphore is not being used, remove it
                        if (semaphore.CurrentCount == 1) // No one is waiting
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }

                    foreach (var key in keysToRemove.Take(100)) // Limit cleanup to prevent blocking
                    {
                        if (_fileSemaphores.TryRemove(key, out var semaphore))
                        {
                            semaphore.Dispose();
                        }
                    }

                    if (keysToRemove.Count > 0)
                    {
                        _logger.LogDebug("Cleaned up {Count} unused file semaphores",
                            Math.Min(keysToRemove.Count, 100));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during semaphore cleanup");
                }
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
                _semaphoreCleanupTimer?.Dispose();
                _streamSemaphore?.Dispose();

                // Dispose all file semaphores
                foreach (var semaphore in _fileSemaphores.Values)
                {
                    semaphore.Dispose();
                }
                _fileSemaphores.Clear();

                // Dispose base service if it implements IDisposable
                if (_baseFileService is IDisposable disposableService)
                {
                    disposableService.Dispose();
                }

                // Dispose caching service if it implements IDisposable
                if (_cachingService is IDisposable disposableCachingService)
                {
                    disposableCachingService.Dispose();
                }

                _disposed = true;
            }
        }

        #endregion
    }
}