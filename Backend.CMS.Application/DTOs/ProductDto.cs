using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Application.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public string SKU { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public decimal? CostPerItem { get; set; }
        public bool TrackQuantity { get; set; }
        public int Quantity { get; set; }
        public bool ContinueSellingWhenOutOfStock { get; set; }
        public bool RequiresShipping { get; set; }
        public bool IsPhysicalProduct { get; set; }
        public decimal Weight { get; set; }
        public string? WeightUnit { get; set; }
        public bool IsTaxable { get; set; }
        public ProductStatus Status { get; set; }
        public ProductType Type { get; set; }
        public string? Vendor { get; set; }
        public string? Barcode { get; set; }
        public bool HasVariants { get; set; }
        public string? Tags { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? Template { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public string? SearchKeywords { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public Dictionary<string, object> SEOSettings { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<ProductVariantDto> Variants { get; set; } = new();
        public List<ProductImageDto> Images { get; set; } = new();
        public List<ProductOptionDto> Options { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Computed properties
        public string StatusName => Status.ToString();
        public string TypeName => Type.ToString();
        public bool IsAvailable => Status == ProductStatus.Active && (HasVariants ? Variants.Any(v => v.IsAvailable) : Quantity > 0 || ContinueSellingWhenOutOfStock);
        public decimal? DiscountPercentage => CompareAtPrice.HasValue && CompareAtPrice > Price ? Math.Round(((CompareAtPrice.Value - Price) / CompareAtPrice.Value) * 100, 2) : null;
        public string? FeaturedImage => Images.OrderBy(i => i.Position).FirstOrDefault()?.Url;
    }

    public class CreateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public string SKU { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public decimal? CostPerItem { get; set; }
        public bool TrackQuantity { get; set; } = true;
        public int Quantity { get; set; } = 0;
        public bool ContinueSellingWhenOutOfStock { get; set; } = false;
        public bool RequiresShipping { get; set; } = true;
        public bool IsPhysicalProduct { get; set; } = true;
        public decimal Weight { get; set; } = 0;
        public string? WeightUnit { get; set; } = "kg";
        public bool IsTaxable { get; set; } = true;
        public ProductStatus Status { get; set; } = ProductStatus.Active;
        public ProductType Type { get; set; } = ProductType.Physical;
        public string? Vendor { get; set; }
        public string? Barcode { get; set; }
        public bool HasVariants { get; set; } = false;
        public string? Tags { get; set; }
        public string? Template { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public string? SearchKeywords { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public Dictionary<string, object> SEOSettings { get; set; } = new();
        public List<int> CategoryIds { get; set; } = new();
        public List<CreateProductImageDto> Images { get; set; } = new();
        public List<CreateProductOptionDto> Options { get; set; } = new();
        public List<CreateProductVariantDto> Variants { get; set; } = new();
    }

    public class UpdateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ShortDescription { get; set; }
        public string SKU { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public decimal? CostPerItem { get; set; }
        public bool TrackQuantity { get; set; }
        public int Quantity { get; set; }
        public bool ContinueSellingWhenOutOfStock { get; set; }
        public bool RequiresShipping { get; set; }
        public bool IsPhysicalProduct { get; set; }
        public decimal Weight { get; set; }
        public string? WeightUnit { get; set; }
        public bool IsTaxable { get; set; }
        public ProductStatus Status { get; set; }
        public ProductType Type { get; set; }
        public string? Vendor { get; set; }
        public string? Barcode { get; set; }
        public bool HasVariants { get; set; }
        public string? Tags { get; set; }
        public string? Template { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public string? SearchKeywords { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public Dictionary<string, object> SEOSettings { get; set; } = new();
        public List<int> CategoryIds { get; set; } = new();
        public List<UpdateProductImageDto> Images { get; set; } = new();
        public List<UpdateProductOptionDto> Options { get; set; } = new();
        public List<UpdateProductVariantDto> Variants { get; set; } = new();
    }

    public class ProductListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public ProductStatus Status { get; set; }
        public ProductType Type { get; set; }
        public int Quantity { get; set; }
        public bool HasVariants { get; set; }
        public string? FeaturedImage { get; set; }
        public List<string> CategoryNames { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string StatusName => Status.ToString();
        public bool IsAvailable => Status == ProductStatus.Active && (Quantity > 0 || HasVariants);
    }

    public class ProductSearchDto
    {
        public string? SearchTerm { get; set; }
        public ProductStatus? Status { get; set; }
        public ProductType? Type { get; set; }
        public List<int> CategoryIds { get; set; } = new();
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool? HasVariants { get; set; }
        public bool? IsAvailable { get; set; }
        public string? Vendor { get; set; }
        public List<string> Tags { get; set; } = new();
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