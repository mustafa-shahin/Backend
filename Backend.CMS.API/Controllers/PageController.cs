using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// Page management controller providing CRUD operations for pages
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    [EnableRateLimiting("ApiPolicy")]
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
        /// <param name="id">Page ID</param>
        /// <returns>Page information with full structure</returns>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(PageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PageDto>> GetPage([FromRoute] int id)
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
        /// Get page by slug (public endpoint for frontend) - returns only published pages
        /// </summary>
        /// <param name="slug">Page slug</param>
        /// <returns>Published page information</returns>
        [HttpGet("by-slug/{slug}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PageDto>> GetPageBySlug([FromRoute] string slug)
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
        /// Get paginated list of pages with advanced filtering and sorting
        /// </summary>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <param name="search">Optional search term</param>
        /// <param name="status">Optional status filter</param>
        /// <param name="parentPageId">Optional parent page filter</param>
        /// <param name="requiresLogin">Optional requires login filter</param>
        /// <param name="adminOnly">Optional admin only filter</param>
        /// <param name="template">Optional template filter</param>
        /// <param name="isPublished">Optional published filter</param>
        /// <param name="sortBy">Sort field (default: UpdatedAt)</param>
        /// <param name="sortDirection">Sort direction (Asc/Desc, default: Desc)</param>
        /// <param name="createdFrom">Optional created from date filter</param>
        /// <param name="createdTo">Optional created to date filter</param>
        /// <param name="updatedFrom">Optional updated from date filter</param>
        /// <param name="updatedTo">Optional updated to date filter</param>
        /// <returns>Paginated list of pages</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<PageListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<PageListDto>>> GetPages(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] PageStatus? status = null,
            [FromQuery] int? parentPageId = null,
            [FromQuery] bool? requiresLogin = null,
            [FromQuery] bool? adminOnly = null,
            [FromQuery] string? template = null,
            [FromQuery] bool? isPublished = null,
            [FromQuery] string sortBy = "UpdatedAt",
            [FromQuery] string sortDirection = "Desc",
            [FromQuery] DateTime? createdFrom = null,
            [FromQuery] DateTime? createdTo = null,
            [FromQuery] DateTime? updatedFrom = null,
            [FromQuery] DateTime? updatedTo = null)
        {
            try
            {
                var searchDto = new PageSearchDto
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SearchTerm = search,
                    Status = status,
                    ParentPageId = parentPageId,
                    RequiresLogin = requiresLogin,
                    AdminOnly = adminOnly,
                    Template = template,
                    IsPublished = isPublished,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    CreatedFrom = createdFrom,
                    CreatedTo = createdTo,
                    UpdatedFrom = updatedFrom,
                    UpdatedTo = updatedTo
                };

                var result = await _pageService.GetPagesPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting pages");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pages");
                return StatusCode(500, new { Message = "An error occurred while retrieving pages" });
            }
        }

        /// <summary>
        /// Advanced page search with filtering options
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>Paginated search results</returns>
        [HttpPost("search")]
        [ProducesResponseType(typeof(PaginatedResult<PageListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<PageListDto>>> SearchPages([FromBody] PageSearchDto searchDto)
        {
            try
            {
                if (searchDto == null)
                {
                    return BadRequest(new { Message = "Search criteria is required" });
                }

                var result = await _pageService.SearchPagesPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid search criteria");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching pages");
                return StatusCode(500, new { Message = "An error occurred while searching pages" });
            }
        }

        /// <summary>
        /// Get page hierarchy for navigation
        /// </summary>
        /// <returns>Hierarchical list of pages</returns>
        [HttpGet("hierarchy")]
        [ProducesResponseType(typeof(List<PageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <returns>List of published pages accessible to current user</returns>
        [HttpGet("published")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<PageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="createPageDto">Page creation data</param>
        /// <returns>Created page information</returns>
        [HttpPost]
        [ProducesResponseType(typeof(PageDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="id">Page ID</param>
        /// <param name="updatePageDto">Page update data</param>
        /// <returns>Updated page information</returns>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin, Dev")]
        [ProducesResponseType(typeof(PageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PageDto>> UpdatePage([FromRoute] int id, [FromBody] UpdatePageDto updatePageDto)
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
        /// Save page structure (components and layout) - Used by the page designer
        /// </summary>
        /// <param name="id">Page ID</param>
        /// <param name="savePageStructureDto">Page structure data</param>
        /// <returns>Updated page information</returns>
        [HttpPost("{id:int}/structure")]
        [ProducesResponseType(typeof(PageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PageDto>> SavePageStructure([FromRoute] int id, [FromBody] SavePageStructureDto savePageStructureDto)
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
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving page structure for {PageId}", id);
                return StatusCode(500, new { Message = "An error occurred while saving page structure" });
            }
        }

        /// <summary>
        /// Delete a page
        /// </summary>
        /// <param name="id">Page ID</param>
        /// <returns>Deletion confirmation</returns>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeletePage([FromRoute] int id)
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
        /// Publish a page
        /// </summary>
        /// <param name="id">Page ID</param>
        /// <returns>Published page information</returns>
        [HttpPost("{id:int}/publish")]
        [ProducesResponseType(typeof(PageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PageDto>> PublishPage([FromRoute] int id)
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
        /// <param name="id">Page ID</param>
        /// <returns>Unpublished page information</returns>
        [HttpPost("{id:int}/unpublish")]
        [ProducesResponseType(typeof(PageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PageDto>> UnpublishPage([FromRoute] int id)
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
        /// <param name="id">Page ID to duplicate</param>
        /// <param name="duplicatePageDto">Duplication parameters</param>
        /// <returns>Newly created page information</returns>
        [HttpPost("{id:int}/duplicate")]
        [ProducesResponseType(typeof(PageDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PageDto>> DuplicatePage([FromRoute] int id, [FromBody] DuplicatePageDto duplicatePageDto)
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
        /// <param name="slug">Slug to validate</param>
        /// <param name="excludePageId">Optional page ID to exclude from validation</param>
        /// <returns>Validation result</returns>
        [HttpGet("validate-slug")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> ValidateSlug([FromQuery][Required] string slug, [FromQuery] int? excludePageId = null)
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
        /// <param name="id">Page ID</param>
        /// <param name="createVersionDto">Version creation data</param>
        /// <returns>Updated page information</returns>
        [HttpPost("{id:int}/versions")]
        [ProducesResponseType(typeof(PageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PageDto>> CreatePageVersion([FromRoute] int id, [FromBody] CreatePageVersionDto createVersionDto)
        {
            try
            {
                await _pageService.CreatePageVersionAsync(id, createVersionDto.ChangeNotes);
                var page = await _pageService.GetPageByIdAsync(id);
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
        /// <param name="id">Page ID</param>
        /// <returns>List of page versions</returns>
        [HttpGet("{id:int}/versions")]
        [ProducesResponseType(typeof(List<PageVersionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<PageVersionDto>>> GetPageVersions([FromRoute] int id)
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
        /// <param name="id">Page ID</param>
        /// <param name="versionId">Version ID to restore</param>
        /// <returns>Restored page information</returns>
        [HttpPost("{id:int}/versions/{versionId:int}/restore")]
        [ProducesResponseType(typeof(PageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PageDto>> RestorePageVersion([FromRoute] int id, [FromRoute] int versionId)
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