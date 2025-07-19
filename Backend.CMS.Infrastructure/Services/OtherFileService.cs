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
    /// Service for handling other file operations
    /// </summary>
    public class OtherFileService : BaseFileService<OtherFileEntity, OtherFileDto>, IOtherFileService
    {
        public OtherFileService(
            IUnitOfWork unitOfWork,
            IFileValidationService fileValidationService,
            IUserSessionService userSessionService,
            IFileUrlBuilder fileUrlBuilder,
            IMapper mapper,
            ILogger<OtherFileService> logger,
            IConfiguration configuration)
            : base(unitOfWork, fileValidationService, userSessionService, fileUrlBuilder, mapper, logger, configuration)
        {
        }

        protected override FileType HandledFileType => FileType.Other;

        #region IOtherFileService Implementation

        public async Task<OtherFileDto> UploadOtherFileAsync(FileUploadDto uploadDto)
        {
            return await UploadFileAsync(uploadDto);
        }

        public async Task<List<OtherFileDto>> UploadMultipleOtherFilesAsync(MultipleFileUploadDto uploadDto)
        {
            return await UploadMultipleFilesAsync(uploadDto);
        }

        public async Task<PaginatedResult<OtherFileDto>> GetOtherFilesPagedAsync(OtherSearchDto searchDto)
        {
            return await GetFilesPagedAsync(searchDto);
        }

        public async Task<OtherFileDto?> GetOtherFileByIdAsync(int fileId)
        {
            return await GetFileByIdAsync(fileId);
        }

        public async Task<OtherFileDto> UpdateOtherFileAsync(int fileId, UpdateOtherDto updateDto)
        {
            return await UpdateFileAsync(fileId, updateDto);
        }

        public async Task<bool> DeleteOtherFileAsync(int fileId)
        {
            return await DeleteFileAsync(fileId);
        }

        #endregion

        #region Protected Override Methods

        protected override OtherFileEntity CreateEntityInstance()
        {
            return new OtherFileEntity();
        }

        protected override async Task ValidateTypeSpecificContentAsync(byte[] content, string contentType)
        {
            // Other file validation can be added here if needed
            await Task.CompletedTask;
        }

        protected override async Task SetTypeSpecificPropertiesAsync(OtherFileEntity entity, byte[] content, FileUploadDto uploadDto)
        {
            try
            {
                // Other file metadata extraction can be added here if needed
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set other file-specific properties for {FileName}", entity.OriginalFileName);
            }
        }

        protected override async Task ProcessFileAfterUploadAsync(OtherFileEntity entity, FileUploadDto uploadDto)
        {
            try
            {
                entity.ProcessingStatus = "Processing";
                _unitOfWork.Files.Update(entity);
                await _unitOfWork.SaveChangesAsync();

                entity.IsProcessed = true;
                entity.ProcessingStatus = "Completed";
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedByUserId = _userSessionService.GetCurrentUserId();

                _unitOfWork.Files.Update(entity);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogDebug("Completed other file processing for {FileName}", entity.OriginalFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process other file: {FileId}", entity.Id);
                entity.ProcessingStatus = "Failed";
                entity.IsProcessed = false;
                _unitOfWork.Files.Update(entity);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        protected override async Task ApplyTypeSpecificUpdatesAsync(OtherFileEntity entity, UpdateFileDto updateDto)
        {
            // No specific updates for other files in simplified version
            await Task.CompletedTask;
        }

        protected override async Task<OtherFileDto> MapEntityToDto(OtherFileEntity entity)
        {
            var urlSet = _fileUrlBuilder.GenerateFileUrls(entity);

            return new OtherFileDto
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
                CanPreview = false
            };
        }

        protected override IQueryable<OtherFileEntity> ApplySearchFilters(IQueryable<OtherFileEntity> query, FileSearchDto searchDto)
        {
            // Apply base filters first
            query = base.ApplySearchFilters(query, searchDto);

            // No specific filters for other files in simplified version
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