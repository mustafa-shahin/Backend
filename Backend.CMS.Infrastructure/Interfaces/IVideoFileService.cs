using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    /// <summary>
    /// Service interface for video file operations
    /// </summary>
    public interface IVideoFileService
    {
        // Basic CRUD operations
        Task<VideoFileDto> UploadVideoAsync(FileUploadDto uploadDto);
        Task<List<VideoFileDto>> UploadMultipleVideosAsync(MultipleFileUploadDto uploadDto);
        Task<PaginatedResult<VideoFileDto>> GetVideosPagedAsync(VideoSearchDto searchDto);
        Task<VideoFileDto?> GetVideoByIdAsync(int fileId);
        Task<VideoFileDto> UpdateVideoAsync(int fileId, UpdateVideoDto updateDto);
        Task<bool> DeleteVideoAsync(int fileId);

        // Video-specific operations
        Task<bool> GenerateVideoThumbnailAsync(int fileId, TimeSpan? timestamp = null);
        Task<bool> ExtractVideoMetadataAsync(int fileId);
        Task<VideoFileDto> CreateVideoClipAsync(int fileId, TimeSpan startTime, TimeSpan endTime, string? name = null);
        
        Task<List<VideoFileDto>> GetVideosByDurationAsync(TimeSpan? minDuration = null, TimeSpan? maxDuration = null);
        Task<List<VideoFileDto>> GetVideosByResolutionAsync(string resolution);
        
        // Batch operations
        Task<BulkOperationResultDto> BulkGenerateVideoThumbnailsAsync(List<int> fileIds, TimeSpan? timestamp = null);
    }

}