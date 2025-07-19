using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// Specialized controller for archive file operations
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    [EnableRateLimiting("ApiPolicy")]
    public class ArchiveController : ControllerBase
    {
        private readonly IArchiveFileService _archiveService;
        private readonly ILogger<ArchiveController> _logger;

        public ArchiveController(
            IArchiveFileService archiveService,
            ILogger<ArchiveController> logger)
        {
            _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of archives
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<ArchiveFileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<ArchiveFileDto>>> GetArchives([FromQuery] ArchiveSearchDto searchDto)
        {
            try
            {
                var result = await _archiveService.GetArchivesPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting archives");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving archives");
                return StatusCode(500, new { Message = "An error occurred while retrieving archives" });
            }
        }

        /// <summary>
        /// Upload a single archive file
        /// </summary>
        [HttpPost("upload")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(ArchiveFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ArchiveFileDto>> UploadArchive([FromForm] FileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.File == null)
                {
                    return BadRequest(new { Message = "Archive file is required" });
                }

                var result = await _archiveService.UploadArchiveAsync(uploadDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Archive upload validation failed");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading archive");
                return StatusCode(500, new { Message = "An error occurred while uploading the archive" });
            }
        }

        /// <summary>
        /// Upload multiple archive files
        /// </summary>
        [HttpPost("upload/multiple")]
        [EnableRateLimiting("FileUploadPolicy")]
        [ProducesResponseType(typeof(List<ArchiveFileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ArchiveFileDto>>> UploadMultipleArchives([FromForm] MultipleFileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.Files == null || !uploadDto.Files.Any())
                {
                    return BadRequest(new { Message = "At least one archive file is required" });
                }

                var results = await _archiveService.UploadMultipleArchivesAsync(uploadDto);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading multiple archives");
                return StatusCode(500, new { Message = "An error occurred while uploading archives" });
            }
        }

        /// <summary>
        /// Get archive by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ArchiveFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ArchiveFileDto>> GetArchive([FromRoute] int id)
        {
            try
            {
                var archive = await _archiveService.GetArchiveByIdAsync(id);
                if (archive == null)
                {
                    return NotFound(new { Message = "Archive not found" });
                }

                return Ok(archive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving archive {ArchiveId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the archive" });
            }
        }

        /// <summary>
        /// Update archive information
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ArchiveFileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ArchiveFileDto>> UpdateArchive([FromRoute] int id, [FromBody] UpdateArchiveDto updateDto)
        {
            try
            {
                if (updateDto == null)
                {
                    return BadRequest(new { Message = "Update data is required" });
                }

                var result = await _archiveService.UpdateArchiveAsync(id, updateDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating archive {ArchiveId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the archive" });
            }
        }

        /// <summary>
        /// Delete archive
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteArchive([FromRoute] int id)
        {
            try
            {
                var success = await _archiveService.DeleteArchiveAsync(id);
                if (!success)
                    return NotFound(new { Message = "Archive not found" });

                return Ok(new { Message = "Archive deleted successfully", ArchiveId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting archive {ArchiveId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the archive" });
            }
        }
    }
}