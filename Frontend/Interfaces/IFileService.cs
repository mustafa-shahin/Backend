using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Components.Forms;

namespace Frontend.Interfaces
{
    public interface IFileService
    {
        Task<PagedResult<FileDto>> GetFilesAsync(int page = 1, int pageSize = 20, int? folderId = null, string? search = null, FileType? fileType = null);
        Task<FileDto?> GetFileByIdAsync(int id);
        Task<FileDto?> UploadFileAsync(FileUploadDto uploadDto);
        Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto);
        Task<FileDto?> UpdateFileAsync(int id, UpdateFileDto updateDto);
        Task<bool> DeleteFileAsync(int id);
        Task<bool> DeleteMultipleFilesAsync(List<int> fileIds);
        Task<FileDto?> MoveFileAsync(MoveFileDto moveDto);
        Task<FileDto?> CopyFileAsync(CopyFileDto copyDto);
        Task<List<FileDto>> GetRecentFilesAsync(int count = 10);
        Task<Dictionary<string, object>> GetFileStatisticsAsync();
        Task<bool> GenerateThumbnailAsync(int id);
        Task<string> GenerateDownloadTokenAsync(int fileId);
        Task DownloadFileAsync(int id);
        Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int id);
        Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int id);
        Task<FilePreviewDto> GetFilePreviewAsync(int id);
        Task<List<FileDto>> SearchFilesAsync(FileSearchDto searchDto);
        Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto);
        Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId);
        Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId);
    }
}