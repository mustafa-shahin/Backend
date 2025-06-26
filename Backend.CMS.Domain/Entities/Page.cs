using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Domain.Entities
{
    public class Page : BaseEntity
    {
        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(255)]
        public string? MetaTitle { get; set; }

        [StringLength(500)]
        public string? MetaDescription { get; set; }

        [StringLength(500)]
        public string? MetaKeywords { get; set; }

        [Required]
        public PageStatus Status { get; set; }

        [StringLength(255)]
        public string? Template { get; set; }

        public int? Priority { get; set; }

        public int? ParentPageId { get; set; }

        public bool RequiresLogin { get; set; } = false;

        public bool AdminOnly { get; set; } = false;

        [ForeignKey("ParentPageId")]
        public Page? ParentPage { get; set; }

        public ICollection<Page> ChildPages { get; set; } = new List<Page>();

        /// <summary>
        /// JSON content containing all page components and layout
        /// </summary>
        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> Content { get; set; } = new();

        /// <summary>
        /// Designer layout configuration
        /// </summary>
        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> Layout { get; set; } = new();

        /// <summary>
        /// Page-level settings
        /// </summary>
        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> Settings { get; set; } = new();

        /// <summary>
        /// Custom CSS styles for the page
        /// </summary>
        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> Styles { get; set; } = new();

        public DateTime? PublishedOn { get; set; }

        [StringLength(255)]
        public string? PublishedBy { get; set; }

        public ICollection<PageVersion> Versions { get; set; } = new List<PageVersion>();
    }
}