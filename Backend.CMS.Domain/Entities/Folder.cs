using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.CMS.Domain.Entities
{
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

        // Updated to use BaseFileEntity instead of FileEntity
        public ICollection<BaseFileEntity> Files { get; set; } = [];

        public bool IsPublic { get; set; } = false;

        [NotMapped]
        public Dictionary<string, object> Metadata { get; set; } = [];

        public FolderType FolderType { get; set; } = FolderType.General;

        // Helper properties
        public int FileCount => Files?.Count ?? 0;
        
        public long TotalSize => Files?.Sum(f => f.FileSize) ?? 0;
        
        public string FormattedSize
        {
            get
            {
                var size = TotalSize;
                if (size < 1024) return $"{size} B";
                if (size < 1024 * 1024) return $"{size / 1024.0:F1} KB";
                if (size < 1024 * 1024 * 1024) return $"{size / (1024.0 * 1024):F1} MB";
                return $"{size / (1024.0 * 1024 * 1024):F1} GB";
            }
        }

        public bool HasSubFolders => SubFolders?.Any() == true;
        
        public bool HasFiles => Files?.Any() == true;
        
        public bool IsEmpty => !HasSubFolders && !HasFiles;

        public string FullPath
        {
            get
            {
                if (ParentFolder == null) return Name;
                return $"{ParentFolder.FullPath}/{Name}";
            }
        }
    }
}
