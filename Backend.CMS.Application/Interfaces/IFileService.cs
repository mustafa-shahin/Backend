using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Backend.CMS.Application.Interfaces
{
    public interface IFileService
    {
        // File upload operations
        Task<FileDto> UploadFileAsync(FileUploadDto uploadDto);
        Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto);
        Task<FileDto> UploadFileFromUrlAsync(string url, int? folderId = null, string? description = null);

        // File retrieval operations
        Task<FileDto> GetFileByIdAsync(int fileId);
        Task<List<FileDto>> GetFilesAsync(int page = 1, int pageSize = 20);
        Task<List<FileDto>> GetFilesByFolderAsync(int? folderId, int page = 1, int pageSize = 20);
        Task<List<FileDto>> SearchFilesAsync(FileSearchDto searchDto);
        Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int fileId);
        Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int fileId);

        // File management operations
        Task<FileDto> UpdateFileAsync(int fileId, UpdateFileDto updateDto);
        Task<bool> DeleteFileAsync(int fileId);
        Task<bool> DeleteMultipleFilesAsync(List<int> fileIds);
        Task<FileDto> MoveFileAsync(MoveFileDto moveDto);
        Task<FileDto> CopyFileAsync(CopyFileDto copyDto);
        Task<bool> RenameFileAsync(int fileId, string newName);

        // File preview and processing
        Task<FilePreviewDto> GetFilePreviewAsync(int fileId);
        Task<string> GenerateFileUrlAsync(int fileId, bool thumbnail = false);
        Task<bool> GenerateThumbnailAsync(int fileId);
        Task<bool> ProcessFileAsync(int fileId);

        // File access and statistics
        Task RecordFileAccessAsync(int fileId, FileAccessType accessType);
        Task<List<FileDto>> GetRecentFilesAsync(int count = 10);
        Task<Dictionary<string, object>> GetFileStatisticsAsync();

        // File validation and utilities
        Task<bool> ValidateFileAsync(IFormFile file);
        Task<string> GetFileHashAsync(Stream stream);
        Task<bool> FileExistsAsync(int fileId);
        Task<long> GetTotalFileSizeAsync(int? folderId = null);

        // Batch operations
        Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto);
        Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId);
        Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId);

    }   

}