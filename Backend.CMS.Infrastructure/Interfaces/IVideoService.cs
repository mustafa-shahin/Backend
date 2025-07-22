using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Services;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IVideoService : IFileService<VideoDto, CreateVideoDto>
    {
        Task<VideoDto?> GetByIdAsync(int id);
        Task<IEnumerable<VideoDto>> GetAllAsync();
        Task<IEnumerable<VideoDto>> GetByFolderIdAsync(int folderId);
        Task<VideoDto> CreateAsync(CreateVideoDto createDto);
        Task<VideoDto> UpdateAsync(int id, UpdateVideoDto updateDto);
        Task<bool> DeleteAsync(int id);
        Task<VideoDto?> GetByNameAsync(string name);
        Task<IEnumerable<VideoDto>> GetByContentTypeAsync(string contentType);
        Task<PaginatedResult<VideoDto>> GetPagedAsync(int pageNumber, int pageSize);
        Task<PaginatedResult<VideoDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
        Task<List<VideoDto>> GetVideosByEntityAsync(string entityType, int entityId);
        
        // Upload/Download methods
        Task<VideoDto> UploadVideoAsync(FileUploadDto uploadDto);
        Task<List<VideoDto>> UploadMultipleVideosAsync(MultipleFileUploadDto uploadDto);
        Task<FileModel> DownloadVideoAsync(int id);
    }
}