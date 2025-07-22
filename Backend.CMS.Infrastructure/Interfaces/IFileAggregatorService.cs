using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.Interfaces
{
    /// <summary>
    /// Service that aggregates all file types and provides a unified interface
    /// </summary>
    public interface IFileAggregatorService
    {
        Task<PaginatedResult<FileDto>> GetFilesPagedAsync(FileSearchDto searchDto);
        Task<FileDto?> GetFileByIdAsync(int id);
        Task<List<FileDto>> GetFilesForEntityAsync(string entityType, int entityId, FileType? fileType = null);
        Task<FileDto> UploadFileAsync(FileUploadDto uploadDto);
        Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto);
        Task<FileDto> UpdateFileAsync(int id, UpdateFileDto updateDto);
        Task<bool> DeleteFileAsync(int id);
        Task<bool> DeleteMultipleFilesAsync(List<int> fileIds);
        Task<FileDto> MoveFileAsync(MoveFileDto moveDto);
        Task<FileDto> CopyFileAsync(CopyFileDto copyDto);
        Task<List<FileDto>> GetRecentFilesAsync(int count = 10);
        Task<Dictionary<string, object>> GetFileStatisticsAsync();
        Task<bool> GenerateThumbnailAsync(int id);
        Task<FilePreviewDto> GetFilePreviewAsync(int id);
        Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int id);
        Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int id);
        Task<bool> VerifyFileIntegrityAsync(int id);
        Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto);
        Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId);
        Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId);
        Task<PaginatedResult<FileDto>> SearchFilesPagedAsync(FileSearchDto searchDto);
    }
}