using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// Specialized controller for audio file operations
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    [EnableRateLimiting("ApiPolicy")]
    public class AudioController : ControllerBase
    {
        private readonly IAudioService _audioService;
        private readonly ILogger<AudioController> _logger;

        public AudioController(
            IAudioService audioService,
            ILogger<AudioController> logger)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of audio files
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<AudioDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<AudioDto>>> GetAudios([FromQuery] AudioSearchDto searchDto)
        {
            try
            {
                var result = await _audioService.GetAudiosPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting audio files");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audio files");
                return StatusCode(500, new { Message = "An error occurred while retrieving audio files" });
            }
        }

        /// <summary>
        /// Upload a single audio file
        /// </summary>
        [HttpPost("upload")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(AudioDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AudioDto>> UploadAudio([FromForm] FileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.File == null)
                {
                    return BadRequest(new { Message = "Audio file is required" });
                }

                var result = await _audioService.UploadAudioAsync(uploadDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Audio upload validation failed");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading audio");
                return StatusCode(500, new { Message = "An error occurred while uploading the audio file" });
            }
        }

        /// <summary>
        /// Upload multiple audio files
        /// </summary>
        [HttpPost("upload/multiple")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(List<AudioDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<AudioDto>>> UploadMultipleAudios([FromForm] MultipleFileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.Files == null || !uploadDto.Files.Any())
                {
                    return BadRequest(new { Message = "At least one audio file is required" });
                }

                var results = await _audioService.UploadMultipleAudiosAsync(uploadDto);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple audio files");
                return StatusCode(500, new { Message = "An error occurred while uploading audio files" });
            }
        }

        /// <summary>
        /// Get audio by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(AudioDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AudioDto>> GetAudio([FromRoute] int id)
        {
            try
            {
                var audio = await _audioService.GetAudioByIdAsync(id);
                if (audio == null)
                {
                    return NotFound(new { Message = "Audio file not found" });
                }

                return Ok(audio);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audio {AudioId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the audio file" });
            }
        }

        /// <summary>
        /// Update audio information
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(AudioDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AudioDto>> UpdateAudio([FromRoute] int id, [FromBody] UpdateAudioDto updateDto)
        {
            try
            {
                if (updateDto == null)
                {
                    return BadRequest(new { Message = "Update data is required" });
                }

                var result = await _audioService.UpdateAudioAsync(id, updateDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating audio {AudioId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the audio file" });
            }
        }

        /// <summary>
        /// Delete audio
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteAudio([FromRoute] int id)
        {
            try
            {
                var success = await _audioService.DeleteAudioAsync(id);
                if (!success)
                    return NotFound(new { Message = "Audio file not found" });

                return Ok(new { Message = "Audio file deleted successfully", AudioId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting audio {AudioId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the audio file" });
            }
        }

        /// <summary>
        /// Get audios linked to a specific entity
        /// </summary>
        [HttpGet("entity")]
        [ProducesResponseType(typeof(List<AudioDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<AudioDto>>> GetAudiosForEntity(
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

                var audios = await _audioService.GetAudiosByEntityAsync(entityType, entityId);
                return Ok(audios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audios for entity {EntityType}:{EntityId}", entityType, entityId);
                return StatusCode(500, new { Message = "An error occurred while retrieving audios" });
            }
        }
    }
}