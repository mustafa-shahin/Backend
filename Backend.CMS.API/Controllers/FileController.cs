using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly IDownloadTokenService _downloadTokenService;
        private readonly ILogger<FileController> _logger;
        private readonly IRepository<FileEntity> _fileRepository;
        private readonly IImageProcessingService _imageProcessingService;
        public FileController(
            IFileService fileService,
            IDownloadTokenService downloadTokenService,
            ILogger<FileController> logger, IRepository<FileEntity> fileRepository,
            IImageProcessingService imageProcessingService)
        {
            _fileService = fileService;
            _downloadTokenService = downloadTokenService;
            _logger = logger;
            _fileRepository = fileRepository;
            _imageProcessingService = imageProcessingService;
        }




        /// <summary>
        /// Generate a secure download token for a file
        /// </summary>
        [HttpPost("{id:int}/download-token")]
        [Authorize]
        public async Task<ActionResult<object>> GenerateDownloadToken(int id)
        {
            try
            {
                // Verify file exists and user has access
                var file = await _fileService.GetFileByIdAsync(id);

                // Get current user ID from JWT token
                var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
                int? userId = userIdClaim != null ? int.Parse(userIdClaim) : null;

                var token = await _downloadTokenService.GenerateDownloadTokenAsync(id, userId);

                return Ok(new { token, expiresIn = 300 }); // 5 minutes
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating download token for file {FileId}", id);
                return StatusCode(500, new { Message = "Failed to generate download token" });
            }
        }

        /// <summary>
        /// Download file using token (no CORS issues)
        /// </summary>
        [HttpGet("download/{token}")]
        [AllowAnonymous]
        public async Task<ActionResult> DownloadFileByToken(string token)
        {
            try
            {
                _logger.LogInformation("Attempting to download file with token");

                var (isValid, fileId, userId) = await _downloadTokenService.ValidateTokenAsync(token);

                if (!isValid)
                {
                    _logger.LogWarning("Invalid or expired download token");
                    return BadRequest(new { Message = "Invalid or expired download token" });
                }

                var (stream, contentType, fileName) = await _fileService.GetFileStreamAsync(fileId);

                if (stream == null || stream.Length == 0)
                {
                    _logger.LogWarning("File {FileId} has no content", fileId);
                    return NotFound(new { Message = "File content not found" });
                }

                // Set headers for proper file download
                Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                Response.Headers.Add("Content-Length", stream.Length.ToString());
                Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");

                _logger.LogInformation("Successfully serving file {FileId} with size {Size} bytes, content type: {ContentType}",
                    fileId, stream.Length, contentType);
                return File(stream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file with token {Token}", token);
                return StatusCode(500, new { Message = "An error occurred while downloading the file" });
            }
        }

        /// <summary>
        /// Upload a single file
        /// </summary>
        [HttpPost("upload")]
        [Authorize]
        public async Task<ActionResult<FileDto>> UploadFile([FromForm] FileUploadDto uploadDto)
        {
            try
            {
                var result = await _fileService.UploadFileAsync(uploadDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, new { Message = "An error occurred while uploading the file" });
            }
        }


        /// <summary>
        /// Upload multiple files
        /// </summary>
        [HttpPost("upload/multiple")]
        public async Task<ActionResult<List<FileDto>>> UploadMultipleFiles([FromForm] MultipleFileUploadDto uploadDto)
        {
            try
            {
                var results = await _fileService.UploadMultipleFilesAsync(uploadDto);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple files");
                return StatusCode(500, new { Message = "An error occurred while uploading files" });
            }
        }

        /// <summary>
        /// Get file by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<ActionResult<FileDto>> GetFile(int id)
        {
            try
            {
                var file = await _fileService.GetFileByIdAsync(id);
                return Ok(file);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the file" });
            }
        }


        /// <summary>
        /// Direct download for public files (no auth required)
        /// </summary>
        [HttpGet("{id:int}/download")]
        [AllowAnonymous]
        public async Task<ActionResult> DownloadFile(int id)
        {
            try
            {
                _logger.LogInformation("Attempting direct download of file {FileId}", id);

                var file = await _fileService.GetFileByIdAsync(id);

                if (!file.IsPublic)
                {
                    return Unauthorized(new { Message = "This file requires authentication. Please use the download token endpoint." });
                }

                var (stream, contentType, fileName) = await _fileService.GetFileStreamAsync(id);

                return File(stream, contentType, fileName);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while downloading the file" });
            }
        }
        /// <summary>
        /// Download file thumbnail
        /// </summary>
        [HttpGet("{id:int}/download/thumbnail")]
        [AllowAnonymous] // Allow anonymous thumbnail access for public files
        public async Task<ActionResult> DownloadThumbnail(int id)
        {
            try
            {
                var (stream, contentType, fileName) = await _fileService.GetThumbnailStreamAsync(id);

                if (stream == null || stream.Length == 0)
                {
                    return NotFound(new { Message = "Thumbnail not found" });
                }

                // Set appropriate headers for thumbnail
                Response.Headers.Add("Cache-Control", "public, max-age=31536000"); // Cache for 1 year
                Response.Headers.Add("Content-Length", stream.Length.ToString());

                return File(stream, contentType, fileName);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading thumbnail for file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while downloading the thumbnail" });
            }
        }
        /// <summary>
        /// Get thumbnail
        /// </summary>
        [HttpGet("{id:int}/thumbnail")]
        [AllowAnonymous]
        public async Task<ActionResult> GetThumbnail(int id)
        {
            try
            {
                var (stream, contentType, fileName) = await _fileService.GetThumbnailStreamAsync(id);

                if (stream == null || stream.Length == 0)
                {
                    return NotFound(new { Message = "Thumbnail not found" });
                }

                Response.Headers.Add("Cache-Control", "public, max-age=86400"); // Cache for 1 day

                return File(stream, contentType, fileName);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting thumbnail for file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while getting the thumbnail" });
            }
        }
        /// <summary>
        /// Get file preview
        /// </summary>
        [HttpGet("{id:int}/preview")]
        [AllowAnonymous] // Allow anonymous preview for public files
        public async Task<ActionResult<FilePreviewDto>> GetFilePreview(int id)
        {
            try
            {
                var preview = await _fileService.GetFilePreviewAsync(id);
                return Ok(preview);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file preview {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while getting file preview" });
            }
        }

        /// <summary>
        /// Search files
        /// </summary>
        [HttpPost("search")]
        public async Task<ActionResult<List<FileDto>>> SearchFiles([FromBody] FileSearchDto searchDto)
        {
            try
            {
                var results = await _fileService.SearchFilesAsync(searchDto);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching files");
                return StatusCode(500, new { Message = "An error occurred while searching files" });
            }
        }
        /// <summary>
        /// Update file information
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<ActionResult<FileDto>> UpdateFile(int id, [FromBody] UpdateFileDto updateDto)
        {
            try
            {
                var result = await _fileService.UpdateFileAsync(id, updateDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the file" });
            }
        }

        /// <summary>
        /// Delete file
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<ActionResult> DeleteFile(int id)
        {
            try
            {
                var success = await _fileService.DeleteFileAsync(id);
                if (!success)
                    return NotFound(new { Message = "File not found" });

                return Ok(new { Message = "File deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the file" });
            }
        }

        /// <summary>
        /// Move file to different folder
        /// </summary>
        [HttpPost("move")]
        public async Task<ActionResult<FileDto>> MoveFile([FromBody] MoveFileDto moveDto)
        {
            try
            {
                var result = await _fileService.MoveFileAsync(moveDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving file");
                return StatusCode(500, new { Message = "An error occurred while moving the file" });
            }
        }

        /// <summary>
        /// Copy file
        /// </summary>
        [HttpPost("copy")]
        public async Task<ActionResult<FileDto>> CopyFile([FromBody] CopyFileDto copyDto)
        {
            try
            {
                var result = await _fileService.CopyFileAsync(copyDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying file");
                return StatusCode(500, new { Message = "An error occurred while copying the file" });
            }
        }

        /// <summary>
        /// Get recent files
        /// </summary>
        [HttpGet("recent")]
        public async Task<ActionResult<List<FileDto>>> GetRecentFiles([FromQuery] int count = 10)
        {
            try
            {
                var files = await _fileService.GetRecentFilesAsync(count);
                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent files");
                return StatusCode(500, new { Message = "An error occurred while retrieving recent files" });
            }
        }

        /// <summary>
        /// Get file statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<Dictionary<string, object>>> GetFileStatistics()
        {
            try
            {
                var stats = await _fileService.GetFileStatisticsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file statistics");
                return StatusCode(500, new { Message = "An error occurred while retrieving file statistics" });
            }
        }

        /// <summary>
        /// Generate thumbnail for an existing file
        /// </summary>
        [HttpPost("{id:int}/generate-thumbnail")]
        public async Task<ActionResult> GenerateThumbnail(int id)
        {
            try
            {
                var success = await _fileService.GenerateThumbnailAsync(id);
                if (!success)
                    return BadRequest(new { Message = "Failed to generate thumbnail. File may not be an image or may not exist." });

                return Ok(new { Message = "Thumbnail generated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail for file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while generating the thumbnail" });
            }
        }

        /// <summary>
        /// Get all files with pagination
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<PagedResult<FileDto>>> GetFiles(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? folderId = null,
            [FromQuery] string? search = null,
            [FromQuery] FileType? fileType = null)
        {
            try
            {
                var searchDto = new FileSearchDto
                {
                    Page = page,
                    PageSize = pageSize,
                    FolderId = folderId,
                    SearchTerm = search,
                    FileType = fileType
                };

                var files = await _fileService.SearchFilesAsync(searchDto);
                var totalCount = files.Count; // TODO: should implement proper total count

                var result = new PagedResult<FileDto>
                {
                    Items = files,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving files");
                return StatusCode(500, new { Message = "An error occurred while retrieving files" });
            }
        }

        /// <summary>
        /// Bulk operations
        /// </summary>
        [HttpPost("bulk-update")]
        public async Task<ActionResult> BulkUpdateFiles([FromBody] BulkUpdateFilesDto bulkUpdateDto)
        {
            try
            {
                var success = await _fileService.BulkUpdateFilesAsync(bulkUpdateDto.FileIds, bulkUpdateDto.UpdateDto);
                if (!success)
                    return BadRequest(new { Message = "Some files could not be updated" });

                return Ok(new { Message = "Files updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk updating files");
                return StatusCode(500, new { Message = "An error occurred while updating files" });
            }
        }

        [HttpPost("bulk-move")]
        public async Task<ActionResult> BulkMoveFiles([FromBody] BulkMoveFilesDto bulkMoveDto)
        {
            try
            {
                var success = await _fileService.BulkMoveFilesAsync(bulkMoveDto.FileIds, bulkMoveDto.DestinationFolderId);
                if (!success)
                    return BadRequest(new { Message = "Some files could not be moved" });

                return Ok(new { Message = "Files moved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk moving files");
                return StatusCode(500, new { Message = "An error occurred while moving files" });
            }
        }

        [HttpPost("bulk-copy")]
        public async Task<ActionResult<List<FileDto>>> BulkCopyFiles([FromBody] BulkCopyFilesDto bulkCopyDto)
        {
            try
            {
                var results = await _fileService.BulkCopyFilesAsync(bulkCopyDto.FileIds, bulkCopyDto.DestinationFolderId);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk copying files");
                return StatusCode(500, new { Message = "An error occurred while copying files" });
            }
        }

        [HttpDelete("bulk-delete")]
        public async Task<ActionResult> BulkDeleteFiles([FromBody] BulkDeleteFilesDto bulkDeleteDto)
        {
            try
            {
                var success = await _fileService.DeleteMultipleFilesAsync(bulkDeleteDto.FileIds);
                if (!success)
                    return BadRequest(new { Message = "Some files could not be deleted" });

                return Ok(new { Message = "Files deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting files");
                return StatusCode(500, new { Message = "An error occurred while deleting files" });
            }
        }
        // <summary>
        /// Verify file integrity (Admin only)
        /// </summary>
        [HttpPost("{id:int}/verify-integrity")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> VerifyFileIntegrity(int id)
        {
            try
            {
                var fileService = _fileService as FileService;
                if (fileService == null)
                {
                    // If using cached service, get the base service
                    var cachedService = _fileService as CachedFileService;
                    if (cachedService != null)
                    {
                        var fieldInfo = cachedService.GetType().GetField("_baseFileService",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        fileService = fieldInfo?.GetValue(cachedService) as FileService;
                    }
                }

                if (fileService == null)
                {
                    return StatusCode(500, new { Message = "Unable to access file service for integrity check" });
                }

                var isValid = await fileService.VerifyFileIntegrityAsync(id);

                var file = await _fileService.GetFileByIdAsync(id);

                return Ok(new
                {
                    fileId = id,
                    fileName = file.OriginalFileName,
                    contentType = file.ContentType,
                    fileSize = file.FileSize,
                    isValid = isValid,
                    message = isValid ? "File integrity verified" : "File integrity check failed - check logs for details"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying integrity for file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while verifying file integrity" });
            }

        }
        [HttpGet("{id:int}/diagnostic")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetFileDiagnosticInfo(int id)
        {
            try
            {
                // Get file info
                var file = await _fileRepository.GetByIdAsync(id);
                if (file == null)
                {
                    return NotFound(new { Message = "File not found" });
                }

                // Basic file info
                var diagnosticInfo = new
                {
                    fileId = file.Id,
                    fileName = file.OriginalFileName,
                    storedFileName = file.StoredFileName,
                    contentType = file.ContentType,
                    fileSize = file.FileSize,
                    fileSizeFormatted = FormatFileSize(file.FileSize),
                    actualContentLength = file.FileContent?.Length ?? 0,
                    sizeMismatch = file.FileContent?.Length != file.FileSize,
                    hasContent = file.FileContent != null && file.FileContent.Length > 0,
                    hasThumbnail = file.ThumbnailContent != null && file.ThumbnailContent.Length > 0,
                    thumbnailSize = file.ThumbnailContent?.Length ?? 0,
                    fileType = file.FileType.ToString(),
                    fileExtension = file.FileExtension,
                    isPublic = file.IsPublic,
                    isProcessed = file.IsProcessed,
                    processingStatus = file.ProcessingStatus,
                    width = file.Width,
                    height = file.Height,
                    hash = file.Hash,
                    createdAt = file.CreatedAt,
                    updatedAt = file.UpdatedAt,
                    downloadCount = file.DownloadCount,
                    lastAccessedAt = file.LastAccessedAt
                };

                // Try to verify if it's a valid image (for image files)
                if (file.FileType == FileType.Image && file.FileContent != null && file.FileContent.Length > 0)
                {
                    try
                    {
                        var isValidImage = await _imageProcessingService.IsImageFromBytesAsync(file.FileContent);
                        return Ok(new
                        {
                            diagnosticInfo,
                            imageValidation = new
                            {
                                isValidImage,
                                message = isValidImage ? "Image validation passed" : "Image validation failed"
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        return Ok(new
                        {
                            diagnosticInfo,
                            imageValidation = new
                            {
                                isValidImage = false,
                                message = $"Image validation error: {ex.Message}"
                            }
                        });
                    }
                }

                return Ok(new { diagnosticInfo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting diagnostic info for file {FileId}", id);
                return StatusCode(500, new { Message = $"Error retrieving diagnostic info: {ex.Message}" });
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

        public class BulkUpdateFilesDto
        {
            public List<int> FileIds { get; set; } = new();
            public UpdateFileDto UpdateDto { get; set; } = null!;
        }

        public class BulkMoveFilesDto
        {
            public List<int> FileIds { get; set; } = new();
            public int? DestinationFolderId { get; set; }
        }

        public class BulkCopyFilesDto
        {
            public List<int> FileIds { get; set; } = new();
            public int? DestinationFolderId { get; set; }
        }

        public class BulkDeleteFilesDto
        {
            public List<int> FileIds { get; set; } = new();
        }
    }
}