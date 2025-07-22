using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Application.DTOs
{
    /// <summary>
    /// Unified file DTO that includes properties from all file types
    /// Used by frontend to handle all file types consistently
    /// </summary>
    public class FileDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public long FileSize { get; set; } // Alias for Size for backward compatibility
        public string Extension { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? FolderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsPublic { get; set; }
        public FileType FileType { get; set; }

        // Image-specific properties
        public string? Alt { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool HasThumbnail { get; set; }
        public string? ImageDimensions => Width.HasValue && Height.HasValue ? $"{Width}x{Height}" : null;

        // Video-specific properties  
        public int? Duration { get; set; } // in seconds
        public TimeSpan? DurationTimeSpan => Duration.HasValue ? TimeSpan.FromSeconds(Duration.Value) : null;
        public int? ThumbnailImageId { get; set; }

        // Entity linking properties
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public string? Tags { get; set; }

        // URLs for file access
        public FileUrlsDto Urls { get; set; } = new();

        // Additional metadata
        public Dictionary<string, object>? Metadata { get; set; }

        // Computed properties for frontend compatibility
        public bool IsImage => FileType == FileType.Image || 
                              new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" }
                              .Contains(Extension.ToLowerInvariant());
        
        public string FileTypeName => FileType switch
        {
            FileType.Image => "Image",
            FileType.Video => "Video", 
            FileType.Audio => "Audio",
            FileType.Document => "Document",
            FileType.Archive => "Archive",
            _ => "Other"
        };

        public string FileSizeFormatted => FormatBytes(Size > 0 ? Size : FileSize);
        
        public string FileExtension => Extension.StartsWith('.') ? Extension : $".{Extension}";
        
        public bool CanPreview
        {
            get
            {
                var ext = Extension.ToLowerInvariant();
                var imageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" };
                var videoExts = new[] { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mkv" };
                var audioExts = new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma" };
                return imageExts.Contains(ext) || videoExts.Contains(ext) || audioExts.Contains(ext) || 
                       ext == ".pdf" || ext == ".txt";
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// File URLs for different access types
    /// </summary>
    public class FileUrlsDto
    {
        public string Download { get; set; } = string.Empty;
        public string DirectAccess { get; set; } = string.Empty;
        public string? Preview { get; set; }
        public string? Thumbnail { get; set; }
        public string? Stream { get; set; }
    }

    /// <summary>
    /// Create DTO for unified file upload
    /// </summary>
    public class CreateFileDto
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

        // Image-specific
        [MaxLength(255)]
        public string? Alt { get; set; }

        // Entity linking
        [MaxLength(100)]
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }

        [MaxLength(500)]
        public string? Tags { get; set; }

        public bool IsPublic { get; set; } = false;
    }

    /// <summary>
    /// Update DTO for unified file updates
    /// </summary>
    public class UpdateFileDto
    {
        [MaxLength(255)]
        public string? Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int? FolderId { get; set; }

        [MaxLength(255)]
        public string? Alt { get; set; }

        public bool? IsPublic { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Search DTO for file queries
    /// </summary>
    public class FileSearchDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Name { get; set; }
        public string? SearchTerm { get; set; }
        public int? FolderId { get; set; }
        public FileType? FileType { get; set; }
        public string? ContentType { get; set; }
        public bool? IsPublic { get; set; }
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public string SortBy { get; set; } = "CreatedAt";
        public string SortDirection { get; set; } = "Desc";
        public bool SortDescending { get; set; } = true;
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
    }

    /// <summary>
    /// File upload result
    /// </summary>
    public class FileUploadResultDto
    {
        public bool Success { get; set; }
        public FileDto? File { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object>? ProcessingInfo { get; set; }
    }

    /// <summary>
    /// File preview DTO
    /// </summary>
    public class FilePreviewDto
    {
        public int FileId { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public bool CanPreview { get; set; }
        public string? PreviewUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public Dictionary<string, object>? PreviewData { get; set; }
    }

    /// <summary>
    /// Move file DTO
    /// </summary>
    public class MoveFileDto
    {
        [Required]
        public int FileId { get; set; }

        public int? DestinationFolderId { get; set; }
    }

    /// <summary>
    /// Copy file DTO
    /// </summary>
    public class CopyFileDto
    {
        [Required]
        public int FileId { get; set; }

        public int? DestinationFolderId { get; set; }

        [MaxLength(255)]
        public string? NewName { get; set; }
    }
}