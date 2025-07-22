using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Services;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IAudioService : IFileService<AudioDto, CreateAudioDto>
    {
        Task<AudioDto?> GetByIdAsync(int id);
        Task<IEnumerable<AudioDto>> GetAllAsync();
        Task<IEnumerable<AudioDto>> GetByFolderIdAsync(int folderId);
        Task<AudioDto> CreateAsync(CreateAudioDto createDto);
        Task<AudioDto> UpdateAsync(int id, UpdateAudioDto updateDto);
        Task<bool> DeleteAsync(int id);
        Task<AudioDto?> GetByNameAsync(string name);
        Task<IEnumerable<AudioDto>> GetByContentTypeAsync(string contentType);
        Task<PaginatedResult<AudioDto>> GetPagedAsync(int pageNumber, int pageSize);
        Task<PaginatedResult<AudioDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
        Task<List<AudioDto>> GetAudiosByEntityAsync(string entityType, int entityId);
        
        // Methods called by controllers but missing from interface
        Task<PaginatedResult<AudioDto>> GetAudiosPagedAsync(AudioSearchDto searchDto);
        Task<AudioDto> UploadAudioAsync(FileUploadDto uploadDto);
        Task<List<AudioDto>> UploadMultipleAudiosAsync(MultipleFileUploadDto uploadDto);
        Task<AudioDto?> GetAudioByIdAsync(int id);
        Task<AudioDto> UpdateAudioAsync(int id, UpdateAudioDto updateDto);
        Task<bool> DeleteAudioAsync(int id);
        Task<FileModel> DownloadAudioAsync(int id);
    }
}