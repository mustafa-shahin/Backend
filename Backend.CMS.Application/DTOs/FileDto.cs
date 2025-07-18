using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Application.DTOs
{
    public class FileDto
    {
        public int Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public FileType FileType { get; set; }
        public string FileTypeName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Alt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool IsPublic { get; set; }
        public int? FolderId { get; set; }
        public string? FolderPath { get; set; }
        public int DownloadCount { get; set; }
        public DateTime? LastAccessedAt { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public TimeSpan? Duration { get; set; }
        public string? DurationFormatted { get; set; }
        public string? Hash { get; set; }
        public bool IsProcessed { get; set; }
        public string? ProcessingStatus { get; set; }
        public Dictionary<string, object> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // URL Properties - populated by FileUrlBuilder
        public FileUrlsDto Urls { get; set; } = new();

        // Computed Properties
        public bool HasThumbnail { get; set; }
        public bool CanPreview { get; set; }
        public bool IsImage => FileType == FileType.Image;
        public bool IsVideo => FileType == FileType.Video;
        public bool IsAudio => FileType == FileType.Audio;
        public bool IsDocument => FileType == FileType.Document;
        public string ImageDimensions => Width.HasValue && Height.HasValue ? $"{Width}x{Height}" : string.Empty;
    }

    /// <summary>
    /// Contains all URL variations for a file
    /// </summary>
    public class FileUrlsDto
    {
        /// <summary>
        /// Primary download URL for the file
        /// </summary>
        public string Download { get; set; } = string.Empty;

        /// <summary>
        /// Preview URL for supported file types (images, videos, documents)
        /// </summary>
        public string? Preview { get; set; }

        /// <summary>
        /// Thumbnail URL for files that have thumbnails
        /// </summary>
        public string? Thumbnail { get; set; }

        /// <summary>
        /// Direct access URL for public files
        /// </summary>
        public string? DirectAccess { get; set; }

        /// <summary>
        /// Streaming URL for video/audio files
        /// </summary>
        public string? Stream { get; set; }

        /// <summary>
        /// API endpoint to generate download token for private files
        /// </summary>
        public string? GenerateToken { get; set; }

        /// <summary>
        /// Additional specialized URLs (resize, metadata, etc.)
        /// </summary>
        public Dictionary<string, string> Additional { get; set; } = new();
    }

    public class FileUploadDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Alt { get; set; }

        public int? FolderId { get; set; }

        public bool IsPublic { get; set; } = false;

        public Dictionary<string, object> Tags { get; set; } = new();

        public bool GenerateThumbnail { get; set; } = true;

        public bool ProcessImmediately { get; set; } = true;

        /// <summary>
        /// Entity type this file is linked to (e.g., "Category", "Product")
        /// </summary>
        [MaxLength(50)]
        public string? EntityType { get; set; }

        /// <summary>
        /// Entity ID this file is linked to
        /// </summary>
        public int? EntityId { get; set; }
    }

    public class MultipleFileUploadDto
    {
        [Required]
        public IFormFileCollection Files { get; set; } = null!;

        public int? FolderId { get; set; }

        public bool IsPublic { get; set; } = false;

        public bool GenerateThumbnails { get; set; } = true;

        public bool ProcessImmediately { get; set; } = true;

        /// <summary>
        /// Whether to process files in parallel (faster but more resource intensive)
        /// </summary>
        public bool ProcessInParallel { get; set; } = true;

        /// <summary>
        /// Entity type these files are linked to (e.g., "Category", "Product")
        /// </summary>
        [MaxLength(50)]
        public string? EntityType { get; set; }

        /// <summary>
        /// Entity ID these files are linked to
        /// </summary>
        public int? EntityId { get; set; }
    }

    public class UpdateFileDto
    {
        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Alt { get; set; }

        public bool IsPublic { get; set; }

        public Dictionary<string, object> Tags { get; set; } = new();

        public int? FolderId { get; set; }

        /// <summary>
        /// Whether to regenerate thumbnail if it's an image
        /// </summary>
        public bool RegenerateThumbnail { get; set; } = false;
    }

    public class MoveFileDto
    {
        [Required]
        public int FileId { get; set; }

        public int? NewFolderId { get; set; }

        /// <summary>
        /// Whether to update file path metadata
        /// </summary>
        public bool UpdateMetadata { get; set; } = true;
    }

    public class CopyFileDto
    {
        [Required]
        public int FileId { get; set; }

        public int? DestinationFolderId { get; set; }

        [MaxLength(255)]
        public string? NewName { get; set; }

        /// <summary>
        /// Whether to copy thumbnail as well
        /// </summary>
        public bool CopyThumbnail { get; set; } = true;

        /// <summary>
        /// Whether to copy metadata and tags
        /// </summary>
        public bool CopyMetadata { get; set; } = true;
    }

    public class FileSearchDto : PaginationRequest
    {
        public string? SearchTerm { get; set; }

        public FileType? FileType { get; set; }

        public int? FolderId { get; set; }

        public bool? IsPublic { get; set; }

        public DateTime? CreatedFrom { get; set; }

        public DateTime? CreatedTo { get; set; }

        public long? MinSize { get; set; }

        public long? MaxSize { get; set; }

        public List<string> Tags { get; set; } = new();

        public string SortBy { get; set; } = "CreatedAt";

        public string SortDirection { get; set; } = "Desc";

        /// <summary>
        /// Include file content in search 
        /// </summary>
        public bool IncludeContent { get; set; } = false;

        /// <summary>
        /// Search only in user's files
        /// </summary>
        public bool UserFilesOnly { get; set; } = false;

        /// <summary>
        /// Filter by entity type (e.g., "Category", "Product")
        /// </summary>
        public string? EntityType { get; set; }

        /// <summary>
        /// Filter by entity ID
        /// </summary>
        public int? EntityId { get; set; }
    }

    public class FilePreviewDto
    {
        public int Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public FileType FileType { get; set; }
        public FileUrlsDto Urls { get; set; } = new();
        public int? Width { get; set; }
        public int? Height { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool CanPreview { get; set; }
        public string? PreviewHtml { get; set; }
        public string? PreviewText { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// File upload result with detailed information
    /// </summary>
    public class FileUploadResultDto
    {
        public bool Success { get; set; }
        public FileDto? File { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> ProcessingInfo { get; set; } = new();
        public int SuccessCount { get; set; }
        public List<FileDto> SuccessfulFiles { get; set; } = new();
    }
    public class ThumbnailResult
    {
        public Stream? Stream { get; set; }
        public string? ContentType { get; set; }
        public string? FileName { get; set; }
        public string? Reason { get; set; } // Optional: "NotFound", "Deleted", "NoThumbnail"

        public bool Found => Stream != null;
    }

    #region Type-Specific DTOs

    public class ImageFileDto : FileDto
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? ColorProfile { get; set; }
        public int? DPI { get; set; }
        public bool HasTransparency { get; set; }
        public string? CameraModel { get; set; }
        public string? CameraMake { get; set; }
        public DateTime? DateTaken { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Orientation { get; set; }
        public double? ExposureTime { get; set; }
        public double? FNumber { get; set; }
        public int? ISO { get; set; }
        public double? FocalLength { get; set; }
        public bool IsAnimated { get; set; }
        public int? FrameCount { get; set; }

        // Computed properties
        public string AspectRatio => Width.HasValue && Height.HasValue && Height > 0 
            ? $"{Width}:{Height}" 
            : string.Empty;
        public bool HasGeoLocation => Latitude.HasValue && Longitude.HasValue;
        public long PixelCount => (Width ?? 0) * (Height ?? 0);
        public string CameraInfo => !string.IsNullOrEmpty(CameraMake) && !string.IsNullOrEmpty(CameraModel)
            ? $"{CameraMake} {CameraModel}"
            : CameraMake ?? CameraModel ?? string.Empty;
    }

    public class VideoFileDto : FileDto
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
        public double? FrameRate { get; set; }
        public long? Bitrate { get; set; }
        public string? AspectRatio { get; set; }
        public TimeSpan? ThumbnailTimestamp { get; set; }
        public bool HasAudio { get; set; }
        public bool HasVideo { get; set; }
        public int? AudioChannels { get; set; }
        public int? AudioSampleRate { get; set; }
        public string? Container { get; set; }
        public bool IsHDR { get; set; }
        public string? ColorSpace { get; set; }
        public double? RotationAngle { get; set; }
        public bool IsVR360 { get; set; }
        public bool HasSubtitles { get; set; }
        public int? ChapterCount { get; set; }

        // Computed properties
        public string Resolution => Width.HasValue && Height.HasValue ? $"{Width}x{Height}" : string.Empty;
        public string FormattedBitrate => Bitrate.HasValue ? $"{Bitrate / 1000} kbps" : string.Empty;
        public bool IsHighDefinition => Width >= 1280 && Height >= 720;
        public bool Is4K => Width >= 3840 && Height >= 2160;
        public string QualityCategory
        {
            get
            {
                if (Is4K) return "4K";
                if (IsHighDefinition) return "HD";
                if (Width >= 640 && Height >= 480) return "SD";
                return "Low Quality";
            }
        }
        public string VideoInfo => !string.IsNullOrEmpty(VideoCodec) && !string.IsNullOrEmpty(AudioCodec)
            ? $"{VideoCodec}/{AudioCodec}"
            : VideoCodec ?? AudioCodec ?? string.Empty;
    }

    public class AudioFileDto : FileDto
    {
        public string? AudioCodec { get; set; }
        public long? Bitrate { get; set; }
        public int? SampleRate { get; set; }
        public int? Channels { get; set; }
        public string? BitDepth { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? Title { get; set; }
        public string? Genre { get; set; }
        public int? Year { get; set; }
        public int? TrackNumber { get; set; }
        public int? TotalTracks { get; set; }
        public string? Composer { get; set; }
        public string? AlbumArtist { get; set; }
        public string? AlbumArtFormat { get; set; }
        public bool IsLossless { get; set; }
        public bool HasLyrics { get; set; }
        public string? Lyrics { get; set; }
        public string? Copyright { get; set; }
        public string? Comment { get; set; }
        public double? ReplayGain { get; set; }
        public double? Peak { get; set; }

        // Computed properties
        public string FormattedBitrate => Bitrate.HasValue ? $"{Bitrate} kbps" : string.Empty;
        public string ChannelConfiguration => Channels switch
        {
            1 => "Mono",
            2 => "Stereo",
            6 => "5.1 Surround",
            8 => "7.1 Surround",
            _ => $"{Channels} Channel"
        };
        public string QualityRating
        {
            get
            {
                if (IsLossless) return "Lossless";
                if (Bitrate >= 320) return "High Quality";
                if (Bitrate >= 192) return "Good Quality";
                if (Bitrate >= 128) return "Standard Quality";
                return "Low Quality";
            }
        }
        public string TrackInfo => TrackNumber.HasValue && TotalTracks.HasValue 
            ? $"{TrackNumber}/{TotalTracks}" 
            : TrackNumber?.ToString() ?? string.Empty;
        public bool HasAlbumArt => !string.IsNullOrEmpty(AlbumArtFormat);
        public string FullTitle
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(Artist)) parts.Add(Artist);
                if (!string.IsNullOrEmpty(Title)) parts.Add(Title);
                return parts.Any() ? string.Join(" - ", parts) : OriginalFileName;
            }
        }
    }

    public class DocumentFileDto : FileDto
    {
        public int? PageCount { get; set; }
        public string? Author { get; set; }
        public string? DocumentTitle { get; set; }
        public string? Subject { get; set; }
        public string? Keywords { get; set; }
        public string? Creator { get; set; }
        public string? Producer { get; set; }
        public DateTime? CreationDate { get; set; }
        public DateTime? ModificationDate { get; set; }
        public string? DocumentVersion { get; set; }
        public bool IsPasswordProtected { get; set; }
        public bool AllowPrinting { get; set; }
        public bool AllowCopying { get; set; }
        public bool AllowModification { get; set; }
        public bool IsDigitallySigned { get; set; }
        public string? SignatureAuthor { get; set; }
        public DateTime? SignatureDate { get; set; }
        public bool HasComments { get; set; }
        public bool HasAnnotations { get; set; }
        public bool HasBookmarks { get; set; }
        public bool HasForms { get; set; }
        public bool HasEmbeddedFiles { get; set; }
        public string? Language { get; set; }
        public int? ThumbnailPageNumber { get; set; }
        public long? WordCount { get; set; }
        public long? CharacterCount { get; set; }
        public long? ParagraphCount { get; set; }
        public long? LineCount { get; set; }
        public string? DocumentFormat { get; set; }
        public bool IsOptimizedForWeb { get; set; }

        // Computed properties
        public string DocumentInfo
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(DocumentTitle)) parts.Add(DocumentTitle);
                if (!string.IsNullOrEmpty(Author)) parts.Add($"by {Author}");
                if (PageCount.HasValue) parts.Add($"{PageCount} pages");
                return parts.Any() ? string.Join(", ", parts) : OriginalFileName;
            }
        }
        public string SecurityInfo
        {
            get
            {
                var features = new List<string>();
                if (IsPasswordProtected) features.Add("Password Protected");
                if (IsDigitallySigned) features.Add("Digitally Signed");
                if (!AllowPrinting) features.Add("Printing Restricted");
                if (!AllowCopying) features.Add("Copying Restricted");
                if (!AllowModification) features.Add("Modification Restricted");
                return features.Any() ? string.Join(", ", features) : "No Restrictions";
            }
        }
        public string ContentInfo
        {
            get
            {
                var info = new List<string>();
                if (WordCount.HasValue) info.Add($"{WordCount:N0} words");
                if (CharacterCount.HasValue) info.Add($"{CharacterCount:N0} characters");
                if (ParagraphCount.HasValue) info.Add($"{ParagraphCount:N0} paragraphs");
                return info.Any() ? string.Join(", ", info) : string.Empty;
            }
        }
        public string FeaturesInfo
        {
            get
            {
                var features = new List<string>();
                if (HasComments) features.Add("Comments");
                if (HasAnnotations) features.Add("Annotations");
                if (HasBookmarks) features.Add("Bookmarks");
                if (HasForms) features.Add("Forms");
                if (HasEmbeddedFiles) features.Add("Embedded Files");
                return features.Any() ? string.Join(", ", features) : "None";
            }
        }
    }

    public class ArchiveFileDto : FileDto
    {
        public int? FileCount { get; set; }
        public long? UncompressedSize { get; set; }
        public double? CompressionRatio { get; set; }
        public string? CompressionMethod { get; set; }
        public bool IsPasswordProtected { get; set; }
        public bool IsEncrypted { get; set; }
        public string? EncryptionMethod { get; set; }
        public bool IsSelfExtracting { get; set; }
        public bool IsMultiVolume { get; set; }
        public int? VolumeCount { get; set; }
        public bool HasComment { get; set; }
        public string? ArchiveComment { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ArchiveDate { get; set; }
        public bool IsCorrupted { get; set; }
        public bool IsTestable { get; set; }
        public DateTime? LastTestedAt { get; set; }
        public bool TestResult { get; set; }
        public string? TestErrorMessage { get; set; }
        public List<ArchiveEntryDto> Entries { get; set; } = new();

        // Computed properties
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
                if (!UncompressedSize.HasValue) return FileSizeFormatted;
                
                var compressed = FileSizeFormatted;
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

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }

    public class ArchiveEntryDto
    {
        public int Id { get; set; }
        public string RelativePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long CompressedSize { get; set; }
        public long UncompressedSize { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime? ModificationDate { get; set; }
        public string? CompressionMethod { get; set; }
        public string? Checksum { get; set; }
        public bool IsEncrypted { get; set; }
        public double CompressionRatio => CompressedSize > 0 && UncompressedSize > 0 
            ? (double)CompressedSize / UncompressedSize 
            : 0;
    }

    #endregion

    #region Type-Specific Search DTOs

    public class ImageSearchDto : FileSearchDto
    {
        public int? MinWidth { get; set; }
        public int? MaxWidth { get; set; }
        public int? MinHeight { get; set; }
        public int? MaxHeight { get; set; }
        public bool? HasGeoLocation { get; set; }
        public string? CameraMake { get; set; }
        public string? CameraModel { get; set; }
        public bool? IsAnimated { get; set; }
        public DateTime? DateTakenFrom { get; set; }
        public DateTime? DateTakenTo { get; set; }
    }

    public class VideoSearchDto : FileSearchDto
    {
        public int? MinWidth { get; set; }
        public int? MaxWidth { get; set; }
        public int? MinHeight { get; set; }
        public int? MaxHeight { get; set; }
        public TimeSpan? MinDuration { get; set; }
        public TimeSpan? MaxDuration { get; set; }
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
        public double? MinFrameRate { get; set; }
        public bool? HasAudio { get; set; }
        public bool? IsHDR { get; set; }
    }

    public class AudioSearchDto : FileSearchDto
    {
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? Genre { get; set; }
        public int? Year { get; set; }
        public bool? IsLossless { get; set; }
        public long? MinBitrate { get; set; }
        public long? MaxBitrate { get; set; }
        public TimeSpan? MinDuration { get; set; }
        public TimeSpan? MaxDuration { get; set; }
        public bool? HasLyrics { get; set; }
    }

    public class DocumentSearchDto : FileSearchDto
    {
        public string? Author { get; set; }
        public string? DocumentTitle { get; set; }
        public string? Subject { get; set; }
        public int? MinPageCount { get; set; }
        public int? MaxPageCount { get; set; }
        public bool? IsPasswordProtected { get; set; }
        public bool? IsDigitallySigned { get; set; }
        public string? Language { get; set; }
        public string? DocumentFormat { get; set; }
        public DateTime? CreationDateFrom { get; set; }
        public DateTime? CreationDateTo { get; set; }
    }

    public class ArchiveSearchDto : FileSearchDto
    {
        public string? CompressionMethod { get; set; }
        public bool? IsPasswordProtected { get; set; }
        public bool? IsEncrypted { get; set; }
        public bool? IsCorrupted { get; set; }
        public int? MinFileCount { get; set; }
        public int? MaxFileCount { get; set; }
        public double? MinCompressionRatio { get; set; }
        public double? MaxCompressionRatio { get; set; }
        public bool? IsSelfExtracting { get; set; }
    }

    #endregion
}