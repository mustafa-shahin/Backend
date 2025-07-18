using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Domain.Factories;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Backend.CMS.Infrastructure.Services
{
    public class FileService : IFileService, IDisposable
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly IFileValidationService _fileValidationService;
        private readonly IUserSessionService _userSessionService;
        private readonly IFileUrlBuilder _fileUrlBuilder;
        private readonly IMapper _mapper;
        private readonly ILogger<FileService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _baseUrl;
        private readonly SemaphoreSlim _uploadSemaphore;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _hashSemaphores;
        private readonly Timer _semaphoreCleanupTimer;
        private readonly int _defaultPageSize;
        private readonly int _maxPageSize;
        private bool _disposed = false;

        public FileService(
            IUnitOfWork unitOfWork,
            IImageProcessingService imageProcessingService,
            IFileValidationService fileValidationService,
            IUserSessionService userSessionService,
            IFileUrlBuilder fileUrlBuilder,
            IMapper mapper,
            ILogger<FileService> logger,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
            _fileValidationService = fileValidationService ?? throw new ArgumentNullException(nameof(fileValidationService));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _fileUrlBuilder = fileUrlBuilder ?? throw new ArgumentNullException(nameof(fileUrlBuilder));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _baseUrl = configuration["FileStorage:BaseUrl"] ?? "/api/v1/files";
            _defaultPageSize = configuration.GetValue("Pagination:DefaultPageSize", 10);
            _maxPageSize = configuration.GetValue("Pagination:MaxPageSize", 100);

            _uploadSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
            _hashSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

            // Cleanup semaphores every 10 minutes
            _semaphoreCleanupTimer = new Timer(CleanupHashSemaphores, null,
                TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        }

        #region File Upload Operations

        public async Task<FileDto> UploadFileAsync(FileUploadDto uploadDto)
        {
            if (uploadDto.File == null || uploadDto.File.Length == 0)
                throw new ArgumentException("File is required");

            // Validate file first
            if (!await ValidateFileAsync(uploadDto.File))
                throw new ArgumentException("File validation failed");

            await _uploadSemaphore.WaitAsync();
            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var fileName = Path.GetFileNameWithoutExtension(uploadDto.File.FileName);
                var cleanFileName = Regex.Replace(fileName, @"[^\w\-]", "_");
                var fileExtension = Path.GetExtension(uploadDto.File.FileName).ToLowerInvariant();
                var fileType = _fileValidationService.GetFileType(uploadDto.File.FileName, uploadDto.File.ContentType);

                var originalFileName = uploadDto.File.FileName;
                var storedFileName = $"{cleanFileName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString()[..8]}{fileExtension}";

                // Read file content efficiently
                byte[] fileContent;
                using (var memoryStream = new MemoryStream((int)uploadDto.File.Length))
                {
                    await uploadDto.File.CopyToAsync(memoryStream);
                    fileContent = memoryStream.ToArray();
                }

                // Verify content integrity
                if (fileContent.Length != uploadDto.File.Length)
                {
                    throw new InvalidOperationException($"File corruption detected during upload. Expected {uploadDto.File.Length} bytes, got {fileContent.Length} bytes");
                }

                // Calculate file hash for duplicate detection
                var fileHash = await CalculateFileHashAsync(fileContent);

                // Check for duplicates with semaphore to prevent race conditions
                var hashSemaphore = GetHashSemaphore(fileHash);
                await hashSemaphore.WaitAsync();
                try
                {
                    var existingFile = await FindDuplicateFileAsync(fileHash);
                    if (existingFile != null)
                    {
                        _logger.LogInformation("Duplicate file detected: {FileName} (Hash: {Hash})",
                            uploadDto.File.FileName, fileHash[..16]);
                        return existingFile;
                    }

                    // Validate image content for image files
                    if (fileType == FileType.Image && !await ValidateImageContentAsync(fileContent))
                    {
                        throw new InvalidOperationException("Uploaded file appears to be corrupted or is not a valid image");
                    }

                    // Create file entity and save to database within transaction
                    var fileEntity = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        // Use factory to create appropriate file entity type
                        var entity = FileEntityFactory.CreateFileEntity(uploadDto.File.ContentType, fileExtension);
                        
                        // Set common properties
                        entity.OriginalFileName = originalFileName;
                        entity.StoredFileName = storedFileName;
                        entity.FileContent = fileContent;
                        entity.ContentType = uploadDto.File.ContentType;
                        entity.FileSize = uploadDto.File.Length;
                        entity.FileExtension = fileExtension;
                        entity.Description = uploadDto.Description;
                        entity.Alt = uploadDto.Alt;
                        entity.IsPublic = uploadDto.IsPublic;
                        entity.FolderId = uploadDto.FolderId;
                        entity.Hash = fileHash;
                        entity.Tags = uploadDto.Tags ?? new Dictionary<string, object>();
                        entity.CreatedByUserId = currentUserId;
                        entity.UpdatedByUserId = currentUserId;
                        entity.IsProcessed = false;
                        entity.ProcessingStatus = "Pending";

                        await _unitOfWork.Files.AddAsync(entity);
                        await _unitOfWork.SaveChangesAsync();

                        // Verify file was saved correctly
                        var savedFile = await _unitOfWork.Files.GetByIdAsync(entity.Id);
                        if (savedFile?.FileContent?.Length != fileContent.Length)
                        {
                            throw new InvalidOperationException("File save failed - integrity check failed");
                        }

                        // Process immediately if requested
                        if (uploadDto.ProcessImmediately && fileType != FileType.Image)
                        {
                            entity.IsProcessed = true;
                            entity.ProcessingStatus = "Completed";
                            _unitOfWork.Files.Update(entity);
                            await _unitOfWork.SaveChangesAsync();
                        }

                        return entity;
                    });

                    // Process image after saving to database
                    if (fileType == FileType.Image && uploadDto.ProcessImmediately)
                    {
                        await ProcessImageFileAsync(fileEntity, uploadDto.GenerateThumbnail);
                    }

                    var fileDto = await MapFileToDto(fileEntity);
                    _logger.LogInformation("File uploaded successfully: {FileName} (ID: {FileId}, Hash: {Hash})",
                        originalFileName, fileEntity.Id, fileHash[..16]);

                    return fileDto;
                }
                finally
                {
                    hashSemaphore.Release();
                }
            }
            finally
            {
                _uploadSemaphore.Release();
            }
        }

        public async Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto)
        {
            if (uploadDto.Files?.Any() != true)
                return new List<FileDto>();

            var results = new ConcurrentBag<FileDto>();
            var errors = new ConcurrentBag<(string fileName, Exception exception)>();

            // Process files in parallel with controlled concurrency
            var semaphore = new SemaphoreSlim(Math.Min(Environment.ProcessorCount, uploadDto.Files.Count()));

            var tasks = uploadDto.Files.Select(async file =>
            {
                if (uploadDto.ProcessInParallel)
                {
                    await semaphore.WaitAsync();
                }

                try
                {
                    var singleUpload = new FileUploadDto
                    {
                        File = file,
                        FolderId = uploadDto.FolderId,
                        IsPublic = uploadDto.IsPublic,
                        GenerateThumbnail = uploadDto.GenerateThumbnails,
                        ProcessImmediately = uploadDto.ProcessImmediately
                    };

                    var result = await UploadFileAsync(singleUpload);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    errors.Add((file.FileName, ex));
                    _logger.LogError(ex, "Failed to upload file: {FileName}", file.FileName);
                }
                finally
                {
                    if (uploadDto.ProcessInParallel)
                    {
                        semaphore.Release();
                    }
                }
            });

            if (uploadDto.ProcessInParallel)
            {
                await Task.WhenAll(tasks);
            }
            else
            {
                // Process sequentially
                foreach (var task in tasks)
                {
                    await task;
                }
            }

            semaphore.Dispose();

            if (errors.Any())
            {
                _logger.LogWarning("Failed to upload {FailedCount} out of {TotalCount} files",
                    errors.Count, uploadDto.Files.Count());
            }

            return results.ToList();
        }

        public async Task<FileDto> UploadFileFromUrlAsync(string url, int? folderId = null, string? description = null)
        {
            if (string.IsNullOrEmpty(url) || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
                throw new ArgumentException("Valid URL is required");

            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5); // Set timeout

                using var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var fileName = Path.GetFileName(new Uri(url).LocalPath);
                if (string.IsNullOrEmpty(fileName) || !Path.HasExtension(fileName))
                    fileName = $"download_{DateTime.UtcNow:yyyyMMddHHmmss}.bin";

                var contentLength = response.Content.Headers.ContentLength ?? 0;
                if (contentLength > _fileValidationService.GetMaxFileSize())
                {
                    throw new InvalidOperationException($"File size ({contentLength} bytes) exceeds maximum allowed size");
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var formFile = new FormFile(memoryStream, 0, memoryStream.Length, "file", fileName)
                {
                    Headers = new HeaderDictionary()
                };
                formFile.ContentType = contentType;

                var uploadDto = new FileUploadDto
                {
                    File = formFile,
                    FolderId = folderId,
                    Description = description ?? $"Downloaded from {url}",
                    GenerateThumbnail = _fileValidationService.IsImageFile(fileName, contentType)
                };

                return await UploadFileAsync(uploadDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file from URL: {Url}", url);
                throw;
            }
        }

        #endregion

        #region File Retrieval Operations with Pagination

        public async Task<PaginatedResult<FileDto>> GetFilesPagedAsync(FileSearchDto searchDto)
        {
            try
            {
                // Validate and normalize pagination parameters
                var pageNumber = Math.Max(1, searchDto.PageNumber);
                var pageSize = Math.Clamp(searchDto.PageSize <= 0 ? _defaultPageSize : searchDto.PageSize, 1, _maxPageSize);

                _logger.LogDebug("Getting paginated files: page {PageNumber}, size {PageSize}", pageNumber, pageSize);

                // Build base query using repository queryable
                var query = _unitOfWork.Files.GetQueryable();

                // Apply search filters efficiently at database level
                query = ApplySearchFilters(query, searchDto);

                // Apply sorting
                query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

                // Get total count for pagination (before applying skip/take)
                var totalCount = await query.CountAsync();

                // Apply pagination at database level
                var files = await query.Where(f => !f.IsDeleted)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Map to DTOs with URLs
                var fileDtos = await MapFilesToDtos(files);

                _logger.LogDebug("Retrieved {Count} files out of {TotalCount} total", files.Count, totalCount);

                return new PaginatedResult<FileDto>(fileDtos, pageNumber, pageSize, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated files");
                throw;
            }
        }

        public async Task<PaginatedResult<FileDto>> SearchFilesPagedAsync(FileSearchDto searchDto)
        {
            // Ensure we have search criteria
            if (string.IsNullOrWhiteSpace(searchDto.SearchTerm) &&
                !searchDto.FileType.HasValue &&
                !searchDto.FolderId.HasValue &&
                !searchDto.IsPublic.HasValue &&
                !searchDto.CreatedFrom.HasValue &&
                !searchDto.CreatedTo.HasValue &&
                !searchDto.MinSize.HasValue &&
                !searchDto.MaxSize.HasValue &&
                (!searchDto.Tags?.Any() ?? true))
            {
                _logger.LogWarning("Search called without any search criteria, returning empty result");
                return PaginatedResult<FileDto>.Empty(searchDto.PageNumber, searchDto.PageSize);
            }

            // Use the same logic as GetFilesPagedAsync since it already handles search
            return await GetFilesPagedAsync(searchDto);
        }

        public async Task<PaginatedResult<FileDto>> GetFilesByFolderPagedAsync(int? folderId, int pageNumber = 1, int pageSize = 10)
        {
            var searchDto = new FileSearchDto
            {
                FolderId = folderId,
                PageNumber = pageNumber,
                PageSize = pageSize <= 0 ? _defaultPageSize : pageSize,
                SortBy = "CreatedAt",
                SortDirection = "Desc"
            };

            return await GetFilesPagedAsync(searchDto);
        }

        public async Task<FileDto> GetFileByIdAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file == null)
                throw new ArgumentException("File not found");

            return await MapFileToDto(file);
        }

        #endregion

        #region File Stream Operations

        public async Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file == null)
                throw new ArgumentException("File not found");

            // Record access and update stats
            await RecordFileAccessAsync(fileId, FileAccessType.Download);
            await UpdateFileStatsAsync(file);

            var stream = new MemoryStream(file.FileContent, false);
            return (stream, file.ContentType, file.OriginalFileName);
        }

        public async Task<ThumbnailResult> GetThumbnailStreamAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);

            if (file == null)
                return new ThumbnailResult { Reason = "NotFound" };

            if (file.IsDeleted)
                return new ThumbnailResult { Reason = "Deleted" };

            // Check for thumbnail content using pattern matching
            byte[]? thumbnailContent = file switch
            {
                ImageFileEntity imageFile => imageFile.ThumbnailContent,
                VideoFileEntity videoFile => videoFile.ThumbnailContent,
                DocumentFileEntity docFile => docFile.ThumbnailContent,
                _ => null
            };

            if (thumbnailContent == null || thumbnailContent.Length == 0)
                return new ThumbnailResult { Reason = "NoThumbnail" };

            await RecordFileAccessAsync(fileId, FileAccessType.Preview);

            return new ThumbnailResult
            {
                Stream = new MemoryStream(thumbnailContent, writable: false),
                ContentType = "image/jpeg",
                FileName = $"thumb_{file.OriginalFileName}"
            };
        }

        #endregion

        #region File Management Operations

        public async Task<FileDto> UpdateFileAsync(int fileId, UpdateFileDto updateDto)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var file = await _unitOfWork.Files.GetByIdAsync(fileId);
                if (file == null)
                    throw new ArgumentException("File not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Apply updates
                file.Description = updateDto.Description;
                file.Alt = updateDto.Alt;
                file.IsPublic = updateDto.IsPublic;
                file.FolderId = updateDto.FolderId;
                file.UpdatedAt = DateTime.UtcNow;
                file.UpdatedByUserId = currentUserId;

                // Merge tags efficiently
                if (updateDto.Tags?.Any() == true)
                {
                    foreach (var tag in updateDto.Tags)
                    {
                        file.Tags[tag.Key] = tag.Value;
                    }
                }

                // Regenerate thumbnail if requested
                if (updateDto.RegenerateThumbnail && file.FileType == FileType.Image)
                {
                    await GenerateThumbnailInternalAsync(file);
                }

                _unitOfWork.Files.Update(file);
                await _unitOfWork.SaveChangesAsync();

                return await MapFileToDto(file);
            });
        }

        public async Task<bool> DeleteFileAsync(int fileId)
        {
            try
            {
                _logger.LogInformation("Starting delete operation for file {FileId}", fileId);

                var currentUserId = _userSessionService.GetCurrentUserId();
                var success = await _unitOfWork.Files.SoftDeleteAsync(fileId, currentUserId);

                if (success)
                {
                    _logger.LogInformation("Successfully deleted file {FileId}", fileId);
                }
                else
                {
                    _logger.LogWarning("File {FileId} not found for deletion", fileId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileId}", fileId);
                throw;
            }
        }

        public async Task<bool> DeleteMultipleFilesAsync(List<int> fileIds)
        {
            if (fileIds?.Any() != true)
                return false;

            try
            {
                _logger.LogInformation("Starting bulk delete operation for {Count} files", fileIds.Count);

                var currentUserId = _userSessionService.GetCurrentUserId();
                var successCount = 0;
                var failureCount = 0;

                // Process deletions in parallel batches to avoid overwhelming the system
                const int batchSize = 10;
                var batches = fileIds.Chunk(batchSize);

                foreach (var batch in batches)
                {
                    var tasks = batch.Select(async fileId =>
                    {
                        try
                        {
                            var success = await _unitOfWork.Files.SoftDeleteAsync(fileId, currentUserId);
                            if (success)
                            {
                                Interlocked.Increment(ref successCount);
                            }
                            else
                            {
                                Interlocked.Increment(ref failureCount);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to delete file: {FileId}", fileId);
                            Interlocked.Increment(ref failureCount);
                        }
                    });

                    await Task.WhenAll(tasks);
                }

                _logger.LogInformation("Bulk delete completed: {SuccessCount} successful, {FailureCount} failed",
                    successCount, failureCount);

                return successCount == fileIds.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk delete operation");
                throw;
            }
        }

        public async Task<FileDto> MoveFileAsync(MoveFileDto moveDto)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var file = await _unitOfWork.Files.GetByIdAsync(moveDto.FileId);
                if (file == null)
                    throw new ArgumentException("File not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                file.FolderId = moveDto.NewFolderId;
                file.UpdatedAt = DateTime.UtcNow;
                file.UpdatedByUserId = currentUserId;

                // Update metadata if requested
                if (moveDto.UpdateMetadata)
                {
                    file.Metadata["lastMoved"] = DateTime.UtcNow;
                    file.Metadata["movedBy"] = currentUserId;
                }

                _unitOfWork.Files.Update(file);
                await _unitOfWork.SaveChangesAsync();

                return await MapFileToDto(file);
            });
        }

        public async Task<FileDto> CopyFileAsync(CopyFileDto copyDto)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var originalFile = await _unitOfWork.Files.GetByIdAsync(copyDto.FileId);
                if (originalFile == null)
                    throw new ArgumentException("File not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                var newFileName = copyDto.NewName ?? $"Copy of {originalFile.OriginalFileName}";
                var newStoredFileName = $"copy_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString()[..8]}{originalFile.FileExtension}";

                // Create new file entity using factory
                var newFile = FileEntityFactory.CreateFileEntity(originalFile.FileType);
                
                // Set common properties
                newFile.OriginalFileName = newFileName;
                newFile.StoredFileName = newStoredFileName;
                newFile.FileContent = (byte[])originalFile.FileContent.Clone();
                newFile.ContentType = originalFile.ContentType;
                newFile.FileSize = originalFile.FileSize;
                newFile.FileExtension = originalFile.FileExtension;
                newFile.Description = originalFile.Description;
                newFile.Alt = originalFile.Alt;
                newFile.IsPublic = originalFile.IsPublic;
                newFile.FolderId = copyDto.DestinationFolderId ?? originalFile.FolderId;
                newFile.Hash = originalFile.Hash;
                newFile.IsProcessed = originalFile.IsProcessed;
                newFile.ProcessingStatus = originalFile.ProcessingStatus;
                newFile.CreatedByUserId = currentUserId;
                newFile.UpdatedByUserId = currentUserId;

                // Copy type-specific properties using pattern matching
                CopyTypeSpecificProperties(originalFile, newFile);

                // Copy metadata and tags if requested
                if (copyDto.CopyMetadata)
                {
                    newFile.Tags = new Dictionary<string, object>(originalFile.Tags);
                    newFile.Metadata = new Dictionary<string, object>(originalFile.Metadata);
                }

                // Copy thumbnail if exists and requested
                if (copyDto.CopyThumbnail)
                {
                    CopyThumbnailContent(originalFile, newFile);
                }

                await _unitOfWork.Files.AddAsync(newFile);
                await _unitOfWork.SaveChangesAsync();

                return await MapFileToDto(newFile);
            });
        }

        public async Task<bool> RenameFileAsync(int fileId, string newName)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var file = await _unitOfWork.Files.GetByIdAsync(fileId);
                if (file == null)
                    return false;

                var currentUserId = _userSessionService.GetCurrentUserId();

                file.OriginalFileName = newName;
                file.UpdatedAt = DateTime.UtcNow;
                file.UpdatedByUserId = currentUserId;

                _unitOfWork.Files.Update(file);
                await _unitOfWork.SaveChangesAsync();

                return true;
            });
        }

        #endregion

        #region File Preview and Processing

        public async Task<FilePreviewDto> GetFilePreviewAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file == null)
                throw new ArgumentException("File not found");

            await RecordFileAccessAsync(fileId, FileAccessType.Preview);

            var urlSet = _fileUrlBuilder.GenerateFileUrls(file);

            var previewDto = new FilePreviewDto
            {
                Id = file.Id,
                OriginalFileName = file.OriginalFileName,
                ContentType = file.ContentType,
                FileType = file.FileType,
                Urls = urlSet,
                Width = GetFileWidth(file),
                Height = GetFileHeight(file),
                Duration = GetFileDuration(file),
                CanPreview = CanPreviewFile(file.FileType, file.ContentType),
                Metadata = file.Metadata
            };

            previewDto.PreviewHtml = GeneratePreviewHtml(file, urlSet);

            return previewDto;
        }

        public async Task<bool> GenerateThumbnailAsync(int fileId)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var file = await _unitOfWork.Files.GetByIdAsync(fileId);
                if (file == null || file.FileType != FileType.Image)
                    return false;

                return await GenerateThumbnailInternalAsync(file);
            });
        }

        private async Task<bool> GenerateThumbnailInternalAsync(BaseFileEntity file)
        {
            try
            {
                var thumbnailBytes = await _imageProcessingService.GenerateThumbnailFromBytesAsync(file.FileContent);

                var currentUserId = _userSessionService.GetCurrentUserId();
                
                // Set thumbnail based on file type
                switch (file)
                {
                    case ImageFileEntity imageFile:
                        imageFile.ThumbnailContent = thumbnailBytes;
                        break;
                    case VideoFileEntity videoFile:
                        videoFile.ThumbnailContent = thumbnailBytes;
                        break;
                    case DocumentFileEntity documentFile:
                        documentFile.ThumbnailContent = thumbnailBytes;
                        break;
                    default:
                        // Other file types don't support thumbnails
                        return false;
                }

                file.UpdatedAt = DateTime.UtcNow;
                file.UpdatedByUserId = currentUserId;

                _unitOfWork.Files.Update(file);
                await _unitOfWork.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate thumbnail for file: {FileId}", file.Id);
                return false;
            }
        }

        public async Task<bool> ProcessFileAsync(int fileId)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var file = await _unitOfWork.Files.GetByIdAsync(fileId);
                if (file == null)
                    return false;

                if (file.FileType == FileType.Image)
                {
                    return await ProcessImageFileAsync(file, true);
                }

                var currentUserId = _userSessionService.GetCurrentUserId();
                file.IsProcessed = true;
                file.ProcessingStatus = "Completed";
                file.UpdatedAt = DateTime.UtcNow;
                file.UpdatedByUserId = currentUserId;

                _unitOfWork.Files.Update(file);
                await _unitOfWork.SaveChangesAsync();

                return true;
            });
        }

        #endregion

        #region File Access and Statistics

        public async Task RecordFileAccessAsync(int fileId, FileAccessType accessType)
        {
            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();

                var fileAccess = new Backend.CMS.Domain.Entities.FileAccess
                {
                    FileId = fileId,
                    UserId = currentUserId,
                    AccessType = accessType,
                    AccessedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                await _unitOfWork.GetRepository<Backend.CMS.Domain.Entities.FileAccess>().AddAsync(fileAccess);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record file access for file {FileId}", fileId);
                // Don't throw - this is non-critical
            }
        }

        public async Task<List<FileDto>> GetRecentFilesAsync(int count = 10)
        {
            var query = _unitOfWork.Files.GetQueryable()
                .OrderByDescending(f => f.CreatedAt)
                .Take(Math.Clamp(count, 1, 50));

            var files = await query.ToListAsync();
            return await MapFilesToDtos(files);
        }

        public async Task<Dictionary<string, object>> GetFileStatisticsAsync()
        {
            var query = _unitOfWork.Files.GetQueryable();

            var totalFiles = await query.CountAsync();
            var totalSize = await query.SumAsync(f => f.FileSize);
            var totalDownloads = await query.SumAsync(f => f.DownloadCount);

            var filesByType = await query
                .GroupBy(f => f.FileType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type.ToString(), x => (object)x.Count);

            var averageFileSize = totalFiles > 0 ? totalSize / totalFiles : 0;

            var lastUpload = await query
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.CreatedAt)
                .FirstOrDefaultAsync();

            return new Dictionary<string, object>
            {
                ["totalFiles"] = totalFiles,
                ["totalSize"] = totalSize,
                ["totalSizeFormatted"] = FormatFileSize(totalSize),
                ["totalDownloads"] = totalDownloads,
                ["averageFileSize"] = averageFileSize,
                ["averageFileSizeFormatted"] = FormatFileSize(averageFileSize),
                ["filesByType"] = filesByType,
                ["lastUpload"] = totalFiles > 0 ? lastUpload : (DateTime?)null,
                ["generatedAt"] = DateTime.UtcNow
            };
        }

        #endregion

        #region File Validation and Utilities

        public async Task<bool> ValidateFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            if (!_fileValidationService.IsAllowedFileType(file.FileName, file.ContentType))
                return false;

            if (!_fileValidationService.IsAllowedFileSize(file.Length))
                return false;

            using var stream = file.OpenReadStream();
            return await _fileValidationService.IsSafeFileAsync(stream, file.FileName);
        }

        public async Task<string> GetFileHashAsync(Stream stream)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = await Task.Run(() => sha256.ComputeHash(stream));
            return Convert.ToBase64String(hashBytes);
        }

        public async Task<bool> FileExistsAsync(int fileId)
        {
            return await _unitOfWork.Files.AnyAsync(f => f.Id == fileId);
        }

        public async Task<long> GetTotalFileSizeAsync(int? folderId = null)
        {
            var query = _unitOfWork.Files.GetQueryable();

            if (folderId.HasValue)
            {
                query = query.Where(f => f.FolderId == folderId.Value);
            }

            return await query.SumAsync(f => f.FileSize);
        }

        #endregion

        #region File Integrity and Diagnostics

        public async Task<bool> VerifyFileIntegrityAsync(int fileId)
        {
            try
            {
                var file = await _unitOfWork.Files.GetByIdAsync(fileId);
                if (file == null)
                {
                    _logger.LogWarning("File verification failed - file not found: {FileId}", fileId);
                    return false;
                }

                // Check if file content exists
                if (file.FileContent == null || file.FileContent.Length == 0)
                {
                    _logger.LogWarning("File verification failed - no content: {FileId}", fileId);
                    return false;
                }

                // Verify file size matches
                if (file.FileContent.Length != file.FileSize)
                {
                    _logger.LogWarning("File verification failed - size mismatch: {FileId}, expected: {ExpectedSize}, actual: {ActualSize}",
                        fileId, file.FileSize, file.FileContent.Length);
                    return false;
                }

                // Verify hash if available
                if (!string.IsNullOrEmpty(file.Hash))
                {
                    var calculatedHash = await CalculateFileHashAsync(file.FileContent);
                    if (calculatedHash != file.Hash)
                    {
                        _logger.LogWarning("File verification failed - hash mismatch: {FileId}, expected: {ExpectedHash}, actual: {ActualHash}",
                            fileId, file.Hash[..16], calculatedHash[..16]);
                        return false;
                    }
                }

                // For image files, verify image integrity
                if (file.FileType == FileType.Image)
                {
                    if (!await ValidateImageContentAsync(file.FileContent))
                    {
                        _logger.LogWarning("File verification failed - invalid image content: {FileId}", fileId);
                        return false;
                    }
                }

                _logger.LogInformation("File integrity verification passed: {FileId}", fileId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file integrity verification: {FileId}", fileId);
                return false;
            }
        }

        #endregion

        #region Bulk Operations

        public async Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto)
        {
            if (fileIds?.Any() != true)
                return false;

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var successCount = 0;

                // Process updates in batches for better performance
                const int batchSize = 50;
                var batches = fileIds.Chunk(batchSize);

                foreach (var batch in batches)
                {
                    var query = _unitOfWork.Files.GetQueryable()
                        .Where(f => batch.Contains(f.Id));

                    var files = await query.ToListAsync();

                    foreach (var file in files)
                    {
                        try
                        {
                            file.Description = updateDto.Description ?? file.Description;
                            file.Alt = updateDto.Alt ?? file.Alt;
                            file.IsPublic = updateDto.IsPublic;
                            file.FolderId = updateDto.FolderId ?? file.FolderId;
                            file.UpdatedAt = DateTime.UtcNow;
                            file.UpdatedByUserId = currentUserId;

                            if (updateDto.Tags?.Any() == true)
                            {
                                foreach (var tag in updateDto.Tags)
                                {
                                    file.Tags[tag.Key] = tag.Value;
                                }
                            }

                            _unitOfWork.Files.Update(file);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to update file: {FileId}", file.Id);
                        }
                    }

                    await _unitOfWork.SaveChangesAsync();
                }

                return successCount == fileIds.Count;
            });
        }

        public async Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId)
        {
            if (fileIds?.Any() != true)
                return false;

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var successCount = 0;

                // Process in batches for better performance
                const int batchSize = 50;
                var batches = fileIds.Chunk(batchSize);

                foreach (var batch in batches)
                {
                    try
                    {
                        var query = _unitOfWork.Files.GetQueryable()
                            .Where(f => batch.Contains(f.Id));

                        var files = await query.ToListAsync();

                        foreach (var file in files)
                        {
                            file.FolderId = destinationFolderId;
                            file.UpdatedAt = DateTime.UtcNow;
                            file.UpdatedByUserId = currentUserId;

                            _unitOfWork.Files.Update(file);
                            successCount++;
                        }

                        await _unitOfWork.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to move files in batch: {BatchFiles}", string.Join(",", batch));
                    }
                }

                return successCount == fileIds.Count;
            });
        }

        public async Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId)
        {
            var copiedFiles = new List<FileDto>();

            foreach (var fileId in fileIds)
            {
                try
                {
                    var copyDto = new CopyFileDto
                    {
                        FileId = fileId,
                        DestinationFolderId = destinationFolderId
                    };

                    var copiedFile = await CopyFileAsync(copyDto);
                    copiedFiles.Add(copiedFile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to copy file: {FileId}", fileId);
                }
            }

            return copiedFiles;
        }

        #endregion

        #region Private Helper Methods

        private IQueryable<BaseFileEntity> ApplySearchFilters(IQueryable<BaseFileEntity> query, FileSearchDto searchDto)
        {
            // Apply filters efficiently at database level
            if (!string.IsNullOrEmpty(searchDto.SearchTerm))
            {
                var searchTermLower = searchDto.SearchTerm.ToLowerInvariant();
                query = query.Where(f => EF.Functions.Like(f.OriginalFileName.ToLower(), $"%{searchTermLower}%") ||
                                        (f.Description != null && EF.Functions.Like(f.Description.ToLower(), $"%{searchTermLower}%")));
            }

            if (searchDto.FileType.HasValue)
                query = query.Where(f => f.FileType == searchDto.FileType.Value);

            if (searchDto.FolderId.HasValue)
                query = query.Where(f => f.FolderId == searchDto.FolderId.Value);

            if (searchDto.IsPublic.HasValue)
                query = query.Where(f => f.IsPublic == searchDto.IsPublic.Value);

            if (searchDto.CreatedFrom.HasValue)
                query = query.Where(f => f.CreatedAt >= searchDto.CreatedFrom.Value);

            if (searchDto.CreatedTo.HasValue)
                query = query.Where(f => f.CreatedAt <= searchDto.CreatedTo.Value);

            if (searchDto.MinSize.HasValue)
                query = query.Where(f => f.FileSize >= searchDto.MinSize.Value);

            if (searchDto.MaxSize.HasValue)
                query = query.Where(f => f.FileSize <= searchDto.MaxSize.Value);

            if (searchDto.UserFilesOnly)
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                if (currentUserId.HasValue)
                {
                    query = query.Where(f => f.CreatedByUserId == currentUserId.Value);
                }
            }

            return query;
        }

        private static IQueryable<BaseFileEntity> ApplySorting(IQueryable<BaseFileEntity> query, string sortBy, string sortDirection)
        {
            var isDescending = sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);

            return sortBy.ToLower() switch
            {
                "name" => isDescending
                    ? query.OrderByDescending(f => f.OriginalFileName)
                    : query.OrderBy(f => f.OriginalFileName),
                "size" => isDescending
                    ? query.OrderByDescending(f => f.FileSize)
                    : query.OrderBy(f => f.FileSize),
                "createdat" => isDescending
                    ? query.OrderByDescending(f => f.CreatedAt)
                    : query.OrderBy(f => f.CreatedAt),
                "updatedat" => isDescending
                    ? query.OrderByDescending(f => f.UpdatedAt)
                    : query.OrderBy(f => f.UpdatedAt),
                "downloads" => isDescending
                    ? query.OrderByDescending(f => f.DownloadCount)
                    : query.OrderBy(f => f.DownloadCount),
                _ => query.OrderByDescending(f => f.CreatedAt)
            };
        }

        private async Task<bool> ValidateImageContentAsync(byte[] content)
        {
            try
            {
                return await _imageProcessingService.IsImageFromBytesAsync(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Image validation failed during upload");
                return false;
            }
        }

        private async Task<string> CalculateFileHashAsync(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = await Task.Run(() => sha256.ComputeHash(content));
            return Convert.ToBase64String(hashBytes);
        }

        private SemaphoreSlim GetHashSemaphore(string hash)
        {
            return _hashSemaphores.GetOrAdd(hash, _ => new SemaphoreSlim(1, 1));
        }

        private void CleanupHashSemaphores(object? state)
        {
            try
            {
                var keysToRemove = new List<string>();

                foreach (var kvp in _hashSemaphores.ToList())
                {
                    if (kvp.Value.CurrentCount == 1) // Not in use
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove.Take(100)) // Limit cleanup
                {
                    if (_hashSemaphores.TryRemove(key, out var semaphore))
                    {
                        semaphore.Dispose();
                    }
                }

                if (keysToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} unused hash semaphores",
                        Math.Min(keysToRemove.Count, 100));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during hash semaphore cleanup");
            }
        }

        private async Task UpdateFileStatsAsync(BaseFileEntity file)
        {
            try
            {
                file.DownloadCount++;
                file.LastAccessedAt = DateTime.UtcNow;
                _unitOfWork.Files.Update(file);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update file stats for file {FileId}", file.Id);
            }
        }

        private async Task<FileDto?> FindDuplicateFileAsync(string hash)
        {
            var file = await _unitOfWork.Files.FirstOrDefaultAsync(f => f.Hash == hash);
            return file != null ? await MapFileToDto(file) : null;
        }

        private async Task<bool> ProcessImageFileAsync(BaseFileEntity file, bool generateThumbnail)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();

            try
            {
                file.ProcessingStatus = "Processing";
                _unitOfWork.Files.Update(file);
                await _unitOfWork.SaveChangesAsync();

                if (await _imageProcessingService.IsImageFromBytesAsync(file.FileContent))
                {
                    var (width, height) = await _imageProcessingService.GetImageDimensionsFromBytesAsync(file.FileContent);
                    
                    // Set image-specific properties if this is an image file
                    if (file is ImageFileEntity imageFile)
                    {
                        imageFile.Width = width;
                        imageFile.Height = height;

                        if (generateThumbnail && (imageFile.ThumbnailContent == null || imageFile.ThumbnailContent.Length == 0))
                        {
                            imageFile.ThumbnailContent = await _imageProcessingService.GenerateThumbnailFromBytesAsync(file.FileContent);
                        }
                    }
                }

                file.IsProcessed = true;
                file.ProcessingStatus = "Completed";
                file.UpdatedAt = DateTime.UtcNow;
                file.UpdatedByUserId = currentUserId;

                _unitOfWork.Files.Update(file);
                await _unitOfWork.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process image file: {FileId}", file.Id);

                file.ProcessingStatus = "Failed";
                file.IsProcessed = false;
                file.UpdatedAt = DateTime.UtcNow;
                file.UpdatedByUserId = currentUserId;
                _unitOfWork.Files.Update(file);
                await _unitOfWork.SaveChangesAsync();

                return false;
            }
        }

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


        private static bool CanPreviewFile(FileType fileType, string contentType)
        {
            return fileType switch
            {
                FileType.Image => true,
                FileType.Video => contentType.StartsWith("video/"),
                FileType.Audio => contentType.StartsWith("audio/"),
                FileType.Document => contentType == "application/pdf" || contentType.Contains("text/"),
                _ => false
            };
        }

        private string GeneratePreviewHtml(BaseFileEntity file, FileUrlsDto urlSet)
        {
            return file.FileType switch
            {
                FileType.Image => $"<img src=\"{urlSet.Download}\" alt=\"{file.Alt ?? file.OriginalFileName}\" style=\"max-width: 100%; height: auto;\" />",
                FileType.Video => $"<video controls style=\"max-width: 100%;\"><source src=\"{urlSet.Download}\" type=\"{file.ContentType}\">Your browser does not support the video tag.</video>",
                FileType.Audio => $"<audio controls><source src=\"{urlSet.Download}\" type=\"{file.ContentType}\">Your browser does not support the audio tag.</audio>",
                FileType.Document when file.ContentType == "application/pdf" => $"<embed src=\"{urlSet.Download}\" type=\"application/pdf\" width=\"100%\" height=\"600px\" />",
                _ => null
            };
        }

        private async Task<FileDto> MapFileToDto(BaseFileEntity file)
        {
            var urlSet = _fileUrlBuilder.GenerateFileUrls(file);
            var hasThumbnail = HasThumbnailContent(file);

            return new FileDto
            {
                Id = file.Id,
                OriginalFileName = file.OriginalFileName,
                StoredFileName = file.StoredFileName,
                ContentType = file.ContentType,
                FileSize = file.FileSize,
                FileSizeFormatted = FormatFileSize(file.FileSize),
                FileExtension = file.FileExtension,
                FileType = file.FileType,
                FileTypeName = file.FileType.ToString(),
                Description = file.Description,
                Alt = file.Alt,
                Metadata = file.Metadata,
                IsPublic = file.IsPublic,
                FolderId = file.FolderId,
                DownloadCount = file.DownloadCount,
                LastAccessedAt = file.LastAccessedAt,
                Width = GetFileWidth(file),
                Height = GetFileHeight(file),
                Duration = GetFileDuration(file),
                DurationFormatted = FormatDuration(GetFileDuration(file)),
                Hash = file.Hash,
                IsProcessed = file.IsProcessed,
                ProcessingStatus = file.ProcessingStatus,
                Tags = file.Tags,
                CreatedAt = file.CreatedAt,
                UpdatedAt = file.UpdatedAt,
                Urls = urlSet,
                HasThumbnail = hasThumbnail,
                CanPreview = CanPreviewFile(file.FileType, file.ContentType)
            };
        }

        private async Task<List<FileDto>> MapFilesToDtos(IEnumerable<BaseFileEntity> files)
        {
            var fileDtos = new List<FileDto>();
            foreach (var file in files)
            {
                fileDtos.Add(await MapFileToDto(file));
            }
            return fileDtos;
        }

        #endregion

        #region Type-Specific Upload Operations

        public async Task<ImageFileDto> UploadImageAsync(FileUploadDto uploadDto)
        {
            var fileDto = await UploadFileAsync(uploadDto);
            return _mapper.Map<ImageFileDto>(fileDto);
        }

        public async Task<VideoFileDto> UploadVideoAsync(FileUploadDto uploadDto)
        {
            var fileDto = await UploadFileAsync(uploadDto);
            return _mapper.Map<VideoFileDto>(fileDto);
        }

        public async Task<AudioFileDto> UploadAudioAsync(FileUploadDto uploadDto)
        {
            var fileDto = await UploadFileAsync(uploadDto);
            return _mapper.Map<AudioFileDto>(fileDto);
        }

        public async Task<DocumentFileDto> UploadDocumentAsync(FileUploadDto uploadDto)
        {
            var fileDto = await UploadFileAsync(uploadDto);
            return _mapper.Map<DocumentFileDto>(fileDto);
        }

        public async Task<ArchiveFileDto> UploadArchiveAsync(FileUploadDto uploadDto)
        {
            var fileDto = await UploadFileAsync(uploadDto);
            return _mapper.Map<ArchiveFileDto>(fileDto);
        }

        #endregion

        #region Type-Specific Retrieval Operations

        public async Task<PaginatedResult<ImageFileDto>> GetImageFilesAsync(ImageSearchDto searchDto)
        {
            var imageFiles = await _unitOfWork.Files.GetImageFilesAsync(searchDto.FolderId);
            var totalCount = imageFiles.Count();
            
            var pagedFiles = imageFiles
                .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .ToList();

            var imageDtos = new List<ImageFileDto>();
            foreach (var file in pagedFiles)
            {
                var fileDto = await MapFileToDto(file);
                imageDtos.Add(_mapper.Map<ImageFileDto>(fileDto));
            }

            return new PaginatedResult<ImageFileDto>(imageDtos, searchDto.PageNumber, searchDto.PageSize, totalCount);
        }

        public async Task<PaginatedResult<VideoFileDto>> GetVideoFilesAsync(VideoSearchDto searchDto)
        {
            var videoFiles = await _unitOfWork.Files.GetVideoFilesAsync(searchDto.FolderId);
            var totalCount = videoFiles.Count();
            
            var pagedFiles = videoFiles
                .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .ToList();

            var videoDtos = new List<VideoFileDto>();
            foreach (var file in pagedFiles)
            {
                var fileDto = await MapFileToDto(file);
                videoDtos.Add(_mapper.Map<VideoFileDto>(fileDto));
            }

            return new PaginatedResult<VideoFileDto>(videoDtos, searchDto.PageNumber, searchDto.PageSize, totalCount);
        }

        public async Task<PaginatedResult<AudioFileDto>> GetAudioFilesAsync(AudioSearchDto searchDto)
        {
            var audioFiles = await _unitOfWork.Files.GetAudioFilesAsync(searchDto.FolderId);
            var totalCount = audioFiles.Count();
            
            var pagedFiles = audioFiles
                .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .ToList();

            var audioDtos = new List<AudioFileDto>();
            foreach (var file in pagedFiles)
            {
                var fileDto = await MapFileToDto(file);
                audioDtos.Add(_mapper.Map<AudioFileDto>(fileDto));
            }

            return new PaginatedResult<AudioFileDto>(audioDtos, searchDto.PageNumber, searchDto.PageSize, totalCount);
        }

        public async Task<PaginatedResult<DocumentFileDto>> GetDocumentFilesAsync(DocumentSearchDto searchDto)
        {
            var documentFiles = await _unitOfWork.Files.GetDocumentFilesAsync(searchDto.FolderId);
            var totalCount = documentFiles.Count();
            
            var pagedFiles = documentFiles
                .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .ToList();

            var documentDtos = new List<DocumentFileDto>();
            foreach (var file in pagedFiles)
            {
                var fileDto = await MapFileToDto(file);
                documentDtos.Add(_mapper.Map<DocumentFileDto>(fileDto));
            }

            return new PaginatedResult<DocumentFileDto>(documentDtos, searchDto.PageNumber, searchDto.PageSize, totalCount);
        }

        public async Task<PaginatedResult<ArchiveFileDto>> GetArchiveFilesAsync(ArchiveSearchDto searchDto)
        {
            var archiveFiles = await _unitOfWork.Files.GetArchiveFilesAsync(searchDto.FolderId);
            var totalCount = archiveFiles.Count();
            
            var pagedFiles = archiveFiles
                .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .ToList();

            var archiveDtos = new List<ArchiveFileDto>();
            foreach (var file in pagedFiles)
            {
                var fileDto = await MapFileToDto(file);
                archiveDtos.Add(_mapper.Map<ArchiveFileDto>(fileDto));
            }

            return new PaginatedResult<ArchiveFileDto>(archiveDtos, searchDto.PageNumber, searchDto.PageSize, totalCount);
        }

        #endregion

        #region Type-Specific Operations

        public async Task<ImageFileDto?> GetImageByIdAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not ImageFileEntity imageFile)
                return null;

            var fileDto = await MapFileToDto(imageFile);
            return _mapper.Map<ImageFileDto>(fileDto);
        }

        public async Task<VideoFileDto?> GetVideoByIdAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not VideoFileEntity videoFile)
                return null;

            var fileDto = await MapFileToDto(videoFile);
            return _mapper.Map<VideoFileDto>(fileDto);
        }

        public async Task<AudioFileDto?> GetAudioByIdAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not AudioFileEntity audioFile)
                return null;

            var fileDto = await MapFileToDto(audioFile);
            return _mapper.Map<AudioFileDto>(fileDto);
        }

        public async Task<DocumentFileDto?> GetDocumentByIdAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not DocumentFileEntity documentFile)
                return null;

            var fileDto = await MapFileToDto(documentFile);
            return _mapper.Map<DocumentFileDto>(fileDto);
        }

        public async Task<ArchiveFileDto?> GetArchiveByIdAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not ArchiveFileEntity archiveFile)
                return null;

            var fileDto = await MapFileToDto(archiveFile);
            return _mapper.Map<ArchiveFileDto>(fileDto);
        }

        public async Task<bool> ExtractImageMetadataAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not ImageFileEntity imageFile)
                return false;

            return await ProcessImageFileAsync(imageFile, false);
        }

        public async Task<bool> GenerateImageThumbnailAsync(int fileId, int width = 200, int height = 200)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not ImageFileEntity imageFile)
                return false;

            return await GenerateThumbnailInternalAsync(imageFile);
        }

        public async Task<bool> ProcessVideoFileAsync(int fileId, TimeSpan? thumbnailTimestamp = null)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not VideoFileEntity videoFile)
                return false;

            // TODO: Implement video processing logic
            _logger.LogInformation("Video processing not yet implemented for file {FileId}", fileId);
            return true;
        }

        public async Task<bool> ExtractAudioMetadataAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not AudioFileEntity audioFile)
                return false;

            // TODO: Implement audio metadata extraction logic
            _logger.LogInformation("Audio metadata extraction not yet implemented for file {FileId}", fileId);
            return true;
        }

        public async Task<bool> ProcessDocumentFileAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not DocumentFileEntity documentFile)
                return false;

            // TODO: Implement document processing logic
            _logger.LogInformation("Document processing not yet implemented for file {FileId}", fileId);
            return true;
        }

        public async Task<bool> AnalyzeArchiveContentsAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not ArchiveFileEntity archiveFile)
                return false;

            // TODO: Implement archive analysis logic
            _logger.LogInformation("Archive analysis not yet implemented for file {FileId}", fileId);
            return true;
        }

        public async Task<bool> TestArchiveIntegrityAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not ArchiveFileEntity archiveFile)
                return false;

            // TODO: Implement archive integrity testing logic
            _logger.LogInformation("Archive integrity testing not yet implemented for file {FileId}", fileId);
            return true;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Checks if a file has thumbnail content
        /// </summary>
        private static bool HasThumbnailContent(BaseFileEntity file)
        {
            return file switch
            {
                ImageFileEntity imageFile => imageFile.ThumbnailContent != null && imageFile.ThumbnailContent.Length > 0,
                VideoFileEntity videoFile => videoFile.ThumbnailContent != null && videoFile.ThumbnailContent.Length > 0,
                DocumentFileEntity docFile => docFile.ThumbnailContent != null && docFile.ThumbnailContent.Length > 0,
                _ => false
            };
        }

        /// <summary>
        /// Gets the width of a file (for images and videos)
        /// </summary>
        private static int? GetFileWidth(BaseFileEntity file)
        {
            return file switch
            {
                ImageFileEntity imageFile => imageFile.Width,
                VideoFileEntity videoFile => videoFile.Width,
                _ => null
            };
        }

        /// <summary>
        /// Gets the height of a file (for images and videos)
        /// </summary>
        private static int? GetFileHeight(BaseFileEntity file)
        {
            return file switch
            {
                ImageFileEntity imageFile => imageFile.Height,
                VideoFileEntity videoFile => videoFile.Height,
                _ => null
            };
        }

        /// <summary>
        /// Gets the duration of a file (for videos and audio)
        /// </summary>
        private static TimeSpan? GetFileDuration(BaseFileEntity file)
        {
            return file switch
            {
                VideoFileEntity videoFile => videoFile.Duration,
                AudioFileEntity audioFile => audioFile.Duration,
                _ => null
            };
        }

        /// <summary>
        /// Copies type-specific properties from source to destination file
        /// </summary>
        private static void CopyTypeSpecificProperties(BaseFileEntity source, BaseFileEntity destination)
        {
            switch (source, destination)
            {
                case (ImageFileEntity sourceImage, ImageFileEntity destImage):
                    destImage.Width = sourceImage.Width;
                    destImage.Height = sourceImage.Height;
                    destImage.ColorProfile = sourceImage.ColorProfile;
                    destImage.DPI = sourceImage.DPI;
                    destImage.HasTransparency = sourceImage.HasTransparency;
                    destImage.CameraModel = sourceImage.CameraModel;
                    destImage.CameraMake = sourceImage.CameraMake;
                    destImage.DateTaken = sourceImage.DateTaken;
                    destImage.Latitude = sourceImage.Latitude;
                    destImage.Longitude = sourceImage.Longitude;
                    destImage.Orientation = sourceImage.Orientation;
                    destImage.ExposureTime = sourceImage.ExposureTime;
                    destImage.FNumber = sourceImage.FNumber;
                    destImage.ISO = sourceImage.ISO;
                    destImage.FocalLength = sourceImage.FocalLength;
                    destImage.IsAnimated = sourceImage.IsAnimated;
                    destImage.FrameCount = sourceImage.FrameCount;
                    break;

                case (VideoFileEntity sourceVideo, VideoFileEntity destVideo):
                    destVideo.Width = sourceVideo.Width;
                    destVideo.Height = sourceVideo.Height;
                    destVideo.Duration = sourceVideo.Duration;
                    destVideo.VideoCodec = sourceVideo.VideoCodec;
                    destVideo.AudioCodec = sourceVideo.AudioCodec;
                    destVideo.FrameRate = sourceVideo.FrameRate;
                    destVideo.Bitrate = sourceVideo.Bitrate;
                    destVideo.AspectRatio = sourceVideo.AspectRatio;
                    destVideo.ThumbnailTimestamp = sourceVideo.ThumbnailTimestamp;
                    destVideo.HasAudio = sourceVideo.HasAudio;
                    destVideo.HasVideo = sourceVideo.HasVideo;
                    destVideo.AudioChannels = sourceVideo.AudioChannels;
                    destVideo.AudioSampleRate = sourceVideo.AudioSampleRate;
                    destVideo.Container = sourceVideo.Container;
                    destVideo.IsHDR = sourceVideo.IsHDR;
                    destVideo.ColorSpace = sourceVideo.ColorSpace;
                    destVideo.RotationAngle = sourceVideo.RotationAngle;
                    destVideo.IsVR360 = sourceVideo.IsVR360;
                    destVideo.HasSubtitles = sourceVideo.HasSubtitles;
                    destVideo.ChapterCount = sourceVideo.ChapterCount;
                    break;

                case (AudioFileEntity sourceAudio, AudioFileEntity destAudio):
                    destAudio.Duration = sourceAudio.Duration;
                    destAudio.AudioCodec = sourceAudio.AudioCodec;
                    destAudio.Bitrate = sourceAudio.Bitrate;
                    destAudio.SampleRate = sourceAudio.SampleRate;
                    destAudio.Channels = sourceAudio.Channels;
                    destAudio.BitDepth = sourceAudio.BitDepth;
                    destAudio.Artist = sourceAudio.Artist;
                    destAudio.Album = sourceAudio.Album;
                    destAudio.Title = sourceAudio.Title;
                    destAudio.Genre = sourceAudio.Genre;
                    destAudio.Year = sourceAudio.Year;
                    destAudio.TrackNumber = sourceAudio.TrackNumber;
                    destAudio.TotalTracks = sourceAudio.TotalTracks;
                    destAudio.Composer = sourceAudio.Composer;
                    destAudio.AlbumArtist = sourceAudio.AlbumArtist;
                    destAudio.AlbumArtFormat = sourceAudio.AlbumArtFormat;
                    destAudio.IsLossless = sourceAudio.IsLossless;
                    destAudio.HasLyrics = sourceAudio.HasLyrics;
                    destAudio.Lyrics = sourceAudio.Lyrics;
                    destAudio.Copyright = sourceAudio.Copyright;
                    destAudio.Comment = sourceAudio.Comment;
                    destAudio.ReplayGain = sourceAudio.ReplayGain;
                    destAudio.Peak = sourceAudio.Peak;
                    break;

                case (DocumentFileEntity sourceDoc, DocumentFileEntity destDoc):
                    destDoc.PageCount = sourceDoc.PageCount;
                    destDoc.Author = sourceDoc.Author;
                    destDoc.DocumentTitle = sourceDoc.DocumentTitle;
                    destDoc.Subject = sourceDoc.Subject;
                    destDoc.Keywords = sourceDoc.Keywords;
                    destDoc.Creator = sourceDoc.Creator;
                    destDoc.Producer = sourceDoc.Producer;
                    destDoc.CreationDate = sourceDoc.CreationDate;
                    destDoc.ModificationDate = sourceDoc.ModificationDate;
                    destDoc.DocumentVersion = sourceDoc.DocumentVersion;
                    destDoc.IsPasswordProtected = sourceDoc.IsPasswordProtected;
                    destDoc.AllowPrinting = sourceDoc.AllowPrinting;
                    destDoc.AllowCopying = sourceDoc.AllowCopying;
                    destDoc.AllowModification = sourceDoc.AllowModification;
                    destDoc.IsDigitallySigned = sourceDoc.IsDigitallySigned;
                    destDoc.SignatureAuthor = sourceDoc.SignatureAuthor;
                    destDoc.SignatureDate = sourceDoc.SignatureDate;
                    destDoc.HasComments = sourceDoc.HasComments;
                    destDoc.HasAnnotations = sourceDoc.HasAnnotations;
                    destDoc.HasBookmarks = sourceDoc.HasBookmarks;
                    destDoc.HasForms = sourceDoc.HasForms;
                    destDoc.HasEmbeddedFiles = sourceDoc.HasEmbeddedFiles;
                    destDoc.Language = sourceDoc.Language;
                    destDoc.ThumbnailPageNumber = sourceDoc.ThumbnailPageNumber;
                    destDoc.WordCount = sourceDoc.WordCount;
                    destDoc.CharacterCount = sourceDoc.CharacterCount;
                    destDoc.ParagraphCount = sourceDoc.ParagraphCount;
                    destDoc.LineCount = sourceDoc.LineCount;
                    destDoc.DocumentFormat = sourceDoc.DocumentFormat;
                    destDoc.IsOptimizedForWeb = sourceDoc.IsOptimizedForWeb;
                    break;

                case (ArchiveFileEntity sourceArchive, ArchiveFileEntity destArchive):
                    destArchive.FileCount = sourceArchive.FileCount;
                    destArchive.UncompressedSize = sourceArchive.UncompressedSize;
                    destArchive.CompressionRatio = sourceArchive.CompressionRatio;
                    destArchive.CompressionMethod = sourceArchive.CompressionMethod;
                    destArchive.IsPasswordProtected = sourceArchive.IsPasswordProtected;
                    destArchive.IsEncrypted = sourceArchive.IsEncrypted;
                    destArchive.EncryptionMethod = sourceArchive.EncryptionMethod;
                    destArchive.IsSelfExtracting = sourceArchive.IsSelfExtracting;
                    destArchive.IsMultiVolume = sourceArchive.IsMultiVolume;
                    destArchive.VolumeCount = sourceArchive.VolumeCount;
                    destArchive.HasComment = sourceArchive.HasComment;
                    destArchive.ArchiveComment = sourceArchive.ArchiveComment;
                    destArchive.CreatedBy = sourceArchive.CreatedBy;
                    destArchive.ArchiveDate = sourceArchive.ArchiveDate;
                    destArchive.IsCorrupted = sourceArchive.IsCorrupted;
                    destArchive.IsTestable = sourceArchive.IsTestable;
                    destArchive.LastTestedAt = sourceArchive.LastTestedAt;
                    destArchive.TestResult = sourceArchive.TestResult;
                    destArchive.TestErrorMessage = sourceArchive.TestErrorMessage;
                    break;

                case (OtherFileEntity sourceOther, OtherFileEntity destOther):
                    destOther.IsSuspicious = sourceOther.IsSuspicious;
                    destOther.IsExecutable = sourceOther.IsExecutable;
                    destOther.HasMacros = sourceOther.HasMacros;
                    destOther.SecurityAnalysisResult = sourceOther.SecurityAnalysisResult;
                    destOther.SecurityScanDate = sourceOther.SecurityScanDate;
                    destOther.ThreatLevel = sourceOther.ThreatLevel;
                    destOther.DetectedFileType = sourceOther.DetectedFileType;
                    destOther.MimeTypeDetected = sourceOther.MimeTypeDetected;
                    destOther.RequiresSpecialHandling = sourceOther.RequiresSpecialHandling;
                    break;
            }
        }

        /// <summary>
        /// Copies thumbnail content from source to destination file
        /// </summary>
        private static void CopyThumbnailContent(BaseFileEntity source, BaseFileEntity destination)
        {
            switch (source, destination)
            {
                case (ImageFileEntity sourceImage, ImageFileEntity destImage):
                    if (sourceImage.ThumbnailContent != null && sourceImage.ThumbnailContent.Length > 0)
                        destImage.ThumbnailContent = (byte[])sourceImage.ThumbnailContent.Clone();
                    break;

                case (VideoFileEntity sourceVideo, VideoFileEntity destVideo):
                    if (sourceVideo.ThumbnailContent != null && sourceVideo.ThumbnailContent.Length > 0)
                        destVideo.ThumbnailContent = (byte[])sourceVideo.ThumbnailContent.Clone();
                    break;

                case (DocumentFileEntity sourceDoc, DocumentFileEntity destDoc):
                    if (sourceDoc.ThumbnailContent != null && sourceDoc.ThumbnailContent.Length > 0)
                        destDoc.ThumbnailContent = (byte[])sourceDoc.ThumbnailContent.Clone();
                    break;
            }
        }

        private static string FormatDuration(TimeSpan? duration)
        {
            if (!duration.HasValue)
                return string.Empty;

            var d = duration.Value;
            if (d.TotalHours >= 1)
                return $"{(int)d.TotalHours:D2}:{d.Minutes:D2}:{d.Seconds:D2}";
            else
                return $"{d.Minutes:D2}:{d.Seconds:D2}";
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _semaphoreCleanupTimer?.Dispose();
                _uploadSemaphore?.Dispose();

                foreach (var semaphore in _hashSemaphores.Values)
                {
                    semaphore.Dispose();
                }
                _hashSemaphores.Clear();

                _disposed = true;
            }
        }

        #endregion
    }
}