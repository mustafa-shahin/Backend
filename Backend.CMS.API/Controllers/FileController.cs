using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
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
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]

    [Authorize]
    [EnableRateLimiting("ApiPolicy")]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly IDownloadTokenService _downloadTokenService;
        private readonly ILogger<FileController> _logger;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly IUnitOfWork _unitOfWork;

        public FileController(
            IFileService fileService,
            IDownloadTokenService downloadTokenService,
            ILogger<FileController> logger,
            IImageProcessingService imageProcessingService,
            IUnitOfWork unitOfWork)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _downloadTokenService = downloadTokenService ?? throw new ArgumentNullException(nameof(downloadTokenService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
        [ProducesResponseType(typeof(PaginatedResult<FileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<FileDto>>> GetFiles(
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
        [ProducesResponseType(typeof(PaginatedResult<FileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<FileDto>>> SearchFiles([FromBody] FileSearchDto searchDto)
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
        /// Stream video/audio file with range request support for public files
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>File stream with range support</returns>
        [HttpGet("{id:int}/stream")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status206PartialContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status416RangeNotSatisfiable)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> StreamFile([FromRoute] int id)
        {
            try
            {
                _logger.LogInformation("Attempting to stream file {FileId}", id);

                var file = await _fileService.GetFileByIdAsync(id);
                if (file == null)
                {
                    return NotFound(new { Message = "File not found" });
                }

                // For private files, require authentication
                if (!file.IsPublic && !User.Identity?.IsAuthenticated == true)
                {
                    return Unauthorized(new { Message = "Authentication required for private files" });
                }

                // Get file entity to access content
                var fileEntity = await _unitOfWork.Files.GetByIdAsync(id);
                if (fileEntity?.FileContent == null || fileEntity.FileContent.Length == 0)
                {
                    return NotFound(new { Message = "File content not found" });
                }

                // Verify this is a streamable file type
                if (!IsStreamableFileType(fileEntity.FileType, fileEntity.ContentType))
                {
                    _logger.LogWarning("Attempted to stream non-streamable file type: {FileType}, {ContentType}",
                        fileEntity.FileType, fileEntity.ContentType);
                    return BadRequest(new { Message = "File type not suitable for streaming" });
                }

                // Record access
                await _fileService.RecordFileAccessAsync(id, FileAccessType.Preview);

                // Handle range requests for video/audio streaming
                var rangeHeader = Request.Headers["Range"].FirstOrDefault();
                var fileContent = fileEntity.FileContent;
                var fileSize = fileContent.Length;

                // Set common headers
                Response.Headers.Add("Accept-Ranges", "bytes");
                Response.Headers.Add("Content-Type", fileEntity.ContentType);
                Response.Headers.Add("Cache-Control", "public, max-age=3600");
                Response.Headers.Add("Last-Modified", fileEntity.UpdatedAt.ToString("R"));

                // Handle range request
                if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                {
                    return await HandleRangeRequest(rangeHeader, fileContent, fileEntity.ContentType);
                }

                // No range request - return entire file
                _logger.LogInformation("Streaming entire file {FileId}, size: {Size} bytes", id, fileSize);

                Response.Headers.Add("Content-Length", fileSize.ToString());

                return File(fileContent, fileEntity.ContentType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while streaming the file" });
            }
        }

        /// <summary>
        /// Stream video/audio file using token with range request support
        /// </summary>
        /// <param name="token">Access token</param>
        /// <returns>File stream with range support</returns>
        [HttpGet("stream/{token}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status206PartialContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status416RangeNotSatisfiable)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> StreamFileByToken([FromRoute] string token)
        {
            try
            {
                _logger.LogInformation("Attempting to stream file with token");

                if (string.IsNullOrWhiteSpace(token))
                {
                    return BadRequest(new { Message = "Token is required" });
                }

                var (isValid, fileId, userId) = await _downloadTokenService.ValidateTokenAsync(token);

                if (!isValid)
                {
                    _logger.LogWarning("Invalid or expired streaming token");
                    return BadRequest(new { Message = "Invalid or expired token" });
                }

                // Get file entity to access content
                var fileEntity = await _unitOfWork.Files.GetByIdAsync(fileId);
                if (fileEntity?.FileContent == null || fileEntity.FileContent.Length == 0)
                {
                    return NotFound(new { Message = "File content not found" });
                }

                // Verify this is a streamable file type
                if (!IsStreamableFileType(fileEntity.FileType, fileEntity.ContentType))
                {
                    return BadRequest(new { Message = "File type not suitable for streaming" });
                }

                // Record access
                await _fileService.RecordFileAccessAsync(fileId, FileAccessType.Preview);

                // Handle range requests
                var rangeHeader = Request.Headers["Range"].FirstOrDefault();
                var fileContent = fileEntity.FileContent;
                var fileSize = fileContent.Length;

                // Set common headers
                Response.Headers.Add("Accept-Ranges", "bytes");
                Response.Headers.Add("Content-Type", fileEntity.ContentType);
                Response.Headers.Add("Cache-Control", "public, max-age=3600");
                Response.Headers.Add("Last-Modified", fileEntity.UpdatedAt.ToString("R"));

                // Handle range request
                if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                {
                    return await HandleRangeRequest(rangeHeader, fileContent, fileEntity.ContentType);
                }

                // No range request - return entire file
                _logger.LogInformation("Streaming entire file {FileId} via token, size: {Size} bytes", fileId, fileSize);

                Response.Headers.Add("Content-Length", fileSize.ToString());

                return File(fileContent, fileEntity.ContentType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming file with token {Token}", token);
                return StatusCode(500, new { Message = "An error occurred while streaming the file" });
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
                var result = await _fileService.GetThumbnailStreamAsync(id);

                if (!result.Found)
                {
                    _logger.LogInformation("Thumbnail not returned for file {FileId}. Reason: {Reason}", id, result.Reason);
                    return NotFound(new { Message = "Thumbnail not found" });
                }

                Response.Headers.Add("Cache-Control", "public, max-age=86400");
                Response.Headers.Add("Content-Length", result.Stream.Length.ToString());

                return File(result.Stream, result.ContentType!, result.FileName!);
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
                var file = await _unitOfWork.Files.GetByIdAsync(id);
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

        /// <summary>
        /// Get files linked to a specific entity
        /// </summary>
        /// <param name="entityType">Entity type (e.g., "Category", "Product")</param>
        /// <param name="entityId">Entity identifier</param>
        /// <param name="fileType">Optional file type filter</param>
        /// <returns>List of files linked to the entity</returns>
        [HttpGet("entity")]
        [ProducesResponseType(typeof(List<FileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<FileDto>>> GetFilesForEntity(
            [FromQuery] string entityType,
            [FromQuery] int entityId,
            [FromQuery] FileType? fileType = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType))
                {
                    return BadRequest(new { Message = "Entity type is required" });
                }

                if (entityId <= 0)
                {
                    return BadRequest(new { Message = "Entity ID must be greater than 0" });
                }

                var searchDto = new FileSearchDto
                {
                    PageNumber = 1,
                    PageSize = 1000, // Get all files for the entity
                    EntityType = entityType,
                    EntityId = entityId,
                    FileType = fileType,
                    SortBy = "CreatedAt",
                    SortDirection = "Desc"
                };

                var result = await _fileService.SearchFilesPagedAsync(searchDto);
                var files = result.Data?.ToList() ?? new List<FileDto>();

                _logger.LogDebug("Retrieved {FileCount} files for entity {EntityType}:{EntityId}",
                    files.Count, entityType, entityId);

                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting files for entity {EntityType}:{EntityId}", entityType, entityId);
                return StatusCode(500, new { Message = "An error occurred while retrieving files" });
            }
        }

        /// <summary>
        /// Count files linked to a specific entity
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="entityId">Entity identifier</param>
        /// <param name="fileType">Optional file type filter</param>
        /// <returns>Number of files linked to the entity</returns>
        [HttpGet("entity/count")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> CountFilesForEntity(
            [FromQuery] string entityType,
            [FromQuery] int entityId,
            [FromQuery] FileType? fileType = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType))
                {
                    return BadRequest(new { Message = "Entity type is required" });
                }

                if (entityId <= 0)
                {
                    return BadRequest(new { Message = "Entity ID must be greater than 0" });
                }

                var searchDto = new FileSearchDto
                {
                    PageNumber = 1,
                    PageSize = 1,
                    EntityType = entityType,
                    EntityId = entityId,
                    FileType = fileType
                };

                var result = await _fileService.SearchFilesPagedAsync(searchDto);

                return Ok(new { Count = result.TotalCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting files for entity {EntityType}:{EntityId}", entityType, entityId);
                return StatusCode(500, new { Message = "An error occurred while counting files" });
            }
        }

        /// <summary>
        /// Delete all files linked to a specific entity
        /// </summary>
        /// <param name="deleteDto">Entity deletion data</param>
        /// <returns>Bulk operation result</returns>
        [HttpPost("delete-for-entity")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> DeleteFilesForEntity([FromBody] DeleteEntityFilesDto deleteDto)
        {
            try
            {
                if (deleteDto == null)
                {
                    return BadRequest(new { Message = "Delete data is required" });
                }

                if (string.IsNullOrWhiteSpace(deleteDto.EntityType))
                {
                    return BadRequest(new { Message = "Entity type is required" });
                }

                if (deleteDto.EntityId <= 0)
                {
                    return BadRequest(new { Message = "Entity ID must be greater than 0" });
                }

                // First, get all files for the entity
                var searchDto = new FileSearchDto
                {
                    PageNumber = 1,
                    PageSize = 1000,
                    EntityType = deleteDto.EntityType,
                    EntityId = deleteDto.EntityId
                };

                var result = await _fileService.SearchFilesPagedAsync(searchDto);
                var fileIds = result.Data?.Select(f => f.Id).ToList() ?? new List<int>();

                if (!fileIds.Any())
                {
                    return Ok(new BulkOperationResultDto
                    {
                        TotalRequested = 0,
                        SuccessCount = 0,
                        FailureCount = 0
                    });
                }

                // Delete all files
                var deleteResult = await _fileService.DeleteMultipleFilesAsync(fileIds);

                _logger.LogInformation("Deleted {SuccessCount} of {TotalCount} files for entity {EntityType}:{EntityId}",
                    deleteResult ? fileIds.Count : 0, fileIds.Count, deleteDto.EntityType, deleteDto.EntityId);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = fileIds.Count,
                    SuccessCount = deleteResult ? fileIds.Count : 0,
                    FailureCount = deleteResult ? 0 : fileIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting files for entity");
                return StatusCode(500, new { Message = "An error occurred while deleting files" });
            }
        }

        /// <summary>
        /// Upload file with entity linking in form data
        /// This is an override of the existing upload method to handle entity parameters
        /// </summary>
        [HttpPost("upload-for-entity")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(FileUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FileUploadResultDto>> UploadFileForEntity([FromForm] FileUploadForEntityDto uploadDto)
        {
            try
            {
                if (uploadDto?.File == null)
                {
                    return BadRequest(new { Message = "File is required" });
                }

                if (string.IsNullOrWhiteSpace(uploadDto.EntityType))
                {
                    return BadRequest(new { Message = "Entity type is required" });
                }

                if (uploadDto.EntityId <= 0)
                {
                    return BadRequest(new { Message = "Entity ID must be greater than 0" });
                }

                // Create the standard upload DTO
                var standardUploadDto = new FileUploadDto
                {
                    File = uploadDto.File,
                    Description = uploadDto.Description ?? $"File uploaded for {uploadDto.EntityType} {uploadDto.EntityId}",
                    Alt = uploadDto.Alt,
                    FolderId = uploadDto.FolderId,
                    IsPublic = uploadDto.IsPublic,
                    GenerateThumbnail = uploadDto.GenerateThumbnail,
                    ProcessImmediately = uploadDto.ProcessImmediately,
                    Tags = uploadDto.Tags ?? new Dictionary<string, object>()
                };

                // Add entity information to tags for tracking
                standardUploadDto.Tags["EntityType"] = uploadDto.EntityType;
                standardUploadDto.Tags["EntityId"] = uploadDto.EntityId.ToString();

                var result = await _fileService.UploadFileAsync(standardUploadDto);

                if (result != null)
                {
                    _logger.LogInformation("Successfully uploaded file {FileName} for entity {EntityType}:{EntityId}",
                        uploadDto.File.FileName, uploadDto.EntityType, uploadDto.EntityId);
                }

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "File upload validation failed for entity");
                return BadRequest(new FileUploadResultDto
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file for entity");
                return StatusCode(500, new FileUploadResultDto
                {
                    Success = false,
                    ErrorMessage = "An error occurred while uploading the file"
                });
            }
        }

        /// <summary>
        /// Upload multiple files with entity linking
        /// </summary>
        [HttpPost("upload-multiple-for-entity")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> UploadMultipleFilesForEntity([FromForm] MultipleFileUploadForEntityDto uploadDto)
        {
            try
            {
                if (uploadDto?.Files == null || !uploadDto.Files.Any())
                {
                    return BadRequest(new { Message = "At least one file is required" });
                }

                if (string.IsNullOrWhiteSpace(uploadDto.EntityType))
                {
                    return BadRequest(new { Message = "Entity type is required" });
                }

                if (uploadDto.EntityId <= 0)
                {
                    return BadRequest(new { Message = "Entity ID must be greater than 0" });
                }

                var standardUploadDto = new MultipleFileUploadDto
                {
                    Files = uploadDto.Files,
                    FolderId = uploadDto.FolderId,
                    IsPublic = uploadDto.IsPublic,
                    GenerateThumbnails = uploadDto.GenerateThumbnails,
                    ProcessImmediately = uploadDto.ProcessImmediately,
                    ProcessInParallel = uploadDto.ProcessInParallel
                };

                var results = await _fileService.UploadMultipleFilesAsync(standardUploadDto);

                // Update the uploaded files with entity information
                if (results != null)
                {
                    foreach (var file in results)
                    {
                        // Add entity tags to each uploaded file
                        var updateDto = new UpdateFileDto
                        {
                            Description = file.Description,
                            Alt = file.Alt,
                            IsPublic = file.IsPublic,
                            Tags = file.Tags ?? new Dictionary<string, object>()
                        };

                        updateDto.Tags["EntityType"] = uploadDto.EntityType;
                        updateDto.Tags["EntityId"] = uploadDto.EntityId.ToString();

                        try
                        {
                            await _fileService.UpdateFileAsync(file.Id, updateDto);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to update entity tags for file {FileId}", file.Id);
                        }
                    }

                    _logger.LogInformation("Successfully uploaded {FileCount} files for entity {EntityType}:{EntityId}",
                        results.Count, uploadDto.EntityType, uploadDto.EntityId);
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple files for entity");
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
        /// DTO for deleting files linked to an entity
        /// </summary>
        public class DeleteEntityFilesDto
        {
            [Required]
            [MaxLength(50)]
            public string EntityType { get; set; } = string.Empty;

            [Required]
            public int EntityId { get; set; }
        }

        /// <summary>
        /// DTO for uploading a file with entity linking
        /// </summary>
        public class FileUploadForEntityDto
        {
            [Required]
            public IFormFile File { get; set; } = null!;

            [MaxLength(1000)]
            public string? Description { get; set; }

            [MaxLength(255)]
            public string? Alt { get; set; }

            public int? FolderId { get; set; }

            public bool IsPublic { get; set; } = false;

            public bool GenerateThumbnail { get; set; } = true;

            public bool ProcessImmediately { get; set; } = true;

            public Dictionary<string, object>? Tags { get; set; }

            [Required]
            [MaxLength(50)]
            public string EntityType { get; set; } = string.Empty;

            [Required]
            public int EntityId { get; set; }
        }

        /// <summary>
        /// DTO for uploading multiple files with entity linking
        /// </summary>
        public class MultipleFileUploadForEntityDto
        {
            [Required]
            public IFormFileCollection Files { get; set; } = null!;

            public int? FolderId { get; set; }

            public bool IsPublic { get; set; } = false;

            public bool GenerateThumbnails { get; set; } = true;

            public bool ProcessImmediately { get; set; } = true;

            public bool ProcessInParallel { get; set; } = true;

            [Required]
            [MaxLength(50)]
            public string EntityType { get; set; } = string.Empty;

            [Required]
            public int EntityId { get; set; }
        }

        #region Private Helper Methods

        /// <summary>
        /// Handles HTTP range requests for video/audio streaming
        /// </summary>
        private async Task<ActionResult> HandleRangeRequest(string rangeHeader, byte[] fileContent, string contentType)
        {
            try
            {
                var fileSize = fileContent.Length;

 
                var range = rangeHeader.Substring(6); // Remove "bytes="
                var parts = range.Split('-');

                long start = 0;
                long end = fileSize - 1;

                if (!string.IsNullOrEmpty(parts[0]))
                {
                    start = long.Parse(parts[0]);
                }

                if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                {
                    end = Math.Min(long.Parse(parts[1]), fileSize - 1);
                }

                // Validate range
                if (start < 0 || start >= fileSize || end >= fileSize || start > end)
                {
                    _logger.LogWarning("Invalid range request: {Range} for file size {Size}", rangeHeader, fileSize);
                    Response.Headers.Add("Content-Range", $"bytes */{fileSize}");
                    return StatusCode(416); // Range Not Satisfiable
                }

                var contentLength = end - start + 1;

                // Set response headers for partial content
                Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{fileSize}");
                Response.Headers.Add("Content-Length", contentLength.ToString());

                // Create a stream for the requested range
                var rangeContent = new byte[contentLength];
                Array.Copy(fileContent, start, rangeContent, 0, contentLength);

                _logger.LogInformation("Serving partial content: bytes {Start}-{End}/{Total}", start, end, fileSize);

                Response.StatusCode = 206; // Partial Content
                return File(rangeContent, contentType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling range request: {Range}", rangeHeader);
                return StatusCode(500, new { Message = "Error processing range request" });
            }
        }

        /// <summary>
        /// Checks if a file type is suitable for streaming
        /// </summary>
        private static bool IsStreamableFileType(FileType fileType, string contentType)
        {
            if (fileType == FileType.Video)
            {
                return IsStreamableVideo(contentType);
            }

            if (fileType == FileType.Audio)
            {
                return IsStreamableAudio(contentType);
            }

            return false;
        }

        /// <summary>
        /// Checks if a video content type is streamable
        /// </summary>
        private static bool IsStreamableVideo(string contentType)
        {
            var streamableTypes = new[]
            {
                "video/mp4",
                "video/webm",
                "video/ogg",
                "video/quicktime",
                "video/x-msvideo", // AVI
                "video/x-ms-wmv",  // WMV
                "video/x-flv",     // FLV
                "video/3gpp",      // 3GP
                "video/x-matroska" // MKV
            };

            return streamableTypes.Any(type => contentType.Contains(type, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if an audio content type is streamable
        /// </summary>
        private static bool IsStreamableAudio(string contentType)
        {
            var streamableTypes = new[]
            {
                "audio/mpeg",
                "audio/mp4",
                "audio/ogg",
                "audio/wav",
                "audio/webm",
                "audio/x-ms-wma",
                "audio/x-wav"
            };

            return streamableTypes.Any(type => contentType.Contains(type, StringComparison.OrdinalIgnoreCase));
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

        #endregion

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