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
        private readonly IVideoFileService _videoService;
        private readonly ILogger<VideoController> _logger;

        public VideoController(
            IVideoFileService videoService,
            ILogger<VideoController> logger)
        {
            _videoService = videoService ?? throw new ArgumentNullException(nameof(videoService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of videos
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<VideoFileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<VideoFileDto>>> GetVideos([FromQuery] VideoSearchDto searchDto)
        {
            try
            {
                var result = await _videoService.GetVideosPagedAsync(searchDto);
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
        [ProducesResponseType(typeof(VideoFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VideoFileDto>> UploadVideo([FromForm] FileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.File == null)
                {
                    return BadRequest(new { Message = "Video file is required" });
                }

                var result = await _videoService.UploadVideoAsync(uploadDto);
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
        [ProducesResponseType(typeof(List<VideoFileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<VideoFileDto>>> UploadMultipleVideos([FromForm] MultipleFileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.Files == null || !uploadDto.Files.Any())
                {
                    return BadRequest(new { Message = "At least one video file is required" });
                }

                var results = await _videoService.UploadMultipleVideosAsync(uploadDto);
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
        [ProducesResponseType(typeof(VideoFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VideoFileDto>> GetVideo([FromRoute] int id)
        {
            try
            {
                var video = await _videoService.GetVideoByIdAsync(id);
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
        [ProducesResponseType(typeof(VideoFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<VideoFileDto>> UpdateVideo([FromRoute] int id, [FromBody] UpdateVideoDto updateDto)
        {
            try
            {
                if (updateDto == null)
                {
                    return BadRequest(new { Message = "Update data is required" });
                }

                var result = await _videoService.UpdateVideoAsync(id, updateDto);
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
                var success = await _videoService.DeleteVideoAsync(id);
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
        /// Get videos by duration
        /// </summary>
        [HttpGet("by-duration")]
        [ProducesResponseType(typeof(List<VideoFileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<VideoFileDto>>> GetVideosByDuration(
            [FromQuery] TimeSpan? minDuration = null,
            [FromQuery] TimeSpan? maxDuration = null)
        {
            try
            {
                var videos = await _videoService.GetVideosByDurationAsync(minDuration, maxDuration);
                return Ok(videos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting videos by duration");
                return StatusCode(500, new { Message = "An error occurred while retrieving videos" });
            }
        }

    }
}