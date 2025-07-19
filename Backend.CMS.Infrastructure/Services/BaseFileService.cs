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
    /// <summary>
    /// Base service providing common file operations for all file types
    /// </summary>
    /// <typeparam name="TEntity">The specific file entity type</typeparam>
    /// <typeparam name="TDto">The specific file DTO type</typeparam>
    public abstract class BaseFileService<TEntity, TDto> : IDisposable
        where TEntity : BaseFileEntity
        where TDto : FileDto
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected readonly IFileValidationService _fileValidationService;
        protected readonly IUserSessionService _userSessionService;
        protected readonly IFileUrlBuilder _fileUrlBuilder;
        protected readonly IMapper _mapper;
        protected readonly ILogger<BaseFileService<TEntity, TDto>> _logger;
        protected readonly IConfiguration _configuration;
        protected readonly string _baseUrl;
        protected readonly SemaphoreSlim _uploadSemaphore;
        protected readonly ConcurrentDictionary<string, SemaphoreSlim> _hashSemaphores;
        protected readonly Timer _semaphoreCleanupTimer;
        protected readonly int _defaultPageSize;
        protected readonly int _maxPageSize;
        protected bool _disposed = false;

        protected BaseFileService(
            IUnitOfWork unitOfWork,
            IFileValidationService fileValidationService,
            IUserSessionService userSessionService,
            IFileUrlBuilder fileUrlBuilder,
            IMapper mapper,
            ILogger<BaseFileService<TEntity, TDto>> logger,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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

        /// <summary>
        /// Gets the file type that this service handles
        /// </summary>
        protected abstract FileType HandledFileType { get; }

        #region Public Upload Operations

        /// <summary>
        /// Uploads a file of the specific type handled by this service
        /// </summary>
        public virtual async Task<TDto> UploadFileAsync(FileUploadDto uploadDto)
        {
            if (uploadDto.File == null || uploadDto.File.Length == 0)
                throw new ArgumentException("File is required");

            // Validate file type matches this service
            var detectedFileType = _fileValidationService.GetFileType(uploadDto.File.FileName, uploadDto.File.ContentType);
            if (detectedFileType != HandledFileType)
                throw new ArgumentException($"File type {detectedFileType} is not supported by this service. Expected: {HandledFileType}");

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

                    // Perform type-specific validation
                    await ValidateTypeSpecificContentAsync(fileContent, uploadDto.File.ContentType);

                    // Create file entity and save to database within transaction
                    var fileEntity = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        // Create specific entity type using factory
                        var entity = CreateEntityInstance();
                        
                        // Set common properties
                        SetCommonProperties(entity, uploadDto, originalFileName, storedFileName, 
                            fileContent, uploadDto.File.ContentType, uploadDto.File.Length, 
                            fileExtension, fileHash, currentUserId);

                        // Set type-specific properties
                        await SetTypeSpecificPropertiesAsync(entity, fileContent, uploadDto);

                        await _unitOfWork.Files.AddAsync(entity);
                        await _unitOfWork.SaveChangesAsync();

                        // Verify file was saved correctly
                        var savedFile = await _unitOfWork.Files.GetByIdAsync(entity.Id);
                        if (savedFile?.FileContent?.Length != fileContent.Length)
                        {
                            throw new InvalidOperationException("File save failed - integrity check failed");
                        }

                        // Process file if requested
                        if (uploadDto.ProcessImmediately)
                        {
                            await ProcessFileAfterUploadAsync(entity, uploadDto);
                        }

                        return entity;
                    });

                    var fileDto = await MapEntityToDto(fileEntity);
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

        /// <summary>
        /// Uploads multiple files of the specific type
        /// </summary>
        public virtual async Task<List<TDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto)
        {
            if (uploadDto.Files?.Any() != true)
                return new List<TDto>();

            var results = new ConcurrentBag<TDto>();
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

        #endregion

        #region Public Retrieval Operations

        /// <summary>
        /// Gets paginated files of the specific type
        /// </summary>
        public virtual async Task<PaginatedResult<TDto>> GetFilesPagedAsync(FileSearchDto searchDto)
        {
            try
            {
                // Validate and normalize pagination parameters
                var pageNumber = Math.Max(1, searchDto.PageNumber);
                var pageSize = Math.Clamp(searchDto.PageSize <= 0 ? _defaultPageSize : searchDto.PageSize, 1, _maxPageSize);

                _logger.LogDebug("Getting paginated {FileType} files: page {PageNumber}, size {PageSize}", 
                    HandledFileType, pageNumber, pageSize);

                // Build query for specific file type
                var query = _unitOfWork.Files.GetQueryable()
                    .OfType<TEntity>() // This filters to the specific type
                    .Where(f => !f.IsDeleted);

                // Apply search filters
                query = ApplySearchFilters(query, searchDto);

                // Apply sorting
                query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

                // Get total count for pagination
                var totalCount = await query.CountAsync();

                // Apply pagination
                var files = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Map to DTOs
                var fileDtos = await MapEntitiesToDtos(files);

                _logger.LogDebug("Retrieved {Count} {FileType} files out of {TotalCount} total", 
                    files.Count, HandledFileType, totalCount);

                return new PaginatedResult<TDto>(fileDtos, pageNumber, pageSize, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated {FileType} files", HandledFileType);
                throw;
            }
        }

        /// <summary>
        /// Gets a file by ID, ensuring it's of the correct type
        /// </summary>
        public virtual async Task<TDto?> GetFileByIdAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not TEntity typedFile)
                return null;

            return await MapEntityToDto(typedFile);
        }

        /// <summary>
        /// Gets file stream with access tracking
        /// </summary>
        public virtual async Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file is not TEntity typedFile)
                throw new ArgumentException("File not found or wrong type");

            // Record access and update stats
            await RecordFileAccessAsync(fileId, FileAccessType.Download);
            await UpdateFileStatsAsync(typedFile);

            var stream = new MemoryStream(typedFile.FileContent, false);
            return (stream, typedFile.ContentType, typedFile.OriginalFileName);
        }

        #endregion

        #region Public Management Operations

        /// <summary>
        /// Updates file information
        /// </summary>
        public virtual async Task<TDto> UpdateFileAsync(int fileId, UpdateFileDto updateDto)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var file = await _unitOfWork.Files.GetByIdAsync(fileId);
                if (file is not TEntity typedFile)
                    throw new ArgumentException("File not found or wrong type");

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Apply common updates
                typedFile.Description = updateDto.Description;
                typedFile.Alt = updateDto.Alt;
                typedFile.IsPublic = updateDto.IsPublic;
                typedFile.FolderId = updateDto.FolderId;
                typedFile.UpdatedAt = DateTime.UtcNow;
                typedFile.UpdatedByUserId = currentUserId;

                // Merge tags efficiently
                if (updateDto.Tags?.Any() == true)
                {
                    foreach (var tag in updateDto.Tags)
                    {
                        typedFile.Tags[tag.Key] = tag.Value;
                    }
                }
                _unitOfWork.Files.Update(typedFile);
                await _unitOfWork.SaveChangesAsync();

                return await MapEntityToDto(typedFile);
            });
        }

        /// <summary>
        /// Deletes a file (soft delete)
        /// </summary>
        public virtual async Task<bool> DeleteFileAsync(int fileId)
        {
            try
            {
                _logger.LogInformation("Starting delete operation for {FileType} file {FileId}", HandledFileType, fileId);

                var currentUserId = _userSessionService.GetCurrentUserId();
                var success = await _unitOfWork.Files.SoftDeleteAsync(fileId, currentUserId);

                if (success)
                {
                    _logger.LogInformation("Successfully deleted {FileType} file {FileId}", HandledFileType, fileId);
                }
                else
                {
                    _logger.LogWarning("{FileType} file {FileId} not found for deletion", HandledFileType, fileId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting {FileType} file {FileId}", HandledFileType, fileId);
                throw;
            }
        }

        #endregion

        #region Protected Abstract/Virtual Methods

        /// <summary>
        /// Creates an instance of the specific entity type
        /// </summary>
        protected abstract TEntity CreateEntityInstance();

        /// <summary>
        /// Validates type-specific file content
        /// </summary>
        protected abstract Task ValidateTypeSpecificContentAsync(byte[] content, string contentType);

        /// <summary>
        /// Sets type-specific properties on the entity during upload
        /// </summary>
        protected abstract Task SetTypeSpecificPropertiesAsync(TEntity entity, byte[] content, FileUploadDto uploadDto);

        /// <summary>
        /// Processes the file after upload (e.g., thumbnail generation, metadata extraction)
        /// </summary>
        protected abstract Task ProcessFileAfterUploadAsync(TEntity entity, FileUploadDto uploadDto);


        /// <summary>
        /// Maps entity to DTO with type-specific properties
        /// </summary>
        protected abstract Task<TDto> MapEntityToDto(TEntity entity);

        /// <summary>
        /// Applies type-specific search filters
        /// </summary>
        protected virtual IQueryable<TEntity> ApplySearchFilters(IQueryable<TEntity> query, FileSearchDto searchDto)
        {
            // Apply common filters
            if (!string.IsNullOrEmpty(searchDto.SearchTerm))
            {
                var searchTermLower = searchDto.SearchTerm.ToLowerInvariant();
                query = query.Where(f => EF.Functions.Like(f.OriginalFileName.ToLower(), $"%{searchTermLower}%") ||
                                        (f.Description != null && EF.Functions.Like(f.Description.ToLower(), $"%{searchTermLower}%")));
            }

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

        /// <summary>
        /// Applies sorting to the query
        /// </summary>
        protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, string sortBy, string sortDirection)
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

        #endregion

        #region Protected Helper Methods

        protected void SetCommonProperties(TEntity entity, FileUploadDto uploadDto, 
            string originalFileName, string storedFileName, byte[] fileContent, 
            string contentType, long fileSize, string fileExtension, string fileHash, int? currentUserId)
        {
            entity.OriginalFileName = originalFileName;
            entity.StoredFileName = storedFileName;
            entity.FileContent = fileContent;
            entity.ContentType = contentType;
            entity.FileSize = fileSize;
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
        }

        protected async Task<bool> ValidateFileAsync(IFormFile file)
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

        protected async Task<string> CalculateFileHashAsync(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = await Task.Run(() => sha256.ComputeHash(content));
            return Convert.ToBase64String(hashBytes);
        }

        protected SemaphoreSlim GetHashSemaphore(string hash)
        {
            return _hashSemaphores.GetOrAdd(hash, _ => new SemaphoreSlim(1, 1));
        }

        protected async Task RecordFileAccessAsync(int fileId, FileAccessType accessType)
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
                _logger.LogWarning(ex, "Failed to record file access for {FileType} file {FileId}", HandledFileType, fileId);
                // Don't throw - this is non-critical
            }
        }

        protected async Task UpdateFileStatsAsync(TEntity file)
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
                _logger.LogWarning(ex, "Failed to update file stats for {FileType} file {FileId}", HandledFileType, file.Id);
            }
        }

        protected async Task<TDto?> FindDuplicateFileAsync(string hash)
        {
            var file = await _unitOfWork.Files.GetQueryable()
                .OfType<TEntity>()
                .FirstOrDefaultAsync(f => f.Hash == hash);
            return file != null ? await MapEntityToDto(file) : null;
        }

        protected async Task<List<TDto>> MapEntitiesToDtos(IEnumerable<TEntity> entities)
        {
            var dtos = new List<TDto>();
            foreach (var entity in entities)
            {
                dtos.Add(await MapEntityToDto(entity));
            }
            return dtos;
        }

        protected void CleanupHashSemaphores(object? state)
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