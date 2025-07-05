using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    /// <summary>
    /// Category service interface with pagination support
    /// </summary>
    public interface ICategoryService
    {
        #region Paginated Methods

        /// <summary>
        /// Get paginated categories with filtering and sorting
        /// </summary>
        /// <param name="searchDto">Search and pagination criteria</param>
        /// <returns>Paginated category results</returns>
        Task<PagedResult<CategoryDto>> GetCategoriesPagedAsync(CategorySearchDto searchDto);

        /// <summary>
        /// Get paginated root categories
        /// </summary>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <returns>Paginated root category results</returns>
        Task<PagedResult<CategoryDto>> GetRootCategoriesPagedAsync(int pageNumber, int pageSize);

        /// <summary>
        /// Get paginated subcategories of a parent category
        /// </summary>
        /// <param name="parentCategoryId">Parent category ID</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <returns>Paginated subcategory results</returns>
        Task<PagedResult<CategoryDto>> GetSubCategoriesPagedAsync(int parentCategoryId, int pageNumber, int pageSize);

        /// <summary>
        /// Search categories with pagination
        /// </summary>
        /// <param name="searchDto">Search criteria with pagination</param>
        /// <returns>Paginated search results</returns>
        Task<PagedResult<CategoryDto>> SearchCategoriesPagedAsync(CategorySearchDto searchDto);

        #endregion

        #region Individual Category Operations

        /// <summary>
        /// Get category by ID with full details
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <returns>Category details</returns>
        /// <exception cref="ArgumentException">Thrown when category not found</exception>
        Task<CategoryDto> GetCategoryByIdAsync(int categoryId);

        /// <summary>
        /// Get category by slug
        /// </summary>
        /// <param name="slug">Category slug</param>
        /// <returns>Category details or null if not found</returns>
        Task<CategoryDto?> GetCategoryBySlugAsync(string slug);

        #endregion
        /// <summary>
        /// Get category tree structure (non-paginated for tree display)
        /// </summary>
        /// <returns>Hierarchical category tree</returns>
        Task<List<CategoryTreeDto>> GetCategoryTreeAsync();

        #region CRUD Operations

        /// <summary>
        /// Create a new category
        /// </summary>
        /// <param name="createCategoryDto">Category creation data</param>
        /// <returns>Created category</returns>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto createCategoryDto);

        /// <summary>
        /// Update an existing category
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <param name="updateCategoryDto">Category update data</param>
        /// <returns>Updated category</returns>
        /// <exception cref="ArgumentException">Thrown when validation fails or category not found</exception>
        Task<CategoryDto> UpdateCategoryAsync(int categoryId, UpdateCategoryDto updateCategoryDto);

        /// <summary>
        /// Delete a category (soft delete)
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <returns>True if deleted successfully, false if not found</returns>
        /// <exception cref="InvalidOperationException">Thrown when category cannot be deleted</exception>
        Task<bool> DeleteCategoryAsync(int categoryId);

        /// <summary>
        /// Move category to a different parent
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <param name="newParentCategoryId">New parent category ID (null for root)</param>
        /// <returns>Updated category</returns>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        Task<CategoryDto> MoveCategoryAsync(int categoryId, int? newParentCategoryId);

        /// <summary>
        /// Reorder categories by updating sort orders
        /// </summary>
        /// <param name="categoryOrders">List of category ID and sort order pairs</param>
        /// <returns>List of updated categories</returns>
        Task<List<CategoryDto>> ReorderCategoriesAsync(List<(int CategoryId, int SortOrder)> categoryOrders);

        #endregion

        #region Validation Operations

        /// <summary>
        /// Validate if a slug is available
        /// </summary>
        /// <param name="slug">Slug to validate</param>
        /// <param name="excludeCategoryId">Category ID to exclude from validation (for updates)</param>
        /// <returns>True if slug is valid/available</returns>
        Task<bool> ValidateSlugAsync(string slug, int? excludeCategoryId = null);

        /// <summary>
        /// Check if a category can be deleted
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <returns>True if category can be deleted</returns>
        Task<bool> CanDeleteAsync(int categoryId);

        #endregion

        #region Image Management

        /// <summary>
        /// Add an image to a category
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <param name="createImageDto">Image creation data</param>
        /// <returns>Created category image</returns>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        Task<CategoryImageDto> AddCategoryImageAsync(int categoryId, CreateCategoryImageDto createImageDto);

        /// <summary>
        /// Update a category image
        /// </summary>
        /// <param name="imageId">Image ID</param>
        /// <param name="updateImageDto">Image update data</param>
        /// <returns>Updated category image</returns>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        Task<CategoryImageDto> UpdateCategoryImageAsync(int imageId, UpdateCategoryImageDto updateImageDto);

        /// <summary>
        /// Delete a category image
        /// </summary>
        /// <param name="imageId">Image ID</param>
        /// <returns>True if deleted successfully</returns>
        Task<bool> DeleteCategoryImageAsync(int imageId);

        /// <summary>
        /// Reorder category images
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <param name="imageOrders">List of image ID and position pairs</param>
        /// <returns>List of reordered images</returns>
        Task<List<CategoryImageDto>> ReorderCategoryImagesAsync(int categoryId, List<(int ImageId, int Position)> imageOrders);

        #endregion
    }
}