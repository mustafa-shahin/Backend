using Backend.CMS.Domain.Common;
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

        // File type relationships
        public ICollection<Image> Images { get; set; } = [];
        public ICollection<Document> Documents { get; set; } = [];
        public ICollection<Audio> Audios { get; set; } = [];
        public ICollection<Video> Videos { get; set; } = [];
        public ICollection<Archive> Archives { get; set; } = [];
        public ICollection<OtherFile> OtherFiles { get; set; } = [];

        public bool IsPublic { get; set; } = false;

        [NotMapped]
        public Dictionary<string, object> Metadata { get; set; } = [];

        public FolderType FolderType { get; set; } = FolderType.General;

        // Helper properties
        public bool HasSubFolders => SubFolders?.Any() == true;
        
        public int TotalFileCount => (Images?.Count ?? 0) + (Documents?.Count ?? 0) + 
                                   (Audios?.Count ?? 0) + (Videos?.Count ?? 0) + 
                                   (Archives?.Count ?? 0) + (OtherFiles?.Count ?? 0);
        
        public long TotalSize => (Images?.Sum(f => f.Size) ?? 0) + (Documents?.Sum(f => f.Size) ?? 0) + 
                               (Audios?.Sum(f => f.Size) ?? 0) + (Videos?.Sum(f => f.Size) ?? 0) + 
                               (Archives?.Sum(f => f.Size) ?? 0) + (OtherFiles?.Sum(f => f.Size) ?? 0);
        
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
        
        public bool HasFiles => TotalFileCount > 0;
        
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
