using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class FileUrlBuilder : IFileUrlBuilder
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<FileUrlBuilder> _logger;
        private readonly string _baseUrl;
        private readonly string _apiVersion;
        private readonly bool _useHttps;

        public FileUrlBuilder(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<FileUrlBuilder> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _baseUrl = GetBaseUrl();
            _apiVersion = _configuration["ApiVersioning:DefaultVersion"] ?? "1.0";
            _useHttps = _configuration.GetValue<bool>("FileStorage:UseHttps", false);
        }

        public string GenerateDownloadUrl(int fileId, bool isPublic = false)
        {
            try
            {
                if (isPublic)
                {
                    return $"{_baseUrl}/api/v{_apiVersion}/file/{fileId}/download";
                }

                // For private files, they need to generate a token first
                return $"{_baseUrl}/api/v{_apiVersion}/file/{fileId}/download";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating download URL for file {FileId}", fileId);
                return string.Empty;
            }
        }

        public string? GeneratePreviewUrl(int fileId, FileType fileType, bool isPublic = false)
        {
            try
            {
                if (!SupportsPreview(fileType, string.Empty))
                {
                    return null;
                }

                return $"{_baseUrl}/api/v{_apiVersion}/file/{fileId}/preview";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating preview URL for file {FileId}", fileId);
                return null;
            }
        }

        public string? GenerateThumbnailUrl(int fileId, FileType fileType, bool hasThumbnail = false)
        {
            try
            {
                if (!SupportsThumbnails(fileType) || !hasThumbnail)
                {
                    return null;
                }

                return $"{_baseUrl}/api/v{_apiVersion}/file/{fileId}/thumbnail";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail URL for file {FileId}", fileId);
                return null;
            }
        }

        public FileUrlSet GenerateFileUrls(FileEntity file)
        {
            try
            {
                var urlSet = new FileUrlSet();

                // Always generate download URL
                urlSet.DownloadUrl = GenerateDownloadUrl(file.Id, file.IsPublic);

                // Generate preview URL for supported types
                if (SupportsPreview(file.FileType, file.ContentType))
                {
                    urlSet.PreviewUrl = GeneratePreviewUrl(file.Id, file.FileType, file.IsPublic);
                }

                // Generate thumbnail URL if available
                var hasThumbnail = file.ThumbnailContent != null && file.ThumbnailContent.Length > 0;
                if (SupportsThumbnails(file.FileType) && hasThumbnail)
                {
                    urlSet.ThumbnailUrl = GenerateThumbnailUrl(file.Id, file.FileType, hasThumbnail);
                }

                // Generate direct access URL for public files
                if (file.IsPublic)
                {
                    urlSet.DirectAccessUrl = GenerateDirectAccessUrl(file.Id);
                }

                // Add additional URLs based on file type
                GenerateAdditionalUrls(file, urlSet);

                return urlSet;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating URLs for file {FileId}", file.Id);
                return new FileUrlSet
                {
                    DownloadUrl = GenerateDownloadUrl(file.Id, file.IsPublic)
                };
            }
        }

        public string GenerateTokenDownloadUrl(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    throw new ArgumentException("Token cannot be null or empty", nameof(token));
                }

                return $"{_baseUrl}/api/v{_apiVersion}/file/download/{token}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating token download URL for token {Token}", token);
                return string.Empty;
            }
        }

        public string GenerateDirectAccessUrl(int fileId)
        {
            try
            {
                return $"{_baseUrl}/api/v{_apiVersion}/file/{fileId}/direct";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating direct access URL for file {FileId}", fileId);
                return string.Empty;
            }
        }

        public bool SupportsPreview(FileType fileType, string contentType)
        {
            return fileType switch
            {
                FileType.Image => true,
                FileType.Video => IsStreamableVideo(contentType),
                FileType.Audio => IsStreamableAudio(contentType),
                FileType.Document => IsPreviewableDocument(contentType),
                _ => false
            };
        }

        public bool SupportsThumbnails(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => true,
                FileType.Video => true,
                FileType.Document => true, // For PDF previews
                _ => false
            };
        }

        private string GetBaseUrl()
        {
            try
            {
                // First try to get from configuration
                var configuredBaseUrl = _configuration["FileStorage:BaseUrl"];
                if (!string.IsNullOrEmpty(configuredBaseUrl))
                {
                    return configuredBaseUrl.TrimEnd('/');
                }

                // Try to get from HTTP context
                if (_httpContextAccessor.HttpContext != null)
                {
                    var request = _httpContextAccessor.HttpContext.Request;
                    var scheme = _useHttps ? "https" : request.Scheme;
                    var host = request.Host.Value;

                    return $"{scheme}://{host}";
                }

                // Fallback to configuration or default
                var fallbackUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
                return fallbackUrl.TrimEnd('/');
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting base URL, using fallback");
                return "http://localhost:5000";
            }
        }

        private void GenerateAdditionalUrls(FileEntity file, FileUrlSet urlSet)
        {
            try
            {
                // Generate diagnostic URL for debugging
                urlSet.AdditionalUrls["diagnostic"] = $"{_baseUrl}/api/v{_apiVersion}/file/{file.Id}/diagnostic";

                // Generate integrity verification URL
                urlSet.AdditionalUrls["verify"] = $"{_baseUrl}/api/v{_apiVersion}/file/{file.Id}/verify-integrity";

                // Generate metadata URL
                urlSet.AdditionalUrls["metadata"] = $"{_baseUrl}/api/v{_apiVersion}/file/{file.Id}";

                // For images, add resize URLs if supported
                if (file.FileType == FileType.Image)
                {
                    urlSet.AdditionalUrls["resize_small"] = $"{_baseUrl}/api/v{_apiVersion}/file/{file.Id}/resize?width=200&height=200";
                    urlSet.AdditionalUrls["resize_medium"] = $"{_baseUrl}/api/v{_apiVersion}/file/{file.Id}/resize?width=500&height=500";
                    urlSet.AdditionalUrls["resize_large"] = $"{_baseUrl}/api/v{_apiVersion}/file/{file.Id}/resize?width=1200&height=1200";
                }

                // For videos, add streaming URLs if supported
                if (file.FileType == FileType.Video && IsStreamableVideo(file.ContentType))
                {
                    urlSet.AdditionalUrls["stream"] = $"{_baseUrl}/api/v{_apiVersion}/file/{file.Id}/stream";
                    urlSet.AdditionalUrls["stream_hls"] = $"{_baseUrl}/api/v{_apiVersion}/file/{file.Id}/stream/hls";
                }

                // Add download token generation URL for private files
                if (!file.IsPublic)
                {
                    urlSet.AdditionalUrls["generate_token"] = $"{_baseUrl}/api/v{_apiVersion}/file/{file.Id}/download-token";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating additional URLs for file {FileId}", file.Id);
            }
        }

        private static bool IsStreamableVideo(string contentType)
        {
            var streamableTypes = new[]
            {
                "video/mp4",
                "video/webm",
                "video/ogg",
                "video/quicktime"
            };

            return streamableTypes.Contains(contentType?.ToLowerInvariant());
        }

        private static bool IsStreamableAudio(string contentType)
        {
            var streamableTypes = new[]
            {
                "audio/mpeg",
                "audio/mp4",
                "audio/ogg",
                "audio/wav",
                "audio/webm"
            };

            return streamableTypes.Contains(contentType?.ToLowerInvariant());
        }

        private static bool IsPreviewableDocument(string contentType)
        {
            var previewableTypes = new[]
            {
                "application/pdf",
                "text/plain",
                "text/html",
                "text/csv",
                "application/json"
            };

            return previewableTypes.Contains(contentType?.ToLowerInvariant());
        }
    }
}