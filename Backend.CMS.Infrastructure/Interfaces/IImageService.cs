using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Services;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IImageService : IFileService<ImageDto, CreateImageDto>
    {
        Task<ImageDto?> GetByIdAsync(int id);
        Task<IEnumerable<ImageDto>> GetAllAsync();
        Task<IEnumerable<ImageDto>> GetByFolderIdAsync(int folderId);
        Task<ImageDto> CreateAsync(CreateImageDto createDto);
        Task<ImageDto> UpdateAsync(int id, UpdateImageDto updateDto);
        Task<bool> DeleteAsync(int id);
        Task<ImageDto?> GetByNameAsync(string name);
        Task<IEnumerable<ImageDto>> GetByContentTypeAsync(string contentType);
        Task<PaginatedResult<ImageDto>> GetPagedAsync(int pageNumber, int pageSize);
        Task<PaginatedResult<ImageDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
        Task<List<ImageDto>> GetImagesByEntityAsync(string entityType, int entityId);
        
        // Upload/Download methods
        Task<ImageDto> UploadImageAsync(FileUploadDto uploadDto);
        Task<List<ImageDto>> UploadMultipleImagesAsync(MultipleFileUploadDto uploadDto);
        Task<ImageDto> UploadImageForEntityAsync(FileUploadDto uploadDto);
        Task<List<ImageDto>> UploadMultipleImagesForEntityAsync(MultipleFileUploadDto uploadDto);
        Task<FileModel> DownloadImageAsync(int id);
    }
}