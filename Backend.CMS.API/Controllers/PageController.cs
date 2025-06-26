using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PageController : ControllerBase
    {
        private readonly IPageService _pageService;
        private readonly ILogger<PageController> _logger;

        public PageController(IPageService pageService, ILogger<PageController> logger)
        {
            _pageService = pageService;
            _logger = logger;
        }

        /// <summary>
        /// Get page by ID with full structure
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<PageDto>> GetPage(int id)
        {
            try
            {
                var page = await _pageService.GetPageByIdAsync(id);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Page not found: {PageId}", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving page {PageId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the page" });
            }
        }

        /// <summary>
        /// Get page by slug (public endpoint for frontend)
        /// </summary>
        [HttpGet("by-slug/{slug}")]
        [AllowAnonymous]
        public async Task<ActionResult<PageDto>> GetPageBySlug(string slug)
        {
            try
            {
                var page = await _pageService.GetPageBySlugAsync(slug);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Page not found by slug: {Slug}", slug);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving page by slug {Slug}", slug);
                return StatusCode(500, new { Message = "An error occurred while retrieving the page" });
            }
        }

        /// <summary>
        /// Get paginated list of pages for admin
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<PageListDto>>> GetPages(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            try
            {
                var pages = await _pageService.GetPagesAsync(page, pageSize, search);
                // You would typically return a paged result with total count
                return Ok(new PagedResult<PageListDto>
                {
                    Items = pages,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = pages.Count // This should come from the service
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pages");
                return StatusCode(500, new { Message = "An error occurred while retrieving pages" });
            }
        }

        /// <summary>
        /// Get page hierarchy for navigation
        /// </summary>
        [HttpGet("hierarchy")]
        public async Task<ActionResult<List<PageDto>>> GetPageHierarchy()
        {
            try
            {
                var hierarchy = await _pageService.GetPageHierarchyAsync();
                return Ok(hierarchy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving page hierarchy");
                return StatusCode(500, new { Message = "An error occurred while retrieving page hierarchy" });
            }
        }

        /// <summary>
        /// Get published pages (public endpoint)
        /// </summary>
        [HttpGet("published")]
        [AllowAnonymous]
        public async Task<ActionResult<List<PageDto>>> GetPublishedPages()
        {
            try
            {
                var pages = await _pageService.GetPublishedPagesAsync();
                var accessiblePages = pages.Where(page =>
                                (!page.RequiresLogin || User.Identity.IsAuthenticated) &&
                                (!page.AdminOnly || IsAdmin())).ToList();

                return Ok(accessiblePages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving published pages");
                return StatusCode(500, new { Message = "An error occurred while retrieving published pages" });
            }
        }

        /// <summary>
        /// Create a new page
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<PageDto>> CreatePage([FromBody] CreatePageDto createPageDto)
        {
            try
            {
                var page = await _pageService.CreatePageAsync(createPageDto);
                return CreatedAtAction(nameof(GetPage), new { id = page.Id }, page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Page creation failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating page");
                return StatusCode(500, new { Message = "An error occurred while creating the page" });
            }
        }

        /// <summary>
        /// Update an existing page
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PageDto>> UpdatePage(int id, [FromBody] UpdatePageDto updatePageDto)
        {
            try
            {
                var page = await _pageService.UpdatePageAsync(id, updatePageDto);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Page update failed for {PageId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating page {PageId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the page" });
            }
        }

        /// <summary>
        /// Delete a page
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeletePage(int id)
        {
            try
            {
                var success = await _pageService.DeletePageAsync(id);
                if (!success)
                {
                    return NotFound(new { Message = "Page not found" });
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting page {PageId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the page" });
            }
        }

        /// <summary>
        /// Save page structure (components and layout)
        /// </summary>
        [HttpPost("{id:int}/structure")]
        public async Task<ActionResult<PageDto>> SavePageStructure(int id, [FromBody] SavePageStructureDto savePageStructureDto)
        {
            try
            {
                savePageStructureDto.PageId = id; // Ensure consistency
                var page = await _pageService.SavePageStructureAsync(savePageStructureDto);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Page structure save failed for {PageId}: {Message}", id, ex.Message);
                return BadRequest(new { ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving page structure for {PageId}", id);
                return StatusCode(500, new { Message = "An error occurred while saving page structure" });
            }
        }

        /// <summary>
        /// Publish a page
        /// </summary>
        [HttpPost("{id:int}/publish")]
        public async Task<ActionResult<PageDto>> PublishPage(int id)
        {
            try
            {
                var page = await _pageService.PublishPageAsync(id);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Page publish failed for {PageId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing page {PageId}", id);
                return StatusCode(500, new { Message = "An error occurred while publishing the page" });
            }
        }

        /// <summary>
        /// Unpublish a page
        /// </summary>
        [HttpPost("{id:int}/unpublish")]
        public async Task<ActionResult<PageDto>> UnpublishPage(int id)
        {
            try
            {
                var page = await _pageService.UnpublishPageAsync(id);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Page unpublish failed for {PageId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpublishing page {PageId}", id);
                return StatusCode(500, new { Message = "An error occurred while unpublishing the page" });
            }
        }

        /// <summary>
        /// Duplicate a page
        /// </summary>
        [HttpPost("{id:int}/duplicate")]
        public async Task<ActionResult<PageDto>> DuplicatePage(int id, [FromBody] DuplicatePageDto duplicatePageDto)
        {
            try
            {
                var page = await _pageService.DuplicatePageAsync(id, duplicatePageDto.NewName);
                return CreatedAtAction(nameof(GetPage), new { id = page.Id }, page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Page duplication failed for {PageId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error duplicating page {PageId}", id);
                return StatusCode(500, new { Message = "An error occurred while duplicating the page" });
            }
        }

        /// <summary>
        /// Validate page slug availability
        /// </summary>
        [HttpGet("validate-slug")]
        public async Task<ActionResult<bool>> ValidateSlug([FromQuery] string slug, [FromQuery] int? excludePageId = null)
        {
            try
            {
                var isValid = await _pageService.ValidateSlugAsync(slug, excludePageId);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating slug {Slug}", slug);
                return StatusCode(500, new { Message = "An error occurred while validating the slug" });
            }
        }

        /// <summary>
        /// Create page version
        /// </summary>
        [HttpPost("{id:int}/versions")]
        public async Task<ActionResult<PageDto>> CreatePageVersion(int id, [FromBody] CreatePageVersionDto createVersionDto)
        {
            try
            {
                var page = await _pageService.CreatePageVersionAsync(id, createVersionDto.ChangeNotes);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Page version creation failed for {PageId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating page version for {PageId}", id);
                return StatusCode(500, new { Message = "An error occurred while creating page version" });
            }
        }

        /// <summary>
        /// Get page versions
        /// </summary>
        [HttpGet("{id:int}/versions")]
        public async Task<ActionResult<List<PageVersionDto>>> GetPageVersions(int id)
        {
            try
            {
                var versions = await _pageService.GetPageVersionsAsync(id);
                return Ok(versions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving page versions for {PageId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving page versions" });
            }
        }

        /// <summary>
        /// Restore page from version
        /// </summary>
        [HttpPost("{id:int}/versions/{versionId:int}/restore")]
        public async Task<ActionResult<PageDto>> RestorePageVersion(int id, int versionId)
        {
            try
            {
                var page = await _pageService.RestorePageVersionAsync(id, versionId);
                return Ok(page);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Page version restore failed for {PageId}, version {VersionId}: {Message}", id, versionId, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring page version for {PageId}, version {VersionId}", id, versionId);
                return StatusCode(500, new { Message = "An error occurred while restoring page version" });
            }
        }
        private bool IsAdmin()
        {
            return User.IsInRole("Admin") || User.HasClaim("role", "Admin");
        }
    }
}