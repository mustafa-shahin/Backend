// Frontend/Extensions/IFormFileExtensions.cs
using Microsoft.AspNetCore.Components.Forms;

namespace Frontend.Extensions
{
    public static class IFormFileExtensions
    {
        /// <summary>
        /// Check if the file is a valid image type
        /// </summary>
        /// <param name="file">The file to check</param>
        /// <returns>True if the file is an image</returns>
        public static bool IsImage(this IBrowserFile file)
        {
            if (file == null) return false;

            var allowedTypes = new[]
            {
                "image/jpeg",
                "image/jpg",
                "image/png",
                "image/gif",
                "image/webp",
                "image/svg+xml",
                "image/bmp",
                "image/tiff"
            };

            return allowedTypes.Contains(file.ContentType.ToLower());
        }

        /// <summary>
        /// Check if the file size is within the specified limit
        /// </summary>
        /// <param name="file">The file to check</param>
        /// <param name="maxSizeInBytes">Maximum size in bytes</param>
        /// <returns>True if the file is within the size limit</returns>
        public static bool IsWithinSizeLimit(this IBrowserFile file, long maxSizeInBytes)
        {
            return file?.Size <= maxSizeInBytes;
        }

        /// <summary>
        /// Get a human-readable file size
        /// </summary>
        /// <param name="file">The file</param>
        /// <returns>Formatted file size string</returns>
        public static string GetFormattedSize(this IBrowserFile file)
        {
            if (file == null) return "0 B";

            return FormatFileSize(file.Size);
        }

        /// <summary>
        /// Get the file extension from the file name
        /// </summary>
        /// <param name="file">The file</param>
        /// <returns>File extension including the dot</returns>
        public static string GetExtension(this IBrowserFile file)
        {
            if (file?.Name == null) return string.Empty;

            return Path.GetExtension(file.Name).ToLower();
        }

        /// <summary>
        /// Check if the file has an allowed extension
        /// </summary>
        /// <param name="file">The file to check</param>
        /// <param name="allowedExtensions">List of allowed extensions (including dots)</param>
        /// <returns>True if the file has an allowed extension</returns>
        public static bool HasAllowedExtension(this IBrowserFile file, IEnumerable<string> allowedExtensions)
        {
            if (file?.Name == null || allowedExtensions?.Any() != true) return false;

            var fileExtension = file.GetExtension();
            return allowedExtensions.Any(ext => string.Equals(ext, fileExtension, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Validate a file against multiple criteria
        /// </summary>
        /// <param name="file">The file to validate</param>
        /// <param name="maxSizeInBytes">Maximum file size in bytes</param>
        /// <param name="allowedExtensions">Allowed file extensions</param>
        /// <param name="requireImage">Whether the file must be an image</param>
        /// <returns>Validation result with error message if invalid</returns>
        public static FileValidationResult Validate(this IBrowserFile file,
            long maxSizeInBytes = long.MaxValue,
            IEnumerable<string>? allowedExtensions = null,
            bool requireImage = false)
        {
            if (file == null)
            {
                return new FileValidationResult { IsValid = false, ErrorMessage = "File is null" };
            }

            // Check file size
            if (!file.IsWithinSizeLimit(maxSizeInBytes))
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File size ({file.GetFormattedSize()}) exceeds maximum allowed size ({FormatFileSize(maxSizeInBytes)})"
                };
            }

            // Check if image is required
            if (requireImage && !file.IsImage())
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "File must be an image"
                };
            }

            // Check allowed extensions
            if (allowedExtensions?.Any() == true && !file.HasAllowedExtension(allowedExtensions))
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File extension '{file.GetExtension()}' is not allowed. Allowed extensions: {string.Join(", ", allowedExtensions)}"
                };
            }

            return new FileValidationResult { IsValid = true };
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class FileValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }
}