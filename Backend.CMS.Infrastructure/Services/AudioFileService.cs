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
    /// Service for handling audio file operations
    /// </summary>
    public class AudioFileService : BaseFileService<AudioFileEntity, AudioFileDto>, IAudioFileService
    {
        public AudioFileService(
            IUnitOfWork unitOfWork,
            IFileValidationService fileValidationService,
            IUserSessionService userSessionService,
            IFileUrlBuilder fileUrlBuilder,
            IMapper mapper,
            ILogger<AudioFileService> logger,
            IConfiguration configuration)
            : base(unitOfWork, fileValidationService, userSessionService, fileUrlBuilder, mapper, logger, configuration)
        {
        }

        protected override FileType HandledFileType => FileType.Audio;

        #region IAudioFileService Implementation

        public async Task<AudioFileDto> UploadAudioAsync(FileUploadDto uploadDto)
        {
            return await UploadFileAsync(uploadDto);
        }

        public async Task<List<AudioFileDto>> UploadMultipleAudiosAsync(MultipleFileUploadDto uploadDto)
        {
            return await UploadMultipleFilesAsync(uploadDto);
        }

        public async Task<PaginatedResult<AudioFileDto>> GetAudiosPagedAsync(AudioSearchDto searchDto)
        {
            return await GetFilesPagedAsync(searchDto);
        }

        public async Task<AudioFileDto?> GetAudioByIdAsync(int fileId)
        {
            return await GetFileByIdAsync(fileId);
        }

        public async Task<AudioFileDto> UpdateAudioAsync(int fileId, UpdateAudioDto updateDto)
        {
            return await UpdateFileAsync(fileId, updateDto);
        }

        public async Task<bool> DeleteAudioAsync(int fileId)
        {
            return await DeleteFileAsync(fileId);
        }

        public async Task<List<AudioFileDto>> GetAudiosByDurationAsync(TimeSpan? minDuration = null, TimeSpan? maxDuration = null)
        {
            var searchDto = new AudioSearchDto
            {
                MinDuration = minDuration,
                MaxDuration = maxDuration,
                PageSize = 1000
            };

            var result = await GetAudiosPagedAsync(searchDto);
            return result.Data?.ToList() ?? new List<AudioFileDto>();
        }

        #endregion

        #region Protected Override Methods

        protected override AudioFileEntity CreateEntityInstance()
        {
            return new AudioFileEntity();
        }

        protected override async Task ValidateTypeSpecificContentAsync(byte[] content, string contentType)
        {
            // Audio validation can be added here if needed
            await Task.CompletedTask;
        }

        protected override async Task SetTypeSpecificPropertiesAsync(AudioFileEntity entity, byte[] content, FileUploadDto uploadDto)
        {
            try
            {
                // Audio metadata extraction can be added here if needed
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set audio-specific properties for {FileName}", entity.OriginalFileName);
            }
        }

        protected override async Task ProcessFileAfterUploadAsync(AudioFileEntity entity, FileUploadDto uploadDto)
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

                _logger.LogDebug("Completed audio processing for {FileName}", entity.OriginalFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process audio file: {FileId}", entity.Id);
                entity.ProcessingStatus = "Failed";
                entity.IsProcessed = false;
                _unitOfWork.Files.Update(entity);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        protected override async Task<AudioFileDto> MapEntityToDto(AudioFileEntity entity)
        {
            var urlSet = _fileUrlBuilder.GenerateFileUrls(entity);

            return new AudioFileDto
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

                // Audio-specific properties
                Duration = entity.Duration
            };
        }

        protected override IQueryable<AudioFileEntity> ApplySearchFilters(IQueryable<AudioFileEntity> query, FileSearchDto searchDto)
        {
            // Apply base filters first
            query = base.ApplySearchFilters(query, searchDto);

            // Apply audio-specific filters
            if (searchDto is AudioSearchDto audioSearchDto)
            {
                if (audioSearchDto.MinDuration.HasValue)
                    query = query.Where(f => f.Duration >= audioSearchDto.MinDuration.Value);

                if (audioSearchDto.MaxDuration.HasValue)
                    query = query.Where(f => f.Duration <= audioSearchDto.MaxDuration.Value);
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