using Backend.CMS.API.Authorization;
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
    /// Category management controller providing category operations with API versioning and pagination
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiVersion("2.0")]
    [EnableRateLimiting("ApiPolicy")]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(ICategoryService categoryService, ILogger<CategoryController> logger)
        {
            _categoryService = categoryService;
            _logger = logger;
        }

        /// <summary>
        /// Get paginated list of categories with optional filtering
        /// </summary>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <param name="parentCategoryId">Optional parent category ID to filter by</param>
        /// <param name="isActive">Optional active status filter</param>
        /// <param name="isVisible">Optional visibility filter</param>
        /// <param name="sortBy">Sort field (default: Name)</param>
        /// <param name="sortDirection">Sort direction (Asc/Desc, default: Asc)</param>
        /// <returns>Paginated list of categories</returns>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<CategoryDto>>> GetCategories(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int? parentCategoryId = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isVisible = null,
            [FromQuery] string sortBy = "Name",
            [FromQuery] string sortDirection = "Asc")
        {
            try
            {
                var searchDto = new CategorySearchDto
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    ParentCategoryId = parentCategoryId,
                    IsActive = isActive,
                    IsVisible = isVisible,
                    SortBy = sortBy,
                    SortDirection = sortDirection
                };

                var result = await _categoryService.GetCategoriesPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting categories");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving categories");
                return StatusCode(500, new { Message = "An error occurred while retrieving categories" });
            }
        }

        /// <summary>
        /// Get category tree structure (non-paginated for tree display)
        /// </summary>
        [HttpGet("tree")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<CategoryTreeDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<CategoryTreeDto>>> GetCategoryTree()
        {
            try
            {
                var categoryTree = await _categoryService.GetCategoryTreeAsync();
                return Ok(categoryTree);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category tree");
                return StatusCode(500, new { Message = "An error occurred while retrieving category tree" });
            }
        }

        /// <summary>
        /// Get root categories (categories without parent) with pagination
        /// </summary>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of root categories</returns>
        [HttpGet("root")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<CategoryDto>>> GetRootCategories(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _categoryService.GetRootCategoriesPagedAsync(pageNumber, pageSize);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting root categories");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving root categories");
                return StatusCode(500, new { Message = "An error occurred while retrieving root categories" });
            }
        }

        /// <summary>
        /// Get category by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CategoryDto>> GetCategory(int id)
        {
            try
            {
                var category = await _categoryService.GetCategoryByIdAsync(id);
                return Ok(category);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Category not found: {CategoryId}", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the category" });
            }
        }

        /// <summary>
        /// Get category by slug
        /// </summary>
        [HttpGet("by-slug/{slug}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CategoryDto>> GetCategoryBySlug(string slug)
        {
            try
            {
                var category = await _categoryService.GetCategoryBySlugAsync(slug);
                if (category == null)
                    return NotFound(new { Message = $"Category with slug '{slug}' not found" });

                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category by slug {Slug}", slug);
                return StatusCode(500, new { Message = "An error occurred while retrieving the category" });
            }
        }

        /// <summary>
        /// Get subcategories of a category with pagination
        /// </summary>
        /// <param name="id">Parent category ID</param>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of subcategories</returns>
        [HttpGet("{id:int}/subcategories")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<CategoryDto>>> GetSubCategories(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _categoryService.GetSubCategoriesPagedAsync(id, pageNumber, pageSize);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting subcategories");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subcategories for category {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving subcategories" });
            }
        }

        /// <summary>
        /// Advanced category search with filtering and pagination
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>Paginated search results</returns>
        [HttpPost("search")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<CategoryDto>>> SearchCategories([FromBody] CategorySearchDto searchDto)
        {
            try
            {
                if (searchDto == null)
                {
                    return BadRequest(new { Message = "Search criteria is required" });
                }

                var result = await _categoryService.SearchCategoriesPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid search criteria");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching categories");
                return StatusCode(500, new { Message = "An error occurred while searching categories" });
            }
        }

        /// <summary>
        /// Create a new category
        /// </summary>
        [HttpPost]
        [AdminOrDev]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryDto createCategoryDto)
        {
            try
            {
                var category = await _categoryService.CreateCategoryAsync(createCategoryDto);
                return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Category creation failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                return StatusCode(500, new { Message = "An error occurred while creating the category" });
            }
        }

        /// <summary>
        /// Update an existing category
        /// </summary>
        [HttpPut("{id:int}")]
        [AdminOrDev]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, [FromBody] UpdateCategoryDto updateCategoryDto)
        {
            try
            {
                var category = await _categoryService.UpdateCategoryAsync(id, updateCategoryDto);
                return Ok(category);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Category update failed for {CategoryId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the category" });
            }
        }

        /// <summary>
        /// Delete a category
        /// </summary>
        [HttpDelete("{id:int}")]
        [AdminOrDev]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteCategory(int id)
        {
            try
            {
                var success = await _categoryService.DeleteCategoryAsync(id);
                if (!success)
                    return NotFound(new { Message = "Category not found" });

                return Ok(new { Message = "Category deleted successfully" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Category deletion failed for {CategoryId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the category" });
            }
        }

        /// <summary>
        /// Move category to different parent
        /// </summary>
        [HttpPost("{id:int}/move")]
        [AdminOrDev]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CategoryDto>> MoveCategory(int id, [FromBody] MoveCategoryDto moveCategoryDto)
        {
            try
            {
                var category = await _categoryService.MoveCategoryAsync(id, moveCategoryDto.NewParentCategoryId);
                return Ok(category);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Category move failed for {CategoryId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving category {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while moving the category" });
            }
        }

        /// <summary>
        /// Reorder categories
        /// </summary>
        [HttpPost("reorder")]
        [AdminOrDev]
        [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<CategoryDto>>> ReorderCategories([FromBody] ReorderCategoriesDto reorderDto)
        {
            try
            {
                var categoryOrders = reorderDto.Categories.Select(c => (c.Id, c.SortOrder)).ToList();
                var categories = await _categoryService.ReorderCategoriesAsync(categoryOrders);
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering categories");
                return StatusCode(500, new { Message = "An error occurred while reordering categories" });
            }
        }

        /// <summary>
        /// Validate category slug
        /// </summary>
        [HttpGet("validate-slug")]
        [AdminOrDev]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> ValidateSlug([FromQuery] string slug, [FromQuery] int? excludeCategoryId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                    return BadRequest(new { Message = "Slug parameter is required" });

                var isValid = await _categoryService.ValidateSlugAsync(slug, excludeCategoryId);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating category slug");
                return StatusCode(500, new { Message = "An error occurred while validating the slug" });
            }
        }

        /// <summary>
        /// Check if category can be deleted
        /// </summary>
        [HttpGet("{id:int}/can-delete")]
        [AdminOrDev]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> CanDelete(int id)
        {
            try
            {
                var canDelete = await _categoryService.CanDeleteAsync(id);
                return Ok(new { CanDelete = canDelete });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if category can be deleted {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while checking delete permissions" });
            }
        }

        /// <summary>
        /// Add image to category
        /// </summary>
        [HttpPost("{id:int}/images")]
        [AdminOrDev]
        [ProducesResponseType(typeof(CategoryImageDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CategoryImageDto>> AddCategoryImage(int id, [FromBody] CreateCategoryImageDto createImageDto)
        {
            try
            {
                var image = await _categoryService.AddCategoryImageAsync(id, createImageDto);
                return CreatedAtAction(nameof(GetCategory), new { id }, image);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Add category image failed for {CategoryId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding image to category {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while adding the image" });
            }
        }

        /// <summary>
        /// Update category image
        /// </summary>
        [HttpPut("images/{imageId:int}")]
        [AdminOrDev]
        [ProducesResponseType(typeof(CategoryImageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CategoryImageDto>> UpdateCategoryImage(int imageId, [FromBody] UpdateCategoryImageDto updateImageDto)
        {
            try
            {
                var image = await _categoryService.UpdateCategoryImageAsync(imageId, updateImageDto);
                return Ok(image);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Update category image failed for {ImageId}: {Message}", imageId, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category image {ImageId}", imageId);
                return StatusCode(500, new { Message = "An error occurred while updating the image" });
            }
        }

        /// <summary>
        /// Delete category image
        /// </summary>
        [HttpDelete("images/{imageId:int}")]
        [AdminOrDev]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteCategoryImage(int imageId)
        {
            try
            {
                var success = await _categoryService.DeleteCategoryImageAsync(imageId);
                if (!success)
                    return NotFound(new { Message = "Category image not found" });

                return Ok(new { Message = "Category image deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category image {ImageId}", imageId);
                return StatusCode(500, new { Message = "An error occurred while deleting the image" });
            }
        }

        /// <summary>
        /// Reorder category images
        /// </summary>
        [HttpPost("{id:int}/images/reorder")]
        [AdminOrDev]
        [ProducesResponseType(typeof(List<CategoryImageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<CategoryImageDto>>> ReorderCategoryImages(int id, [FromBody] ReorderCategoryImagesDto reorderDto)
        {
            try
            {
                var imageOrders = reorderDto.Images.Select(i => (i.Id, i.Position)).ToList();
                var images = await _categoryService.ReorderCategoryImagesAsync(id, imageOrders);
                return Ok(images);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering category images for category {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while reordering images" });
            }
        }

        // Supporting DTOs for controller actions
        public class MoveCategoryDto
        {
            [Required]
            public int? NewParentCategoryId { get; set; }
        }

        public class ReorderCategoriesDto
        {
            [Required]
            public List<CategoryOrderDto> Categories { get; set; } = new();
        }

        public class CategoryOrderDto
        {
            [Required]
            public int Id { get; set; }

            [Required]
            public int SortOrder { get; set; }
        }

        public class ReorderCategoryImagesDto
        {
            [Required]
            public List<CategoryImageOrderDto> Images { get; set; } = new();
        }

        public class CategoryImageOrderDto
        {
            [Required]
            public int Id { get; set; }

            [Required]
            public int Position { get; set; }
        }
    }
}