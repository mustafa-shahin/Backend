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
    /// Service for handling document file operations
    /// </summary>
    public class DocumentFileService : BaseFileService<DocumentFileEntity, DocumentFileDto>, IDocumentFileService
    {
        public DocumentFileService(
            IUnitOfWork unitOfWork,
            IFileValidationService fileValidationService,
            IUserSessionService userSessionService,
            IFileUrlBuilder fileUrlBuilder,
            IMapper mapper,
            ILogger<DocumentFileService> logger,
            IConfiguration configuration)
            : base(unitOfWork, fileValidationService, userSessionService, fileUrlBuilder, mapper, logger, configuration)
        {
        }

        protected override FileType HandledFileType => FileType.Document;

        #region IDocumentFileService Implementation

        public async Task<DocumentFileDto> UploadDocumentAsync(FileUploadDto uploadDto)
        {
            return await UploadFileAsync(uploadDto);
        }

        public async Task<List<DocumentFileDto>> UploadMultipleDocumentsAsync(MultipleFileUploadDto uploadDto)
        {
            return await UploadMultipleFilesAsync(uploadDto);
        }

        public async Task<PaginatedResult<DocumentFileDto>> GetDocumentsPagedAsync(DocumentSearchDto searchDto)
        {
            return await GetFilesPagedAsync(searchDto);
        }

        public async Task<DocumentFileDto?> GetDocumentByIdAsync(int fileId)
        {
            return await GetFileByIdAsync(fileId);
        }

        public async Task<DocumentFileDto> UpdateDocumentAsync(int fileId, UpdateDocumentDto updateDto)
        {
            return await UpdateFileAsync(fileId, updateDto);
        }

        public async Task<bool> DeleteDocumentAsync(int fileId)
        {
            return await DeleteFileAsync(fileId);
        }

        public async Task<bool> GenerateThumbnailAsync(int fileId)
        {
            try
            {
                var file = await _unitOfWork.Files.GetByIdAsync(fileId);
                if (file is not DocumentFileEntity documentFile)
                    return false;

                // Document thumbnail generation logic would go here
                documentFile.UpdatedAt = DateTime.UtcNow;
                documentFile.UpdatedByUserId = _userSessionService.GetCurrentUserId();

                _unitOfWork.Files.Update(documentFile);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Generated thumbnail for document {FileId}", fileId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate thumbnail for document {FileId}", fileId);
                return false;
            }
        }

        public async Task<List<DocumentFileDto>> GetDocumentsByPageCountAsync(int? minPages = null, int? maxPages = null)
        {
            var searchDto = new DocumentSearchDto
            {
                MinPageCount = minPages,
                MaxPageCount = maxPages,
                PageSize = 1000
            };

            var result = await GetDocumentsPagedAsync(searchDto);
            return result.Data?.ToList() ?? new List<DocumentFileDto>();
        }

        #endregion

        #region Protected Override Methods

        protected override DocumentFileEntity CreateEntityInstance()
        {
            return new DocumentFileEntity();
        }

        protected override async Task ValidateTypeSpecificContentAsync(byte[] content, string contentType)
        {
            // Document validation can be added here if needed
            await Task.CompletedTask;
        }

        protected override async Task SetTypeSpecificPropertiesAsync(DocumentFileEntity entity, byte[] content, FileUploadDto uploadDto)
        {
            try
            {
                // Document metadata extraction can be added here if needed
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set document-specific properties for {FileName}", entity.OriginalFileName);
            }
        }

        protected override async Task ProcessFileAfterUploadAsync(DocumentFileEntity entity, FileUploadDto uploadDto)
        {
            try
            {
                entity.ProcessingStatus = "Processing";
                _unitOfWork.Files.Update(entity);
                await _unitOfWork.SaveChangesAsync();

                // Generate thumbnail if requested
                if (uploadDto.GenerateThumbnail)
                {
                    // Document thumbnail generation logic would go here
                }

                entity.IsProcessed = true;
                entity.ProcessingStatus = "Completed";
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedByUserId = _userSessionService.GetCurrentUserId();

                _unitOfWork.Files.Update(entity);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogDebug("Completed document processing for {FileName}", entity.OriginalFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process document file: {FileId}", entity.Id);
                entity.ProcessingStatus = "Failed";
                entity.IsProcessed = false;
                _unitOfWork.Files.Update(entity);
                await _unitOfWork.SaveChangesAsync();
            }
        }
  
       

        protected override async Task<DocumentFileDto> MapEntityToDto(DocumentFileEntity entity)
        {
            var urlSet = _fileUrlBuilder.GenerateFileUrls(entity);

            return new DocumentFileDto
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

                // Document-specific properties
                PageCount = entity.PageCount,
                HasThumbnail = entity.ThumbnailContent?.Length > 0
            };
        }

        protected override IQueryable<DocumentFileEntity> ApplySearchFilters(IQueryable<DocumentFileEntity> query, FileSearchDto searchDto)
        {
            // Apply base filters first
            query = base.ApplySearchFilters(query, searchDto);

            // Apply document-specific filters
            if (searchDto is DocumentSearchDto documentSearchDto)
            {
                if (documentSearchDto.MinPageCount.HasValue)
                    query = query.Where(f => f.PageCount >= documentSearchDto.MinPageCount.Value);

                if (documentSearchDto.MaxPageCount.HasValue)
                    query = query.Where(f => f.PageCount <= documentSearchDto.MaxPageCount.Value);

                if (documentSearchDto.HasThumbnail.HasValue)
                {
                    if (documentSearchDto.HasThumbnail.Value)
                        query = query.Where(f => f.ThumbnailContent != null);
                    else
                        query = query.Where(f => f.ThumbnailContent == null);
                }
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