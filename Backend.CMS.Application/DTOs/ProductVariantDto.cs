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
        public string? Image { get; set; }
        public int Position { get; set; }
        public bool IsDefault { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public string? Option1 { get; set; }
        public string? Option2 { get; set; }
        public string? Option3 { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Computed properties
        public bool IsAvailable => Quantity > 0 || ContinueSellingWhenOutOfStock;
        public decimal? DiscountPercentage => CompareAtPrice.HasValue && CompareAtPrice > Price ? Math.Round(((CompareAtPrice.Value - Price) / CompareAtPrice.Value) * 100, 2) : null;
        public string DisplayTitle => !string.IsNullOrEmpty(Title) ? Title : GenerateVariantTitle();

        private string GenerateVariantTitle()
        {
            var options = new[] { Option1, Option2, Option3 }.Where(o => !string.IsNullOrEmpty(o)).ToArray();
            return options.Any() ? string.Join(" / ", options) : "Default";
        }
    }

    public class CreateProductVariantDto
    {
        public string Title { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; }
        public decimal? CostPerItem { get; set; }
        public int Quantity { get; set; } = 0;
        public bool TrackQuantity { get; set; } = true;
        public bool ContinueSellingWhenOutOfStock { get; set; } = false;
        public bool RequiresShipping { get; set; } = true;
        public bool IsTaxable { get; set; } = true;
        public decimal Weight { get; set; } = 0;
        public string? WeightUnit { get; set; } = "kg";
        public string? Barcode { get; set; }
        public string? Image { get; set; }
        public int Position { get; set; } = 0;
        public bool IsDefault { get; set; } = false;
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public string? Option1 { get; set; }
        public string? Option2 { get; set; }
        public string? Option3 { get; set; }
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
        public string? Image { get; set; }
        public int Position { get; set; }
        public bool IsDefault { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new();
        public string? Option1 { get; set; }
        public string? Option2 { get; set; }
        public string? Option3 { get; set; }
    }

    public class ProductImageDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int? ProductVariantId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Alt { get; set; }
        public int Position { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? OriginalSource { get; set; }
    }

    public class CreateProductImageDto
    {
        public int? ProductVariantId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Alt { get; set; }
        public int Position { get; set; } = 0;
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? OriginalSource { get; set; }
    }

    public class UpdateProductImageDto
    {
        public int Id { get; set; }
        public int? ProductVariantId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Alt { get; set; }
        public int Position { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? OriginalSource { get; set; }
    }

    public class ProductOptionDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Position { get; set; }
        public List<ProductOptionValueDto> Values { get; set; } = new();
    }

    public class CreateProductOptionDto
    {
        public string Name { get; set; } = string.Empty;
        public int Position { get; set; } = 0;
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

    public class UpdateVariantStockDto
    {
        public int NewQuantity { get; set; }
    }
}