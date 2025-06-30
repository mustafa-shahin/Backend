using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Components.Forms;

namespace Frontend.Interfaces
{
    public interface IFileService
    {
        // Core file operations
        Task<PagedResult<FileDto>> GetFilesAsync(int page = 1, int pageSize = 20, int? folderId = null, string? search = null, FileType? fileType = null);
        Task<FileDto?> GetFileByIdAsync(int id);
        Task<FileDto?> UploadFileAsync(FileUploadDto uploadDto);
        Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto);
        Task<FileDto?> UpdateFileAsync(int id, UpdateFileDto updateDto);
        Task<bool> DeleteFileAsync(int id);
        Task<bool> DeleteMultipleFilesAsync(List<int> fileIds);

        // File operations
        Task<FileDto?> MoveFileAsync(MoveFileDto moveDto);
        Task<FileDto?> CopyFileAsync(CopyFileDto copyDto);
        Task<List<FileDto>> GetRecentFilesAsync(int count = 10);
        Task<Dictionary<string, object>> GetFileStatisticsAsync();
        Task<bool> GenerateThumbnailAsync(int id);

        // Download and streaming
        Task<string> GenerateDownloadTokenAsync(int fileId);
        Task DownloadFileAsync(int id);
        Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int id);
        Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int id);
        Task<FilePreviewDto> GetFilePreviewAsync(int id);

        // Search and bulk operations
        Task<List<FileDto>> SearchFilesAsync(FileSearchDto searchDto);
        Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto);
        Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId);
        Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId);

        // URL helpers
        string GetFileUrl(int fileId);
        string GetThumbnailUrl(int fileId);
        string GetPreviewUrl(int fileId);

        // Additional operations
        Task<bool> VerifyFileIntegrityAsync(int fileId);
        Task<object> GetFileDiagnosticInfoAsync(int fileId);
        string FormatFileSize(long bytes);
        string FormatDuration(TimeSpan duration);
    }
}