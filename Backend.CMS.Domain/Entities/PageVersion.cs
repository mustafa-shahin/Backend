using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;

namespace Backend.CMS.Domain.Entities
{
    public class PageVersion : BaseEntity
    {
        public int PageId { get; set; }

        [ForeignKey("PageId")]
        public Page Page { get; set; } = null!;

        [Required]
        public int VersionNumber { get; set; }

        [StringLength(1000)]
        public string? ChangeNotes { get; set; }

        /// <summary>
        /// Complete page snapshot as JSON
        /// </summary>
        [Required]
        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> PageSnapshot { get; set; } = new();

        /// <summary>
        /// Version metadata
        /// </summary>
        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> Metadata { get; set; } = new();

        public bool IsPublished { get; set; } = false;
        public DateTime? PublishedAt { get; set; }
    }
}