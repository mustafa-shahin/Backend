using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Domain.Entities.Files;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.CMS.Domain.Entities
{
    public class FileAccess : BaseEntity
    {
        [Required]
        public int FileId { get; set; }

        [ForeignKey("FileId")]
        public BaseFileEntity File { get; set; } = null!;

        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(512)]
        public string? UserAgent { get; set; }

        public FileAccessType AccessType { get; set; }

        public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    }
}