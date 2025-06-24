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
        private readonly object _semaphoreLock = new();
        private bool _disposed = false;

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

        public async Task<bool> DeleteFileAsync(int fileId)
        {
            var result = await _baseFileService.DeleteFileAsync(fileId);
            if (result)
            {
                await InvalidateCacheAsync(fileId);
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
                await InvalidateCacheAsync(fileIds);
            }
            return result;
        }

        public async Task<FileDto> UpdateFileAsync(int fileId, UpdateFileDto updateDto)
        {
            var result = await _baseFileService.UpdateFileAsync(fileId, updateDto);
            await InvalidateCacheAsync(fileId);
            return result;
        }

        public async Task<FileDto> MoveFileAsync(MoveFileDto moveDto)
        {
            var result = await _baseFileService.MoveFileAsync(moveDto);
            await InvalidateCacheAsync(result.Id);
            return result;
        }

        public async Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto)
        {
            if (fileIds?.Any() != true)
                return false;

            var result = await _baseFileService.BulkUpdateFilesAsync(fileIds, updateDto);
            if (result)
            {
                await InvalidateCacheAsync(fileIds);
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
                await InvalidateCacheAsync(fileIds);
            }
            return result;
        }

        public async Task<bool> RenameFileAsync(int fileId, string newName)
        {
            var result = await _baseFileService.RenameFileAsync(fileId, newName);
            if (result)
            {
                await InvalidateCacheAsync(fileId);
            }
            return result;
        }

        public async Task<bool> GenerateThumbnailAsync(int fileId)
        {
            var result = await _baseFileService.GenerateThumbnailAsync(fileId);
            if (result)
            {
                await InvalidateCacheAsync(fileId);
            }
            return result;
        }

        public async Task<bool> ProcessFileAsync(int fileId)
        {
            var result = await _baseFileService.ProcessFileAsync(fileId);
            if (result)
            {
                await InvalidateCacheAsync(fileId);
            }
            return result;
        }

        // Helper methods
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

        private async Task InvalidateCacheAsync(int fileId)
        {
            try
            {
                await _cachingService.InvalidateFileAsync(fileId);
                _logger.LogDebug("Cache invalidated for file {FileId}", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate cache for file {FileId}", fileId);
            }
        }

        private async Task InvalidateCacheAsync(List<int> fileIds)
        {
            try
            {
                var tasks = fileIds.Select(fileId => _cachingService.InvalidateFileAsync(fileId));
                await Task.WhenAll(tasks);
                _logger.LogDebug("Cache invalidated for {Count} files", fileIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate cache for some files");
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

        // Delegate all other methods to the base service without caching
        public Task<FileDto> UploadFileAsync(FileUploadDto uploadDto) => _baseFileService.UploadFileAsync(uploadDto);
        public Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto) => _baseFileService.UploadMultipleFilesAsync(uploadDto);
        public Task<FileDto> UploadFileFromUrlAsync(string url, int? folderId = null, string? description = null) => _baseFileService.UploadFileFromUrlAsync(url, folderId, description);
        public Task<FileDto> GetFileByIdAsync(int fileId) => _baseFileService.GetFileByIdAsync(fileId);
        public Task<List<FileDto>> GetFilesAsync(int page = 1, int pageSize = 20) => _baseFileService.GetFilesAsync(page, pageSize);
        public Task<List<FileDto>> GetFilesByFolderAsync(int? folderId, int page = 1, int pageSize = 20) => _baseFileService.GetFilesByFolderAsync(folderId, page, pageSize);
        public Task<List<FileDto>> SearchFilesAsync(FileSearchDto searchDto) => _baseFileService.SearchFilesAsync(searchDto);
        public Task<FileDto> CopyFileAsync(CopyFileDto copyDto) => _baseFileService.CopyFileAsync(copyDto);
        public Task<FilePreviewDto> GetFilePreviewAsync(int fileId) => _baseFileService.GetFilePreviewAsync(fileId);
        public Task<string> GenerateFileUrlAsync(int fileId, bool thumbnail = false) => _baseFileService.GenerateFileUrlAsync(fileId, thumbnail);
        public Task RecordFileAccessAsync(int fileId, FileAccessType accessType) => _baseFileService.RecordFileAccessAsync(fileId, accessType);
        public Task<List<FileDto>> GetRecentFilesAsync(int count = 10) => _baseFileService.GetRecentFilesAsync(count);
        public Task<Dictionary<string, object>> GetFileStatisticsAsync() => _baseFileService.GetFileStatisticsAsync();
        public Task<bool> ValidateFileAsync(IFormFile file) => _baseFileService.ValidateFileAsync(file);
        public Task<string> GetFileHashAsync(Stream stream) => _baseFileService.GetFileHashAsync(stream);
        public Task<bool> FileExistsAsync(int fileId) => _baseFileService.FileExistsAsync(fileId);
        public Task<long> GetTotalFileSizeAsync(int? folderId = null) => _baseFileService.GetTotalFileSizeAsync(folderId);
        public Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId) => _baseFileService.BulkCopyFilesAsync(fileIds, destinationFolderId);

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
    }
}