using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    /// <summary>
    /// Service for handling image file operations with image-specific functionality
    /// </summary>
    public class ImageFileService : BaseFileService<ImageFileEntity, ImageFileDto>, IImageFileService
    {
        private readonly IImageProcessingService _imageProcessingService;

        public ImageFileService(
            IUnitOfWork unitOfWork,
            IFileValidationService fileValidationService,
            IUserSessionService userSessionService,
            IFileUrlBuilder fileUrlBuilder,
            IMapper mapper,
            ILogger<ImageFileService> logger,
            IConfiguration configuration,
            IImageProcessingService imageProcessingService)
            : base(unitOfWork, fileValidationService, userSessionService, fileUrlBuilder, mapper, logger, configuration)
        {
            _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
        }

        protected override FileType HandledFileType => FileType.Image;

        #region IImageFileService Implementation

        public async Task<ImageFileDto> UploadImageAsync(FileUploadDto uploadDto)
        {
            return await UploadFileAsync(uploadDto);
        }

        public async Task<List<ImageFileDto>> UploadMultipleImagesAsync(MultipleFileUploadDto uploadDto)
        {
            return await UploadMultipleFilesAsync(uploadDto);
        }

        public async Task<PaginatedResult<ImageFileDto>> GetImagesPagedAsync(ImageSearchDto searchDto)
        {
            return await GetFilesPagedAsync(searchDto);
        }

        public async Task<ImageFileDto?> GetImageByIdAsync(int fileId)
        {
            return await GetFileByIdAsync(fileId);
        }

        public async Task<ImageFileDto> UpdateImageAsync(int fileId, UpdateImageDto updateDto)
        {
            return await UpdateFileAsync(fileId, updateDto);
        }

        public async Task<bool> DeleteImageAsync(int fileId)
        {
            return await DeleteFileAsync(fileId);
        }

        public async Task<bool> GenerateThumbnailAsync(int fileId, int width = 200, int height = 200)
        {
            try
            {
                var file = await _unitOfWork.Files.GetByIdAsync(fileId);
                if (file is not ImageFileEntity imageFile)
                    return false;

                var thumbnailBytes = await _imageProcessingService.GenerateThumbnailFromBytesAsync(
                    imageFile.FileContent, width, height);

                imageFile.ThumbnailContent = thumbnailBytes;
                imageFile.UpdatedAt = DateTime.UtcNow;
                imageFile.UpdatedByUserId = _userSessionService.GetCurrentUserId();

                _unitOfWork.Files.Update(imageFile);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Generated thumbnail for image {FileId} ({Width}x{Height})", 
                    fileId, width, height);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate thumbnail for image {FileId}", fileId);
                return false;
            }
        }

        public async Task<bool> ExtractMetadataAsync(int fileId)
        {
            try
            {
                var file = await _unitOfWork.Files.GetByIdAsync(fileId);
                if (file is not ImageFileEntity imageFile)
                    return false;

                // Extract image dimensions and basic metadata
                var (width, height) = await _imageProcessingService.GetImageDimensionsFromBytesAsync(imageFile.FileContent);
                imageFile.Width = width;
                imageFile.Height = height;

                imageFile.UpdatedAt = DateTime.UtcNow;
                imageFile.UpdatedByUserId = _userSessionService.GetCurrentUserId();

                _unitOfWork.Files.Update(imageFile);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Extracted metadata for image {FileId}", fileId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract metadata for image {FileId}", fileId);
                return false;
            }
        }


        public async Task<List<ImageFileDto>> GetImagesByDimensionsAsync(int? minWidth = null, int? maxWidth = null, 
            int? minHeight = null, int? maxHeight = null)
        {
            var searchDto = new ImageSearchDto
            {
                MinWidth = minWidth,
                MaxWidth = maxWidth,
                MinHeight = minHeight,
                MaxHeight = maxHeight,
                PageSize = 1000
            };

            var result = await GetImagesPagedAsync(searchDto);
            return result.Data?.ToList() ?? new List<ImageFileDto>();
        }


        public async Task<BulkOperationResultDto> BulkGenerateThumbnailsAsync(List<int> fileIds, int width = 200, int height = 200)
        {
            var successCount = 0;
            var failures = new List<BulkOperationErrorDto>();

            foreach (var fileId in fileIds)
            {
                try
                {
                    var success = await GenerateThumbnailAsync(fileId, width, height);
                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failures.Add(new BulkOperationErrorDto
                        {
                            EntityId = fileId,
                            ErrorMessage = "Failed to generate thumbnail"
                        });
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(new BulkOperationErrorDto
                    {
                        EntityId = fileId,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return new BulkOperationResultDto
            {
                TotalRequested = fileIds.Count,
                SuccessCount = successCount,
                FailureCount = fileIds.Count - successCount,
                Errors = failures
            };
        }

        #endregion

        #region Protected Override Methods

        protected override ImageFileEntity CreateEntityInstance()
        {
            return new ImageFileEntity();
        }

        protected override async Task ValidateTypeSpecificContentAsync(byte[] content, string contentType)
        {
            if (!await _imageProcessingService.IsImageFromBytesAsync(content))
            {
                throw new InvalidOperationException("Uploaded file appears to be corrupted or is not a valid image");
            }
        }

        protected override async Task SetTypeSpecificPropertiesAsync(ImageFileEntity entity, byte[] content, FileUploadDto uploadDto)
        {
            try
            {
                // Extract basic image properties
                var (width, height) = await _imageProcessingService.GetImageDimensionsFromBytesAsync(content);
                entity.Width = width;
                entity.Height = height;

                // Generate thumbnail if requested
                if (uploadDto.GenerateThumbnail)
                {
                    entity.ThumbnailContent = await _imageProcessingService.GenerateThumbnailFromBytesAsync(content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set image-specific properties for {FileName}", entity.OriginalFileName);
                // Don't throw - allow the upload to continue with basic properties
            }
        }

        protected override async Task ProcessFileAfterUploadAsync(ImageFileEntity entity, FileUploadDto uploadDto)
        {
            try
            {
                entity.ProcessingStatus = "Processing";
                _unitOfWork.Files.Update(entity);
                await _unitOfWork.SaveChangesAsync();

                // Process image metadata and generate thumbnail if not already done
                if (entity.ThumbnailContent == null && uploadDto.GenerateThumbnail)
                {
                    entity.ThumbnailContent = await _imageProcessingService.GenerateThumbnailFromBytesAsync(entity.FileContent);
                }

                entity.IsProcessed = true;
                entity.ProcessingStatus = "Completed";
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedByUserId = _userSessionService.GetCurrentUserId();

                _unitOfWork.Files.Update(entity);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogDebug("Completed image processing for {FileName}", entity.OriginalFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process image file: {FileId}", entity.Id);
                entity.ProcessingStatus = "Failed";
                entity.IsProcessed = false;
                _unitOfWork.Files.Update(entity);
                await _unitOfWork.SaveChangesAsync();
            }
        }


        protected override async Task<ImageFileDto> MapEntityToDto(ImageFileEntity entity)
        {
            var urlSet = _fileUrlBuilder.GenerateFileUrls(entity);

            return new ImageFileDto
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

                // Image-specific properties
                Width = entity.Width,
                Height = entity.Height,
                HasThumbnail = entity.ThumbnailContent?.Length > 0,
            };
        }

        protected override IQueryable<ImageFileEntity> ApplySearchFilters(IQueryable<ImageFileEntity> query, FileSearchDto searchDto)
        {
            // Apply base filters first
            query = base.ApplySearchFilters(query, searchDto);

            // Apply image-specific filters
            if (searchDto is ImageSearchDto imageSearchDto)
            {
                if (imageSearchDto.MinWidth.HasValue)
                    query = query.Where(f => f.Width >= imageSearchDto.MinWidth.Value);

                if (imageSearchDto.MaxWidth.HasValue)
                    query = query.Where(f => f.Width <= imageSearchDto.MaxWidth.Value);

                if (imageSearchDto.MinHeight.HasValue)
                    query = query.Where(f => f.Height >= imageSearchDto.MinHeight.Value);

                if (imageSearchDto.MaxHeight.HasValue)
                    query = query.Where(f => f.Height <= imageSearchDto.MaxHeight.Value);
            }

            return query;
        }

        #endregion

        #region Private Helper Methods

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
        #endregion
    }
}