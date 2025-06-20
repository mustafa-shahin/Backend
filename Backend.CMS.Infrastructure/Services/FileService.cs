﻿using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Backend.CMS.Infrastructure.Services
{
    public class FileService : IFileService
    {
        private readonly IRepository<FileEntity> _fileRepository;
        private readonly IRepository<Backend.CMS.Domain.Entities.FileAccess> _fileAccessRepository;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly IFileValidationService _fileValidationService;
        private readonly IUserSessionService _userSessionService;
        private readonly IMapper _mapper;
        private readonly ILogger<FileService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _baseUrl;

        public FileService(
            IRepository<FileEntity> fileRepository,
            IRepository<Backend.CMS.Domain.Entities.FileAccess> fileAccessRepository,
            IImageProcessingService imageProcessingService,
            IFileValidationService fileValidationService,
            IUserSessionService userSessionService,
            IMapper mapper,
            ILogger<FileService> logger,
            IConfiguration configuration)
        {
            _fileRepository = fileRepository;
            _fileAccessRepository = fileAccessRepository;
            _imageProcessingService = imageProcessingService;
            _fileValidationService = fileValidationService;
            _userSessionService = userSessionService;
            _mapper = mapper;
            _logger = logger;
            _configuration = configuration;
            _baseUrl = configuration["FileStorage:BaseUrl"] ?? "/api/files";
        }

        public async Task<FileDto> UploadFileAsync(FileUploadDto uploadDto)
        {


            if (uploadDto.File == null || uploadDto.File.Length == 0)
                throw new ArgumentException("File is required");

            // Validate file
            if (!await ValidateFileAsync(uploadDto.File))
                throw new ArgumentException("File validation failed");

            var currentUserId = _userSessionService.GetCurrentUserId();
            var fileName = Path.GetFileNameWithoutExtension(uploadDto.File.FileName);
            var cleanFileName = Regex.Replace(fileName, @"[^\w\-]", "_");
            var fileExtension = Path.GetExtension(uploadDto.File.FileName).ToLowerInvariant();
            var fileType = _fileValidationService.GetFileType(uploadDto.File.FileName, uploadDto.File.ContentType);

            var originalFileName = uploadDto.File.FileName;
            var storedFileName = $"{cleanFileName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}[..8]{fileExtension}";

            // Read file content into byte array
            byte[] fileContent;
            using (var memoryStream = new MemoryStream())
            {
                await uploadDto.File.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            //  Verify the uploaded content is not corrupted
            if (fileContent.Length != uploadDto.File.Length)
            {
                throw new InvalidOperationException($"File corruption detected during upload. Expected {uploadDto.File.Length} bytes, got {fileContent.Length} bytes");
            }

            // For images, verify the content is actually an image
            if (fileType == FileType.Image)
            {
                try
                {
                    var isValidImage = await _imageProcessingService.IsImageFromBytesAsync(fileContent);
                    if (!isValidImage)
                    {
                        throw new InvalidOperationException("Uploaded file appears to be corrupted or is not a valid image");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Image validation failed for uploaded file: {FileName}", uploadDto.File.FileName);
                    throw new InvalidOperationException("Image validation failed - file may be corrupted");
                }
            }

            // Calculate file hash for duplicate detection
            var fileHash = GetFileHash(fileContent);

            // Check for duplicates
            var existingFile = await FindDuplicateFileAsync(fileHash);
            if (existingFile != null)
            {
                _logger.LogInformation("Duplicate file detected: {FileName}", uploadDto.File.FileName);
                return existingFile;
            }

            // Create file entity
            var fileEntity = new FileEntity
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

            // Add to database first
            await _fileRepository.AddAsync(fileEntity);
            await _fileRepository.SaveChangesAsync();
            var savedFile = await _fileRepository.GetByIdAsync(fileEntity.Id);
            if (savedFile?.FileContent?.Length != fileContent.Length)
            {
                _logger.LogError("File integrity check failed after save. Expected {Expected} bytes, got {Actual} bytes",
                    fileContent.Length, savedFile?.FileContent?.Length ?? 0);
                throw new InvalidOperationException("File save failed - integrity check failed");
            }
            // Process image after saving to database
            if (fileType == FileType.Image)
            {
                try
                {
                    await ProcessImageFileAsync(fileEntity, uploadDto.GenerateThumbnail);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process image during upload: {FileName}", originalFileName);
                    fileEntity.ProcessingStatus = "Failed";
                    fileEntity.IsProcessed = false;
                    _fileRepository.Update(fileEntity);
                    await _fileRepository.SaveChangesAsync();
                }
            }
            else
            {
                fileEntity.IsProcessed = true;
                fileEntity.ProcessingStatus = "Completed";
                _fileRepository.Update(fileEntity);
                await _fileRepository.SaveChangesAsync();
            }

            var fileDto = await MapFileToDto(fileEntity);

            _logger.LogInformation("File uploaded successfully: {FileName} (ID: {FileId})",
                originalFileName, fileEntity.Id);

            return fileDto;
        }

        public async Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto)
        {
            var results = new List<FileDto>();

            foreach (var file in uploadDto.Files)
            {
                try
                {
                    var singleUpload = new FileUploadDto
                    {
                        File = file,
                        FolderId = uploadDto.FolderId,
                        IsPublic = uploadDto.IsPublic,
                        GenerateThumbnail = uploadDto.GenerateThumbnails
                    };

                    var result = await UploadFileAsync(singleUpload);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload file: {FileName}", file.FileName);
                }
            }

            return results;
        }

        public async Task<FileDto> UploadFileFromUrlAsync(string url, int? folderId = null, string? description = null)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL is required");

            try
            {
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var fileName = Path.GetFileName(new Uri(url).LocalPath);
                if (string.IsNullOrEmpty(fileName))
                    fileName = $"download_{DateTime.UtcNow:yyyyMMddHHmmss}";

                using var stream = await response.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var formFile = new FormFile(memoryStream, 0, memoryStream.Length, "file", fileName)
                {
                    Headers = new HeaderDictionary(),
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

        public async Task<FileDto> GetFileByIdAsync(int fileId)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
                throw new ArgumentException("File not found");

            return await MapFileToDto(file);
        }

        public async Task<List<FileDto>> GetFilesAsync(int page = 1, int pageSize = 20)
        {
            var files = await _fileRepository.GetPagedAsync(page, pageSize);
            return await MapFilesToDtos(files);
        }

        public async Task<List<FileDto>> GetFilesByFolderAsync(int? folderId, int page = 1, int pageSize = 20)
        {
            var files = await _fileRepository.FindAsync(f => f.FolderId == folderId);
            var pagedFiles = files.Skip((page - 1) * pageSize).Take(pageSize);
            return await MapFilesToDtos(pagedFiles);
        }

        public async Task<List<FileDto>> SearchFilesAsync(FileSearchDto searchDto)
        {
            var query = await _fileRepository.GetAllAsync();
            var files = query.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchDto.SearchTerm))
            {
                files = files.Where(f => f.OriginalFileName.Contains(searchDto.SearchTerm) ||
                                        (f.Description != null && f.Description.Contains(searchDto.SearchTerm)));
            }

            if (searchDto.FileType.HasValue)
            {
                files = files.Where(f => f.FileType == searchDto.FileType.Value);
            }

            if (searchDto.FolderId.HasValue)
            {
                files = files.Where(f => f.FolderId == searchDto.FolderId.Value);
            }

            if (searchDto.IsPublic.HasValue)
            {
                files = files.Where(f => f.IsPublic == searchDto.IsPublic.Value);
            }

            if (searchDto.CreatedFrom.HasValue)
            {
                files = files.Where(f => f.CreatedAt >= searchDto.CreatedFrom.Value);
            }

            if (searchDto.CreatedTo.HasValue)
            {
                files = files.Where(f => f.CreatedAt <= searchDto.CreatedTo.Value);
            }

            if (searchDto.MinSize.HasValue)
            {
                files = files.Where(f => f.FileSize >= searchDto.MinSize.Value);
            }

            if (searchDto.MaxSize.HasValue)
            {
                files = files.Where(f => f.FileSize <= searchDto.MaxSize.Value);
            }

            // Apply sorting
            files = searchDto.SortBy.ToLower() switch
            {
                "name" => searchDto.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase)
                    ? files.OrderByDescending(f => f.OriginalFileName)
                    : files.OrderBy(f => f.OriginalFileName),
                "size" => searchDto.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase)
                    ? files.OrderByDescending(f => f.FileSize)
                    : files.OrderBy(f => f.FileSize),
                "createdat" => searchDto.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase)
                    ? files.OrderByDescending(f => f.CreatedAt)
                    : files.OrderBy(f => f.CreatedAt),
                _ => files.OrderByDescending(f => f.CreatedAt)
            };

            // Apply pagination
            var pagedFiles = files.Skip((searchDto.Page - 1) * searchDto.PageSize)
                                 .Take(searchDto.PageSize);

            return await MapFilesToDtos(pagedFiles);
        }

      public async Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int fileId)
{
    var file = await _fileRepository.GetByIdAsync(fileId);
    if (file == null)
        throw new ArgumentException("File not found");

    // Record access
    await RecordFileAccessAsync(fileId, FileAccessType.Download);

    // Update download count and last accessed
    file.DownloadCount++;
    file.LastAccessedAt = DateTime.UtcNow;
    _fileRepository.Update(file);
    await _fileRepository.SaveChangesAsync();

    var stream = new MemoryStream(file.FileContent);
    stream.Position = 0;
    
    return (stream, file.ContentType, file.OriginalFileName);
}

        public async Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int fileId)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
                throw new ArgumentException("File not found");

            if (file.ThumbnailContent == null || file.ThumbnailContent.Length == 0)
                throw new ArgumentException("Thumbnail not available for this file");

            await RecordFileAccessAsync(fileId, FileAccessType.Preview);

            var stream = new MemoryStream(file.ThumbnailContent);
            stream.Position = 0; 
            return (stream, "image/jpeg", $"thumb_{file.OriginalFileName}");
        }

        public async Task<FileDto> UpdateFileAsync(int fileId, UpdateFileDto updateDto)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
                throw new ArgumentException("File not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            file.Description = updateDto.Description;
            file.Alt = updateDto.Alt;
            file.IsPublic = updateDto.IsPublic;
            file.Tags = updateDto.Tags ?? file.Tags;
            file.FolderId = updateDto.FolderId;
            file.UpdatedAt = DateTime.UtcNow;
            file.UpdatedByUserId = currentUserId;

            _fileRepository.Update(file);
            await _fileRepository.SaveChangesAsync();

            return await MapFileToDto(file);
        }

        public async Task<bool> DeleteFileAsync(int fileId)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
                return false;

            var currentUserId = _userSessionService.GetCurrentUserId();

            // Soft delete from database (file content will be deleted with the entity)
            return await _fileRepository.SoftDeleteAsync(fileId, currentUserId);
        }

        public async Task<bool> DeleteMultipleFilesAsync(List<int> fileIds)
        {
            var results = new List<bool>();

            foreach (var fileId in fileIds)
            {
                try
                {
                    var result = await DeleteFileAsync(fileId);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete file: {FileId}", fileId);
                    results.Add(false);
                }
            }

            return results.All(r => r);
        }

        public async Task<FileDto> MoveFileAsync(MoveFileDto moveDto)
        {
            var file = await _fileRepository.GetByIdAsync(moveDto.FileId);
            if (file == null)
                throw new ArgumentException("File not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            file.FolderId = moveDto.NewFolderId;
            file.UpdatedAt = DateTime.UtcNow;
            file.UpdatedByUserId = currentUserId;

            _fileRepository.Update(file);
            await _fileRepository.SaveChangesAsync();

            return await MapFileToDto(file);
        }

        public async Task<FileDto> CopyFileAsync(CopyFileDto copyDto)
        {
            var originalFile = await _fileRepository.GetByIdAsync(copyDto.FileId);
            if (originalFile == null)
                throw new ArgumentException("File not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            var newFileName = copyDto.NewName ?? $"Copy of {originalFile.OriginalFileName}";
            var newStoredFileName = $"copy_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}[..8]{originalFile.FileExtension}";

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
                Tags = new Dictionary<string, object>(originalFile.Tags),
                Width = originalFile.Width,
                Height = originalFile.Height,
                Duration = originalFile.Duration,
                IsProcessed = originalFile.IsProcessed,
                ProcessingStatus = originalFile.ProcessingStatus,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            // Copy thumbnail if exists
            if (originalFile.ThumbnailContent != null && originalFile.ThumbnailContent.Length > 0)
            {
                newFile.ThumbnailContent = (byte[])originalFile.ThumbnailContent.Clone();
            }

            await _fileRepository.AddAsync(newFile);
            await _fileRepository.SaveChangesAsync();

            return await MapFileToDto(newFile);
        }

        public async Task<bool> RenameFileAsync(int fileId, string newName)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
                return false;

            var currentUserId = _userSessionService.GetCurrentUserId();

            file.OriginalFileName = newName;
            file.UpdatedAt = DateTime.UtcNow;
            file.UpdatedByUserId = currentUserId;

            _fileRepository.Update(file);
            await _fileRepository.SaveChangesAsync();

            return true;
        }

        public async Task<FilePreviewDto> GetFilePreviewAsync(int fileId)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
                throw new ArgumentException("File not found");

            await RecordFileAccessAsync(fileId, FileAccessType.Preview);

            var previewDto = new FilePreviewDto
            {
                Id = file.Id,
                OriginalFileName = file.OriginalFileName,
                ContentType = file.ContentType,
                FileType = file.FileType,
                FileUrl = GenerateFileUrl(file.Id),
                ThumbnailUrl = file.ThumbnailContent != null && file.ThumbnailContent.Length > 0
                    ? GenerateThumbnailUrl(file.Id)
                    : null,
                Width = file.Width,
                Height = file.Height,
                Duration = file.Duration,
                CanPreview = CanPreviewFile(file.FileType, file.ContentType)
            };

            previewDto.PreviewHtml = GeneratePreviewHtml(file);

            return previewDto;
        }

        public async Task<string> GenerateFileUrlAsync(int fileId, bool thumbnail = false)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
                throw new ArgumentException("File not found");

            if (thumbnail && file.ThumbnailContent != null && file.ThumbnailContent.Length > 0)
            {
                return GenerateThumbnailUrl(fileId);
            }

            return GenerateFileUrl(fileId);
        }

        public async Task<bool> GenerateThumbnailAsync(int fileId)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null || file.FileType != FileType.Image)
                return false;

            try
            {
                var thumbnailBytes = await _imageProcessingService.GenerateThumbnailFromBytesAsync(file.FileContent);

                var currentUserId = _userSessionService.GetCurrentUserId();
                file.ThumbnailContent = thumbnailBytes;
                file.UpdatedAt = DateTime.UtcNow;
                file.UpdatedByUserId = currentUserId;

                _fileRepository.Update(file);
                await _fileRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate thumbnail for file: {FileId}", fileId);
                return false;
            }
        }

        public async Task<bool> ProcessFileAsync(int fileId)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
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

            _fileRepository.Update(file);
            await _fileRepository.SaveChangesAsync();

            return true;
        }

        public async Task<List<FileDto>> GetRecentFilesAsync(int count = 10)
        {
            var files = await _fileRepository.GetAllAsync();
            var recentFiles = files.OrderByDescending(f => f.CreatedAt).Take(count);
            return await MapFilesToDtos(recentFiles);
        }

        public async Task<Dictionary<string, object>> GetFileStatisticsAsync()
        {
            var files = await _fileRepository.GetAllAsync();

            var totalFiles = files.Count();
            var totalSize = files.Sum(f => f.FileSize);
            var totalDownloads = files.Sum(f => f.DownloadCount);

            var filesByType = files.GroupBy(f => f.FileType)
                                  .ToDictionary(g => g.Key.ToString(), g => g.Count());

            var averageFileSize = totalFiles > 0 ? totalSize / totalFiles : 0;

            return new Dictionary<string, object>
            {
                ["totalFiles"] = totalFiles,
                ["totalSize"] = totalSize,
                ["totalSizeFormatted"] = FormatFileSize(totalSize),
                ["totalDownloads"] = totalDownloads,
                ["averageFileSize"] = averageFileSize,
                ["averageFileSizeFormatted"] = FormatFileSize(averageFileSize),
                ["filesByType"] = filesByType,
                ["lastUpload"] = files.Any() ? files.Max(f => f.CreatedAt) : (DateTime?)null
            };
        }

        public async Task RecordFileAccessAsync(int fileId, FileAccessType accessType)
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

            await _fileAccessRepository.AddAsync(fileAccess);
            await _fileAccessRepository.SaveChangesAsync();
        }

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

        public string GetFileHash(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(content);
            return Convert.ToBase64String(hashBytes);
        }

        public async Task<bool> FileExistsAsync(int fileId)
        {
            return await _fileRepository.AnyAsync(f => f.Id == fileId);
        }

        public async Task<long> GetTotalFileSizeAsync(int? folderId = null)
        {
            var files = folderId.HasValue
                ? await _fileRepository.FindAsync(f => f.FolderId == folderId.Value)
                : await _fileRepository.GetAllAsync();

            return files.Sum(f => f.FileSize);
        }

        public async Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();
            var success = true;

            foreach (var fileId in fileIds)
            {
                try
                {
                    var file = await _fileRepository.GetByIdAsync(fileId);
                    if (file != null)
                    {
                        file.Description = updateDto.Description ?? file.Description;
                        file.Alt = updateDto.Alt ?? file.Alt;
                        file.IsPublic = updateDto.IsPublic;
                        file.FolderId = updateDto.FolderId ?? file.FolderId;

                        if (updateDto.Tags != null)
                        {
                            foreach (var tag in updateDto.Tags)
                            {
                                file.Tags[tag.Key] = tag.Value;
                            }
                        }

                        file.UpdatedAt = DateTime.UtcNow;
                        file.UpdatedByUserId = currentUserId;

                        _fileRepository.Update(file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update file: {FileId}", fileId);
                    success = false;
                }
            }

            await _fileRepository.SaveChangesAsync();
            return success;
        }

        public async Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();
            var success = true;

            foreach (var fileId in fileIds)
            {
                try
                {
                    var file = await _fileRepository.GetByIdAsync(fileId);
                    if (file != null)
                    {
                        file.FolderId = destinationFolderId;
                        file.UpdatedAt = DateTime.UtcNow;
                        file.UpdatedByUserId = currentUserId;

                        _fileRepository.Update(file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move file: {FileId}", fileId);
                    success = false;
                }
            }

            await _fileRepository.SaveChangesAsync();
            return success;
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

        // Private helper methods
        private async Task<bool> ProcessImageFileAsync(FileEntity file, bool generateThumbnail)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();

            try
            {
                file.ProcessingStatus = "Processing";
                _fileRepository.Update(file);
                await _fileRepository.SaveChangesAsync();

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

                _fileRepository.Update(file);
                await _fileRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process image file: {FileId}", file.Id);

                file.ProcessingStatus = "Failed";
                file.IsProcessed = false;
                file.UpdatedAt = DateTime.UtcNow;
                file.UpdatedByUserId = currentUserId;
                _fileRepository.Update(file);
                await _fileRepository.SaveChangesAsync();

                return false;
            }
        }

        private async Task<FileDto?> FindDuplicateFileAsync(string hash)
        {
            var file = await _fileRepository.FirstOrDefaultAsync(f => f.Hash == hash);
            return file != null ? await MapFileToDto(file) : null;
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

        private string GeneratePreviewHtml(FileEntity file)
        {
            var fileUrl = GenerateFileUrl(file.Id);

            return file.FileType switch
            {
                FileType.Image => $"<img src=\"{fileUrl}\" alt=\"{file.Alt ?? file.OriginalFileName}\" style=\"max-width: 100%; height: auto;\" />",
                FileType.Video => $"<video controls style=\"max-width: 100%;\"><source src=\"{fileUrl}\" type=\"{file.ContentType}\">Your browser does not support the video tag.</video>",
                FileType.Audio => $"<audio controls><source src=\"{fileUrl}\" type=\"{file.ContentType}\">Your browser does not support the audio tag.</audio>",
                FileType.Document when file.ContentType == "application/pdf" => $"<embed src=\"{fileUrl}\" type=\"application/pdf\" width=\"100%\" height=\"600px\" />",
                _ => null
            };
        }

        private string GenerateFileUrl(int fileId)
        {
            return $"{_baseUrl.TrimEnd('/')}/{fileId}/download";
        }

        private string GenerateThumbnailUrl(int fileId)
        {
            return $"{_baseUrl.TrimEnd('/')}/{fileId}/thumbnail";
        }

        private async Task<FileDto> MapFileToDto(FileEntity file)
        {
            return new FileDto
            {
                Id = file.Id,
                OriginalFileName = file.OriginalFileName,
                StoredFileName = file.StoredFileName,
                ContentType = file.ContentType,
                FileSize = file.FileSize,
                FileExtension = file.FileExtension,
                FileType = file.FileType,
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
                IsProcessed = file.IsProcessed,
                ProcessingStatus = file.ProcessingStatus,
                Tags = file.Tags,
                CreatedAt = file.CreatedAt,
                UpdatedAt = file.UpdatedAt,
                Hash = file.Hash
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
        public async Task<bool> VerifyFileIntegrityAsync(int fileId)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
                return false;

            try
            {
                // Check if content exists and size matches
                if (file.FileContent == null || file.FileContent.Length == 0)
                {
                    _logger.LogError("File {FileId} has null or empty content", fileId);
                    return false;
                }

                if (file.FileContent.Length != file.FileSize)
                {
                    _logger.LogError("File {FileId} size mismatch. Expected: {Expected}, Actual: {Actual}",
                        fileId, file.FileSize, file.FileContent.Length);
                    return false;
                }

                // For images, try to process them
                if (file.FileType == FileType.Image)
                {
                    var isValidImage = await _imageProcessingService.IsImageFromBytesAsync(file.FileContent);
                    if (!isValidImage)
                    {
                        _logger.LogError("File {FileId} failed image validation", fileId);
                        return false;
                    }
                }

                // Verify hash if available
                if (!string.IsNullOrEmpty(file.Hash))
                {
                    var currentHash = GetFileHash(file.FileContent);
                    if (currentHash != file.Hash)
                    {
                        _logger.LogError("File {FileId} hash mismatch. Expected: {Expected}, Actual: {Actual}",
                            fileId, file.Hash, currentHash);
                        return false;
                    }
                }

                _logger.LogInformation("File {FileId} integrity check passed", fileId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File integrity check failed for file {FileId}", fileId);
                return false;
            }
        }
        public async Task<Dictionary<int, bool>> BulkVerifyFileIntegrityAsync(List<int> fileIds = null)
        {
            var files = fileIds != null
                ? await _fileRepository.FindAsync(f => fileIds.Contains(f.Id))
                : await _fileRepository.GetAllAsync();

            var results = new Dictionary<int, bool>();

            foreach (var file in files)
            {
                try
                {
                    var isValid = await VerifyFileIntegrityAsync(file.Id);
                    results[file.Id] = isValid;

                    if (!isValid)
                    {
                        _logger.LogWarning("File integrity check failed for file {FileId}: {FileName}",
                            file.Id, file.OriginalFileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking integrity for file {FileId}", file.Id);
                    results[file.Id] = false;
                }
            }

            return results;
        }
    }
}