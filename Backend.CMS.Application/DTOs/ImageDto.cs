using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Application.DTOs
{
    public class ImageDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Extension { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Alt { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool HasThumbnail { get; set; }
        public int? FolderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateImageDto
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public byte[] Content { get; set; } = [];

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Alt { get; set; }

        public int? FolderId { get; set; }

        [MaxLength(100)]
        public string? EntityType { get; set; }

        public int? EntityId { get; set; }
    }

    public class UpdateImageDto
    {
        [MaxLength(255)]
        public string? Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Alt { get; set; }

        public int? FolderId { get; set; }
    }
}