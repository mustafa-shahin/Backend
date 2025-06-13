using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class FileValidationService : IFileValidationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileValidationService> _logger;
        private readonly long _maxFileSize;
        private readonly List<string> _allowedExtensions;

        // Common file signatures for validation
        private static readonly Dictionary<string, List<byte[]>> FileSignatures = new()
        {
            // Images
            [".jpg"] = new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".jpeg"] = new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".png"] = new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
            [".gif"] = new List<byte[]> { new byte[] { 0x47, 0x49, 0x46, 0x38 } },
            [".bmp"] = new List<byte[]> { new byte[] { 0x42, 0x4D } },
            [".webp"] = new List<byte[]> { new byte[] { 0x52, 0x49, 0x46, 0x46 } },
            [".svg"] = new List<byte[]> { new byte[] { 0x3C, 0x73, 0x76, 0x67 }, new byte[] { 0x3C, 0x3F, 0x78, 0x6D, 0x6C } },

            // Documents
            [".pdf"] = new List<byte[]> { new byte[] { 0x25, 0x50, 0x44, 0x46 } },
            [".doc"] = new List<byte[]> { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } },
            [".docx"] = new List<byte[]> { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x07, 0x08 } },
            [".xls"] = new List<byte[]> { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } },
            [".xlsx"] = new List<byte[]> { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x07, 0x08 } },
            [".ppt"] = new List<byte[]> { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } },
            [".pptx"] = new List<byte[]> { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x07, 0x08 } },
            [".txt"] = new List<byte[]>(), // Text files can have various encodings
            [".rtf"] = new List<byte[]> { new byte[] { 0x7B, 0x5C, 0x72, 0x74, 0x66 } },
            [".csv"] = new List<byte[]>(), // CSV files are text-based

            // Videos
            [".mp4"] = new List<byte[]> { new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 }, new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 } },
            [".avi"] = new List<byte[]> { new byte[] { 0x52, 0x49, 0x46, 0x46 } },
            [".mov"] = new List<byte[]> { new byte[] { 0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70 } },
            [".wmv"] = new List<byte[]> { new byte[] { 0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11 } },
            [".flv"] = new List<byte[]> { new byte[] { 0x46, 0x4C, 0x56, 0x01 } },
            [".webm"] = new List<byte[]> { new byte[] { 0x1A, 0x45, 0xDF, 0xA3 } },

            // Audio
            [".mp3"] = new List<byte[]> { new byte[] { 0xFF, 0xFB }, new byte[] { 0x49, 0x44, 0x33 } },
            [".wav"] = new List<byte[]> { new byte[] { 0x52, 0x49, 0x46, 0x46 } },
            [".ogg"] = new List<byte[]> { new byte[] { 0x4F, 0x67, 0x67, 0x53 } },
            [".flac"] = new List<byte[]> { new byte[] { 0x66, 0x4C, 0x61, 0x43 } },

            // Archives
            [".zip"] = new List<byte[]> { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x05, 0x06 }, new byte[] { 0x50, 0x4B, 0x07, 0x08 } },
            [".rar"] = new List<byte[]> { new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }, new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 } },
            [".7z"] = new List<byte[]> { new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C } },
            [".tar"] = new List<byte[]> { new byte[] { 0x75, 0x73, 0x74, 0x61, 0x72, 0x00, 0x30, 0x30 }, new byte[] { 0x75, 0x73, 0x74, 0x61, 0x72, 0x20, 0x20, 0x00 } },
            [".gz"] = new List<byte[]> { new byte[] { 0x1F, 0x8B, 0x08 } }
        };

        public FileValidationService(IConfiguration configuration, ILogger<FileValidationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _maxFileSize = long.Parse(_configuration["FileStorage:MaxFileSize"] ?? "10485760"); // 10MB default
            _allowedExtensions = _configuration.GetSection("FileStorage:AllowedExtensions").Get<List<string>>() ?? GetDefaultAllowedExtensions();
        }

        public bool IsAllowedFileType(string fileName, string contentType)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (!_allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("File type not allowed: {Extension} for file: {FileName}", extension, fileName);
                return false;
            }

            // Additional content type validation for security
            if (!IsValidContentType(extension, contentType))
            {
                _logger.LogWarning("Content type mismatch: {ContentType} for extension: {Extension}", contentType, extension);
                return false;
            }

            return true;
        }

        public bool IsAllowedFileSize(long fileSize)
        {
            if (fileSize > _maxFileSize)
            {
                _logger.LogWarning("File size exceeds limit: {FileSize} bytes (max: {MaxFileSize})", fileSize, _maxFileSize);
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
            try
            {
                if (fileStream == null || fileStream.Length == 0)
                    return false;

                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                // Check file signature
                if (!await ValidateFileSignatureAsync(fileStream, extension))
                {
                    _logger.LogWarning("File signature validation failed for: {FileName}", fileName);
                    return false;
                }

                // Check for suspicious content
                if (await ContainsSuspiciousContentAsync(fileStream, extension))
                {
                    _logger.LogWarning("File contains suspicious content: {FileName}", fileName);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file safety: {FileName}", fileName);
                return false;
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
            if (!FileSignatures.ContainsKey(extension))
                return true; // No signature check for unknown extensions

            var signatures = FileSignatures[extension];
            if (signatures.Count == 0)
                return true; // No signature required for text-based files

            var originalPosition = fileStream.Position;
            fileStream.Position = 0;

            try
            {
                var headerBytes = new byte[32]; // Read first 32 bytes for signature checking
                var bytesRead = await fileStream.ReadAsync(headerBytes, 0, headerBytes.Length);

                foreach (var signature in signatures)
                {
                    if (bytesRead >= signature.Length)
                    {
                        var isMatch = true;
                        for (int i = 0; i < signature.Length; i++)
                        {
                            if (headerBytes[i] != signature[i])
                            {
                                isMatch = false;
                                break;
                            }
                        }
                        if (isMatch)
                            return true;
                    }
                }

                return false;
            }
            finally
            {
                fileStream.Position = originalPosition;
            }
        }

        private async Task<bool> ContainsSuspiciousContentAsync(Stream fileStream, string extension)
        {
            // For text-based files, check for suspicious content
            if (IsTextBasedFile(extension))
            {
                return await CheckTextForSuspiciousContentAsync(fileStream);
            }

            // For binary files, perform basic checks
            return await CheckBinaryForSuspiciousContentAsync(fileStream);
        }

        private async Task<bool> CheckTextForSuspiciousContentAsync(Stream fileStream)
        {
            var originalPosition = fileStream.Position;
            fileStream.Position = 0;

            try
            {
                using var reader = new StreamReader(fileStream, leaveOpen: true);
                var content = await reader.ReadToEndAsync();

                // Check for suspicious patterns (basic check)
                var suspiciousPatterns = new[]
                {
                    "<script", "javascript:", "vbscript:", "onload=", "onerror=",
                    "<?php", "<%", "eval(", "exec(", "system(",
                    "cmd.exe", "powershell", "/bin/sh", "/bin/bash"
                };

                var lowerContent = content.ToLowerInvariant();
                return suspiciousPatterns.Any(pattern => lowerContent.Contains(pattern));
            }
            catch
            {
                return true; // If we cant read it as text, consider it suspicious
            }
            finally
            {
                fileStream.Position = originalPosition;
            }
        }

        private async Task<bool> CheckBinaryForSuspiciousContentAsync(Stream fileStream)
        {
            var originalPosition = fileStream.Position;
            fileStream.Position = 0;

            try
            {
                var buffer = new byte[1024]; // Check first 1KB
                var bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length);

                // Look for executable signatures
                var executableSignatures = new[]
                {
                    new byte[] { 0x4D, 0x5A }, // PE executable
                    new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, // ELF executable
                    new byte[] { 0xFE, 0xED, 0xFA, 0xCE }, // Mach-O executable (32-bit)
                    new byte[] { 0xFE, 0xED, 0xFA, 0xCF }, // Mach-O executable (64-bit)
                };

                foreach (var signature in executableSignatures)
                {
                    if (bytesRead >= signature.Length)
                    {
                        var isMatch = true;
                        for (int i = 0; i < signature.Length; i++)
                        {
                            if (buffer[i] != signature[i])
                            {
                                isMatch = false;
                                break;
                            }
                        }
                        if (isMatch)
                            return true; // Found executable signature
                    }
                }

                return false;
            }
            finally
            {
                fileStream.Position = originalPosition;
            }
        }

        private static bool IsTextBasedFile(string extension)
        {
            var textExtensions = new[] { ".txt", ".csv", ".rtf", ".svg", ".xml", ".html", ".htm", ".css", ".js", ".json" };
            return textExtensions.Contains(extension);
        }

        private static bool IsValidContentType(string extension, string contentType)
        {
            // Basic content type validation
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
                [".ppt"] = new[] { "application/vnd.ms-powerpoint" },
                [".pptx"] = new[] { "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
                [".txt"] = new[] { "text/plain" },
                [".csv"] = new[] { "text/csv", "text/plain", "application/csv" },
                [".mp4"] = new[] { "video/mp4" },
                [".avi"] = new[] { "video/avi", "video/x-msvideo" },
                [".mov"] = new[] { "video/quicktime" },
                [".mp3"] = new[] { "audio/mpeg", "audio/mp3" },
                [".wav"] = new[] { "audio/wav", "audio/wave" },
                [".zip"] = new[] { "application/zip", "application/x-zip-compressed" }
            };

            if (!validContentTypes.ContainsKey(extension))
                return true; // No specific validation for this extension

            var allowedTypes = validContentTypes[extension];
            return allowedTypes.Any(type => contentType.StartsWith(type, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> GetDefaultAllowedExtensions()
        {
            return
            [
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
            ];
        }
    }
}