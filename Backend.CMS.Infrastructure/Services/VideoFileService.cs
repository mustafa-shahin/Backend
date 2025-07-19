using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class VideoFileService : BaseFileService<VideoFileEntity, VideoFileDto>, IVideoFileService
    {
        public VideoFileService(
            IUnitOfWork unitOfWork,
            IFileValidationService fileValidationService,
            IUserSessionService userSessionService,
            IFileUrlBuilder fileUrlBuilder,
            IMapper mapper,
            ILogger<VideoFileService> logger,
            IConfiguration configuration)
            : base(unitOfWork, fileValidationService, userSessionService, fileUrlBuilder, mapper, logger, configuration)
        {
        }

        protected override FileType HandledFileType => FileType.Video;

        #region IVideoFileService Implementation

        public async Task<VideoFileDto> UploadVideoAsync(FileUploadDto uploadDto)
        {
            return await UploadFileAsync(uploadDto);
        }

        public async Task<List<VideoFileDto>> UploadMultipleVideosAsync(MultipleFileUploadDto uploadDto)
        {
            return await UploadMultipleFilesAsync(uploadDto);
        }

        public async Task<PaginatedResult<VideoFileDto>> GetVideosPagedAsync(VideoSearchDto searchDto)
        {
            return await GetFilesPagedAsync(searchDto);
        }

        public async Task<VideoFileDto?> GetVideoByIdAsync(int fileId)
        {
            return await GetFileByIdAsync(fileId);
        }

        public async Task<VideoFileDto> UpdateVideoAsync(int fileId, UpdateVideoDto updateDto)
        {
            return await UpdateFileAsync(fileId, updateDto);
        }

        public async Task<bool> DeleteVideoAsync(int fileId)
        {
            return await DeleteFileAsync(fileId);
        }

        // Video-specific operations - placeholder implementations
        public async Task<bool> GenerateVideoThumbnailAsync(int fileId, TimeSpan? timestamp = null)
        {
            _logger.LogInformation("Video thumbnail generation not yet implemented for file {FileId}", fileId);
            return true;
        }

        public async Task<bool> ExtractVideoMetadataAsync(int fileId)
        {
            _logger.LogInformation("Video metadata extraction not yet implemented for file {FileId}", fileId);
            return true;
        }

        public async Task<VideoFileDto> CreateVideoClipAsync(int fileId, TimeSpan startTime, TimeSpan endTime, string? name = null)
        {
            throw new NotImplementedException("Video clip creation not yet implemented");
        }

        public async Task<List<VideoFileDto>> GetVideosByDurationAsync(TimeSpan? minDuration = null, TimeSpan? maxDuration = null)
        {
            var searchDto = new VideoSearchDto
            {
                MinDuration = minDuration,
                MaxDuration = maxDuration,
                PageSize = 1000
            };

            var result = await GetVideosPagedAsync(searchDto);
            return result.Data?.ToList() ?? new List<VideoFileDto>();
        }

        public async Task<List<VideoFileDto>> GetVideosByResolutionAsync(string resolution)
        {
            // Parse resolution like "1920x1080"
            var parts = resolution.Split('x', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height))
            {
                var searchDto = new VideoSearchDto
                {
                    MinWidth = width,
                    MaxWidth = width,
                    MinHeight = height,
                    MaxHeight = height,
                    PageSize = 1000
                };

                var result = await GetVideosPagedAsync(searchDto);
                return result.Data?.ToList() ?? new List<VideoFileDto>();
            }

            return new List<VideoFileDto>();
        }



        public async Task<BulkOperationResultDto> BulkGenerateVideoThumbnailsAsync(List<int> fileIds, TimeSpan? timestamp = null)
        {
            return new BulkOperationResultDto
            {
                TotalRequested = fileIds.Count,
                SuccessCount = 0,
                FailureCount = fileIds.Count
            };
        }



        #endregion

        #region Protected Override Methods

        protected override VideoFileEntity CreateEntityInstance()
        {
            return new VideoFileEntity();
        }

        protected override async Task ValidateTypeSpecificContentAsync(byte[] content, string contentType)
        {
            // TODO: Implement video-specific validation
            await Task.CompletedTask;
        }

        protected override async Task SetTypeSpecificPropertiesAsync(VideoFileEntity entity, byte[] content, FileUploadDto uploadDto)
        {
            // TODO: Extract video metadata, duration, resolution, etc.
            await Task.CompletedTask;
        }

        protected override async Task ProcessFileAfterUploadAsync(VideoFileEntity entity, FileUploadDto uploadDto)
        {
            // TODO: Process video after upload
            entity.IsProcessed = true;
            entity.ProcessingStatus = "Completed";
            await Task.CompletedTask;
        }

        protected override async Task<VideoFileDto> MapEntityToDto(VideoFileEntity entity)
        {
            var urlSet = _fileUrlBuilder.GenerateFileUrls(entity);

            return new VideoFileDto
            {
                Id = entity.Id,
                OriginalFileName = entity.OriginalFileName,
                StoredFileName = entity.StoredFileName,
                ContentType = entity.ContentType,
                FileSize = entity.FileSize,
                FileSizeFormatted = FormatFileSize(entity.FileSize),
                FileExtension = entity.FileExtension,
                FileType = entity.FileType,
                FileTypeName = entity.FileType.ToString(),
                Description = entity.Description,
                Alt = entity.Alt,
                Metadata = entity.Metadata,
                IsPublic = entity.IsPublic,
                FolderId = entity.FolderId,
                DownloadCount = entity.DownloadCount,
                LastAccessedAt = entity.LastAccessedAt,
                Hash = entity.Hash,
                IsProcessed = entity.IsProcessed,
                ProcessingStatus = entity.ProcessingStatus,
                Tags = entity.Tags,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Urls = urlSet,
                CanPreview = true,

                // Video-specific properties
                Width = entity.Width,
                Height = entity.Height,
                Duration = entity.Duration,           
            };
        }

        #endregion

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}