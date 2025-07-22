using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Services;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IOtherFileService : IFileService<OtherFileDto, CreateOtherFileDto>
    {
        Task<OtherFileDto?> GetByIdAsync(int id);
        Task<IEnumerable<OtherFileDto>> GetAllAsync();
        Task<IEnumerable<OtherFileDto>> GetByFolderIdAsync(int folderId);
        Task<OtherFileDto> CreateAsync(CreateOtherFileDto createDto);
        Task<OtherFileDto> UpdateAsync(int id, UpdateOtherFileDto updateDto);
        Task<bool> DeleteAsync(int id);
        Task<OtherFileDto?> GetByNameAsync(string name);
        Task<IEnumerable<OtherFileDto>> GetByContentTypeAsync(string contentType);
        Task<PaginatedResult<OtherFileDto>> GetPagedAsync(int pageNumber, int pageSize);
        Task<PaginatedResult<OtherFileDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
        
        // Additional methods to match other file services
        Task<PaginatedResult<OtherFileDto>> GetOtherFilesPagedAsync(OtherFileSearchDto searchDto);
        Task<OtherFileDto> UploadOtherFileAsync(FileUploadDto uploadDto);
        Task<List<OtherFileDto>> UploadMultipleOtherFilesAsync(MultipleFileUploadDto uploadDto);
        Task<OtherFileDto?> GetOtherFileByIdAsync(int id);
        Task<OtherFileDto> UpdateOtherFileAsync(int id, UpdateOtherFileDto updateDto);
        Task<bool> DeleteOtherFileAsync(int id);
        Task<FileModel> DownloadOtherFileAsync(int id);
    }
}