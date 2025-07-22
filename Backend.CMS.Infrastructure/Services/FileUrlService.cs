using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    /// <summary>
    /// Service for generating URLs for file access and preview
    /// </summary>
    public class FileUrlService : IFileUrlService
    {
        private readonly ILogger<FileUrlService> _logger;
        private readonly string _baseUrl;
        private readonly string _apiVersion;

        public FileUrlService(
            IConfiguration configuration,
            ILogger<FileUrlService> logger)
        {
            _logger = logger;
            _baseUrl = configuration["FileUrls:BaseUrl"] ?? "/api";
            _apiVersion = configuration["FileUrls:ApiVersion"] ?? "v1";
        }

        #region Image URLs

        public string GenerateImagePreviewUrl(int imageId)
        {
            // Generate URL without version for backward compatibility with existing endpoints
            return $"{_baseUrl}/images/{imageId}/preview";
        }

        public string GenerateImageThumbnailUrl(int imageId)
        {
            // Generate URL without version for backward compatibility with existing endpoints
            return $"{_baseUrl}/images/{imageId}/thumbnail";
        }

        public string GenerateImageDownloadUrl(int imageId)
        {
            // Generate URL without version for backward compatibility with existing endpoints
            return $"{_baseUrl}/images/{imageId}/download";
        }

        #endregion

        #region Document URLs

        public string GenerateDocumentDownloadUrl(int documentId)
        {
            // Generate URL without version for backward compatibility with existing endpoints
            return $"{_baseUrl}/documents/{documentId}/download";
        }

        #endregion

        #region Audio URLs

        public string GenerateAudioDownloadUrl(int audioId)
        {
            // Generate URL without version for backward compatibility with existing endpoints
            return $"{_baseUrl}/audios/{audioId}/download";
        }

        #endregion

        #region Video URLs

        public string GenerateVideoPreviewUrl(int videoId)
        {
            // Generate URL without version for backward compatibility with existing endpoints
            return $"{_baseUrl}/videos/{videoId}/preview";
        }

        public string GenerateVideoThumbnailUrl(int videoId)
        {
            // Generate URL without version for backward compatibility with existing endpoints
            return $"{_baseUrl}/videos/{videoId}/thumbnail";
        }

        public string GenerateVideoDownloadUrl(int videoId)
        {
            // Generate URL without version for backward compatibility with existing endpoints
            return $"{_baseUrl}/videos/{videoId}/download";
        }

        #endregion

        #region Archive URLs

        public string GenerateArchiveDownloadUrl(int archiveId)
        {
            // Generate URL without version for backward compatibility with existing endpoints
            return $"{_baseUrl}/archives/{archiveId}/download";
        }

        #endregion

        #region Other File URLs

        public string GenerateOtherFileDownloadUrl(int otherFileId)
        {
            // Generate URL without version for backward compatibility with existing endpoints
            return $"{_baseUrl}/otherfiles/{otherFileId}/download";
        }

        #endregion

        #region Entity-based URL Generation

        public string? GeneratePreviewUrl(Image image)
        {
            if (image == null)
            {
                _logger.LogWarning("Cannot generate preview URL for null image");
                return null;
            }

            // Images always support preview
            return GenerateImagePreviewUrl(image.Id);
        }

        public string GenerateDownloadUrl(Image image)
        {
            if (image == null)
            {
                _logger.LogWarning("Cannot generate download URL for null image");
                throw new ArgumentNullException(nameof(image));
            }

            return GenerateImageDownloadUrl(image.Id);
        }

        public string GenerateDownloadUrl(Document document)
        {
            if (document == null)
            {
                _logger.LogWarning("Cannot generate download URL for null document");
                throw new ArgumentNullException(nameof(document));
            }

            return GenerateDocumentDownloadUrl(document.Id);
        }

        public string GenerateDownloadUrl(Audio audio)
        {
            if (audio == null)
            {
                _logger.LogWarning("Cannot generate download URL for null audio");
                throw new ArgumentNullException(nameof(audio));
            }

            return GenerateAudioDownloadUrl(audio.Id);
        }

        public string GenerateDownloadUrl(Video video)
        {
            if (video == null)
            {
                _logger.LogWarning("Cannot generate download URL for null video");
                throw new ArgumentNullException(nameof(video));
            }

            return GenerateVideoDownloadUrl(video.Id);
        }

        public string GenerateDownloadUrl(Archive archive)
        {
            if (archive == null)
            {
                _logger.LogWarning("Cannot generate download URL for null archive");
                throw new ArgumentNullException(nameof(archive));
            }

            return GenerateArchiveDownloadUrl(archive.Id);
        }

        public string GenerateDownloadUrl(OtherFile otherFile)
        {
            if (otherFile == null)
            {
                _logger.LogWarning("Cannot generate download URL for null other file");
                throw new ArgumentNullException(nameof(otherFile));
            }

            return GenerateOtherFileDownloadUrl(otherFile.Id);
        }

        #endregion
    }
}