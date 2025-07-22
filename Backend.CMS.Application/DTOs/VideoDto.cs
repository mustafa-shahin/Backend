using Backend.CMS.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Application.DTOs
{
    public class VideoDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Extension { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? FolderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? Duration { get; set; }
        public int? ThumbnailImageId { get; set; }
        public Image? ThumbnailImage { get; set; }
    }

    public class CreateVideoDto
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

        public int? FolderId { get; set; }
    }

    public class UpdateVideoDto
    {
        [MaxLength(255)]
        public string? Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int? FolderId { get; set; }
        public int? ThumbnailImageId { get; set; }
    }
}