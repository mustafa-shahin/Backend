using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    /// <summary>
    /// Service that aggregates all file types and provides a unified interface
    /// </summary>
    public class FileAggregatorService : IFileAggregatorService
    {
        private readonly IImageService _imageService;
        private readonly IDocumentService _documentService;
        private readonly IAudioService _audioService;
        private readonly IVideoService _videoService;
        private readonly IArchiveService _archiveService;
        private readonly IFileUrlService _fileUrlService;
        private readonly IMapper _mapper;
        private readonly ILogger<FileAggregatorService> _logger;

        public FileAggregatorService(
            IImageService imageService,
            IDocumentService documentService,
            IAudioService audioService,
            IVideoService videoService,
            IArchiveService archiveService,
            IFileUrlService fileUrlService,
            IMapper mapper,
            ILogger<FileAggregatorService> logger)
        {
            _imageService = imageService;
            _documentService = documentService;
            _audioService = audioService;
            _videoService = videoService;
            _archiveService = archiveService;
            _fileUrlService = fileUrlService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<PaginatedResult<FileDto>> GetFilesPagedAsync(FileSearchDto searchDto)
        {
            var allFiles = new List<FileDto>();

            try
            {
                // Get files from all services based on file type filter
                if (searchDto.FileType == null || searchDto.FileType == FileType.Image)
                {
                    var images = await _imageService.GetPagedAsync(searchDto.PageNumber, searchDto.PageSize);
                    allFiles.AddRange(images.Data.Select(ConvertImageToFileDto));
                }

                if (searchDto.FileType == null || searchDto.FileType == FileType.Document)
                {
                    var documents = await _documentService.GetPagedAsync(searchDto.PageNumber, searchDto.PageSize);
                    allFiles.AddRange(documents.Data.Select(ConvertDocumentToFileDto));
                }

                if (searchDto.FileType == null || searchDto.FileType == FileType.Audio)
                {
                    var audios = await _audioService.GetPagedAsync(searchDto.PageNumber, searchDto.PageSize);
                    allFiles.AddRange(audios.Data.Select(ConvertAudioToFileDto));
                }

                if (searchDto.FileType == null || searchDto.FileType == FileType.Video)
                {
                    var videos = await _videoService.GetPagedAsync(searchDto.PageNumber, searchDto.PageSize);
                    allFiles.AddRange(videos.Data.Select(ConvertVideoToFileDto));
                }

                if (searchDto.FileType == null || searchDto.FileType == FileType.Archive)
                {
                    var archives = await _archiveService.GetPagedAsync(searchDto.PageNumber, searchDto.PageSize);
                    allFiles.AddRange(archives.Data.Select(ConvertArchiveToFileDto));
                }

                // Apply additional filtering
                if (!string.IsNullOrEmpty(searchDto.Name))
                {
                    allFiles = allFiles.Where(f => f.Name.Contains(searchDto.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (searchDto.FolderId.HasValue)
                {
                    allFiles = allFiles.Where(f => f.FolderId == searchDto.FolderId).ToList();
                }

                // Apply sorting
                allFiles = ApplySorting(allFiles, searchDto.SortBy, searchDto.SortDescending);

                // Apply pagination
                var totalCount = allFiles.Count;
                var pagedFiles = allFiles
                    .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToList();

                return new PaginatedResult<FileDto>
                {
                    Data = pagedFiles,
                    PageNumber = searchDto.PageNumber,
                    PageSize = searchDto.PageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged files");
                return PaginatedResult<FileDto>.Empty(searchDto.PageNumber, searchDto.PageSize);
            }
        }

        public async Task<FileDto?> GetFileByIdAsync(int id)
        {
            try
            {
                // Try each service to find the file
                var image = await _imageService.GetByIdAsync(id);
                if (image != null) return ConvertImageToFileDto(image);

                var document = await _documentService.GetByIdAsync(id);
                if (document != null) return ConvertDocumentToFileDto(document);

                var audio = await _audioService.GetByIdAsync(id);
                if (audio != null) return ConvertAudioToFileDto(audio);

                var video = await _videoService.GetByIdAsync(id);
                if (video != null) return ConvertVideoToFileDto(video);

                var archive = await _archiveService.GetByIdAsync(id);
                if (archive != null) return ConvertArchiveToFileDto(archive);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file by ID {FileId}", id);
                return null;
            }
        }

        public async Task<List<FileDto>> GetFilesForEntityAsync(string entityType, int entityId, FileType? fileType = null)
        {
            var allFiles = new List<FileDto>();

            try
            {
                if (fileType == null || fileType == FileType.Image)
                {
                    var images = await _imageService.GetImagesByEntityAsync(entityType, entityId);
                    allFiles.AddRange(images.Select(ConvertImageToFileDto));
                }

                if (fileType == null || fileType == FileType.Audio)
                {
                    var audios = await _audioService.GetAudiosByEntityAsync(entityType, entityId);
                    allFiles.AddRange(audios.Select(ConvertAudioToFileDto));
                }

                if (fileType == null || fileType == FileType.Video)
                {
                    var videos = await _videoService.GetVideosByEntityAsync(entityType, entityId);
                    allFiles.AddRange(videos.Select(ConvertVideoToFileDto));
                }

                // Note: Document and Archive services don't have GetByEntityAsync methods yet
                // TODO: Add these methods to the respective services

                return allFiles.OrderByDescending(f => f.CreatedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files for entity {EntityType}:{EntityId}", entityType, entityId);
                return new List<FileDto>();
            }
        }

        public async Task<FileDto> UploadFileAsync(FileUploadDto uploadDto)
        {
            var fileType = DetermineFileType(uploadDto.File.ContentType);

            switch (fileType)
            {
                case FileType.Image:
                    var image = await _imageService.UploadImageAsync(uploadDto);
                    return ConvertImageToFileDto(image);

                case FileType.Document:
                    var document = await _documentService.UploadDocumentAsync(uploadDto);
                    return ConvertDocumentToFileDto(document);

                case FileType.Audio:
                    var audio = await _audioService.UploadAudioAsync(uploadDto);
                    return ConvertAudioToFileDto(audio);

                case FileType.Video:
                    var video = await _videoService.UploadVideoAsync(uploadDto);
                    return ConvertVideoToFileDto(video);

                case FileType.Archive:
                    var archive = await _archiveService.UploadArchiveAsync(uploadDto);
                    return ConvertArchiveToFileDto(archive);

                default:
                    throw new NotSupportedException($"File type {fileType} is not supported for upload");
            }
        }

        public async Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto)
        {
            var results = new List<FileDto>();

            foreach (var file in uploadDto.Files)
            {
                try
                {
                    var singleUploadDto = new FileUploadDto
                    {
                        File = file,
                        Description = uploadDto.Description,
                        FolderId = uploadDto.FolderId,
                        IsPublic = uploadDto.IsPublic,
                        GenerateThumbnail = uploadDto.GenerateThumbnails,
                        ProcessImmediately = uploadDto.ProcessImmediately,
                        EntityType = uploadDto.EntityType,
                        EntityId = uploadDto.EntityId
                    };

                    var result = await UploadFileAsync(singleUploadDto);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading file {FileName}", file.FileName);
                    // Continue with other files
                }
            }

            return results;
        }

        // Placeholder implementations for other methods
        public Task<FileDto> UpdateFileAsync(int id, UpdateFileDto updateDto)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteFileAsync(int id)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteMultipleFilesAsync(List<int> fileIds)
        {
            throw new NotImplementedException();
        }

        public Task<FileDto> MoveFileAsync(MoveFileDto moveDto)
        {
            throw new NotImplementedException();
        }

        public Task<FileDto> CopyFileAsync(CopyFileDto copyDto)
        {
            throw new NotImplementedException();
        }

        public Task<List<FileDto>> GetRecentFilesAsync(int count = 10)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, object>> GetFileStatisticsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> GenerateThumbnailAsync(int id)
        {
            throw new NotImplementedException();
        }

        public Task<FilePreviewDto> GetFilePreviewAsync(int id)
        {
            throw new NotImplementedException();
        }

        public Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int id)
        {
            throw new NotImplementedException();
        }

        public Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int id)
        {
            throw new NotImplementedException();
        }

        public Task<bool> VerifyFileIntegrityAsync(int id)
        {
            throw new NotImplementedException();
        }

        public Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto)
        {
            throw new NotImplementedException();
        }

        public Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId)
        {
            throw new NotImplementedException();
        }

        public Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId)
        {
            throw new NotImplementedException();
        }

        public Task<PaginatedResult<FileDto>> SearchFilesPagedAsync(FileSearchDto searchDto)
        {
            return GetFilesPagedAsync(searchDto);
        }

        #region Private Helper Methods

        private FileType DetermineFileType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return FileType.Other;

            var lowerContentType = contentType.ToLowerInvariant();

            if (lowerContentType.StartsWith("image/"))
                return FileType.Image;

            if (lowerContentType.StartsWith("video/"))
                return FileType.Video;

            if (lowerContentType.StartsWith("audio/"))
                return FileType.Audio;

            var archiveTypes = new[] { "application/zip", "application/x-rar", "application/x-7z-compressed", "application/gzip" };
            if (archiveTypes.Any(type => lowerContentType.Contains(type)))
                return FileType.Archive;

            return FileType.Document;
        }

        private FileDto ConvertImageToFileDto(ImageDto image)
        {
            return new FileDto
            {
                Id = image.Id,
                Name = image.Name,
                FileName = image.FileName,
                OriginalFileName = image.FileName,
                ContentType = image.ContentType,
                Size = image.Size,
                FileSize = image.Size,
                Extension = image.Extension,
                Description = image.Description,
                FolderId = image.FolderId,
                CreatedAt = image.CreatedAt,
                UpdatedAt = image.UpdatedAt,
                FileType = FileType.Image,
                Alt = image.Alt,
                Width = image.Width,
                Height = image.Height,
                HasThumbnail = image.HasThumbnail,
                Urls = new FileUrlsDto
                {
                    Download = _fileUrlService.GenerateImageDownloadUrl(image.Id),
                    DirectAccess = _fileUrlService.GenerateImageDownloadUrl(image.Id),
                    Preview = _fileUrlService.GenerateImagePreviewUrl(image.Id),
                    Thumbnail = _fileUrlService.GenerateImageThumbnailUrl(image.Id)
                }
            };
        }

        private FileDto ConvertDocumentToFileDto(DocumentDto document)
        {
            return new FileDto
            {
                Id = document.Id,
                Name = document.Name,
                FileName = document.FileName,
                OriginalFileName = document.FileName,
                ContentType = document.ContentType,
                Size = document.Size,
                FileSize = document.Size,
                Extension = document.Extension,
                Description = document.Description,
                FolderId = document.FolderId,
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.UpdatedAt,
                FileType = FileType.Document,
                Urls = new FileUrlsDto
                {
                    Download = _fileUrlService.GenerateDocumentDownloadUrl(document.Id),
                    DirectAccess = _fileUrlService.GenerateDocumentDownloadUrl(document.Id)
                }
            };
        }

        private FileDto ConvertAudioToFileDto(AudioDto audio)
        {
            return new FileDto
            {
                Id = audio.Id,
                Name = audio.Name,
                FileName = audio.FileName,
                OriginalFileName = audio.FileName,
                ContentType = audio.ContentType,
                Size = audio.Size,
                FileSize = audio.Size,
                Extension = audio.Extension,
                Description = audio.Description,
                FolderId = audio.FolderId,
                CreatedAt = audio.CreatedAt,
                UpdatedAt = audio.UpdatedAt,
                FileType = FileType.Audio,
                Duration = audio.Duration,
                Urls = new FileUrlsDto
                {
                    Download = _fileUrlService.GenerateAudioDownloadUrl(audio.Id),
                    DirectAccess = _fileUrlService.GenerateAudioDownloadUrl(audio.Id),
                    Stream = $"/api/v1/audio/{audio.Id}/stream"
                }
            };
        }

        private FileDto ConvertVideoToFileDto(VideoDto video)
        {
            return new FileDto
            {
                Id = video.Id,
                Name = video.Name,
                FileName = video.FileName,
                OriginalFileName = video.FileName,
                ContentType = video.ContentType,
                Size = video.Size,
                FileSize = video.Size,
                Extension = video.Extension,
                Description = video.Description,
                FolderId = video.FolderId,
                CreatedAt = video.CreatedAt,
                UpdatedAt = video.UpdatedAt,
                FileType = FileType.Video,
                Width = video.Width,
                Height = video.Height,
                Duration = video.Duration,
                ThumbnailImageId = video.ThumbnailImageId,
                Urls = new FileUrlsDto
                {
                    Download = _fileUrlService.GenerateVideoDownloadUrl(video.Id),
                    DirectAccess = _fileUrlService.GenerateVideoDownloadUrl(video.Id),
                    Preview = _fileUrlService.GenerateVideoPreviewUrl(video.Id),
                    Thumbnail = _fileUrlService.GenerateVideoThumbnailUrl(video.Id),
                    Stream = $"/api/v1/video/{video.Id}/stream"
                }
            };
        }

        private FileDto ConvertArchiveToFileDto(ArchiveDto archive)
        {
            return new FileDto
            {
                Id = archive.Id,
                Name = archive.Name,
                FileName = archive.FileName,
                OriginalFileName = archive.FileName,
                ContentType = archive.ContentType,
                Size = archive.Size,
                FileSize = archive.Size,
                Extension = archive.Extension,
                Description = archive.Description,
                FolderId = archive.FolderId,
                CreatedAt = archive.CreatedAt,
                UpdatedAt = archive.UpdatedAt,
                FileType = FileType.Archive,
                Urls = new FileUrlsDto
                {
                    Download = _fileUrlService.GenerateArchiveDownloadUrl(archive.Id),
                    DirectAccess = _fileUrlService.GenerateArchiveDownloadUrl(archive.Id)
                }
            };
        }

        private List<FileDto> ApplySorting(List<FileDto> files, string sortBy, bool sortDescending)
        {
            return sortBy.ToLowerInvariant() switch
            {
                "name" => sortDescending ? files.OrderByDescending(f => f.Name).ToList() : files.OrderBy(f => f.Name).ToList(),
                "size" => sortDescending ? files.OrderByDescending(f => f.Size).ToList() : files.OrderBy(f => f.Size).ToList(),
                "createdat" => sortDescending ? files.OrderByDescending(f => f.CreatedAt).ToList() : files.OrderBy(f => f.CreatedAt).ToList(),
                "updatedat" => sortDescending ? files.OrderByDescending(f => f.UpdatedAt).ToList() : files.OrderBy(f => f.UpdatedAt).ToList(),
                _ => sortDescending ? files.OrderByDescending(f => f.CreatedAt).ToList() : files.OrderBy(f => f.CreatedAt).ToList()
            };
        }

        #endregion
    }
}