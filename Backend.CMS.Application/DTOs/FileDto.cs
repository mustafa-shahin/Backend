using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Backend.CMS.Application.DTOs
{
    public class FileDto
    {
        public int Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public FileType FileType { get; set; }
        public string FileTypeName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Alt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool IsPublic { get; set; }
        public int? FolderId { get; set; }
        public string? FolderPath { get; set; }
        public int DownloadCount { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public TimeSpan? Duration { get; set; }
        public string? DurationFormatted { get; set; }
        public string? Hash { get; set; }
        public bool IsProcessed { get; set; }
        public string? ProcessingStatus { get; set; }
        public Dictionary<string, object> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // URL Properties - populated by FileUrlBuilder
        public FileUrlsDto Urls { get; set; } = new();

        // Computed Properties
        public bool HasThumbnail { get; set; }
        public bool CanPreview { get; set; }
        public bool IsImage => FileType == FileType.Image;
        public bool IsVideo => FileType == FileType.Video;
        public bool IsAudio => FileType == FileType.Audio;
        public bool IsDocument => FileType == FileType.Document;
        public string ImageDimensions => Width.HasValue && Height.HasValue ? $"{Width}x{Height}" : string.Empty;
    }

    /// <summary>
    /// Contains all URL variations for a file
    /// </summary>
    public class FileUrlsDto
    {
        /// <summary>
        /// Primary download URL for the file
        /// </summary>
        public string Download { get; set; } = string.Empty;

        /// <summary>
        /// Preview URL for supported file types (images, videos, documents)
        /// </summary>
        public string? Preview { get; set; }

        /// <summary>
        /// Thumbnail URL for files that have thumbnails
        /// </summary>
        public string? Thumbnail { get; set; }

        /// <summary>
        /// Direct access URL for public files
        /// </summary>
        public string? DirectAccess { get; set; }

        /// <summary>
        /// Streaming URL for video/audio files
        /// </summary>
        public string? Stream { get; set; }

        /// <summary>
        /// API endpoint to generate download token for private files
        /// </summary>
        public string? GenerateToken { get; set; }

        /// <summary>
        /// Additional specialized URLs (resize, metadata, etc.)
        /// </summary>
        public Dictionary<string, string> Additional { get; set; } = new();
    }

    public class FileUploadDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Alt { get; set; }

        public int? FolderId { get; set; }

        public bool IsPublic { get; set; } = false;

        public Dictionary<string, object> Tags { get; set; } = new();

        public bool GenerateThumbnail { get; set; } = true;

        public bool ProcessImmediately { get; set; } = true;
    }

    public class MultipleFileUploadDto
    {
        [Required]
        public IFormFileCollection Files { get; set; } = null!;

        public int? FolderId { get; set; }

        public bool IsPublic { get; set; } = false;

        public bool GenerateThumbnails { get; set; } = true;

        public bool ProcessImmediately { get; set; } = true;

        /// <summary>
        /// Whether to process files in parallel (faster but more resource intensive)
        /// </summary>
        public bool ProcessInParallel { get; set; } = true;
    }

    public class UpdateFileDto
    {
        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Alt { get; set; }

        public bool IsPublic { get; set; }

        public Dictionary<string, object> Tags { get; set; } = new();

        public int? FolderId { get; set; }

        /// <summary>
        /// Whether to regenerate thumbnail if it's an image
        /// </summary>
        public bool RegenerateThumbnail { get; set; } = false;
    }

    public class MoveFileDto
    {
        [Required]
        public int FileId { get; set; }

        public int? NewFolderId { get; set; }

        /// <summary>
        /// Whether to update file path metadata
        /// </summary>
        public bool UpdateMetadata { get; set; } = true;
    }

    public class CopyFileDto
    {
        [Required]
        public int FileId { get; set; }

        public int? DestinationFolderId { get; set; }

        [MaxLength(255)]
        public string? NewName { get; set; }

        /// <summary>
        /// Whether to copy thumbnail as well
        /// </summary>
        public bool CopyThumbnail { get; set; } = true;

        /// <summary>
        /// Whether to copy metadata and tags
        /// </summary>
        public bool CopyMetadata { get; set; } = true;
    }

    public class FileSearchDto : PaginationRequest
    {
        public string? SearchTerm { get; set; }

        public FileType? FileType { get; set; }

        public int? FolderId { get; set; }

        public bool? IsPublic { get; set; }

        public DateTime? CreatedFrom { get; set; }

        public DateTime? CreatedTo { get; set; }

        public long? MinSize { get; set; }

        public long? MaxSize { get; set; }

        public List<string> Tags { get; set; } = new();

        public string SortBy { get; set; } = "CreatedAt";

        public string SortDirection { get; set; } = "Desc";

        /// <summary>
        /// Include file content in search 
        /// </summary>
        public bool IncludeContent { get; set; } = false;

        /// <summary>
        /// Search only in user's files
        /// </summary>
        public bool UserFilesOnly { get; set; } = false;
    }

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

    public class FilePreviewDto
    {
        public int Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public FileType FileType { get; set; }
        public FileUrlsDto Urls { get; set; } = new();
        public int? Width { get; set; }
        public int? Height { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool CanPreview { get; set; }
        public string? PreviewHtml { get; set; }
        public string? PreviewText { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class RenameFolderDto
    {
        [Required]
        [MaxLength(255)]
        public string NewName { get; set; } = string.Empty;
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

    /// <summary>
    /// File upload result with detailed information
    /// </summary>
    public class FileUploadResultDto
    {
        public bool Success { get; set; }
        public FileDto? File { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> ProcessingInfo { get; set; } = new();
    }

    /// <summary>
    /// Bulk operation result
    /// </summary>
    public class BulkOperationResultDto
    {
        public int TotalRequested { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<FileDto> SuccessfulFiles { get; set; } = new();
        public List<BulkOperationErrorDto> Errors { get; set; } = new();
        public bool IsPartialSuccess => SuccessCount > 0 && FailureCount > 0;
        public bool IsCompleteSuccess => SuccessCount == TotalRequested;
        public bool IsCompleteFailure => FailureCount == TotalRequested;
    }

    public class BulkOperationErrorDto
    {
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
    }
}