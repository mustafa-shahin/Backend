using Backend.CMS.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.CMS.Domain.Entities
{
    [Table("Audios")]
    public class Audio : BaseEntity
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public byte[] Content { get; set; } = [];

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long Size { get; set; }

        [MaxLength(20)]
        public string Extension { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int? FolderId { get; set; }

        [ForeignKey("FolderId")]
        public Folder? Folder { get; set; }

        public int? Duration { get; set; }

        // Entity linking properties
        [MaxLength(100)]
        public string? EntityType { get; set; }

        public int? EntityId { get; set; }

        [MaxLength(500)]
        public string? Tags { get; set; }
    }
}