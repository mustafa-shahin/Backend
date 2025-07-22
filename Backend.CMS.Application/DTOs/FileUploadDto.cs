using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Application.DTOs
{
    public class FileUploadDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? FolderId { get; set; }
        public string? Alt { get; set; }
        public bool IsPublic { get; set; } = false;
        public bool GenerateThumbnail { get; set; } = true;
        public bool ProcessImmediately { get; set; } = true;

        // Entity linking properties
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }

        // Additional metadata
        public Dictionary<string, object>? Tags { get; set; }

        // Helper properties for easy access to file data
        public string ContentType => File.ContentType;
    }

    public class MultipleFileUploadDto
    {
        [Required]
        public ICollection<IFormFile> Files { get; set; } = new List<IFormFile>();
        public string? Description { get; set; }
        public int? FolderId { get; set; }
        public bool IsPublic { get; set; } = false;
        public bool GenerateThumbnails { get; set; } = true;
        public bool ProcessImmediately { get; set; } = true;
        public bool ProcessInParallel { get; set; } = true;

        // Entity linking properties
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
    }
}