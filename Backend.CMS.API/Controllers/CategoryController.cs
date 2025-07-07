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
    /// Category management controller providing category operations with API versioning and service-level pagination
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
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
        /// Get paginated list of categories with optional filtering (Service-level pagination - 10 items per page)
        /// </summary>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="parentCategoryId">Optional parent category ID to filter by</param>
        /// <param name="isActive">Optional active status filter</param>
        /// <param name="isVisible">Optional visibility filter</param>
        /// <param name="sortBy">Sort field (default: Name)</param>
        /// <param name="sortDirection">Sort direction (Asc/Desc, default: Asc)</param>
        /// <param name="searchTerm">Optional search term for name, description, or slug</param>
        /// <returns>Paginated list of categories (10 items per page)</returns>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]       
        public async Task<ActionResult<PagedResult<CategoryDto>>> GetCategories(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int? parentCategoryId = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isVisible = null,
            [FromQuery] string sortBy = "Name",
            [FromQuery] string sortDirection = "Asc",
            [FromQuery] string? searchTerm = null)
        {
            try
            {
                // Create search DTO with service-controlled pagination (10 items per page)
                var searchDto = new CategorySearchDto
                {
                    PageNumber = pageNumber,
                    PageSize = 10, // Fixed page size - controlled by service
                    ParentCategoryId = parentCategoryId,
                    IsActive = isActive,
                    IsVisible = isVisible,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    SearchTerm = searchTerm
                };

                // Service handles all pagination logic
                var result = await _categoryService.GetCategoriesPagedAsync(searchDto);

                _logger.LogDebug("Retrieved {ItemCount} categories for page {PageNumber} of {TotalPages}",
                    result.Data.Count, result.PageNumber, result.TotalPages);

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
                _logger.LogDebug("Retrieved category tree with {RootCount} root categories", categoryTree.Count);
                return Ok(categoryTree);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category tree");
                return StatusCode(500, new { Message = "An error occurred while retrieving category tree" });
            }
        }

        /// <summary>
        /// Get root categories with service-level pagination (10 items per page)
        /// </summary>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <returns>Paginated list of root categories (10 items per page)</returns>
        [HttpGet("root")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]        
        public async Task<ActionResult<PagedResult<CategoryDto>>> GetRootCategories(
            [FromQuery] int pageNumber = 1)
        {
            try
            {
                // Service controls page size (10 items per page)
                var result = await _categoryService.GetRootCategoriesPagedAsync(pageNumber, 10);

                _logger.LogDebug("Retrieved {ItemCount} root categories for page {PageNumber} of {TotalPages}",
                    result.Data.Count, result.PageNumber, result.TotalPages);

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
        /// <param name="id">Category ID</param>
        /// <returns>Category details</returns>
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]       
        public async Task<ActionResult<CategoryDto>> GetCategory(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { Message = "Category ID must be greater than 0" });
                }

                var category = await _categoryService.GetCategoryByIdAsync(id);
                _logger.LogDebug("Retrieved category {CategoryName} (ID: {CategoryId})", category.Name, id);
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
        /// <param name="slug">Category slug</param>
        /// <returns>Category details</returns>
        [HttpGet("by-slug/{slug}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]     
        public async Task<ActionResult<CategoryDto>> GetCategoryBySlug(string slug)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                {
                    return BadRequest(new { Message = "Slug cannot be empty" });
                }

                var category = await _categoryService.GetCategoryBySlugAsync(slug);
                if (category == null)
                    return NotFound(new { Message = $"Category with slug '{slug}' not found" });

                _logger.LogDebug("Retrieved category {CategoryName} by slug '{Slug}'", category.Name, slug);
                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category by slug {Slug}", slug);
                return StatusCode(500, new { Message = "An error occurred while retrieving the category" });
            }
        }

        /// <summary>
        /// Get subcategories of a category with service-level pagination (10 items per page)
        /// </summary>
        /// <param name="id">Parent category ID</param>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <returns>Paginated list of subcategories (10 items per page)</returns>
        [HttpGet("{id:int}/subcategories")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]       
        public async Task<ActionResult<PagedResult<CategoryDto>>> GetSubCategories(
            int id,
            [FromQuery] int pageNumber = 1)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { Message = "Parent category ID must be greater than 0" });
                }

                // Service controls page size (10 items per page)
                var result = await _categoryService.GetSubCategoriesPagedAsync(id, pageNumber, 10);

                _logger.LogDebug("Retrieved {ItemCount} subcategories for parent {ParentId} on page {PageNumber} of {TotalPages}",
                    result.Data.Count, id, result.PageNumber, result.TotalPages);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting subcategories of category {CategoryId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subcategories for category {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving subcategories" });
            }
        }

        /// <summary>
        /// Advanced category search with filtering and service-level pagination (10 items per page)
        /// </summary>
        /// <param name="searchDto">Search criteria with service-controlled pagination</param>
        /// <returns>Paginated search results (10 items per page)</returns>
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

                // Service will normalize and enforce page size of 10
                var result = await _categoryService.SearchCategoriesPagedAsync(searchDto);

                _logger.LogDebug("Search '{SearchTerm}' returned {ItemCount} categories for page {PageNumber} of {TotalPages}",
                    searchDto.SearchTerm, result.Data.Count, result.PageNumber, result.TotalPages);

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
        /// <param name="createCategoryDto">Category creation data</param>
        /// <returns>Created category</returns>
        [HttpPost]
        [AdminOrDev]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]        
        public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryDto createCategoryDto)
        {
            try
            {
                if (createCategoryDto == null)
                {
                    return BadRequest(new { Message = "Category data is required" });
                }

                var category = await _categoryService.CreateCategoryAsync(createCategoryDto);

                _logger.LogInformation("Created category {CategoryName} (ID: {CategoryId})", category.Name, category.Id);

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
        /// <param name="id">Category ID</param>
        /// <param name="updateCategoryDto">Category update data</param>
        /// <returns>Updated category</returns>
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
                if (id <= 0)
                {
                    return BadRequest(new { Message = "Category ID must be greater than 0" });
                }

                if (updateCategoryDto == null)
                {
                    return BadRequest(new { Message = "Category data is required" });
                }

                var category = await _categoryService.UpdateCategoryAsync(id, updateCategoryDto);

                _logger.LogInformation("Updated category {CategoryName} (ID: {CategoryId})", category.Name, id);

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
        /// <param name="id">Category ID</param>
        /// <returns>Success status</returns>
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
                if (id <= 0)
                {
                    return BadRequest(new { Message = "Category ID must be greater than 0" });
                }

                var success = await _categoryService.DeleteCategoryAsync(id);
                if (!success)
                    return NotFound(new { Message = "Category not found" });

                _logger.LogInformation("Deleted category with ID {CategoryId}", id);

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
        /// <param name="id">Category ID</param>
        /// <param name="moveCategoryDto">Move operation data</param>
        /// <returns>Updated category</returns>
        [HttpPost("{id:int}/move")]
        [AdminOrDev]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]    
        public async Task<ActionResult<CategoryDto>> MoveCategory(int id, [FromBody] MoveCategoryDto moveCategoryDto)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { Message = "Category ID must be greater than 0" });
                }

                if (moveCategoryDto == null)
                {
                    return BadRequest(new { Message = "Move data is required" });
                }

                var category = await _categoryService.MoveCategoryAsync(id, moveCategoryDto.NewParentCategoryId);

                _logger.LogInformation("Moved category {CategoryId} to parent {NewParentId}", id, moveCategoryDto.NewParentCategoryId);

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
        /// <param name="reorderDto">Reorder operation data</param>
        /// <returns>Reordered categories</returns>
        [HttpPost("reorder")]
        [AdminOrDev]
        [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<CategoryDto>>> ReorderCategories([FromBody] ReorderCategoriesDto reorderDto)
        {
            try
            {
                if (reorderDto?.Categories == null || !reorderDto.Categories.Any())
                {
                    return BadRequest(new { Message = "Category orders are required" });
                }

                var categoryOrders = reorderDto.Categories.Select(c => (c.Id, c.SortOrder)).ToList();
                var categories = await _categoryService.ReorderCategoriesAsync(categoryOrders);

                _logger.LogInformation("Reordered {CategoryCount} categories", categories.Count);

                return Ok(categories);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Category reorder failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
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
        /// <param name="slug">Slug to validate</param>
        /// <param name="excludeCategoryId">Category ID to exclude from validation (for updates)</param>
        /// <returns>Validation result</returns>
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
        /// <param name="id">Category ID</param>
        /// <returns>Deletion permission status</returns>
        [HttpGet("{id:int}/can-delete")]
        [AdminOrDev]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> CanDelete(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { Message = "Category ID must be greater than 0" });
                }

                var canDelete = await _categoryService.CanDeleteAsync(id);
                return Ok(new { CanDelete = canDelete });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if category can be deleted {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while checking delete permissions" });
            }
        }

        #region Image Management Endpoints

        /// <summary>
        /// Add image to category
        /// </summary>
        /// <param name="id">Category ID</param>
        /// <param name="createImageDto">Image creation data</param>
        /// <returns>Created category image</returns>
        [HttpPost("{id:int}/images")]
        [AdminOrDev]
        [ProducesResponseType(typeof(CategoryImageDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CategoryImageDto>> AddCategoryImage(int id, [FromBody] CreateCategoryImageDto createImageDto)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { Message = "Category ID must be greater than 0" });
                }

                if (createImageDto == null)
                {
                    return BadRequest(new { Message = "Image data is required" });
                }

                var image = await _categoryService.AddCategoryImageAsync(id, createImageDto);

                _logger.LogInformation("Added image {FileId} to category {CategoryId}", createImageDto.FileId, id);

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
        /// <param name="imageId">Image ID</param>
        /// <param name="updateImageDto">Image update data</param>
        /// <returns>Updated category image</returns>
        [HttpPut("images/{imageId:int}")]
        [AdminOrDev]
        [ProducesResponseType(typeof(CategoryImageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CategoryImageDto>> UpdateCategoryImage(int imageId, [FromBody] UpdateCategoryImageDto updateImageDto)
        {
            try
            {
                if (imageId <= 0)
                {
                    return BadRequest(new { Message = "Image ID must be greater than 0" });
                }

                if (updateImageDto == null)
                {
                    return BadRequest(new { Message = "Image data is required" });
                }

                var image = await _categoryService.UpdateCategoryImageAsync(imageId, updateImageDto);

                _logger.LogInformation("Updated category image {ImageId}", imageId);

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
        /// <param name="imageId">Image ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("images/{imageId:int}")]
        [AdminOrDev]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteCategoryImage(int imageId)
        {
            try
            {
                if (imageId <= 0)
                {
                    return BadRequest(new { Message = "Image ID must be greater than 0" });
                }

                var success = await _categoryService.DeleteCategoryImageAsync(imageId);
                if (!success)
                    return NotFound(new { Message = "Category image not found" });

                _logger.LogInformation("Deleted category image {ImageId}", imageId);

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
        /// <param name="id">Category ID</param>
        /// <param name="reorderDto">Image reorder data</param>
        /// <returns>Reordered images</returns>
        [HttpPost("{id:int}/images/reorder")]
        [AdminOrDev]
        [ProducesResponseType(typeof(List<CategoryImageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<CategoryImageDto>>> ReorderCategoryImages(int id, [FromBody] ReorderCategoryImagesDto reorderDto)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { Message = "Category ID must be greater than 0" });
                }

                if (reorderDto?.Images == null || !reorderDto.Images.Any())
                {
                    return BadRequest(new { Message = "Image orders are required" });
                }

                var imageOrders = reorderDto.Images.Select(i => (i.Id, i.Position)).ToList();
                var images = await _categoryService.ReorderCategoryImagesAsync(id, imageOrders);

                _logger.LogInformation("Reordered {ImageCount} images for category {CategoryId}", images.Count, id);

                return Ok(images);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Image reorder failed for category {CategoryId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering category images for category {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while reordering images" });
            }
        }

        #endregion

        #region Supporting DTOs for controller actions

        /// <summary>
        /// DTO for moving a category to a different parent
        /// </summary>
        public class MoveCategoryDto
        {
            [Required]
            public int? NewParentCategoryId { get; set; }
        }

        /// <summary>
        /// DTO for reordering categories
        /// </summary>
        public class ReorderCategoriesDto
        {
            [Required]
            public List<CategoryOrderDto> Categories { get; set; } = new();
        }

        /// <summary>
        /// DTO for category order information
        /// </summary>
        public class CategoryOrderDto
        {
            [Required]
            public int Id { get; set; }

            [Required]
            public int SortOrder { get; set; }
        }

        /// <summary>
        /// DTO for reordering category images
        /// </summary>
        public class ReorderCategoryImagesDto
        {
            [Required]
            public List<CategoryImageOrderDto> Images { get; set; } = new();
        }

        /// <summary>
        /// DTO for category image order information
        /// </summary>
        public class CategoryImageOrderDto
        {
            [Required]
            public int Id { get; set; }

            [Required]
            public int Position { get; set; }
        }

        #endregion
    }
}