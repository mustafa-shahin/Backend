using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.CMS.Domain.Entities.Files
{
    [Table("Files")]
    public abstract class BaseFileEntity : BaseEntity
    {
        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required]
        public byte[] FileContent { get; set; } = [];

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [MaxLength(20)]
        public string FileExtension { get; set; } = string.Empty;

        public abstract FileType FileType { get; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Alt { get; set; }

        [NotMapped]
        public Dictionary<string, object> Metadata { get; set; } = [];

        public bool IsPublic { get; set; } = false;

        public int? FolderId { get; set; }

        [ForeignKey("FolderId")]
        public Folder? Folder { get; set; }

        public int DownloadCount { get; set; } = 0;

        public DateTime? LastAccessedAt { get; set; }

        [MaxLength(512)]
        public string? Hash { get; set; }

        public bool IsProcessed { get; set; } = true;

        [MaxLength(100)]
        public string? ProcessingStatus { get; set; }

        [NotMapped]
        public Dictionary<string, object> Tags { get; set; } = [];

        // Navigation property for file access tracking
        public virtual ICollection<FileAccess> FileAccesses { get; set; } = [];

        // Virtual method for custom validation per file type
        public virtual ValidationResult ValidateFileType()
        {
            return ValidationResult.Success!;
        }

        // Virtual method for custom processing per file type
        public virtual Task<bool> ProcessFileAsync()
        {
            return Task.FromResult(true);
        }
    }
}