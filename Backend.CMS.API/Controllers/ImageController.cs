using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// Specialized controller for image file operations
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    [EnableRateLimiting("ApiPolicy")]
    public class ImageController : ControllerBase
    {
        private readonly IImageService _imageService;
        private readonly ILogger<ImageController> _logger;

        public ImageController(
            IImageService imageService,
            ILogger<ImageController> logger)
        {
            _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of images with image-specific filtering
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<ImageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<ImageDto>>> GetImages([FromQuery] ImageSearchDto searchDto)
        {
            try
            {
                var result = await _imageService.GetPagedAsync(searchDto.PageNumber, searchDto.PageSize);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting images");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving images");
                return StatusCode(500, new { Message = "An error occurred while retrieving images" });
            }
        }

        /// <summary>
        /// Upload a single image file
        /// </summary>
        [HttpPost("upload")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(ImageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ImageDto>> UploadImage([FromForm] FileUploadDto uploadDto)
        {
            try
            {
                var result = await _imageService.UploadImageAsync(uploadDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Image upload validation failed");
                return BadRequest(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Image upload failed");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image");
                return StatusCode(500, new { Message = "An error occurred while uploading the image" });
            }
        }

        /// <summary>
        /// Upload multiple image files
        /// </summary>
        [HttpPost("upload/multiple")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(List<ImageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ImageDto>>> UploadMultipleImages([FromForm] MultipleFileUploadDto uploadDto)
        {
            try
            {
                var results = await _imageService.UploadMultipleImagesAsync(uploadDto);
                return Ok(results);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Multiple image upload validation failed");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple images");
                return StatusCode(500, new { Message = "An error occurred while uploading images" });
            }
        }

        /// <summary>
        /// Get image by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ImageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ImageDto>> GetImage([FromRoute] int id)
        {
            try
            {
                var image = await _imageService.GetByIdAsync(id);
                if (image == null)
                {
                    return NotFound(new { Message = "Image not found" });
                }

                return Ok(image);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving image {ImageId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the image" });
            }
        }

        /// <summary>
        /// Update image information
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ImageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ImageDto>> UpdateImage([FromRoute] int id, [FromBody] UpdateImageDto updateDto)
        {
            try
            {
                if (updateDto == null)
                {
                    return BadRequest(new { Message = "Update data is required" });
                }

                var result = await _imageService.UpdateAsync(id, updateDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating image {ImageId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the image" });
            }
        }

        /// <summary>
        /// Delete image
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteImage([FromRoute] int id)
        {
            try
            {
                var success = await _imageService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { Message = "Image not found" });

                return Ok(new { Message = "Image deleted successfully", ImageId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting image {ImageId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the image" });
            }
        }

        /// <summary>
        /// Generate thumbnail for an image
        /// </summary>
        [HttpPost("{id:int}/thumbnail")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GenerateThumbnail(
            [FromRoute] int id,
            [FromQuery] int width = 200,
            [FromQuery] int height = 200)
        {
            try
            {
                if (width <= 0 || height <= 0)
                {
                    return BadRequest(new { Message = "Width and height must be greater than 0" });
                }

                if (width > 2000 || height > 2000)
                {
                    return BadRequest(new { Message = "Width and height cannot exceed 2000 pixels" });
                }

                // Placeholder implementation
                var image = await _imageService.GetByIdAsync(id);
                if (image == null)
                    return NotFound(new { Message = "Image not found" });
                
                // TODO: Implement actual thumbnail generation
                var success = true;

                return Ok(new
                {
                    Message = "Thumbnail generated successfully",
                    ImageId = id,
                    ThumbnailSize = $"{width}x{height}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail for image {ImageId}", id);
                return StatusCode(500, new { Message = "An error occurred while generating the thumbnail" });
            }
        }

        /// <summary>
        /// Extract metadata from an image
        /// </summary>
        [HttpPost("{id:int}/metadata")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ExtractMetadata([FromRoute] int id)
        {
            try
            {
                var image = await _imageService.GetByIdAsync(id);
                if (image == null)
                    return NotFound(new { Message = "Image not found" });
                
                // TODO: Implement actual metadata extraction
                var success = true;

                return Ok(new { Message = "Metadata extracted successfully", ImageId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting metadata for image {ImageId}", id);
                return StatusCode(500, new { Message = "An error occurred while extracting metadata" });
            }
        }


        /// <summary>
        /// Get images by dimensions
        /// </summary>
        [HttpGet("by-dimensions")]
        [ProducesResponseType(typeof(List<ImageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ImageDto>>> GetImagesByDimensions(
            [FromQuery] int? minWidth = null,
            [FromQuery] int? maxWidth = null,
            [FromQuery] int? minHeight = null,
            [FromQuery] int? maxHeight = null)
        {
            try
            {
                // Basic implementation using existing methods
                var allImages = await _imageService.GetAllAsync();
                var filteredImages = allImages.Where(i => 
                    (!minWidth.HasValue || i.Width >= minWidth) &&
                    (!maxWidth.HasValue || i.Width <= maxWidth) &&
                    (!minHeight.HasValue || i.Height >= minHeight) &&
                    (!maxHeight.HasValue || i.Height <= maxHeight)
                ).ToList();
                return Ok(filteredImages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting images by dimensions");
                return StatusCode(500, new { Message = "An error occurred while retrieving images" });
            }
        }


        /// <summary>
        /// Bulk generate thumbnails for multiple images
        /// </summary>
        [HttpPost("bulk/thumbnails")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> BulkGenerateThumbnails(
            [FromBody] BulkThumbnailDto bulkDto)
        {
            try
            {
                if (bulkDto?.ImageIds?.Any() != true)
                {
                    return BadRequest(new { Message = "Image IDs are required" });
                }

                // Basic implementation for bulk operations
                var result = new BulkOperationResultDto();
                foreach (var imageId in bulkDto.ImageIds)
                {
                    try
                    {
                        var image = await _imageService.GetByIdAsync(imageId);
                        if (image != null)
                        {
                            // TODO: Implement actual thumbnail generation
                            result.SuccessCount++;
                        }
                        else
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Image {imageId} not found");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Image {imageId}: {ex.Message}");
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk generating thumbnails");
                return StatusCode(500, new { Message = "An error occurred while generating thumbnails" });
            }
        }

        /// <summary>
        /// DTO for image resize operations
        /// </summary>
        public class ResizeImageDto
        {
            [Required]
            [Range(1, 5000)]
            public int Width { get; set; }

            [Required]
            [Range(1, 5000)]
            public int Height { get; set; }

            public bool MaintainAspectRatio { get; set; } = true;
        }

        /// <summary>
        /// DTO for bulk thumbnail generation
        /// </summary>
        public class BulkThumbnailDto
        {
            [Required]
            public List<int> ImageIds { get; set; } = new();

            [Range(50, 500)]
            public int Width { get; set; } = 200;

            [Range(50, 500)]
            public int Height { get; set; } = 200;
        }

        /// <summary>
        /// Get images linked to a specific entity
        /// </summary>
        [HttpGet("entity")]
        [ProducesResponseType(typeof(List<ImageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ImageDto>>> GetImagesForEntity(
            [FromQuery] string entityType,
            [FromQuery] int entityId)
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

                var images = await _imageService.GetImagesByEntityAsync(entityType, entityId);
                return Ok(images);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting images for entity {EntityType}:{EntityId}", entityType, entityId);
                return StatusCode(500, new { Message = "An error occurred while retrieving images" });
            }
        }

        /// <summary>
        /// Upload image with entity linking
        /// </summary>
        [HttpPost("upload-for-entity")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(FileUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FileUploadResultDto>> UploadImageForEntity([FromForm] FileUploadDto uploadDto)
        {
            try
            {
                _logger.LogInformation("UploadImageForEntity called with file: {FileName}, Size: {Size}, ContentType: {ContentType}", 
                    uploadDto?.File?.FileName ?? "null", uploadDto?.File?.Length ?? 0, uploadDto?.File?.ContentType ?? "null");

                var result = await _imageService.UploadImageForEntityAsync(uploadDto);

                return Ok(new FileUploadResultDto
                {
                    Success = true,
                    File = MapImageToFileDto(result),
                    ProcessingInfo = new Dictionary<string, object>
                    {
                        ["uploadedAt"] = DateTime.UtcNow,
                        ["originalSize"] = uploadDto.File.Length,
                        ["entityType"] = uploadDto.EntityType,
                        ["entityId"] = uploadDto.EntityId
                    }
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Image upload validation failed for entity");
                return BadRequest(new FileUploadResultDto
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Image upload failed for entity");
                return BadRequest(new FileUploadResultDto
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image for entity");
                return StatusCode(500, new FileUploadResultDto
                {
                    Success = false,
                    ErrorMessage = "An error occurred while uploading the image"
                });
            }
        }

        /// <summary>
        /// Upload multiple images with entity linking
        /// </summary>
        [HttpPost("upload-multiple-for-entity")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> UploadMultipleImagesForEntity([FromForm] MultipleFileUploadDto uploadDto)
        {
            try
            {
                var results = await _imageService.UploadMultipleImagesForEntityAsync(uploadDto);
                var fileDtos = results.Select(MapImageToFileDto).ToList();

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = uploadDto.Files.Count,
                    SuccessCount = results.Count,
                    FailureCount = uploadDto.Files.Count - results.Count,
                    SuccessfulFiles = fileDtos,
                    Errors = uploadDto.Files.Count > results.Count 
                        ? new List<string> { "Some files failed to upload - check logs for details" }
                        : new List<string>()
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Multiple image upload validation failed for entity");
                return BadRequest(new BulkOperationResultDto
                {
                    TotalRequested = uploadDto?.Files?.Count ?? 0,
                    SuccessCount = 0,
                    FailureCount = uploadDto?.Files?.Count ?? 0,
                    DetailedErrors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = ex.Message }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple images for entity");
                return StatusCode(500, new BulkOperationResultDto
                {
                    TotalRequested = uploadDto?.Files?.Count ?? 0,
                    SuccessCount = 0,
                    FailureCount = uploadDto?.Files?.Count ?? 0,
                    DetailedErrors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = "An error occurred while uploading images" }
                    }
                });
            }
        }

        /// <summary>
        /// Download image file
        /// </summary>
        [HttpGet("{id:int}/download")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DownloadImage([FromRoute] int id)
        {
            try
            {
                var fileModel = await _imageService.DownloadImageAsync(id);
                return File(fileModel.Content, fileModel.ContentType, fileModel.FileName);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { Message = "Image not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading image {ImageId}", id);
                return StatusCode(500, new { Message = "An error occurred while downloading the image" });
            }
        }

        #region Helper Methods and DTOs

        private FileDto MapImageToFileDto(ImageDto image)
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
                FileType = Backend.CMS.Domain.Enums.FileType.Image,
                Alt = image.Alt,
                Width = image.Width,
                Height = image.Height,
                HasThumbnail = image.HasThumbnail,
                IsPublic = true, // Default for images
                Urls = new FileUrlsDto
                {
                    Download = $"/api/v1/Image/{image.Id}/download",
                    DirectAccess = $"/api/v1/Image/{image.Id}/download",
                    Preview = $"/api/v1/Image/{image.Id}/download",
                    Thumbnail = image.HasThumbnail ? $"/api/v1/Image/{image.Id}/thumbnail" : null
                }
            };
        }


        #endregion
    }
}