using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IArchiveFileService
    {
        Task<ArchiveFileDto> UploadArchiveAsync(FileUploadDto uploadDto);
        Task<List<ArchiveFileDto>> UploadMultipleArchivesAsync(MultipleFileUploadDto uploadDto);
        Task<PaginatedResult<ArchiveFileDto>> GetArchivesPagedAsync(ArchiveSearchDto searchDto);
        Task<ArchiveFileDto?> GetArchiveByIdAsync(int fileId);
        Task<ArchiveFileDto> UpdateArchiveAsync(int fileId, UpdateArchiveDto updateDto);
        Task<bool> DeleteArchiveAsync(int fileId);
    }

}