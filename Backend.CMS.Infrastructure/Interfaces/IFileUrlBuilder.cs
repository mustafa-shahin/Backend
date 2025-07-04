using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IFileUrlBuilder
    {
        /// <summary>
        /// Generates download URL for a file
        /// </summary>
        string GenerateDownloadUrl(int fileId, bool isPublic = false);

        /// <summary>
        /// Generates preview URL for a file (images/videos)
        /// </summary>
        string? GeneratePreviewUrl(int fileId, FileType fileType, bool isPublic = false);

        /// <summary>
        /// Generates thumbnail URL for a file
        /// </summary>
        string? GenerateThumbnailUrl(int fileId, FileType fileType, bool hasThumbnail = false);

        /// <summary>
        /// Generates all applicable URLs for a file entity
        /// </summary>
        FileUrlSet GenerateFileUrls(FileEntity file);

        /// <summary>
        /// Generates download token URL for secure file access
        /// </summary>
        string GenerateTokenDownloadUrl(string token);

        /// <summary>
        /// Generates direct file access URL for public files
        /// </summary>
        string GenerateDirectAccessUrl(int fileId);

        /// <summary>
        /// Validates if a file type supports preview
        /// </summary>
        bool SupportsPreview(FileType fileType, string contentType);

        /// <summary>
        /// Validates if a file type supports thumbnails
        /// </summary>
        bool SupportsThumbnails(FileType fileType);
    }

    public class FileUrlSet
    {
        public string DownloadUrl { get; set; } = string.Empty;
        public string? PreviewUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? DirectAccessUrl { get; set; }
        public Dictionary<string, string> AdditionalUrls { get; set; } = new();
    }
}