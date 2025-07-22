using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// Specialized controller for video file operations
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    [EnableRateLimiting("ApiPolicy")]
    public class VideoController : ControllerBase
    {
        private readonly IVideoService _videoService;
        private readonly ILogger<VideoController> _logger;

        public VideoController(
            IVideoService videoService,
            ILogger<VideoController> logger)
        {
            _videoService = videoService ?? throw new ArgumentNullException(nameof(videoService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of videos
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<VideoDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<VideoDto>>> GetVideos([FromQuery] VideoSearchDto searchDto)
        {
            try
            {
                var result = await _videoService.GetPagedAsync(searchDto.PageNumber, searchDto.PageSize);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting videos");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving videos");
                return StatusCode(500, new { Message = "An error occurred while retrieving videos" });
            }
        }

        /// <summary>
        /// Upload a single video file
        /// </summary>
        [HttpPost("upload")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(VideoDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VideoDto>> UploadVideo([FromForm] FileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.File == null)
                {
                    return BadRequest(new { Message = "Video file is required" });
                }

                // Convert to CreateVideoDto
                var createDto = new CreateVideoDto
                {
                    Name = uploadDto.Name ?? uploadDto.File.FileName,
                    Description = uploadDto.Description,
                    FolderId = uploadDto.FolderId,
                    ContentType = uploadDto.File.ContentType
                };

                using var memoryStream = new MemoryStream();
                await uploadDto.File.CopyToAsync(memoryStream);
                createDto.Content = memoryStream.ToArray();
                var result = await _videoService.CreateAsync(createDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Video upload validation failed");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading video");
                return StatusCode(500, new { Message = "An error occurred while uploading the video" });
            }
        }

        /// <summary>
        /// Upload multiple video files
        /// </summary>
        [HttpPost("upload/multiple")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(List<VideoDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<VideoDto>>> UploadMultipleVideos([FromForm] MultipleFileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.Files == null || !uploadDto.Files.Any())
                {
                    return BadRequest(new { Message = "At least one video file is required" });
                }

                // Basic implementation - should be moved to service
                var results = new List<VideoDto>();
                foreach (var file in uploadDto.Files)
                {
                    var createDto = new CreateVideoDto
                    {
                        Name = file.FileName,
                        Description = uploadDto.Description,
                        FolderId = uploadDto.FolderId
                    };
                    var result = await _videoService.CreateAsync(createDto);
                    results.Add(result);
                }
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple videos");
                return StatusCode(500, new { Message = "An error occurred while uploading videos" });
            }
        }

        /// <summary>
        /// Get video by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(VideoDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VideoDto>> GetVideo([FromRoute] int id)
        {
            try
            {
                var video = await _videoService.GetByIdAsync(id);
                if (video == null)
                {
                    return NotFound(new { Message = "Video not found" });
                }

                return Ok(video);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving video {VideoId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the video" });
            }
        }

        /// <summary>
        /// Update video information
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(VideoDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VideoDto>> UpdateVideo([FromRoute] int id, [FromBody] UpdateVideoDto updateDto)
        {
            try
            {
                if (updateDto == null)
                {
                    return BadRequest(new { Message = "Update data is required" });
                }

                var result = await _videoService.UpdateAsync(id, updateDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating video {VideoId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the video" });
            }
        }

        /// <summary>
        /// Delete video
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteVideo([FromRoute] int id)
        {
            try
            {
                var success = await _videoService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { Message = "Video not found" });

                return Ok(new { Message = "Video deleted successfully", VideoId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting video {VideoId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the video" });
            }
        }

        /// <summary>
        /// Get videos by duration (in seconds)
        /// </summary>
        [HttpGet("by-duration")]
        [ProducesResponseType(typeof(List<VideoDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<VideoDto>>> GetVideosByDuration(
            [FromQuery] int? minDuration = null,
            [FromQuery] int? maxDuration = null)
        {
            try
            {
                // Basic implementation using existing methods
                var allVideos = await _videoService.GetAllAsync();
                var filteredVideos = allVideos.Where(v => 
                    (!minDuration.HasValue || v.Duration >= minDuration) &&
                    (!maxDuration.HasValue || v.Duration <= maxDuration)
                ).ToList();
                return Ok(filteredVideos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting videos by duration");
                return StatusCode(500, new { Message = "An error occurred while retrieving videos" });
            }
        }

        /// <summary>
        /// Get videos linked to a specific entity
        /// </summary>
        [HttpGet("entity")]
        [ProducesResponseType(typeof(List<VideoDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<VideoDto>>> GetVideosForEntity(
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

                var videos = await _videoService.GetVideosByEntityAsync(entityType, entityId);
                return Ok(videos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting videos for entity {EntityType}:{EntityId}", entityType, entityId);
                return StatusCode(500, new { Message = "An error occurred while retrieving videos" });
            }
        }

    }
}