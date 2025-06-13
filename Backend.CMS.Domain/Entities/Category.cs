using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;

namespace Backend.CMS.Domain.Entities
{
    public class Category : BaseEntity
    {
        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? ShortDescription { get; set; }

        [StringLength(500)]
        public string? Image { get; set; }

        public int? ParentCategoryId { get; set; }

        public Category? ParentCategory { get; set; }

        public ICollection<Category> SubCategories { get; set; } = new List<Category>();

        public bool IsActive { get; set; } = true;

        public bool IsVisible { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        [StringLength(255)]
        public string? MetaTitle { get; set; }

        [StringLength(500)]
        public string? MetaDescription { get; set; }

        [StringLength(500)]
        public string? MetaKeywords { get; set; }

        [Column(TypeName = "jsonb")] 
        public Dictionary<string, object> CustomFields { get; set; } = new();

        public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
    }
}