using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class CachedFileService : IFileService
    {
        private readonly IFileService _baseFileService;
        private readonly IFileCachingService _cachingService;
        private readonly ILogger<CachedFileService> _logger;

        public CachedFileService(
            IFileService baseFileService,
            IFileCachingService cachingService,
            ILogger<CachedFileService> logger)
        {
            _baseFileService = baseFileService;
            _cachingService = cachingService;
            _logger = logger;
        }

        public async Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int fileId)
        {
            try
            {
                // Try to get from cache first
                var cachedContent = await _cachingService.GetFileContentAsync(fileId);
                if (cachedContent != null)
                {
                    _logger.LogDebug("File {FileId} served from cache", fileId);
                    var fileInfo = await _baseFileService.GetFileByIdAsync(fileId);

                    // Create stream and ensure position is at beginning
                    var cachedStream = new MemoryStream(cachedContent);
                    cachedStream.Position = 0;

                    return (cachedStream, fileInfo.ContentType, fileInfo.OriginalFileName);
                }

                // Get from database
                var (stream, contentType, fileName) = await _baseFileService.GetFileStreamAsync(fileId);

                // Read content from stream for caching
                byte[] content;
                using (stream) // Ensure stream is disposed after reading
                {
                    // Ensure we're reading from the beginning
                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                    }

                    using (var memoryStream = new MemoryStream())
                    {
                        await stream.CopyToAsync(memoryStream);
                        content = memoryStream.ToArray();
                    }
                }

                // Cache the content for future requests
                await _cachingService.SetFileContentAsync(fileId, content);

                _logger.LogDebug("File {FileId} loaded from database and cached", fileId);

                // Return a new stream with the content, positioned at start
                var resultStream = new MemoryStream(content);
                resultStream.Position = 0;

                return (resultStream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file stream for file {FileId}", fileId);
                throw;
            }
        }

        public async Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int fileId)
        {
            try
            {
                // Try to get from cache first
                var cachedThumbnail = await _cachingService.GetThumbnailContentAsync(fileId);
                if (cachedThumbnail != null)
                {
                    _logger.LogDebug("Thumbnail for file {FileId} served from cache", fileId);

                    // Create stream and ensure position is at beginning
                    var cachedStream = new MemoryStream(cachedThumbnail);
                    cachedStream.Position = 0;

                    return (cachedStream, "image/jpeg", $"thumb_file_{fileId}");
                }

                // Get from database
                var (stream, contentType, fileName) = await _baseFileService.GetThumbnailStreamAsync(fileId);

                // Read content from stream for caching
                byte[] content;
                using (stream) // Ensure stream is disposed after reading
                {
                    // Ensure reading from the beginning
                    if (stream.CanSeek)
                    {
                        stream.Position = 0;
                    }

                    using (var memoryStream = new MemoryStream())
                    {
                        await stream.CopyToAsync(memoryStream);
                        content = memoryStream.ToArray();
                    }
                }

                // Cache the thumbnail for future requests
                await _cachingService.SetThumbnailContentAsync(fileId, content);

                _logger.LogDebug("Thumbnail for file {FileId} loaded from database and cached", fileId);

                // Return a new stream with the content, positioned at start
                var resultStream = new MemoryStream(content);
                resultStream.Position = 0;

                return (resultStream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting thumbnail stream for file {FileId}", fileId);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(int fileId)
        {
            var result = await _baseFileService.DeleteFileAsync(fileId);
            if (result)
            {
                await _cachingService.InvalidateFileAsync(fileId);
            }
            return result;
        }

        public async Task<FileDto> UpdateFileAsync(int fileId, UpdateFileDto updateDto)
        {
            var result = await _baseFileService.UpdateFileAsync(fileId, updateDto);
            // Invalidate cache when file is updated
            await _cachingService.InvalidateFileAsync(fileId);
            return result;
        }


        // Delegate all other methods to the base service
        public Task<FileDto> UploadFileAsync(FileUploadDto uploadDto) => _baseFileService.UploadFileAsync(uploadDto);
        public Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto) => _baseFileService.UploadMultipleFilesAsync(uploadDto);
        public Task<FileDto> UploadFileFromUrlAsync(string url, int? folderId = null, string? description = null) => _baseFileService.UploadFileFromUrlAsync(url, folderId, description);
        public Task<FileDto> GetFileByIdAsync(int fileId) => _baseFileService.GetFileByIdAsync(fileId);
        public Task<List<FileDto>> GetFilesAsync(int page = 1, int pageSize = 20) => _baseFileService.GetFilesAsync(page, pageSize);
        public Task<List<FileDto>> GetFilesByFolderAsync(int? folderId, int page = 1, int pageSize = 20) => _baseFileService.GetFilesByFolderAsync(folderId, page, pageSize);
        public Task<List<FileDto>> SearchFilesAsync(FileSearchDto searchDto) => _baseFileService.SearchFilesAsync(searchDto);
        public Task<bool> DeleteMultipleFilesAsync(List<int> fileIds) => _baseFileService.DeleteMultipleFilesAsync(fileIds);
        public Task<FileDto> MoveFileAsync(MoveFileDto moveDto) => _baseFileService.MoveFileAsync(moveDto);
        public Task<FileDto> CopyFileAsync(CopyFileDto copyDto) => _baseFileService.CopyFileAsync(copyDto);
        public Task<bool> RenameFileAsync(int fileId, string newName) => _baseFileService.RenameFileAsync(fileId, newName);
        public Task<FilePreviewDto> GetFilePreviewAsync(int fileId) => _baseFileService.GetFilePreviewAsync(fileId);
        public Task<string> GenerateFileUrlAsync(int fileId, bool thumbnail = false) => _baseFileService.GenerateFileUrlAsync(fileId, thumbnail);
        public Task<bool> GenerateThumbnailAsync(int fileId) => _baseFileService.GenerateThumbnailAsync(fileId);
        public Task<bool> ProcessFileAsync(int fileId) => _baseFileService.ProcessFileAsync(fileId);
        public Task RecordFileAccessAsync(int fileId, Domain.Enums.FileAccessType accessType) => _baseFileService.RecordFileAccessAsync(fileId, accessType);
        public Task<List<FileDto>> GetRecentFilesAsync(int count = 10) => _baseFileService.GetRecentFilesAsync(count);
        public Task<Dictionary<string, object>> GetFileStatisticsAsync() => _baseFileService.GetFileStatisticsAsync();
        public Task<bool> ValidateFileAsync(Microsoft.AspNetCore.Http.IFormFile file) => _baseFileService.ValidateFileAsync(file);
        public Task<string> GetFileHashAsync(Stream stream) => _baseFileService.GetFileHashAsync(stream);
        public Task<bool> FileExistsAsync(int fileId) => _baseFileService.FileExistsAsync(fileId);
        public Task<long> GetTotalFileSizeAsync(int? folderId = null) => _baseFileService.GetTotalFileSizeAsync(folderId);
        public Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto) => _baseFileService.BulkUpdateFilesAsync(fileIds, updateDto);
        public Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId) => _baseFileService.BulkMoveFilesAsync(fileIds, destinationFolderId);
        public Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId) => _baseFileService.BulkCopyFilesAsync(fileIds, destinationFolderId);
    }
}