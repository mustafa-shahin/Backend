using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities.Files;
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
        FileUrlsDto GenerateFileUrls(BaseFileEntity file);

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
}