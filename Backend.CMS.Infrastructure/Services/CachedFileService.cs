using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class CachedFileService : IFileService, IDisposable
    {
        private readonly IFileService _baseFileService;
        private readonly IFileCachingService _cachingService;
        private readonly ILogger<CachedFileService> _logger;
        private readonly SemaphoreSlim _streamSemaphore;
        private bool _disposed = false;

        public CachedFileService(
            IFileService baseFileService,
            IFileCachingService cachingService,
            ILogger<CachedFileService> logger)
        {
            _baseFileService = baseFileService ?? throw new ArgumentNullException(nameof(baseFileService));
            _cachingService = cachingService ?? throw new ArgumentNullException(nameof(cachingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        }

        public async Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int fileId)
        {
            if (fileId <= 0)
                throw new ArgumentException("File ID must be greater than 0", nameof(fileId));

            await _streamSemaphore.WaitAsync();
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
                byte[] content;
                if (stream.CanSeek && stream.CanRead)
                {
                    stream.Position = 0;
                    using (var memoryStream = new MemoryStream())
                    {
                        await stream.CopyToAsync(memoryStream);
                        content = memoryStream.ToArray();
                    }

                    // Cache the content for future requests (fire and forget)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _cachingService.SetFileContentAsync(fileId, content);
                            _logger.LogDebug("File {FileId} cached, size: {Size} bytes", fileId, content.Length);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to cache file {FileId}", fileId);
                        }
                    });

                    // Dispose the original stream and return a new one with cached content
                    await stream.DisposeAsync();
                    return (new MemoryStream(content, false), contentType, fileName);
                }
                else
                {
                    // Stream doesn't support seeking, return as-is without caching
                    _logger.LogDebug("File {FileId} stream doesn't support seeking, not caching", fileId);
                    return (stream, contentType, fileName);
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

            await _streamSemaphore.WaitAsync();
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
                byte[] content;
                if (stream.CanSeek && stream.CanRead)
                {
                    stream.Position = 0;
                    using (var memoryStream = new MemoryStream())
                    {
                        await stream.CopyToAsync(memoryStream);
                        content = memoryStream.ToArray();
                    }

                    // Cache the thumbnail for future requests (fire and forget)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _cachingService.SetThumbnailContentAsync(fileId, content);
                            _logger.LogDebug("Thumbnail for file {FileId} cached, size: {Size} bytes", fileId, content.Length);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to cache thumbnail for file {FileId}", fileId);
                        }
                    });

                    // Dispose the original stream and return a new one with cached content
                    await stream.DisposeAsync();
                    return (new MemoryStream(content, false), contentType, fileName);
                }
                else
                {
                    _logger.LogDebug("Thumbnail stream for file {FileId} doesn't support seeking, not caching", fileId);
                    return (stream, contentType, fileName);
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
                // Invalidate cache when file is deleted (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _cachingService.InvalidateFileAsync(fileId);
                        _logger.LogDebug("Cache invalidated for deleted file {FileId}", fileId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to invalidate cache for deleted file {FileId}", fileId);
                    }
                });
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
                // Invalidate cache for all deleted files (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var tasks = fileIds.Select(fileId => _cachingService.InvalidateFileAsync(fileId));
                        await Task.WhenAll(tasks);
                        _logger.LogDebug("Cache invalidated for {Count} deleted files", fileIds.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to invalidate cache for some deleted files");
                    }
                });
            }
            return result;
        }

        public async Task<FileDto> UpdateFileAsync(int fileId, UpdateFileDto updateDto)
        {
            var result = await _baseFileService.UpdateFileAsync(fileId, updateDto);

            // Invalidate cache when file is updated (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _cachingService.InvalidateFileAsync(fileId);
                    _logger.LogDebug("Cache invalidated for updated file {FileId}", fileId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to invalidate cache for updated file {FileId}", fileId);
                }
            });

            return result;
        }

        public async Task<FileDto> MoveFileAsync(MoveFileDto moveDto)
        {
            var result = await _baseFileService.MoveFileAsync(moveDto);

            // Invalidate cache when file is moved (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _cachingService.InvalidateFileAsync(result.Id);
                    _logger.LogDebug("Cache invalidated for moved file {FileId}", result.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to invalidate cache for moved file {FileId}", result.Id);
                }
            });

            return result;
        }

        public async Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto)
        {
            if (fileIds?.Any() != true)
                return false;

            var result = await _baseFileService.BulkUpdateFilesAsync(fileIds, updateDto);
            if (result)
            {
                // Invalidate cache for all updated files (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var tasks = fileIds.Select(fileId => _cachingService.InvalidateFileAsync(fileId));
                        await Task.WhenAll(tasks);
                        _logger.LogDebug("Cache invalidated for {Count} bulk updated files", fileIds.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to invalidate cache for some bulk updated files");
                    }
                });
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
                // Invalidate cache for all moved files (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var tasks = fileIds.Select(fileId => _cachingService.InvalidateFileAsync(fileId));
                        await Task.WhenAll(tasks);
                        _logger.LogDebug("Cache invalidated for {Count} bulk moved files", fileIds.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to invalidate cache for some bulk moved files");
                    }
                });
            }
            return result;
        }

        public async Task<bool> RenameFileAsync(int fileId, string newName)
        {
            var result = await _baseFileService.RenameFileAsync(fileId, newName);
            if (result)
            {
                // Invalidate cache when file is renamed (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _cachingService.InvalidateFileAsync(fileId);
                        _logger.LogDebug("Cache invalidated for renamed file {FileId}", fileId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to invalidate cache for renamed file {FileId}", fileId);
                    }
                });
            }
            return result;
        }

        public async Task<bool> GenerateThumbnailAsync(int fileId)
        {
            var result = await _baseFileService.GenerateThumbnailAsync(fileId);
            if (result)
            {
                // Invalidate thumbnail cache when new thumbnail is generated (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _cachingService.InvalidateFileAsync(fileId);
                        _logger.LogDebug("Cache invalidated for file {FileId} after thumbnail generation", fileId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to invalidate cache after thumbnail generation for file {FileId}", fileId);
                    }
                });
            }
            return result;
        }

        public async Task<bool> ProcessFileAsync(int fileId)
        {
            var result = await _baseFileService.ProcessFileAsync(fileId);
            if (result)
            {
                // Invalidate cache when file is processed (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _cachingService.InvalidateFileAsync(fileId);
                        _logger.LogDebug("Cache invalidated for processed file {FileId}", fileId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to invalidate cache for processed file {FileId}", fileId);
                    }
                });
            }
            return result;
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
                _streamSemaphore?.Dispose();

                // If the base service implements IDisposable, dispose it
                if (_baseFileService is IDisposable disposableService)
                {
                    disposableService.Dispose();
                }

                // If the caching service implements IDisposable, dispose it
                if (_cachingService is IDisposable disposableCachingService)
                {
                    disposableCachingService.Dispose();
                }

                _disposed = true;
            }
        }
    }
}