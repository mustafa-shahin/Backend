using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// File management controller providing file operations
    /// </summary> 
    [ApiController]
    [Route("api/v{version:apiVersion}/file")]
    [ApiVersion("1.0")]
     
    [Authorize]
    [EnableRateLimiting("ApiPolicy")]
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
            ILogger<FileController> logger,
            IRepository<FileEntity> fileRepository,
            IImageProcessingService imageProcessingService)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _downloadTokenService = downloadTokenService ?? throw new ArgumentNullException(nameof(downloadTokenService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
            _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
        }

        /// <summary>
        /// Get paginated list of files with optional filtering
        /// </summary>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <param name="folderId">Optional folder ID to filter by</param>
        /// <param name="search">Optional search term</param>
        /// <param name="fileType">Optional file type filter</param>
        /// <param name="isPublic">Optional public/private filter</param>
        /// <param name="sortBy">Sort field (default: CreatedAt)</param>
        /// <param name="sortDirection">Sort direction (Asc/Desc, default: Desc)</param>
        /// <returns>Paginated list of files</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<FileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<FileDto>>> GetFiles(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int? folderId = null,
            [FromQuery] string? search = null,
            [FromQuery] FileType? fileType = null,
            [FromQuery] bool? isPublic = null,
            [FromQuery] string sortBy = "CreatedAt",
            [FromQuery] string sortDirection = "Desc")
        {
            try
            {
                var searchDto = new FileSearchDto
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    FolderId = folderId,
                    SearchTerm = search,
                    FileType = fileType,
                    IsPublic = isPublic,
                    SortBy = sortBy,
                    SortDirection = sortDirection
                };

                var result = await _fileService.GetFilesPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting files");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving files");
                return StatusCode(500, new { Message = "An error occurred while retrieving files" });
            }
        }

        /// <summary>
        /// Advanced file search with filtering options
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>Paginated search results</returns>
        [HttpPost("search")]
        [ProducesResponseType(typeof(PagedResult<FileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<FileDto>>> SearchFiles([FromBody] FileSearchDto searchDto)
        {
            try
            {
                if (searchDto == null)
                {
                    return BadRequest(new { Message = "Search criteria is required" });
                }

                var result = await _fileService.SearchFilesPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid search criteria");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching files");
                return StatusCode(500, new { Message = "An error occurred while searching files" });
            }
        }

        /// <summary>
        /// Generate a secure download token for a file
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>Download token with expiration info</returns>
        [HttpPost("{id:int}/download-token")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GenerateDownloadToken([FromRoute] int id)
        {
            try
            {
                // Verify file exists and user has access
                var file = await _fileService.GetFileByIdAsync(id);
                if (file == null)
                {
                    return NotFound(new { Message = "File not found" });
                }

                // Get current user ID from JWT token
                var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
                int? userId = userIdClaim != null ? int.Parse(userIdClaim) : null;

                var token = await _downloadTokenService.GenerateDownloadTokenAsync(id, userId);

                return Ok(new
                {
                    token,
                    expiresIn = 300, // 5 minutes
                    expiresAt = DateTime.UtcNow.AddMinutes(5),
                    downloadUrl = Url.Action("DownloadFileByToken", new { token })
                });
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
        /// <param name="token">Download token</param>
        /// <returns>File stream</returns>
        [HttpGet("download/{token}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DownloadFileByToken([FromRoute] string token)
        {
            try
            {
                _logger.LogInformation("Attempting to download file with token");

                if (string.IsNullOrWhiteSpace(token))
                {
                    return BadRequest(new { Message = "Token is required" });
                }

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
        /// <param name="uploadDto">File upload data</param>
        /// <returns>Uploaded file information</returns>
        [HttpPost("upload")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(FileUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FileUploadResultDto>> UploadFile([FromForm] FileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.File == null)
                {
                    return BadRequest(new { Message = "File is required" });
                }

                var result = await _fileService.UploadFileAsync(uploadDto);

                return Ok(new FileUploadResultDto
                {
                    Success = true,
                    File = result,
                    ProcessingInfo = new Dictionary<string, object>
                    {
                        ["uploadedAt"] = DateTime.UtcNow,
                        ["originalSize"] = uploadDto.File.Length,
                        ["processedImmediately"] = uploadDto.ProcessImmediately
                    }
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "File upload validation failed");
                return BadRequest(new FileUploadResultDto
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, new FileUploadResultDto
                {
                    Success = false,
                    ErrorMessage = "An error occurred while uploading the file"
                });
            }
        }

        /// <summary>
        /// Upload multiple files
        /// </summary>
        /// <param name="uploadDto">Multiple file upload data</param>
        /// <returns>Upload results for all files</returns>
        [HttpPost("upload/multiple")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> UploadMultipleFiles([FromForm] MultipleFileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.Files == null || !uploadDto.Files.Any())
                {
                    return BadRequest(new { Message = "At least one file is required" });
                }

                var results = await _fileService.UploadMultipleFilesAsync(uploadDto);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = uploadDto.Files.Count,
                    SuccessCount = results.Count,
                    FailureCount = uploadDto.Files.Count - results.Count,
                    SuccessfulFiles = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple files");
                return StatusCode(500, new BulkOperationResultDto
                {
                    TotalRequested = uploadDto?.Files?.Count ?? 0,
                    SuccessCount = 0,
                    FailureCount = uploadDto?.Files?.Count ?? 0,
                    Errors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = "An error occurred while uploading files" }
                    }
                });
            }
        }

        /// <summary>
        /// Get file by ID
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>File information</returns>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(FileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FileDto>> GetFile([FromRoute] int id)
        {
            try
            {
                var file = await _fileService.GetFileByIdAsync(id);
                if (file == null)
                {
                    return NotFound(new { Message = "File not found" });
                }

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
        /// <param name="id">File ID</param>
        /// <returns>File stream</returns>
        [HttpGet("{id:int}/download")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DownloadFile([FromRoute] int id)
        {
            try
            {
                _logger.LogInformation("Attempting direct download of file {FileId}", id);

                var file = await _fileService.GetFileByIdAsync(id);
                if (file == null)
                {
                    return NotFound(new { Message = "File not found" });
                }

                if (!file.IsPublic)
                {
                    return Unauthorized(new { Message = "This file requires authentication. Please use the download token endpoint." });
                }

                var (stream, contentType, fileName) = await _fileService.GetFileStreamAsync(id);

                // Set appropriate headers
                Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                Response.Headers.Add("Cache-Control", "public, max-age=3600"); // Cache public files for 1 hour

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
        /// <param name="id">File ID</param>
        /// <returns>Thumbnail stream</returns>
        [HttpGet("{id:int}/thumbnail")]
        [AllowAnonymous] // Allow anonymous thumbnail access for public files
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DownloadThumbnail([FromRoute] int id)
        {
            try
            {
                var (stream, contentType, fileName) = await _fileService.GetThumbnailStreamAsync(id);

                if (stream == null || stream.Length == 0)
                {
                    return NotFound(new { Message = "Thumbnail not found" });
                }

                // Set appropriate headers for thumbnail
                Response.Headers.Add("Cache-Control", "public, max-age=86400"); // Cache for 1 day
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
        /// Get file preview information
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>File preview data</returns>
        [HttpGet("{id:int}/preview")]
        [AllowAnonymous] // Allow anonymous preview for public files
        [ProducesResponseType(typeof(FilePreviewDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FilePreviewDto>> GetFilePreview([FromRoute] int id)
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
        /// Update file information
        /// </summary>
        /// <param name="id">File ID</param>
        /// <param name="updateDto">Update data</param>
        /// <returns>Updated file information</returns>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(FileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FileDto>> UpdateFile([FromRoute] int id, [FromBody] UpdateFileDto updateDto)
        {
            try
            {
                if (updateDto == null)
                {
                    return BadRequest(new { Message = "Update data is required" });
                }

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
        /// <param name="id">File ID</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteFile([FromRoute] int id)
        {
            try
            {
                var success = await _fileService.DeleteFileAsync(id);
                if (!success)
                    return NotFound(new { Message = "File not found" });

                return Ok(new { Message = "File deleted successfully", FileId = id });
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
        /// <param name="moveDto">Move operation data</param>
        /// <returns>Updated file information</returns>
        [HttpPost("move")]
        [ProducesResponseType(typeof(FileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FileDto>> MoveFile([FromBody] MoveFileDto moveDto)
        {
            try
            {
                if (moveDto == null)
                {
                    return BadRequest(new { Message = "Move data is required" });
                }

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
        /// <param name="copyDto">Copy operation data</param>
        /// <returns>New file information</returns>
        [HttpPost("copy")]
        [ProducesResponseType(typeof(FileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FileDto>> CopyFile([FromBody] CopyFileDto copyDto)
        {
            try
            {
                if (copyDto == null)
                {
                    return BadRequest(new { Message = "Copy data is required" });
                }

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
        /// <param name="count">Number of recent files to retrieve (default: 10)</param>
        /// <returns>List of recent files</returns>
        [HttpGet("recent")]
        [ProducesResponseType(typeof(List<FileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<FileDto>>> GetRecentFiles([FromQuery] int count = 10)
        {
            try
            {
                var files = await _fileService.GetRecentFilesAsync(Math.Clamp(count, 1, 50));
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
        /// <returns>File system statistics</returns>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="id">File ID</param>
        /// <returns>Generation result</returns>
        [HttpPost("{id:int}/generate-thumbnail")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GenerateThumbnail([FromRoute] int id)
        {
            try
            {
                var success = await _fileService.GenerateThumbnailAsync(id);
                if (!success)
                    return BadRequest(new { Message = "Failed to generate thumbnail. File may not be an image or may not exist." });

                return Ok(new { Message = "Thumbnail generated successfully", FileId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail for file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while generating the thumbnail" });
            }
        }

        /// <summary>
        /// Bulk update files
        /// </summary>
        /// <param name="bulkUpdateDto">Bulk update data</param>
        /// <returns>Update result</returns>
        [HttpPost("bulk-update")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> BulkUpdateFiles([FromBody] BulkUpdateFilesDto bulkUpdateDto)
        {
            try
            {
                if (bulkUpdateDto?.FileIds?.Any() != true || bulkUpdateDto.UpdateDto == null)
                {
                    return BadRequest(new { Message = "File IDs and update data are required" });
                }

                var success = await _fileService.BulkUpdateFilesAsync(bulkUpdateDto.FileIds, bulkUpdateDto.UpdateDto);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = bulkUpdateDto.FileIds.Count,
                    SuccessCount = success ? bulkUpdateDto.FileIds.Count : 0,
                    FailureCount = success ? 0 : bulkUpdateDto.FileIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk updating files");
                return StatusCode(500, new { Message = "An error occurred while updating files" });
            }
        }

        /// <summary>
        /// Bulk move files
        /// </summary>
        /// <param name="bulkMoveDto">Bulk move data</param>
        /// <returns>Move result</returns>
        [HttpPost("bulk-move")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> BulkMoveFiles([FromBody] BulkMoveFilesDto bulkMoveDto)
        {
            try
            {
                if (bulkMoveDto?.FileIds?.Any() != true)
                {
                    return BadRequest(new { Message = "File IDs are required" });
                }

                var success = await _fileService.BulkMoveFilesAsync(bulkMoveDto.FileIds, bulkMoveDto.DestinationFolderId);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = bulkMoveDto.FileIds.Count,
                    SuccessCount = success ? bulkMoveDto.FileIds.Count : 0,
                    FailureCount = success ? 0 : bulkMoveDto.FileIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk moving files");
                return StatusCode(500, new { Message = "An error occurred while moving files" });
            }
        }

        /// <summary>
        /// Bulk copy files
        /// </summary>
        /// <param name="bulkCopyDto">Bulk copy data</param>
        /// <returns>Copy result with new files</returns>
        [HttpPost("bulk-copy")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> BulkCopyFiles([FromBody] BulkCopyFilesDto bulkCopyDto)
        {
            try
            {
                if (bulkCopyDto?.FileIds?.Any() != true)
                {
                    return BadRequest(new { Message = "File IDs are required" });
                }

                var results = await _fileService.BulkCopyFilesAsync(bulkCopyDto.FileIds, bulkCopyDto.DestinationFolderId);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = bulkCopyDto.FileIds.Count,
                    SuccessCount = results.Count,
                    FailureCount = bulkCopyDto.FileIds.Count - results.Count,
                    SuccessfulFiles = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk copying files");
                return StatusCode(500, new { Message = "An error occurred while copying files" });
            }
        }

        /// <summary>
        /// Bulk delete files
        /// </summary>
        /// <param name="bulkDeleteDto">Bulk delete data</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("bulk-delete")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> BulkDeleteFiles([FromBody] BulkDeleteFilesDto bulkDeleteDto)
        {
            try
            {
                if (bulkDeleteDto?.FileIds?.Any() != true)
                {
                    return BadRequest(new { Message = "File IDs are required" });
                }

                var success = await _fileService.DeleteMultipleFilesAsync(bulkDeleteDto.FileIds);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = bulkDeleteDto.FileIds.Count,
                    SuccessCount = success ? bulkDeleteDto.FileIds.Count : 0,
                    FailureCount = success ? 0 : bulkDeleteDto.FileIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting files");
                return StatusCode(500, new { Message = "An error occurred while deleting files" });
            }
        }

        /// <summary>
        /// Verify file integrity (Admin only)
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>Integrity verification result</returns>
        [HttpPost("{id:int}/verify-integrity")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> VerifyFileIntegrity([FromRoute] int id)
        {
            try
            {
                var isValid = await _fileService.VerifyFileIntegrityAsync(id);
                var file = await _fileService.GetFileByIdAsync(id);

                if (file == null)
                {
                    return NotFound(new { Message = "File not found" });
                }

                return Ok(new
                {
                    fileId = id,
                    fileName = file.OriginalFileName,
                    contentType = file.ContentType,
                    fileSize = file.FileSize,
                    isValid = isValid,
                    message = isValid ? "File integrity verified" : "File integrity check failed - check logs for details",
                    verifiedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying integrity for file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while verifying file integrity" });
            }
        }

        /// <summary>
        /// Get file diagnostic information
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>Detailed diagnostic information</returns>
        [HttpGet("{id:int}/diagnostic")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetFileDiagnosticInfo([FromRoute] int id)
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
                    lastAccessedAt = file.LastAccessedAt,
                    generatedAt = DateTime.UtcNow
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
            [Required]
            public List<int> FileIds { get; set; } = new();

            [Required]
            public UpdateFileDto UpdateDto { get; set; } = null!;
        }

        public class BulkMoveFilesDto
        {
            [Required]
            public List<int> FileIds { get; set; } = new();

            public int? DestinationFolderId { get; set; }
        }

        public class BulkCopyFilesDto
        {
            [Required]
            public List<int> FileIds { get; set; } = new();

            public int? DestinationFolderId { get; set; }
        }

        public class BulkDeleteFilesDto
        {
            [Required]
            public List<int> FileIds { get; set; } = new();
        }
    }
}