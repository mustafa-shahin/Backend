using Backend.CMS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Application.DTOs
{
    public class FolderDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Path { get; set; } = string.Empty;
        public int? ParentFolderId { get; set; }
        public string? ParentFolderPath { get; set; }
        public List<FolderDto> SubFolders { get; set; } = new();
        public List<FileDto> Files { get; set; } = new();
        public bool IsPublic { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public FolderType FolderType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Computed properties
        public int FileCount { get; set; }
        public int SubFolderCount { get; set; }
        public long TotalSize { get; set; }
        public string TotalSizeFormatted { get; set; } = string.Empty;
    }
    public class CreateFolderDto
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int? ParentFolderId { get; set; }

        public bool IsPublic { get; set; } = false;

        public FolderType FolderType { get; set; } = FolderType.General;

        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class UpdateFolderDto
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public bool IsPublic { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class MoveFolderDto
    {
        [Required]
        public int FolderId { get; set; }

        public int? NewParentFolderId { get; set; }
    }

    public class FolderTreeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int? ParentFolderId { get; set; }
        public List<FolderTreeDto> Children { get; set; } = new();
        public FolderType FolderType { get; set; }
        public bool IsPublic { get; set; }
        public int FileCount { get; set; }
        public bool HasSubFolders { get; set; }
        public int Level { get; set; }
        public bool IsExpanded { get; set; }
    }
    public class CopyFolderDto
    {
        [Required]
        public int FolderId { get; set; }

        public int? DestinationFolderId { get; set; }

        [MaxLength(255)]
        public string? NewName { get; set; }

        /// <summary>
        /// Whether to copy all files and subfolders
        /// </summary>
        public bool DeepCopy { get; set; } = true;
    }

    public class RenameFolderDto
    {
        [Required]
        [MaxLength(255)]
        public string NewName { get; set; } = string.Empty;
    }
}
