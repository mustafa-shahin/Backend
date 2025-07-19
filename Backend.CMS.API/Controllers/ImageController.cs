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
        private readonly IImageFileService _imageService;
        private readonly ILogger<ImageController> _logger;

        public ImageController(
            IImageFileService imageService,
            ILogger<ImageController> logger)
        {
            _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of images with image-specific filtering
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<ImageFileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<ImageFileDto>>> GetImages([FromQuery] ImageSearchDto searchDto)
        {
            try
            {
                var result = await _imageService.GetImagesPagedAsync(searchDto);
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
        [ProducesResponseType(typeof(ImageFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ImageFileDto>> UploadImage([FromForm] FileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.File == null)
                {
                    return BadRequest(new { Message = "Image file is required" });
                }

                var result = await _imageService.UploadImageAsync(uploadDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Image upload validation failed");
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
        [ProducesResponseType(typeof(List<ImageFileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ImageFileDto>>> UploadMultipleImages([FromForm] MultipleFileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.Files == null || !uploadDto.Files.Any())
                {
                    return BadRequest(new { Message = "At least one image file is required" });
                }

                var results = await _imageService.UploadMultipleImagesAsync(uploadDto);
                return Ok(results);
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
        [ProducesResponseType(typeof(ImageFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ImageFileDto>> GetImage([FromRoute] int id)
        {
            try
            {
                var image = await _imageService.GetImageByIdAsync(id);
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
        [ProducesResponseType(typeof(ImageFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ImageFileDto>> UpdateImage([FromRoute] int id, [FromBody] UpdateImageDto updateDto)
        {
            try
            {
                if (updateDto == null)
                {
                    return BadRequest(new { Message = "Update data is required" });
                }

                var result = await _imageService.UpdateImageAsync(id, updateDto);
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
                var success = await _imageService.DeleteImageAsync(id);
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

                var success = await _imageService.GenerateThumbnailAsync(id, width, height);
                if (!success)
                    return NotFound(new { Message = "Image not found or thumbnail generation failed" });

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
                var success = await _imageService.ExtractMetadataAsync(id);
                if (!success)
                    return NotFound(new { Message = "Image not found or metadata extraction failed" });

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
        [ProducesResponseType(typeof(List<ImageFileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ImageFileDto>>> GetImagesByDimensions(
            [FromQuery] int? minWidth = null,
            [FromQuery] int? maxWidth = null,
            [FromQuery] int? minHeight = null,
            [FromQuery] int? maxHeight = null)
        {
            try
            {
                var images = await _imageService.GetImagesByDimensionsAsync(minWidth, maxWidth, minHeight, maxHeight);
                return Ok(images);
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

                var result = await _imageService.BulkGenerateThumbnailsAsync(
                    bulkDto.ImageIds,
                    bulkDto.Width,
                    bulkDto.Height);

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
    }
}