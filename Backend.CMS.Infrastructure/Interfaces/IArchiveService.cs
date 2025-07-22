using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Services;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IArchiveService : IFileService<ArchiveDto, CreateArchiveDto>
    {
        Task<ArchiveDto?> GetByIdAsync(int id);
        Task<IEnumerable<ArchiveDto>> GetAllAsync();
        Task<IEnumerable<ArchiveDto>> GetByFolderIdAsync(int folderId);
        Task<ArchiveDto> CreateAsync(CreateArchiveDto createDto);
        Task<ArchiveDto> UpdateAsync(int id, UpdateArchiveDto updateDto);
        Task<bool> DeleteAsync(int id);
        Task<ArchiveDto?> GetByNameAsync(string name);
        Task<IEnumerable<ArchiveDto>> GetByContentTypeAsync(string contentType);
        Task<PaginatedResult<ArchiveDto>> GetPagedAsync(int pageNumber, int pageSize);
        Task<PaginatedResult<ArchiveDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
        
        // Methods called by controllers but missing from interface
        Task<PaginatedResult<ArchiveDto>> GetArchivesPagedAsync(ArchiveSearchDto searchDto);
        Task<ArchiveDto> UploadArchiveAsync(FileUploadDto uploadDto);
        Task<List<ArchiveDto>> UploadMultipleArchivesAsync(MultipleFileUploadDto uploadDto);
        Task<ArchiveDto?> GetArchiveByIdAsync(int id);
        Task<ArchiveDto> UpdateArchiveAsync(int id, UpdateArchiveDto updateDto);
        Task<bool> DeleteArchiveAsync(int id);
        Task<FileModel> DownloadArchiveAsync(int id);
    }
}