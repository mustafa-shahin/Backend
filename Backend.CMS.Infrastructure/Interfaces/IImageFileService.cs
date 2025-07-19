using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    /// <summary>
    /// Service interface for image file operations
    /// </summary>
    public interface IImageFileService
    {
        // Basic CRUD operations
        Task<ImageFileDto> UploadImageAsync(FileUploadDto uploadDto);
        Task<List<ImageFileDto>> UploadMultipleImagesAsync(MultipleFileUploadDto uploadDto);
        Task<PaginatedResult<ImageFileDto>> GetImagesPagedAsync(ImageSearchDto searchDto);
        Task<ImageFileDto?> GetImageByIdAsync(int fileId);
        Task<ImageFileDto> UpdateImageAsync(int fileId, UpdateImageDto updateDto);
        Task<bool> DeleteImageAsync(int fileId);

        // Image-specific operations
        Task<bool> GenerateThumbnailAsync(int fileId, int width = 200, int height = 200);
        Task<bool> ExtractMetadataAsync(int fileId);
        
        Task<List<ImageFileDto>> GetImagesByDimensionsAsync(int? minWidth = null, int? maxWidth = null, 
            int? minHeight = null, int? maxHeight = null);
        
        // Batch operations
        Task<BulkOperationResultDto> BulkGenerateThumbnailsAsync(List<int> fileIds, int width = 200, int height = 200);
    }

}