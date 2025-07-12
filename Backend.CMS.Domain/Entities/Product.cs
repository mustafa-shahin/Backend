using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Backend.CMS.Domain.Entities
{
    public class Product : BaseEntity
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(1000)]
        public string? ShortDescription { get; set; }

        public bool RequiresShipping { get; set; } = true;

        public ProductStatus Status { get; set; } = ProductStatus.Active;

        public ProductType Type { get; set; } = ProductType.Physical;

        [MaxLength(255)]
        public string? Vendor { get; set; }

        public bool HasVariants { get; set; } = false;

        public DateTime? PublishedAt { get; set; }

        [MaxLength(255)]
        public string? MetaTitle { get; set; }

        [MaxLength(1000)]
        public string? MetaDescription { get; set; }

        [MaxLength(500)]
        public string? MetaKeywords { get; set; }

        [MaxLength(1000)]
        public string? SearchKeywords { get; set; }

        [NotMapped]
        public Dictionary<string, object> CustomFields { get; set; } = new();

        [NotMapped]
        public Dictionary<string, object> SEOSettings { get; set; } = new();

        public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();

        public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();

        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

        // Computed property for featured image
        [NotMapped]
        public string? FeaturedImageUrl => Images.OrderBy(i => i.Position).FirstOrDefault()?.ImageUrl;
    }

   

    public class ProductCategory : BaseEntity
    {
        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;

        [Required]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category Category { get; set; } = null!;

        public int SortOrder { get; set; } = 0;
    }

    public class ProductImage : BaseEntity
    {
        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;

        [Required]
        public int FileId { get; set; }

        [ForeignKey("FileId")]
        public FileEntity File { get; set; } = null!;

        [MaxLength(255)]
        public string? Alt { get; set; }

        [MaxLength(500)]
        public string? Caption { get; set; }

        public int Position { get; set; } = 0;

        public bool IsFeatured { get; set; } = false;

        // Computed property for image URL
        [NotMapped]
        public string ImageUrl => $"/api/files/{FileId}/download";

        [NotMapped]
        public string? ThumbnailUrl => $"/api/files/{FileId}/thumbnail";
    }

    public class ProductVariantImage : BaseEntity
    {
        [Required]
        public int ProductVariantId { get; set; }

        [ForeignKey("ProductVariantId")]
        public ProductVariant ProductVariant { get; set; } = null!;

        [Required]
        public int FileId { get; set; }

        [ForeignKey("FileId")]
        public FileEntity File { get; set; } = null!;

        [MaxLength(255)]
        public string? Alt { get; set; }

        [MaxLength(500)]
        public string? Caption { get; set; }

        public int Position { get; set; } = 0;

        public bool IsFeatured { get; set; } = false;

        // Computed property for image URL
        [NotMapped]
        public string ImageUrl => $"/api/files/{FileId}/download";

        [NotMapped]
        public string? ThumbnailUrl => $"/api/files/{FileId}/thumbnail";
    }

}