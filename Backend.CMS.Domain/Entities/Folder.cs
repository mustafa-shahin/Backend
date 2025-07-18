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

        public ICollection<FileEntity> Files { get; set; } = [];

        public bool IsPublic { get; set; } = false;

        [NotMapped]
        public Dictionary<string, object> Metadata { get; set; } = [];

        public FolderType FolderType { get; set; } = FolderType.General;
    }
}
