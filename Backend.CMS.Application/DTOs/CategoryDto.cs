using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Application.DTOs
{
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public int? ParentCategoryId { get; set; }
        public string? ParentCategoryName { get; set; }
        public bool IsActive { get; set; }
        public bool IsVisible { get; set; }
        public int SortOrder { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public string? FeaturedImageUrl { get; set; }
        public int ProductCount { get; set; }
        public int SubCategoryCount { get; set; }
        public List<CategoryImageDto> Images { get; set; } = new();
        public List<CategoryDto> SubCategories { get; set; } = new();
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }
        public string? CreatedByUserName { get; set; }
        public string? UpdatedByUserName { get; set; }
    }

    /// <summary>
    /// DTO for creating a new category
    /// </summary>
    public class CreateCategoryDto
    {
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? ShortDescription { get; set; }

        public int? ParentCategoryId { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsVisible { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        [StringLength(255)]
        public string? MetaTitle { get; set; }

        [StringLength(500)]
        public string? MetaDescription { get; set; }

        [StringLength(500)]
        public string? MetaKeywords { get; set; }

        public Dictionary<string, object> CustomFields { get; set; } = new();

        public List<CreateCategoryImageDto> Images { get; set; } = new();
    }

    // <summary>
    /// DTO for updating an existing category
    /// </summary>
    public class UpdateCategoryDto
    {
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? ShortDescription { get; set; }

        public int? ParentCategoryId { get; set; }

        public bool IsActive { get; set; }

        public bool IsVisible { get; set; }

        public int SortOrder { get; set; }

        [StringLength(255)]
        public string? MetaTitle { get; set; }

        [StringLength(500)]
        public string? MetaDescription { get; set; }

        [StringLength(500)]
        public string? MetaKeywords { get; set; }

        public Dictionary<string, object> CustomFields { get; set; } = new();

        public List<UpdateCategoryImageDto> Images { get; set; } = new();
    }

    /// <summary>
    /// Enhanced category search DTO with filtering and pagination support
    /// </summary>
    /// <summary>
    /// DTO for category search and filtering with pagination
    /// </summary>
    public class CategorySearchDto : PaginationRequest
    {
        /// <summary>
        /// Search term for name, description, slug, or meta keywords
        /// </summary>
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Filter by parent category ID (null for root categories)
        /// </summary>
        public int? ParentCategoryId { get; set; }

        /// <summary>
        /// Filter by active status
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Filter by visibility status
        /// </summary>
        public bool? IsVisible { get; set; }

        /// <summary>
        /// Sort field (Name, CreatedAt, UpdatedAt, SortOrder)
        /// </summary>
        public string SortBy { get; set; } = "Name";

        /// <summary>
        /// Sort direction (Asc, Desc)
        /// </summary>
        public string SortDirection { get; set; } = "Asc";

        /// <summary>
        /// Filter by creation date range - from
        /// </summary>
        public DateTime? CreatedFrom { get; set; }

        /// <summary>
        /// Filter by creation date range - to
        /// </summary>
        public DateTime? CreatedTo { get; set; }

        /// <summary>
        /// Filter by update date range - from
        /// </summary>
        public DateTime? UpdatedFrom { get; set; }

        /// <summary>
        /// Filter by update date range - to
        /// </summary>
        public DateTime? UpdatedTo { get; set; }

        /// <summary>
        /// Filter by whether category has images
        /// </summary>
        public bool? HasImages { get; set; }

        /// <summary>
        /// Filter by meta keywords (comma-separated)
        /// </summary>
        public string? MetaKeywords { get; set; }

        /// <summary>
        /// Filter by minimum product count
        /// </summary>
        public int? MinProductCount { get; set; }

        /// <summary>
        /// Filter by maximum product count
        /// </summary>
        public int? MaxProductCount { get; set; }

        /// <summary>
        /// Filter categories created by specific user
        /// </summary>
        public int? CreatedByUserId { get; set; }

        /// <summary>
        /// Filter categories updated by specific user
        /// </summary>
        public int? UpdatedByUserId { get; set; }

    }


    /// <summary>
    /// DTO for category tree node
    /// </summary>
    public class CategoryTreeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int? ParentCategoryId { get; set; }
        public bool IsActive { get; set; }
        public bool IsVisible { get; set; }
        public int SortOrder { get; set; }
        public string? FeaturedImageUrl { get; set; }
        public int ProductCount { get; set; }
        public int Level { get; set; }
        public string Path { get; set; } = string.Empty;
        public int TotalDescendants { get; set; }
        public List<CategoryTreeDto> Children { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO for category image
    /// </summary>
    public class CategoryImageDto
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public int FileId { get; set; }
        public string? Alt { get; set; }
        public string? Caption { get; set; }
        public int Position { get; set; }
        public bool IsFeatured { get; set; }
        public string? ImageUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO for creating a category image
    /// </summary>
    public class CreateCategoryImageDto
    {
        [Required]
        public int FileId { get; set; }

        [StringLength(255)]
        public string? Alt { get; set; }

        [StringLength(500)]
        public string? Caption { get; set; }

        public int Position { get; set; } = 0;

        public bool IsFeatured { get; set; } = false;
    }

    public class UpdateCategoryImageDto
    {
        public int Id { get; set; }

        [Required]
        public int FileId { get; set; }

        [StringLength(255)]
        public string? Alt { get; set; }

        [StringLength(500)]
        public string? Caption { get; set; }

        public int Position { get; set; }

        public bool IsFeatured { get; set; }
    }

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


}

