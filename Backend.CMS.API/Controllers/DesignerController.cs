using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/designer")]
    [Authorize]
    public class DesignerController : ControllerBase
    {
        private readonly IDesignerService _designerService;
        private readonly IPageContentValidationService _contentValidationService;
        private readonly ILogger<DesignerController> _logger;

        public DesignerController(
            IDesignerService designerService,
            IPageContentValidationService contentValidationService,
            ILogger<DesignerController> logger)
        {
            _designerService = designerService ?? throw new ArgumentNullException(nameof(designerService));
            _contentValidationService = contentValidationService ?? throw new ArgumentNullException(nameof(contentValidationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Page Designer Operations

        /// <summary>
        /// Get page data for the designer
        /// </summary>
        [HttpGet("pages/{pageId:int}")]
        public async Task<ActionResult<DesignerPageDto>> GetDesignerPage(int pageId)
        {
            try
            {
                var page = await _designerService.GetDesignerPageAsync(pageId);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Designer page not found: {PageId}", pageId);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting designer page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while retrieving the page" });
            }
        }

        /// <summary>
        /// Save complete page content and structure (created by frontend)
        /// </summary>
        [HttpPost("pages/{pageId:int}/save")]
        public async Task<ActionResult<DesignerPageDto>> SaveDesignerPage(int pageId, [FromBody] SaveDesignerPageDto saveDto)
        {
            try
            {
                saveDto.PageId = pageId;
                var page = await _designerService.SaveDesignerPageAsync(saveDto);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid save request for page {PageId}: {Message}", pageId, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving designer page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while saving the page" });
            }
        }

        /// <summary>
        /// Auto-save page content (for draft versions)
        /// </summary>
        [HttpPost("pages/{pageId:int}/autosave")]
        public async Task<ActionResult<DesignerPageDto>> AutoSaveDesignerPage(int pageId, [FromBody] SaveDesignerPageDto saveDto)
        {
            try
            {
                saveDto.PageId = pageId;
                saveDto.AutoSave = true;
                saveDto.CreateVersion = false; // Don't create versions for auto-saves

                var page = await _designerService.SaveDesignerPageAsync(saveDto);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid auto-save request for page {PageId}: {Message}", pageId, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-saving designer page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while auto-saving the page" });
            }
        }

        /// <summary>
        /// Validate page structure before saving
        /// </summary>
        [HttpPost("pages/{pageId:int}/validate")]
        public async Task<ActionResult> ValidatePageStructure(int pageId, [FromBody] SaveDesignerPageDto saveDto)
        {
            try
            {
                var errors = new List<string>();

                // Validate layout structure
                if (saveDto.Layout.Any())
                {
                    var layoutErrors = _contentValidationService.GetValidationErrors(saveDto.Layout, "layout");
                    errors.AddRange(layoutErrors);
                }

                // Validate content structure
                if (saveDto.Content.Any())
                {
                    var contentErrors = _contentValidationService.GetValidationErrors(saveDto.Content, "content");
                    errors.AddRange(contentErrors);
                }

                // Validate settings and styles
                if (saveDto.Settings.Any())
                {
                    var settingsErrors = _contentValidationService.GetValidationErrors(saveDto.Settings, "settings");
                    errors.AddRange(settingsErrors);
                }

                if (saveDto.Styles.Any())
                {
                    var stylesErrors = _contentValidationService.GetValidationErrors(saveDto.Styles, "styles");
                    errors.AddRange(stylesErrors);
                }

                var isValid = !errors.Any();
                return Ok(new { IsValid = isValid, Errors = errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating page structure for page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while validating the page structure" });
            }
        }

        /// <summary>
        /// Delete page
        /// </summary>
        [HttpDelete("pages/{pageId:int}")]
        [AdminOrDev]
        public async Task<ActionResult> DeleteDesignerPage(int pageId)
        {
            try
            {
                var success = await _designerService.DeleteDesignerPageAsync(pageId);
                if (!success)
                    return NotFound(new { Message = "Page not found" });

                return Ok(new { Message = "Page deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting designer page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while deleting the page" });
            }
        }

        #endregion

        #region Preview and Publishing

        /// <summary>
        /// Generate preview URL for page
        /// </summary>
        [HttpPost("pages/{pageId:int}/preview")]
        public async Task<ActionResult<DesignerPreviewDto>> GeneratePreview(int pageId, [FromBody] GeneratePreviewDto? previewDto = null)
        {
            try
            {
                var preview = await _designerService.GeneratePreviewAsync(pageId, previewDto?.Settings);
                return Ok(preview);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid preview request for page {PageId}: {Message}", pageId, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating preview for page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while generating the preview" });
            }
        }

        /// <summary>
        /// Get preview content by token (public endpoint for the preview app)
        /// </summary>
        [HttpGet("preview/{previewToken}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetPreviewContent(string previewToken)
        {
            try
            {
                var previewContent = await _designerService.GetPreviewContentAsync(previewToken);
                return Content(previewContent, "application/json");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Preview not found: {PreviewToken}", previewToken);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting preview content for token {PreviewToken}", previewToken);
                return StatusCode(500, new { Message = "An error occurred while retrieving the preview" });
            }
        }

        /// <summary>
        /// Publish page to make it live
        /// </summary>
        [HttpPost("pages/{pageId:int}/publish")]
        public async Task<ActionResult<DesignerPageDto>> PublishPage(int pageId, [FromBody] PublishPageDto publishDto)
        {
            try
            {
                publishDto.PageId = pageId;
                var page = await _designerService.PublishPageAsync(publishDto);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid publish request for page {PageId}: {Message}", pageId, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while publishing the page" });
            }
        }

        /// <summary>
        /// Unpublish page to take it offline
        /// </summary>
        [HttpPost("pages/{pageId:int}/unpublish")]
        public async Task<ActionResult<DesignerPageDto>> UnpublishPage(int pageId)
        {
            try
            {
                var page = await _designerService.UnpublishPageAsync(pageId);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid unpublish request for page {PageId}: {Message}", pageId, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpublishing page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while unpublishing the page" });
            }
        }

        #endregion

        #region Versioning

        /// <summary>
        /// Create a version snapshot of the current page
        /// </summary>
        [HttpPost("pages/{pageId:int}/versions")]
        public async Task<ActionResult<PageVersion>> CreateVersion(int pageId, [FromBody] CreateVersionDto createDto)
        {
            try
            {
                var version = await _designerService.CreateVersionAsync(pageId, createDto.ChangeNotes);
                return Ok(version);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid version creation request for page {PageId}: {Message}", pageId, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating version for page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while creating the version" });
            }
        }

        /// <summary>
        /// Get all versions for a page
        /// </summary>
        [HttpGet("pages/{pageId:int}/versions")]
        public async Task<ActionResult<List<PageVersionDto>>> GetPageVersions(int pageId)
        {
            try
            {
                var versions = await _designerService.GetPageVersionsAsync(pageId);
                return Ok(versions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting versions for page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while retrieving page versions" });
            }
        }

        /// <summary>
        /// Restore page from a specific version
        /// </summary>
        [HttpPost("pages/{pageId:int}/versions/{versionId:int}/restore")]
        public async Task<ActionResult<DesignerPageDto>> RestoreVersion(int pageId, int versionId)
        {
            try
            {
                var page = await _designerService.RestoreVersionAsync(pageId, versionId);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid version restore request for page {PageId}, version {VersionId}: {Message}", pageId, versionId, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring version {VersionId} for page {PageId}", versionId, pageId);
                return StatusCode(500, new { Message = "An error occurred while restoring the version" });
            }
        }

        #endregion

        #region Designer State Management

        /// <summary>
        /// Get current designer state for user (selected components, zoom, etc.)
        /// </summary>
        [HttpGet("pages/{pageId:int}/state")]
        public async Task<ActionResult<DesignerStateDto>> GetDesignerState(int pageId)
        {
            try
            {
                var state = await _designerService.GetDesignerStateAsync(pageId);
                return Ok(state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting designer state for page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while retrieving designer state" });
            }
        }

        /// <summary>
        /// Save current designer state for user
        /// </summary>
        [HttpPost("pages/{pageId:int}/state")]
        public async Task<ActionResult<DesignerStateDto>> SaveDesignerState(int pageId, [FromBody] DesignerStateDto stateDto)
        {
            try
            {
                stateDto.PageId = pageId;
                var state = await _designerService.SaveDesignerStateAsync(stateDto);
                return Ok(state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving designer state for page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while saving designer state" });
            }
        }

        /// <summary>
        /// Clear designer state for user
        /// </summary>
        [HttpDelete("pages/{pageId:int}/state")]
        public async Task<ActionResult> ClearDesignerState(int pageId)
        {
            try
            {
                await _designerService.ClearDesignerStateAsync(pageId);
                return Ok(new { Message = "Designer state cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing designer state for page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while clearing designer state" });
            }
        }

        #endregion

    }
}