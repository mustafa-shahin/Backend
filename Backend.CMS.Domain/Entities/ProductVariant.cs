using Backend.CMS.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.CMS.Domain.Entities
{
    public class ProductVariant : BaseEntity
    {
        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public decimal? CompareAtPrice { get; set; }

        public decimal? CostPerItem { get; set; }

        public int Quantity { get; set; } = 0;

        public bool TrackQuantity { get; set; } = true;

        public bool ContinueSellingWhenOutOfStock { get; set; } = false;

        public bool RequiresShipping { get; set; } = true;

        public bool IsTaxable { get; set; } = true;

        public decimal Weight { get; set; } = 0;

        [MaxLength(10)]
        public string? WeightUnit { get; set; } = "kg";

        [MaxLength(255)]
        public string? Barcode { get; set; }

        public int Position { get; set; } = 0;

        public bool IsDefault { get; set; } = false;

        [NotMapped]
        public Dictionary<string, object> CustomFields { get; set; } = new();

        [MaxLength(255)]
        public string? Option1 { get; set; }

        [MaxLength(255)]
        public string? Option2 { get; set; }

        [MaxLength(255)]
        public string? Option3 { get; set; }

        // Images relationship
        public ICollection<ProductVariantImage> Images { get; set; } = new List<ProductVariantImage>();

        // Computed property for featured image
        [NotMapped]
        public string? FeaturedImageUrl => Images.OrderBy(i => i.Position).FirstOrDefault()?.ImageUrl;
    }
}
