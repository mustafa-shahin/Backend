using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
        /// Get all categories
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<CategoryDto>>> GetCategories(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        {
            try
            {
                var allCategories = await _categoryService.GetCategoriesAsync();

                // Apply pagination
                var totalCount = allCategories.Count;
                var items = allCategories
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new PagedResult<CategoryDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving categories");
                return StatusCode(500, new { Message = "An error occurred while retrieving categories" });
            }
        }

        /// <summary>
        /// Get category tree structure
        /// </summary>
        [HttpGet("tree")]
        [AllowAnonymous]
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
        /// Get root categories (categories without parent)
        /// </summary>
        [HttpGet("root")]
        [AllowAnonymous]
        public async Task<ActionResult<List<CategoryDto>>> GetRootCategories()
        {
            try
            {
                var categories = await _categoryService.GetRootCategoriesAsync();
                return Ok(categories);
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
        /// Get subcategories of a category
        /// </summary>
        [HttpGet("{id:int}/subcategories")]
        [AllowAnonymous]
        public async Task<ActionResult<List<CategoryDto>>> GetSubCategories(int id)
        {
            try
            {
                var subcategories = await _categoryService.GetSubCategoriesAsync(id);
                return Ok(subcategories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subcategories for category {CategoryId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving subcategories" });
            }
        }

        /// <summary>
        /// Search categories
        /// </summary>
        [HttpPost("search")]
        [AllowAnonymous]
        public async Task<ActionResult<List<CategoryDto>>> SearchCategories([FromBody] CategorySearchDto searchDto)
        {
            try
            {
                var categories = await _categoryService.SearchCategoriesAsync(searchDto);
                var totalCount = await _categoryService.GetSearchCountAsync(searchDto);

                return Ok(new
                {
                    Categories = categories,
                    TotalCount = totalCount,
                    Page = searchDto.Page,
                    PageSize = searchDto.PageSize
                });
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
        public async Task<ActionResult<bool>> ValidateSlug([FromQuery] string slug, [FromQuery] int? excludeCategoryId = null)
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
        public async Task<ActionResult<bool>> CanDelete(int id)
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
        // <summary>
        /// Add image to category
        /// </summary>
        [HttpPost("{id:int}/images")]
        [AdminOrDev]
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
        // <summary>
        /// Reorder category images
        /// </summary>
        [HttpPost("{id:int}/images/reorder")]
        [AdminOrDev]
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
            public int? NewParentCategoryId { get; set; }
        }

        public class ReorderCategoriesDto
        {
            public List<CategoryOrderDto> Categories { get; set; } = new();
        }

        public class CategoryOrderDto
        {
            public int Id { get; set; }
            public int SortOrder { get; set; }
        }

        public class ReorderCategoryImagesDto
        {
            public List<CategoryImageOrderDto> Images { get; set; } = new();
        }

        public class CategoryImageOrderDto
        {
            public int Id { get; set; }
            public int Position { get; set; }
        }
    }
}