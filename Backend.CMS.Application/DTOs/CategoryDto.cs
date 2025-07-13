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
        public List<CategoryDto> SubCategories { get; set; } = new();
        public bool IsActive { get; set; }
        public bool IsVisible { get; set; }
        public int SortOrder { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public int ProductCount { get; set; }
        public List<CategoryImageDto> Images { get; set; } = new();
        public string? FeaturedImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

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

        [Range(0, int.MaxValue)]
        public int SortOrder { get; set; } = 0;

        [StringLength(255)]
        public string? MetaTitle { get; set; }

        [StringLength(500)]
        public string? MetaDescription { get; set; }

        [StringLength(500)]
        public string? MetaKeywords { get; set; }

        public Dictionary<string, object> CustomFields { get; set; } = [];

        public List<CreateCategoryImageDto> Images { get; set; } = [];
        public string? FeaturedImageUrl { get; set; }
    }

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

        [Range(0, int.MaxValue)]
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
    public class CategorySearchDto
    {
        private int _pageNumber = 1;
        private int _pageSize = 10;

        [StringLength(500)]
        public string? SearchTerm { get; set; }

        public int? ParentCategoryId { get; set; }

        public bool? IsActive { get; set; }

        public bool? IsVisible { get; set; }

        /// <summary>
        /// Page number (1-based)
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0")]
        public int PageNumber
        {
            get => _pageNumber;
            set => _pageNumber = Math.Max(1, value);
        }

        /// <summary>
        /// Number of items per page (1-100)
        /// </summary>
        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = Math.Clamp(value, 1, 100);
        }

        /// <summary>
        /// Sort field (Name, CreatedAt, UpdatedAt, SortOrder)
        /// </summary>
        [StringLength(50)]
        public string SortBy { get; set; } = "Name";

        /// <summary>
        /// Sort direction (Asc, Desc)
        /// </summary>
        [StringLength(10)]
        public string SortDirection { get; set; } = "Asc";

        /// <summary>
        /// Filter by creation date range
        /// </summary>
        public DateTime? CreatedFrom { get; set; }

        /// <summary>
        /// Filter by creation date range
        /// </summary>
        public DateTime? CreatedTo { get; set; }

        /// <summary>
        /// Filter by update date range
        /// </summary>
        public DateTime? UpdatedFrom { get; set; }

        /// <summary>
        /// Filter by update date range
        /// </summary>
        public DateTime? UpdatedTo { get; set; }

        /// <summary>
        /// Filter by minimum product count
        /// </summary>
        [Range(0, int.MaxValue)]
        public int? MinProductCount { get; set; }

        /// <summary>
        /// Filter by maximum product count
        /// </summary>
        [Range(0, int.MaxValue)]
        public int? MaxProductCount { get; set; }

        /// <summary>
        /// Filter categories with images only
        /// </summary>
        public bool? HasImages { get; set; }

        /// <summary>
        /// Filter by meta keywords (comma-separated)
        /// </summary>
        [StringLength(1000)]
        public string? MetaKeywords { get; set; }

        /// <summary>
        /// Include subcategories in search
        /// </summary>
        public bool IncludeSubCategories { get; set; } = true;

        /// <summary>
        /// Include parent categories in search
        /// </summary>
        public bool IncludeParentCategories { get; set; } = true;

        // Legacy properties for backward compatibility
        [Obsolete("Use PageNumber instead")]
        public int Page
        {
            get => PageNumber;
            set => PageNumber = value;
        }
    }

    public class CategoryTreeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int? ParentCategoryId { get; set; }
        public List<CategoryTreeDto> Children { get; set; } = new();
        public int ProductCount { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string? FeaturedImageUrl { get; set; }
        public int Level { get; set; }
        public string Path { get; set; } = string.Empty;
        public bool HasChildren => Children.Any();
        public int TotalDescendants { get; set; }
    }

    public class CategoryImageDto
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public int FileId { get; set; }

        [StringLength(255)]
        public string? Alt { get; set; }

        [StringLength(500)]
        public string? Caption { get; set; }

        [Range(0, int.MaxValue)]
        public int Position { get; set; }

        public bool IsFeatured { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateCategoryImageDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "File ID must be greater than 0")]
        public int FileId { get; set; }

        [StringLength(255)]
        public string? Alt { get; set; }

        [StringLength(500)]
        public string? Caption { get; set; }

        [Range(0, int.MaxValue)]
        public int Position { get; set; } = 0;

        public bool IsFeatured { get; set; } = false;
    }

    public class UpdateCategoryImageDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "ID must be greater than 0")]
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "File ID must be greater than 0")]
        public int FileId { get; set; }

        [StringLength(255)]
        public string? Alt { get; set; }

        [StringLength(500)]
        public string? Caption { get; set; }

        [Range(0, int.MaxValue)]
        public int Position { get; set; }

        public bool IsFeatured { get; set; }
    }

    
}