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

        public double? CompressionRatio { get; set; }

        [MaxLength(50)]
        public string? CompressionMethod { get; set; }

        public bool IsPasswordProtected { get; set; } = false;

        public bool IsEncrypted { get; set; } = false;

        [MaxLength(50)]
        public string? EncryptionMethod { get; set; }

        public bool IsSelfExtracting { get; set; } = false;

        public bool IsMultiVolume { get; set; } = false;

        public int? VolumeCount { get; set; }

        public bool HasComment { get; set; } = false;

        [MaxLength(1000)]
        public string? ArchiveComment { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ArchiveDate { get; set; }

        public bool IsCorrupted { get; set; } = false;

        public bool IsTestable { get; set; } = true;

        public DateTime? LastTestedAt { get; set; }

        public bool TestResult { get; set; } = false;

        [MaxLength(500)]
        public string? TestErrorMessage { get; set; }

        // Navigation property for archive contents
        public virtual ICollection<ArchiveEntry> ArchiveEntries { get; set; } = [];

        // Archive-specific validation
        public override ValidationResult ValidateFileType()
        {
            var errors = new List<string>();

            if (FileCount.HasValue && FileCount < 0)
                errors.Add("File count cannot be negative");

            if (UncompressedSize.HasValue && UncompressedSize < 0)
                errors.Add("Uncompressed size cannot be negative");

            if (CompressionRatio.HasValue && (CompressionRatio < 0 || CompressionRatio > 1))
                errors.Add("Compression ratio must be between 0 and 1");

            if (VolumeCount.HasValue && VolumeCount <= 0)
                errors.Add("Volume count must be greater than 0");

            if (IsMultiVolume && (!VolumeCount.HasValue || VolumeCount <= 1))
                errors.Add("Multi-volume archives must have more than 1 volume");

            if (ArchiveDate.HasValue && ArchiveDate > DateTime.UtcNow)
                errors.Add("Archive date cannot be in the future");

            return errors.Any() 
                ? new ValidationResult(string.Join("; ", errors))
                : ValidationResult.Success!;
        }

        // Archive-specific processing
        public override async Task<bool> ProcessFileAsync()
        {
            try
            {
                await ExtractArchiveMetadataAsync();
                await ListArchiveContentsAsync();
                await TestArchiveIntegrityAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task ExtractArchiveMetadataAsync()
        {
            // Implementation for extracting archive metadata
            await Task.CompletedTask;
        }

        private async Task ListArchiveContentsAsync()
        {
            // Implementation for listing archive contents without extracting
            await Task.CompletedTask;
        }

        private async Task TestArchiveIntegrityAsync()
        {
            // Implementation for testing archive integrity
            await Task.CompletedTask;
        }

        // Helper properties
        public string CompressionInfo
        {
            get
            {
                var info = new List<string>();
                if (!string.IsNullOrEmpty(CompressionMethod)) info.Add(CompressionMethod);
                if (CompressionRatio.HasValue) info.Add($"{CompressionRatio * 100:F1}% compression");
                return info.Any() ? string.Join(", ", info) : "Unknown compression";
            }
        }

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

        public string SecurityInfo
        {
            get
            {
                var features = new List<string>();
                if (IsPasswordProtected) features.Add("Password Protected");
                if (IsEncrypted) features.Add($"Encrypted ({EncryptionMethod})");
                if (IsSelfExtracting) features.Add("Self-Extracting");
                return features.Any() ? string.Join(", ", features) : "No Security";
            }
        }

        public string ArchiveInfo
        {
            get
            {
                var info = new List<string>();
                if (FileCount.HasValue) info.Add($"{FileCount} files");
                if (IsMultiVolume && VolumeCount.HasValue) info.Add($"{VolumeCount} volumes");
                return info.Any() ? string.Join(", ", info) : "Unknown content";
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

        public bool RequiresPassword => IsPasswordProtected || IsEncrypted;

        public string IntegrityStatus
        {
            get
            {
                if (IsCorrupted) return "Corrupted";
                if (!LastTestedAt.HasValue) return "Not Tested";
                if (TestResult) return $"Verified ({LastTestedAt:yyyy-MM-dd})";
                return $"Test Failed ({LastTestedAt:yyyy-MM-dd})";
            }
        }
    }

    // Supporting entity for archive contents
    public class ArchiveEntry : BaseEntity
    {
        [Required]
        public int ArchiveFileId { get; set; }

        [ForeignKey("ArchiveFileId")]
        public ArchiveFileEntity ArchiveFile { get; set; } = null!;

        [Required]
        [MaxLength(500)]
        public string RelativePath { get; set; } = string.Empty;

        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        public long CompressedSize { get; set; }

        public long UncompressedSize { get; set; }

        public bool IsDirectory { get; set; } = false;

        public DateTime? ModificationDate { get; set; }

        [MaxLength(50)]
        public string? CompressionMethod { get; set; }

        [MaxLength(64)]
        public string? Checksum { get; set; }

        public bool IsEncrypted { get; set; } = false;

        public double CompressionRatio => CompressedSize > 0 && UncompressedSize > 0 
            ? (double)CompressedSize / UncompressedSize 
            : 0;
    }
}