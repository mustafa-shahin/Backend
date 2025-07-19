using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.CMS.Domain.Entities.Files
{
    public class ArchiveFileEntity : BaseFileEntity
    {
        public override FileType FileType => FileType.Archive;

        public int? FileCount { get; set; }

        public long? UncompressedSize { get; set; }

        // Archive-specific validation
        public override ValidationResult ValidateFileType()
        {
            var errors = new List<string>();

            if (FileCount.HasValue && FileCount < 0)
                errors.Add("File count cannot be negative");

            if (UncompressedSize.HasValue && UncompressedSize < 0)
                errors.Add("Uncompressed size cannot be negative");

            return errors.Any() 
                ? new ValidationResult(string.Join("; ", errors))
                : ValidationResult.Success!;
        }

        // Archive-specific processing
        public override async Task<bool> ProcessFileAsync()
        {
            try
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Helper properties
        public string SizeInfo
        {
            get
            {
                if (!UncompressedSize.HasValue) return FormattedFileSize;
                
                var compressed = FormattedFileSize;
                var uncompressed = FormatBytes(UncompressedSize.Value);
                return $"{compressed} (uncompressed: {uncompressed})";
            }
        }

        public string FormattedFileSize
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
                if (FileSize < 1024 * 1024 * 1024) return $"{FileSize / (1024.0 * 1024):F1} MB";
                return $"{FileSize / (1024.0 * 1024 * 1024):F1} GB";
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}