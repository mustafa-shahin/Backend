namespace Backend.CMS.Application.DTOs
{
    public class ProductVariantDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public decimal? CostPerItem { get; set; }
        public int Quantity { get; set; }
        public bool TrackQuantity { get; set; }
        public bool ContinueSellingWhenOutOfStock { get; set; }
        public bool RequiresShipping { get; set; }
        public bool IsTaxable { get; set; }
        public decimal Weight { get; set; }
        public string? WeightUnit { get; set; }
        public string? Barcode { get; set; }
        public int Position { get; set; }
        public bool IsDefault { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public string? Option1 { get; set; }
        public string? Option2 { get; set; }
        public string? Option3 { get; set; }
        public List<ProductVariantImageDto> Images { get; set; } = new();
        public string? FeaturedImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        // Computed properties
        public bool IsAvailable => Quantity > 0;
        public decimal? DiscountPercentage { get; set; }
        public string DisplayTitle { get; set; } = string.Empty;
    }


    public class CreateProductVariantDto
    {
        public int? ProductId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public decimal? CostPerItem { get; set; }
        public int Quantity { get; set; }
        public bool TrackQuantity { get; set; } = true;
        public bool ContinueSellingWhenOutOfStock { get; set; } = false;
        public bool RequiresShipping { get; set; } = true;
        public bool IsTaxable { get; set; } = true;
        public decimal Weight { get; set; } = 0;
        public string? WeightUnit { get; set; } = "kg";
        public string? Barcode { get; set; }
        public int Position { get; set; } = 0;
        public bool IsDefault { get; set; } = false;
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public string? Option1 { get; set; }
        public string? Option2 { get; set; }
        public string? Option3 { get; set; }
        public List<CreateProductVariantImageDto> Images { get; set; } = new();
    }

    public class UpdateProductVariantDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public decimal? CostPerItem { get; set; }
        public int Quantity { get; set; }
        public bool TrackQuantity { get; set; }
        public bool ContinueSellingWhenOutOfStock { get; set; }
        public bool RequiresShipping { get; set; }
        public bool IsTaxable { get; set; }
        public decimal Weight { get; set; }
        public string? WeightUnit { get; set; }
        public string? Barcode { get; set; }
        public int Position { get; set; }
        public bool IsDefault { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public string? Option1 { get; set; }
        public string? Option2 { get; set; }
        public string? Option3 { get; set; }
        public List<UpdateProductVariantImageDto> Images { get; set; } = new();
    }


    public class ProductImageDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int FileId { get; set; }
        public string? Alt { get; set; }
        public string? Caption { get; set; }
        public int Position { get; set; }
        public bool IsFeatured { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }


    public class CreateProductImageDto
    {
        public int FileId { get; set; }
        public string? Alt { get; set; }
        public string? Caption { get; set; }
        public int Position { get; set; }
        public bool IsFeatured { get; set; }
    }

    public class UpdateProductImageDto
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public string? Alt { get; set; }
        public string? Caption { get; set; }
        public int Position { get; set; }
        public bool IsFeatured { get; set; }
    }



    public class ProductVariantImageDto
    {
        public int Id { get; set; }
        public int ProductVariantId { get; set; }
        public int FileId { get; set; }
        public string? Alt { get; set; }
        public string? Caption { get; set; }
        public int Position { get; set; }
        public bool IsFeatured { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }


    public class CreateProductVariantImageDto
    {
        public int FileId { get; set; }
        public string? Alt { get; set; }
        public string? Caption { get; set; }
        public int Position { get; set; }
        public bool IsFeatured { get; set; }
    }

    public class UpdateProductVariantImageDto
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public string? Alt { get; set; }
        public string? Caption { get; set; }
        public int Position { get; set; }
        public bool IsFeatured { get; set; }
    }

    public class ProductOptionDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Position { get; set; }
        public List<ProductOptionValueDto> Values { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    public class CreateProductOptionDto
    {
        public string Name { get; set; } = string.Empty;
        public int Position { get; set; }
        public List<CreateProductOptionValueDto> Values { get; set; } = new();
    }

    public class UpdateProductOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Position { get; set; }
        public List<UpdateProductOptionValueDto> Values { get; set; } = new();
    }

    public class ProductOptionValueDto
    {
        public int Id { get; set; }
        public int ProductOptionId { get; set; }
        public string Value { get; set; } = string.Empty;
        public int Position { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateProductOptionValueDto
    {
        public string Value { get; set; } = string.Empty;
        public int Position { get; set; } = 0;
    }

    public class UpdateProductOptionValueDto
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
        public int Position { get; set; }
    }

    public class ReorderVariantsDto
    {
        public List<VariantOrderDto> Variants { get; set; } = new();
    }

    public class VariantOrderDto
    {
        public int Id { get; set; }
        public int Position { get; set; }
    }
    public class VariantPositionDto
    {
        public int Id { get; set; }
        public int Position { get; set; }
    }
    public class UpdateVariantStockDto
    {
        public int NewQuantity { get; set; }
    }
}