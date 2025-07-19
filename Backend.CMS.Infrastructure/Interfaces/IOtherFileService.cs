using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    /// <summary>
    /// Service interface for other file operations
    /// </summary>
    public interface IOtherFileService
    {
        // Basic CRUD operations
        Task<OtherFileDto> UploadOtherFileAsync(FileUploadDto uploadDto);
        Task<List<OtherFileDto>> UploadMultipleOtherFilesAsync(MultipleFileUploadDto uploadDto);
        Task<PaginatedResult<OtherFileDto>> GetOtherFilesPagedAsync(OtherSearchDto searchDto);
        Task<OtherFileDto?> GetOtherFileByIdAsync(int fileId);
        Task<OtherFileDto> UpdateOtherFileAsync(int fileId, UpdateOtherDto updateDto);
        Task<bool> DeleteOtherFileAsync(int fileId);
    }
}