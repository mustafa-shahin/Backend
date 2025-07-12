using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Application.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public bool ContinueSellingWhenOutOfStock { get; set; }
        public bool RequiresShipping { get; set; }
        public ProductStatus Status { get; set; }
        public ProductType Type { get; set; }
        public string? Vendor { get; set; }
        public bool HasVariants { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public string? SearchKeywords { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public Dictionary<string, object> SEOSettings { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<ProductVariantDto> Variants { get; set; } = new();
        public List<ProductImageDto> Images { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Computed properties
        public string StatusName => Status.ToString();
        public string TypeName => Type.ToString();
        public bool IsAvailable => Status == ProductStatus.Active && Variants.Any(v => v.Quantity > 0);
        public string? FeaturedImageUrl => Images.OrderBy(i => i.Position).FirstOrDefault()?.ImageUrl;
    }

    public class CreateProductDto
    {
        [Required(ErrorMessage = "Product name is required")]
        [StringLength(255, ErrorMessage = "Product name cannot exceed 255 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "URL slug is required")]
        [StringLength(255, ErrorMessage = "URL slug cannot exceed 255 characters")]
        public string Slug { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string? Description { get; set; }

        [StringLength(1000, ErrorMessage = "Short description cannot exceed 1000 characters")]
        public string? ShortDescription { get; set; }

        public bool ContinueSellingWhenOutOfStock { get; set; } = false;
        public bool RequiresShipping { get; set; } = true;

        [Required(ErrorMessage = "Status is required")]
        public ProductStatus Status { get; set; } = ProductStatus.Active;

        [Required(ErrorMessage = "Product type is required")]
        public ProductType Type { get; set; } = ProductType.Physical;

        [StringLength(255, ErrorMessage = "Vendor cannot exceed 255 characters")]
        public string? Vendor { get; set; }

        public bool HasVariants { get; set; } = false;

        [StringLength(255, ErrorMessage = "Meta title cannot exceed 255 characters")]
        public string? MetaTitle { get; set; }

        [StringLength(1000, ErrorMessage = "Meta description cannot exceed 1000 characters")]
        public string? MetaDescription { get; set; }

        [StringLength(500, ErrorMessage = "Meta keywords cannot exceed 500 characters")]
        public string? MetaKeywords { get; set; }

        [StringLength(1000, ErrorMessage = "Search keywords cannot exceed 1000 characters")]
        public string? SearchKeywords { get; set; }

        public Dictionary<string, object> CustomFields { get; set; } = new();
        public Dictionary<string, object> SEOSettings { get; set; } = new();
        public List<int> CategoryIds { get; set; } = new();
        public List<CreateProductImageDto> Images { get; set; } = new();
        public List<CreateProductVariantDto> Variants { get; set; } = new();
    }

    public class UpdateProductDto
    {
        [Required(ErrorMessage = "Product name is required")]
        [StringLength(255, ErrorMessage = "Product name cannot exceed 255 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "URL slug is required")]
        [StringLength(255, ErrorMessage = "URL slug cannot exceed 255 characters")]
        public string Slug { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string? Description { get; set; }

        [StringLength(1000, ErrorMessage = "Short description cannot exceed 1000 characters")]
        public string? ShortDescription { get; set; }

        public bool ContinueSellingWhenOutOfStock { get; set; } = false;
        public bool RequiresShipping { get; set; } = true;

        [Required(ErrorMessage = "Status is required")]
        public ProductStatus Status { get; set; }

        [Required(ErrorMessage = "Product type is required")]
        public ProductType Type { get; set; }

        [StringLength(255, ErrorMessage = "Vendor cannot exceed 255 characters")]
        public string? Vendor { get; set; }

        public bool HasVariants { get; set; }

        [StringLength(255, ErrorMessage = "Meta title cannot exceed 255 characters")]
        public string? MetaTitle { get; set; }

        [StringLength(1000, ErrorMessage = "Meta description cannot exceed 1000 characters")]
        public string? MetaDescription { get; set; }

        [StringLength(500, ErrorMessage = "Meta keywords cannot exceed 500 characters")]
        public string? MetaKeywords { get; set; }

        [StringLength(1000, ErrorMessage = "Search keywords cannot exceed 1000 characters")]
        public string? SearchKeywords { get; set; }

        public Dictionary<string, object> CustomFields { get; set; } = new();
        public Dictionary<string, object> SEOSettings { get; set; } = new();
        public List<int> CategoryIds { get; set; } = new();
        public List<UpdateProductImageDto> Images { get; set; } = new();
        public List<UpdateProductVariantDto> Variants { get; set; } = new();
    }

    public class ProductListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ProductStatus Status { get; set; }
        public ProductType Type { get; set; }
        public bool HasVariants { get; set; }
        public string? FeaturedImageUrl { get; set; }
        public List<string> CategoryNames { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string StatusName => Status.ToString();
        public bool IsAvailable { get; set; } 
    }

    public class ProductSearchDto
    {
        public string? SearchTerm { get; set; }
        public ProductStatus? Status { get; set; }
        public ProductType? Type { get; set; }
        public List<int> CategoryIds { get; set; } = new();
        public bool? HasVariants { get; set; }
        public bool? IsAvailable { get; set; }
        public string? Vendor { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "Name";
        public string SortDirection { get; set; } = "Asc";
    }

    public class DuplicateProductDto
    {
        public string NewName { get; set; } = string.Empty;
    }

    public class UpdateStockDto
    {
        public int? VariantId { get; set; }
        public int NewQuantity { get; set; }
    }
}