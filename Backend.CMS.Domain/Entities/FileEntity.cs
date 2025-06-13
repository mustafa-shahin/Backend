using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.CMS.Domain.Entities
{
    public class FileEntity : BaseEntity
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

        public FileType FileType { get; set; }

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

        public byte[]? ThumbnailContent { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public TimeSpan? Duration { get; set; }

        [MaxLength(512)]
        public string? Hash { get; set; }

        public bool IsProcessed { get; set; } = true;

        [MaxLength(100)]
        public string? ProcessingStatus { get; set; }

        [NotMapped]
        public Dictionary<string, object> Tags { get; set; } = [];
    }

    public class Folder : BaseEntity
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(1024)]
        public string Path { get; set; } = string.Empty;

        public int? ParentFolderId { get; set; }

        [ForeignKey("ParentFolderId")]
        public Folder? ParentFolder { get; set; }

        public ICollection<Folder> SubFolders { get; set; } = [];

        public ICollection<FileEntity> Files { get; set; } = [];

        public bool IsPublic { get; set; } = false;

        [NotMapped]
        public Dictionary<string, object> Metadata { get; set; } = [];

        public FolderType FolderType { get; set; } = FolderType.General;
    }


    public class FileAccess : BaseEntity
    {
        [Required]
        public int FileId { get; set; }

        [ForeignKey("FileId")]
        public FileEntity File { get; set; } = null!;

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