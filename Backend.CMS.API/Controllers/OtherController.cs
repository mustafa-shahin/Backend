using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// Specialized controller for other file operations
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    [EnableRateLimiting("ApiPolicy")]
    public class OtherController : ControllerBase
    {
        private readonly IOtherFileService _otherService;
        private readonly ILogger<OtherController> _logger;

        public OtherController(
            IOtherFileService otherService,
            ILogger<OtherController> logger)
        {
            _otherService = otherService ?? throw new ArgumentNullException(nameof(otherService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of other files
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<OtherFileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<OtherFileDto>>> GetOtherFiles([FromQuery] OtherSearchDto searchDto)
        {
            try
            {
                var result = await _otherService.GetOtherFilesPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting other files");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving other files");
                return StatusCode(500, new { Message = "An error occurred while retrieving other files" });
            }
        }

        /// <summary>
        /// Upload a single other file
        /// </summary>
        [HttpPost("upload")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(OtherFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OtherFileDto>> UploadOtherFile([FromForm] FileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.File == null)
                {
                    return BadRequest(new { Message = "File is required" });
                }

                var result = await _otherService.UploadOtherFileAsync(uploadDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Other file upload validation failed");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading other file");
                return StatusCode(500, new { Message = "An error occurred while uploading the file" });
            }
        }

        /// <summary>
        /// Upload multiple other files
        /// </summary>
        [HttpPost("upload/multiple")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(List<OtherFileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<OtherFileDto>>> UploadMultipleOtherFiles([FromForm] MultipleFileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.Files == null || !uploadDto.Files.Any())
                {
                    return BadRequest(new { Message = "At least one file is required" });
                }

                var results = await _otherService.UploadMultipleOtherFilesAsync(uploadDto);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple other files");
                return StatusCode(500, new { Message = "An error occurred while uploading files" });
            }
        }

        /// <summary>
        /// Get other file by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(OtherFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OtherFileDto>> GetOtherFile([FromRoute] int id)
        {
            try
            {
                var file = await _otherService.GetOtherFileByIdAsync(id);
                if (file == null)
                {
                    return NotFound(new { Message = "File not found" });
                }

                return Ok(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving other file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the file" });
            }
        }

        /// <summary>
        /// Update other file information
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(OtherFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OtherFileDto>> UpdateOtherFile([FromRoute] int id, [FromBody] UpdateOtherDto updateDto)
        {
            try
            {
                if (updateDto == null)
                {
                    return BadRequest(new { Message = "Update data is required" });
                }

                var result = await _otherService.UpdateOtherFileAsync(id, updateDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating other file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the file" });
            }
        }

        /// <summary>
        /// Delete other file
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteOtherFile([FromRoute] int id)
        {
            try
            {
                var success = await _otherService.DeleteOtherFileAsync(id);
                if (!success)
                    return NotFound(new { Message = "File not found" });

                return Ok(new { Message = "File deleted successfully", FileId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting other file {FileId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the file" });
            }
        }
    }
}