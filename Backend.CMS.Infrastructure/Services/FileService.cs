using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Linq.Expressions;

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

            _baseUrl = configuration["FileStorage:BaseUrl"] ?? "/api/files";
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
                        var entity = new FileEntity
                        {
                            OriginalFileName = originalFileName,
                            StoredFileName = storedFileName,
                            FileContent = fileContent,
                            ContentType = uploadDto.File.ContentType,
                            FileSize = uploadDto.File.Length,
                            FileExtension = fileExtension,
                            FileType = fileType,
                            Description = uploadDto.Description,
                            Alt = uploadDto.Alt,
                            IsPublic = uploadDto.IsPublic,
                            FolderId = uploadDto.FolderId,
                            Hash = fileHash,
                            Tags = uploadDto.Tags ?? new Dictionary<string, object>(),
                            CreatedByUserId = currentUserId,
                            UpdatedByUserId = currentUserId,
                            IsProcessed = false,
                            ProcessingStatus = "Pending"
                        };

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
                // Validate pagination parameters
                var pageNumber = Math.Max(1, searchDto.PageNumber);
                var pageSize = Math.Clamp(searchDto.PageSize, 1, 100);

                // Build query using EF queryable
                var query = BuildFileQuery(searchDto);

                // Apply sorting
                query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

                // Get total count for pagination
                var totalCount = await query.CountAsync();

                // Apply pagination and get results
                var files = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Map to DTOs with URLs
                var fileDtos = await MapFilesToDtos(files);

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
            // SearchFilesPagedAsync uses the same logic as GetFilesPagedAsync
            // but with more emphasis on search functionality
            return await GetFilesPagedAsync(searchDto);
        }

        public async Task<PaginatedResult<FileDto>> GetFilesByFolderPagedAsync(int? folderId, int pageNumber = 1, int pageSize = 10)
        {
            var searchDto = new FileSearchDto
            {
                FolderId = folderId,
                PageNumber = pageNumber,
                PageSize = pageSize,
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

            if (file.ThumbnailContent == null || file.ThumbnailContent.Length == 0)
                return new ThumbnailResult { Reason = "NoThumbnail" };

            await RecordFileAccessAsync(fileId, FileAccessType.Preview);

            return new ThumbnailResult
            {
                Stream = new MemoryStream(file.ThumbnailContent, writable: false),
                ContentType = "image/jpeg",
                FileName = $"thumb_{file.OriginalFileName}"
            };
        }


        #endregion

        #region File Management Operations - FIXED DEADLOCK ISSUES

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

                // Use single repository call to avoid nested semaphore acquisition
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

                    // Process deletions in parallel batches to avoid overwhelming the semaphore
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

                // Create new file entity with copied content
                var newFile = new FileEntity
                {
                    OriginalFileName = newFileName,
                    StoredFileName = newStoredFileName,
                    FileContent = (byte[])originalFile.FileContent.Clone(),
                    ContentType = originalFile.ContentType,
                    FileSize = originalFile.FileSize,
                    FileExtension = originalFile.FileExtension,
                    FileType = originalFile.FileType,
                    Description = originalFile.Description,
                    Alt = originalFile.Alt,
                    IsPublic = originalFile.IsPublic,
                    FolderId = copyDto.DestinationFolderId ?? originalFile.FolderId,
                    Hash = originalFile.Hash,
                    Width = originalFile.Width,
                    Height = originalFile.Height,
                    Duration = originalFile.Duration,
                    IsProcessed = originalFile.IsProcessed,
                    ProcessingStatus = originalFile.ProcessingStatus,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                // Copy metadata and tags if requested
                if (copyDto.CopyMetadata)
                {
                    newFile.Tags = new Dictionary<string, object>(originalFile.Tags);
                    newFile.Metadata = new Dictionary<string, object>(originalFile.Metadata);
                }

                // Copy thumbnail if exists and requested
                if (copyDto.CopyThumbnail && originalFile.ThumbnailContent != null && originalFile.ThumbnailContent.Length > 0)
                {
                    newFile.ThumbnailContent = (byte[])originalFile.ThumbnailContent.Clone();
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
                Urls = new FileUrlsDto
                {
                    Download = urlSet.DownloadUrl,
                    Preview = urlSet.PreviewUrl,
                    Thumbnail = urlSet.ThumbnailUrl,
                    DirectAccess = urlSet.DirectAccessUrl,
                    Additional = urlSet.AdditionalUrls
                },
                Width = file.Width,
                Height = file.Height,
                Duration = file.Duration,
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

        private async Task<bool> GenerateThumbnailInternalAsync(FileEntity file)
        {
            try
            {
                var thumbnailBytes = await _imageProcessingService.GenerateThumbnailFromBytesAsync(file.FileContent);

                var currentUserId = _userSessionService.GetCurrentUserId();
                file.ThumbnailContent = thumbnailBytes;
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

        #region Bulk Operations - FIXED DEADLOCK ISSUES

        public async Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto)
        {
            if (fileIds?.Any() != true)
                return false;

            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var successCount = 0;

                // Process updates in batches
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

        private IQueryable<FileEntity> BuildFileQuery(FileSearchDto searchDto)
        {
            var query = _unitOfWork.Files.GetQueryable();

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

            return query.Where(f => !f.IsDeleted);
        }

        private static IQueryable<FileEntity> ApplySorting(IQueryable<FileEntity> query, string sortBy, string sortDirection)
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

        private async Task UpdateFileStatsAsync(FileEntity file)
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

        private async Task<bool> ProcessImageFileAsync(FileEntity file, bool generateThumbnail)
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
                    file.Width = width;
                    file.Height = height;

                    if (generateThumbnail && (file.ThumbnailContent == null || file.ThumbnailContent.Length == 0))
                    {
                        file.ThumbnailContent = await _imageProcessingService.GenerateThumbnailFromBytesAsync(file.FileContent);
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

        private static string FormatDuration(TimeSpan? duration)
        {
            if (!duration.HasValue) return string.Empty;

            var d = duration.Value;
            if (d.TotalHours >= 1)
            {
                return d.ToString(@"h\:mm\:ss");
            }
            return d.ToString(@"m\:ss");
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

        private string GeneratePreviewHtml(FileEntity file, FileUrlSet urlSet)
        {
            return file.FileType switch
            {
                FileType.Image => $"<img src=\"{urlSet.DownloadUrl}\" alt=\"{file.Alt ?? file.OriginalFileName}\" style=\"max-width: 100%; height: auto;\" />",
                FileType.Video => $"<video controls style=\"max-width: 100%;\"><source src=\"{urlSet.DownloadUrl}\" type=\"{file.ContentType}\">Your browser does not support the video tag.</video>",
                FileType.Audio => $"<audio controls><source src=\"{urlSet.DownloadUrl}\" type=\"{file.ContentType}\">Your browser does not support the audio tag.</audio>",
                FileType.Document when file.ContentType == "application/pdf" => $"<embed src=\"{urlSet.DownloadUrl}\" type=\"application/pdf\" width=\"100%\" height=\"600px\" />",
                _ => null
            };
        }

        private async Task<FileDto> MapFileToDto(FileEntity file)
        {
            var urlSet = _fileUrlBuilder.GenerateFileUrls(file);
            var hasThumbnail = file.ThumbnailContent != null && file.ThumbnailContent.Length > 0;

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
                Width = file.Width,
                Height = file.Height,
                Duration = file.Duration,
                DurationFormatted = FormatDuration(file.Duration),
                Hash = file.Hash,
                IsProcessed = file.IsProcessed,
                ProcessingStatus = file.ProcessingStatus,
                Tags = file.Tags,
                CreatedAt = file.CreatedAt,
                UpdatedAt = file.UpdatedAt,
                Urls = new FileUrlsDto
                {
                    Download = urlSet.DownloadUrl,
                    Preview = urlSet.PreviewUrl,
                    Thumbnail = urlSet.ThumbnailUrl,
                    DirectAccess = urlSet.DirectAccessUrl,
                    Additional = urlSet.AdditionalUrls
                },
                HasThumbnail = hasThumbnail,
                CanPreview = CanPreviewFile(file.FileType, file.ContentType)
            };
        }

        private async Task<List<FileDto>> MapFilesToDtos(IEnumerable<FileEntity> files)
        {
            var fileDtos = new List<FileDto>();
            foreach (var file in files)
            {
                fileDtos.Add(await MapFileToDto(file));
            }
            return fileDtos;
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