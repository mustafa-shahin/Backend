using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs.Designer;
using Backend.CMS.Application.Interfaces;
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
        private readonly ILogger<DesignerController> _logger;

        public DesignerController(IDesignerService designerService, ILogger<DesignerController> logger)
        {
            _designerService = designerService;
            _logger = logger;
        }

        #region Page Designer Operations

        /// <summary>
        /// Get page data for the designer with grid layout
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
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting designer page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while retrieving the page" });
            }
        }

        /// <summary>
        /// Save page structure and components with grid positioning
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
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving designer page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while saving the page" });
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

        #region Component Management

        /// <summary>
        /// Create a new component on the page
        /// </summary>
        [HttpPost("pages/{pageId:int}/components")]
        public async Task<ActionResult<DesignerComponentDto>> CreateComponent(int pageId, [FromBody] CreateComponentDto createDto)
        {
            try
            {
                createDto.PageId = pageId;
                var component = await _designerService.CreateComponentAsync(createDto);
                return CreatedAtAction(nameof(GetDesignerPage), new { pageId }, component);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating component on page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while creating the component" });
            }
        }

        /// <summary>
        /// Update component properties, styles, and grid position
        /// </summary>
        [HttpPut("components/{componentId:int}")]
        public async Task<ActionResult<DesignerComponentDto>> UpdateComponent(int componentId, [FromBody] UpdateComponentDto updateDto)
        {
            try
            {
                updateDto.ComponentId = componentId;
                var component = await _designerService.UpdateComponentAsync(updateDto);
                return Ok(component);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating component {ComponentId}", componentId);
                return StatusCode(500, new { Message = "An error occurred while updating the component" });
            }
        }

        /// <summary>
        /// Duplicate component with new grid position
        /// </summary>
        [HttpPost("components/{componentId:int}/duplicate")]
        public async Task<ActionResult<DesignerComponentDto>> DuplicateComponent(int componentId, [FromBody] DuplicateComponentDto duplicateDto)
        {
            try
            {
                duplicateDto.ComponentId = componentId;
                var component = await _designerService.DuplicateComponentAsync(duplicateDto);
                return CreatedAtAction(nameof(UpdateComponent), new { componentId = component.Id }, component);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error duplicating component {ComponentId}", componentId);
                return StatusCode(500, new { Message = "An error occurred while duplicating the component" });
            }
        }

        /// <summary>
        /// Delete component from the page
        /// </summary>
        [HttpDelete("components/{componentId:int}")]
        public async Task<ActionResult> DeleteComponent(int componentId, [FromQuery] string componentKey)
        {
            try
            {
                var success = await _designerService.DeleteComponentAsync(componentId, componentKey);
                if (!success)
                    return NotFound(new { Message = "Component not found" });

                return Ok(new { Message = "Component deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting component {ComponentId}", componentId);
                return StatusCode(500, new { Message = "An error occurred while deleting the component" });
            }
        }

        /// <summary>
        /// Move component to new grid position
        /// </summary>
        [HttpPut("components/{componentId:int}/move")]
        public async Task<ActionResult<DesignerComponentDto>> MoveComponent(int componentId, [FromBody] MoveComponentDto moveDto)
        {
            try
            {
                moveDto.ComponentId = componentId;
                var component = await _designerService.MoveComponentAsync(moveDto);
                return Ok(component);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving component {ComponentId}", componentId);
                return StatusCode(500, new { Message = "An error occurred while moving the component" });
            }
        }

        /// <summary>
        /// Reorder multiple components on the grid
        /// </summary>
        [HttpPut("pages/{pageId:int}/components/reorder")]
        public async Task<ActionResult<List<DesignerComponentDto>>> ReorderComponents(int pageId, [FromBody] ReorderComponentsDto reorderDto)
        {
            try
            {
                var components = await _designerService.ReorderComponentsAsync(pageId, reorderDto.ComponentOrders);
                return Ok(components);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering components on page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while reordering components" });
            }
        }

        #endregion

        #region Component Library

        /// <summary>
        /// Get available component types for the designer
        /// </summary>
        [HttpGet("component-library")]
        public async Task<ActionResult<ComponentLibraryDto>> GetComponentLibrary()
        {
            try
            {
                var library = await _designerService.GetComponentLibraryAsync();
                return Ok(library);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting component library");
                return StatusCode(500, new { Message = "An error occurred while retrieving the component library" });
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
        public async Task<ActionResult<DesignerPageDto>> CreateVersion(int pageId, [FromBody] CreateVersionDto createDto)
        {
            try
            {
                var page = await _designerService.CreateVersionAsync(pageId, createDto.ChangeNotes);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
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

        #region Helper DTOs

        public class ReorderComponentsDto
        {
            public List<ComponentOrderDto> ComponentOrders { get; set; } = new();
        }

        #endregion
    }
}