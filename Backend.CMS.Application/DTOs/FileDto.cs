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
        public string FileExtension { get; set; } = string.Empty;
        public FileType FileType { get; set; }
        public string? Description { get; set; }
        public string? Alt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new(); 
        public bool IsPublic { get; set; }
        public int? FolderId { get; set; }
        public int DownloadCount { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public TimeSpan? Duration { get; set; }
        public string? Hash { get; set; } // Added from FileEntity
        public bool IsProcessed { get; set; }
        public string? ProcessingStatus { get; set; }
        public Dictionary<string, object> Tags { get; set; } = new(); 
        public DateTime CreatedAt { get; set; } 
        public DateTime UpdatedAt { get; set; } 


    }


    public class FileUploadDto
    {
        public IFormFile File { get; set; } = null!;
        public string? Description { get; set; }
        public string? Alt { get; set; }
        public int? FolderId { get; set; }
        public bool IsPublic { get; set; } = false;
        public Dictionary<string, object> Tags { get; set; } = new();
        public bool GenerateThumbnail { get; set; } = true;
    }

    public class MultipleFileUploadDto
    {
        public IFormFileCollection Files { get; set; } = null!;
        public int? FolderId { get; set; }
        public bool IsPublic { get; set; } = false;
        public bool GenerateThumbnails { get; set; } = true;
    }

    public class UpdateFileDto
    {
        public string? Description { get; set; }
        public string? Alt { get; set; }
        public bool IsPublic { get; set; }
        public Dictionary<string, object> Tags { get; set; } = new();
        public int? FolderId { get; set; }
    }

    public class MoveFileDto
    {
        public int FileId { get; set; }
        public int? NewFolderId { get; set; }
    }

    public class CopyFileDto
    {
        public int FileId { get; set; }
        public int? DestinationFolderId { get; set; }
        public string? NewName { get; set; }
    }

    public class FileSearchDto
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
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "CreatedAt";
        public string SortDirection { get; set; } = "Desc";
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
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentFolderId { get; set; }
        public bool IsPublic { get; set; } = false;
        public FolderType FolderType { get; set; } = FolderType.General;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class UpdateFolderDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPublic { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class MoveFolderDto
    {
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
    }

    public class FilePreviewDto
    {
        public int Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public FileType FileType { get; set; }
        public string FileUrl { get; set; } = string.Empty; // API endpoint for file download
        public string? ThumbnailUrl { get; set; } // API endpoint for thumbnail download
        public int? Width { get; set; }
        public int? Height { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool CanPreview { get; set; }
        public string? PreviewHtml { get; set; }
    }

    public class RenameFolderDto
    {
        public string NewName { get; set; } = string.Empty;
    }

    public class CopyFolderDto
    {
        public int FolderId { get; set; }
        public int? DestinationFolderId { get; set; }
        public string? NewName { get; set; }
    }
}