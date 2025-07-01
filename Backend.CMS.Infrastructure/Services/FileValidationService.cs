using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Backend.CMS.Infrastructure.Services
{
    public class FileValidationService : IFileValidationService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileValidationService> _logger;
        private readonly long _maxFileSize;
        private readonly List<string> _allowedExtensions;
        private readonly ConcurrentDictionary<string, bool> _validationCache;
        private readonly Timer _cacheCleanupTimer;
        private readonly SemaphoreSlim _validationSemaphore;
        private bool _disposed = false;

        // Optimized file signatures for validation - organized by frequency of use
        private static readonly Dictionary<string, List<byte[]>> FileSignatures = new()
        {
            // Most common image formats first
            [".jpg"] = new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".jpeg"] = new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".png"] = new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
            [".gif"] = new List<byte[]> { new byte[] { 0x47, 0x49, 0x46, 0x38 } },
            [".webp"] = new List<byte[]> { new byte[] { 0x52, 0x49, 0x46, 0x46 } },
            [".bmp"] = new List<byte[]> { new byte[] { 0x42, 0x4D } },
            [".svg"] = new List<byte[]> { new byte[] { 0x3C, 0x73, 0x76, 0x67 }, new byte[] { 0x3C, 0x3F, 0x78, 0x6D, 0x6C } },

            // Common document formats
            [".pdf"] = new List<byte[]> { new byte[] { 0x25, 0x50, 0x44, 0x46 } },
            [".docx"] = new List<byte[]> { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x07, 0x08 } },
            [".xlsx"] = new List<byte[]> { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x07, 0x08 } },
            [".pptx"] = new List<byte[]> { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x07, 0x08 } },
            [".doc"] = new List<byte[]> { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } },
            [".xls"] = new List<byte[]> { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } },
            [".ppt"] = new List<byte[]> { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } },
            [".rtf"] = new List<byte[]> { new byte[] { 0x7B, 0x5C, 0x72, 0x74, 0x66 } },

            // Video formats
            [".mp4"] = new List<byte[]> {
                new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 },
                new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 }
            },
            [".avi"] = new List<byte[]> { new byte[] { 0x52, 0x49, 0x46, 0x46 } },
            [".mov"] = new List<byte[]> { new byte[] { 0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70 } },
            [".wmv"] = new List<byte[]> { new byte[] { 0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11 } },
            [".flv"] = new List<byte[]> { new byte[] { 0x46, 0x4C, 0x56, 0x01 } },
            [".webm"] = new List<byte[]> { new byte[] { 0x1A, 0x45, 0xDF, 0xA3 } },

            // Audio formats
            [".mp3"] = new List<byte[]> { new byte[] { 0xFF, 0xFB }, new byte[] { 0x49, 0x44, 0x33 } },
            [".wav"] = new List<byte[]> { new byte[] { 0x52, 0x49, 0x46, 0x46 } },
            [".ogg"] = new List<byte[]> { new byte[] { 0x4F, 0x67, 0x67, 0x53 } },
            [".flac"] = new List<byte[]> { new byte[] { 0x66, 0x4C, 0x61, 0x43 } },

            // Archive formats
            [".zip"] = new List<byte[]> {
                new byte[] { 0x50, 0x4B, 0x03, 0x04 },
                new byte[] { 0x50, 0x4B, 0x05, 0x06 },
                new byte[] { 0x50, 0x4B, 0x07, 0x08 }
            },
            [".rar"] = new List<byte[]> {
                new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 },
                new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 }
            },
            [".7z"] = new List<byte[]> { new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C } },
            [".tar"] = new List<byte[]> {
                new byte[] { 0x75, 0x73, 0x74, 0x61, 0x72, 0x00, 0x30, 0x30 },
                new byte[] { 0x75, 0x73, 0x74, 0x61, 0x72, 0x20, 0x20, 0x00 }
            },
            [".gz"] = new List<byte[]> { new byte[] { 0x1F, 0x8B, 0x08 } },

            // Text files - no signature required
            [".txt"] = new List<byte[]>(),
            [".csv"] = new List<byte[]>()
        };

        // Pre-compiled suspicious content patterns for performance
        private static readonly string[] SuspiciousPatterns = {
            "<script", "javascript:", "vbscript:", "onload=", "onerror=",
            "<?php", "<%", "eval(", "exec(", "system(",
            "cmd.exe", "powershell", "/bin/sh", "/bin/bash"
        };

        // Executable signatures for security scanning
        private static readonly byte[][] ExecutableSignatures = {
            new byte[] { 0x4D, 0x5A }, // PE executable
            new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, // ELF executable
            new byte[] { 0xFE, 0xED, 0xFA, 0xCE }, // Mach-O executable (32-bit)
            new byte[] { 0xFE, 0xED, 0xFA, 0xCF }, // Mach-O executable (64-bit)
        };

        private static readonly HashSet<string> TextBasedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".csv", ".rtf", ".svg", ".xml", ".html", ".htm", ".css", ".js", ".json"
        };

        public FileValidationService(IConfiguration configuration, ILogger<FileValidationService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _maxFileSize = long.Parse(_configuration["FileStorage:MaxFileSize"] ?? "10485760"); // 10MB default
            _allowedExtensions = _configuration.GetSection("FileStorage:AllowedExtensions").Get<List<string>>()
                               ?? GetDefaultAllowedExtensions();

            _validationCache = new ConcurrentDictionary<string, bool>();
            _validationSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

            // Cleanup cache every 30 minutes
            _cacheCleanupTimer = new Timer(CleanupCache, null,
                TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

            _logger.LogInformation("FileValidationService initialized with {ExtensionCount} allowed extensions, max size: {MaxSize} bytes",
                _allowedExtensions.Count, _maxFileSize);
        }

        public bool IsAllowedFileType(string fileName, string contentType)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            // Use cache for frequently checked extensions
            var cacheKey = $"ext_{extension}_{contentType}";
            if (_validationCache.TryGetValue(cacheKey, out var cachedResult))
                return cachedResult;

            var result = ValidateFileTypeInternal(extension, contentType);
            _validationCache.TryAdd(cacheKey, result);

            return result;
        }

        private bool ValidateFileTypeInternal(string extension, string contentType)
        {
            if (!_allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("File type not allowed: {Extension}", extension);
                return false;
            }

            // Additional content type validation for security
            if (!IsValidContentType(extension, contentType))
            {
                _logger.LogWarning("Content type mismatch: {ContentType} for extension: {Extension}",
                    contentType, extension);
                return false;
            }

            return true;
        }

        public bool IsAllowedFileSize(long fileSize)
        {
            if (fileSize > _maxFileSize)
            {
                _logger.LogWarning("File size exceeds limit: {FileSize} bytes (max: {MaxFileSize})",
                    fileSize, _maxFileSize);
                return false;
            }

            if (fileSize <= 0)
            {
                _logger.LogWarning("Invalid file size: {FileSize}", fileSize);
                return false;
            }

            return true;
        }

        public async Task<bool> IsSafeFileAsync(Stream fileStream, string fileName)
        {
            if (fileStream == null || fileStream.Length == 0)
                return false;

            await _validationSemaphore.WaitAsync();
            try
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                var originalPosition = fileStream.Position;

                try
                {
                    // Reset stream position
                    if (fileStream.CanSeek)
                        fileStream.Position = 0;

                    // Check file signature
                    if (!await ValidateFileSignatureAsync(fileStream, extension))
                    {
                        _logger.LogWarning("File signature validation failed for: {FileName}", fileName);
                        return false;
                    }

                    // Reset position for content scanning
                    if (fileStream.CanSeek)
                        fileStream.Position = 0;

                    // Check for suspicious content (limited scan for performance)
                    if (await ContainsSuspiciousContentAsync(fileStream, extension))
                    {
                        _logger.LogWarning("File contains suspicious content: {FileName}", fileName);
                        return false;
                    }

                    return true;
                }
                finally
                {
                    // Restore original position
                    if (fileStream.CanSeek)
                        fileStream.Position = originalPosition;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file safety: {FileName}", fileName);
                return false;
            }
            finally
            {
                _validationSemaphore.Release();
            }
        }

        public bool IsImageFile(string fileName, string contentType)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" };

            return imageExtensions.Contains(extension) &&
                   (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || extension == ".svg");
        }

        public bool IsVideoFile(string fileName, string contentType)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm" };

            return videoExtensions.Contains(extension) &&
                   contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsDocumentFile(string fileName, string contentType)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var documentExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".rtf" };

            return documentExtensions.Contains(extension);
        }

        public FileType GetFileType(string fileName, string contentType)
        {
            if (IsImageFile(fileName, contentType))
                return FileType.Image;

            if (IsVideoFile(fileName, contentType))
                return FileType.Video;

            if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                return FileType.Audio;

            if (IsDocumentFile(fileName, contentType))
                return FileType.Document;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var archiveExtensions = new[] { ".zip", ".rar", ".7z", ".tar", ".gz" };

            if (archiveExtensions.Contains(extension))
                return FileType.Archive;

            return FileType.Other;
        }

        public List<string> GetAllowedExtensions()
        {
            return new List<string>(_allowedExtensions);
        }

        public long GetMaxFileSize()
        {
            return _maxFileSize;
        }

        // Private helper methods
        private async Task<bool> ValidateFileSignatureAsync(Stream fileStream, string extension)
        {
            if (!FileSignatures.TryGetValue(extension, out var signatures) || signatures.Count == 0)
                return true; // No signature check required

            const int maxHeaderSize = 32;
            var headerBytes = new byte[maxHeaderSize];

            try
            {
                var bytesRead = await fileStream.ReadAsync(headerBytes, 0, maxHeaderSize);

                if (bytesRead == 0)
                    return false; // Empty file

                // Check against all possible signatures for this extension
                foreach (var signature in signatures)
                {
                    if (bytesRead >= signature.Length && IsSignatureMatch(headerBytes, signature))
                        return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading file header for signature validation");
                return false;
            }
        }

        private static bool IsSignatureMatch(byte[] headerBytes, byte[] signature)
        {
            for (int i = 0; i < signature.Length; i++)
            {
                if (headerBytes[i] != signature[i])
                    return false;
            }
            return true;
        }

        private async Task<bool> ContainsSuspiciousContentAsync(Stream fileStream, string extension)
        {
            // Only scan text-based files and limit scan size for performance
            if (!TextBasedExtensions.Contains(extension))
                return await CheckBinaryForSuspiciousContentAsync(fileStream);

            return await CheckTextForSuspiciousContentAsync(fileStream);
        }

        private async Task<bool> CheckTextForSuspiciousContentAsync(Stream fileStream)
        {
            try
            {
                const int maxScanSize = 8192; // Only scan first 8KB for performance
                var buffer = new byte[maxScanSize];
                var bytesRead = await fileStream.ReadAsync(buffer, 0, maxScanSize);

                if (bytesRead == 0)
                    return false;

                // Convert to string for pattern matching
                var content = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead).ToLowerInvariant();

                // Check for suspicious patterns
                return SuspiciousPatterns.Any(pattern => content.Contains(pattern));
            }
            catch
            {
                // If we can't read it as text, consider it suspicious
                return true;
            }
        }

        private async Task<bool> CheckBinaryForSuspiciousContentAsync(Stream fileStream)
        {
            try
            {
                const int scanSize = 1024; // Check first 1KB only
                var buffer = new byte[scanSize];
                var bytesRead = await fileStream.ReadAsync(buffer, 0, scanSize);

                if (bytesRead == 0)
                    return false;

                // Look for executable signatures
                return ExecutableSignatures.Any(signature =>
                    bytesRead >= signature.Length && IsSignatureMatch(buffer, signature));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scanning binary content");
                return true; // Consider suspicious if we can't scan
            }
        }

        private static bool IsValidContentType(string extension, string contentType)
        {
            // Basic content type validation - simplified for performance
            var validContentTypes = new Dictionary<string, string[]>
            {
                [".jpg"] = new[] { "image/jpeg", "image/jpg" },
                [".jpeg"] = new[] { "image/jpeg", "image/jpg" },
                [".png"] = new[] { "image/png" },
                [".gif"] = new[] { "image/gif" },
                [".bmp"] = new[] { "image/bmp", "image/x-windows-bmp" },
                [".webp"] = new[] { "image/webp" },
                [".svg"] = new[] { "image/svg+xml", "text/xml", "application/xml" },
                [".pdf"] = new[] { "application/pdf" },
                [".doc"] = new[] { "application/msword" },
                [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
                [".xls"] = new[] { "application/vnd.ms-excel" },
                [".xlsx"] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                [".txt"] = new[] { "text/plain" },
                [".csv"] = new[] { "text/csv", "text/plain", "application/csv" },
                [".mp4"] = new[] { "video/mp4" },
                [".mp3"] = new[] { "audio/mpeg", "audio/mp3" },
                [".zip"] = new[] { "application/zip", "application/x-zip-compressed" }
            };

            if (!validContentTypes.TryGetValue(extension, out var allowedTypes))
                return true; // No specific validation required

            return allowedTypes.Any(type => contentType.StartsWith(type, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> GetDefaultAllowedExtensions()
        {
            return new List<string>
            {
                // Images
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg",
                
                // Documents
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".rtf",
                
                // Videos
                ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm",
                
                // Audio
                ".mp3", ".wav", ".ogg", ".flac",
                
                // Archives
                ".zip", ".rar", ".7z", ".tar", ".gz"
            };
        }

        private void CleanupCache(object? state)
        {
            try
            {
                // Clear cache if it gets too large (prevent memory issues)
                if (_validationCache.Count > 1000)
                {
                    var keysToRemove = _validationCache.Keys.Take(500).ToList();
                    foreach (var key in keysToRemove)
                    {
                        _validationCache.TryRemove(key, out _);
                    }

                    _logger.LogDebug("Cleaned up {Count} validation cache entries", keysToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during validation cache cleanup");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _cacheCleanupTimer?.Dispose();
                _validationSemaphore?.Dispose();
                _validationCache.Clear();
                _disposed = true;
            }
        }
    }
}