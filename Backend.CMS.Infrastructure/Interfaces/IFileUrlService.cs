using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.Interfaces
{
    /// <summary>
    /// Service for generating URLs for file access and preview
    /// </summary>
    public interface IFileUrlService
    {
        /// <summary>
        /// Generate preview URL for an image
        /// </summary>
        /// <param name="imageId">Image ID</param>
        /// <returns>Preview URL</returns>
        string GenerateImagePreviewUrl(int imageId);

        /// <summary>
        /// Generate thumbnail URL for an image
        /// </summary>
        /// <param name="imageId">Image ID</param>
        /// <returns>Thumbnail URL</returns>
        string GenerateImageThumbnailUrl(int imageId);

        /// <summary>
        /// Generate download URL for an image
        /// </summary>
        /// <param name="imageId">Image ID</param>
        /// <returns>Download URL</returns>
        string GenerateImageDownloadUrl(int imageId);

        /// <summary>
        /// Generate download URL for a document
        /// </summary>
        /// <param name="documentId">Document ID</param>
        /// <returns>Download URL</returns>
        string GenerateDocumentDownloadUrl(int documentId);

        /// <summary>
        /// Generate download URL for an audio file
        /// </summary>
        /// <param name="audioId">Audio ID</param>
        /// <returns>Download URL</returns>
        string GenerateAudioDownloadUrl(int audioId);

        /// <summary>
        /// Generate preview URL for a video
        /// </summary>
        /// <param name="videoId">Video ID</param>
        /// <returns>Preview URL</returns>
        string GenerateVideoPreviewUrl(int videoId);

        /// <summary>
        /// Generate thumbnail URL for a video (from its thumbnail image)
        /// </summary>
        /// <param name="videoId">Video ID</param>
        /// <returns>Thumbnail URL</returns>
        string GenerateVideoThumbnailUrl(int videoId);

        /// <summary>
        /// Generate download URL for a video
        /// </summary>
        /// <param name="videoId">Video ID</param>
        /// <returns>Download URL</returns>
        string GenerateVideoDownloadUrl(int videoId);

        /// <summary>
        /// Generate download URL for an archive
        /// </summary>
        /// <param name="archiveId">Archive ID</param>
        /// <returns>Download URL</returns>
        string GenerateArchiveDownloadUrl(int archiveId);

        /// <summary>
        /// Generate download URL for other file types
        /// </summary>
        /// <param name="otherFileId">Other file ID</param>
        /// <returns>Download URL</returns>
        string GenerateOtherFileDownloadUrl(int otherFileId);

        /// <summary>
        /// Generate preview URL for any file type based on its entity
        /// </summary>
        /// <param name="image">Image entity</param>
        /// <returns>Preview URL or null if preview not supported</returns>
        string? GeneratePreviewUrl(Image image);

        /// <summary>
        /// Generate download URL for any file type based on its entity
        /// </summary>
        /// <param name="image">Image entity</param>
        /// <returns>Download URL</returns>
        string GenerateDownloadUrl(Image image);

        /// <summary>
        /// Generate download URL for any file type based on its entity
        /// </summary>
        /// <param name="document">Document entity</param>
        /// <returns>Download URL</returns>
        string GenerateDownloadUrl(Document document);

        /// <summary>
        /// Generate download URL for any file type based on its entity
        /// </summary>
        /// <param name="audio">Audio entity</param>
        /// <returns>Download URL</returns>
        string GenerateDownloadUrl(Audio audio);

        /// <summary>
        /// Generate download URL for any file type based on its entity
        /// </summary>
        /// <param name="video">Video entity</param>
        /// <returns>Download URL</returns>
        string GenerateDownloadUrl(Video video);

        /// <summary>
        /// Generate download URL for any file type based on its entity
        /// </summary>
        /// <param name="archive">Archive entity</param>
        /// <returns>Download URL</returns>
        string GenerateDownloadUrl(Archive archive);

        /// <summary>
        /// Generate download URL for any file type based on its entity
        /// </summary>
        /// <param name="otherFile">Other file entity</param>
        /// <returns>Download URL</returns>
        string GenerateDownloadUrl(OtherFile otherFile);
    }
}