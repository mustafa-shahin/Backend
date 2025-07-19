

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
    /// Service for handling archive file operations
    /// </summary>
    public class ArchiveFileService : BaseFileService<ArchiveFileEntity, ArchiveFileDto>, IArchiveFileService
    {
        public ArchiveFileService(
            IUnitOfWork unitOfWork,
            IFileValidationService fileValidationService,
            IUserSessionService userSessionService,
            IFileUrlBuilder fileUrlBuilder,
            IMapper mapper,
            ILogger<ArchiveFileService> logger,
            IConfiguration configuration)
            : base(unitOfWork, fileValidationService, userSessionService, fileUrlBuilder, mapper, logger, configuration)
        {
        }

        protected override FileType HandledFileType => FileType.Archive;

        #region IArchiveFileService Implementation

        public async Task<ArchiveFileDto> UploadArchiveAsync(FileUploadDto uploadDto)
        {
            return await UploadFileAsync(uploadDto);
        }

        public async Task<List<ArchiveFileDto>> UploadMultipleArchivesAsync(MultipleFileUploadDto uploadDto)
        {
            return await UploadMultipleFilesAsync(uploadDto);
        }

        public async Task<PaginatedResult<ArchiveFileDto>> GetArchivesPagedAsync(ArchiveSearchDto searchDto)
        {
            return await GetFilesPagedAsync(searchDto);
        }

        public async Task<ArchiveFileDto?> GetArchiveByIdAsync(int fileId)
        {
            return await GetFileByIdAsync(fileId);
        }

        public async Task<ArchiveFileDto> UpdateArchiveAsync(int fileId, UpdateArchiveDto updateDto)
        {
            return await UpdateFileAsync(fileId, updateDto);
        }

        public async Task<bool> DeleteArchiveAsync(int fileId)
        {
            return await DeleteFileAsync(fileId);
        }

        public async Task<List<ArchiveFileDto>> GetArchivesByFileCountAsync(int? minFiles = null, int? maxFiles = null)
        {
            var searchDto = new ArchiveSearchDto
            {
                MinFileCount = minFiles,
                MaxFileCount = maxFiles,
                PageSize = 1000
            };

            var result = await GetArchivesPagedAsync(searchDto);
            return result.Data?.ToList() ?? new List<ArchiveFileDto>();
        }

        public async Task<List<ArchiveFileDto>> GetArchivesBySizeAsync(long? minUncompressedSize = null, long? maxUncompressedSize = null)
        {
            var searchDto = new ArchiveSearchDto
            {
                MinUncompressedSize = minUncompressedSize,
                MaxUncompressedSize = maxUncompressedSize,
                PageSize = 1000
            };

            var result = await GetArchivesPagedAsync(searchDto);
            return result.Data?.ToList() ?? new List<ArchiveFileDto>();
        }

        #endregion

        #region Protected Override Methods

        protected override ArchiveFileEntity CreateEntityInstance()
        {
            return new ArchiveFileEntity();
        }

        protected override async Task ValidateTypeSpecificContentAsync(byte[] content, string contentType)
        {
            // Archive validation can be added here if needed
            await Task.CompletedTask;
        }

        protected override async Task SetTypeSpecificPropertiesAsync(ArchiveFileEntity entity, byte[] content, FileUploadDto uploadDto)
        {
            try
            {
                // Archive metadata extraction can be added here if needed
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set archive-specific properties for {FileName}", entity.OriginalFileName);
            }
        }

        protected override async Task ProcessFileAfterUploadAsync(ArchiveFileEntity entity, FileUploadDto uploadDto)
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

                _logger.LogDebug("Completed archive processing for {FileName}", entity.OriginalFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process archive file: {FileId}", entity.Id);
                entity.ProcessingStatus = "Failed";
                entity.IsProcessed = false;
                _unitOfWork.Files.Update(entity);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        protected override async Task<ArchiveFileDto> MapEntityToDto(ArchiveFileEntity entity)
        {
            var urlSet = _fileUrlBuilder.GenerateFileUrls(entity);

            return new ArchiveFileDto
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
                CanPreview = false,

                // Archive-specific properties
                FileCount = entity.FileCount,
                UncompressedSize = entity.UncompressedSize
            };
        }

        protected override IQueryable<ArchiveFileEntity> ApplySearchFilters(IQueryable<ArchiveFileEntity> query, FileSearchDto searchDto)
        {
            // Apply base filters first
            query = base.ApplySearchFilters(query, searchDto);

            // Apply archive-specific filters
            if (searchDto is ArchiveSearchDto archiveSearchDto)
            {
                if (archiveSearchDto.MinFileCount.HasValue)
                    query = query.Where(f => f.FileCount >= archiveSearchDto.MinFileCount.Value);

                if (archiveSearchDto.MaxFileCount.HasValue)
                    query = query.Where(f => f.FileCount <= archiveSearchDto.MaxFileCount.Value);

                if (archiveSearchDto.MinUncompressedSize.HasValue)
                    query = query.Where(f => f.UncompressedSize >= archiveSearchDto.MinUncompressedSize.Value);

                if (archiveSearchDto.MaxUncompressedSize.HasValue)
                    query = query.Where(f => f.UncompressedSize <= archiveSearchDto.MaxUncompressedSize.Value);
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