using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Domain.Entities.Files
{
    public class VideoFileEntity : BaseFileEntity
    {
        public override FileType FileType => FileType.Video;

        public int? Width { get; set; }

        public int? Height { get; set; }

        public TimeSpan? Duration { get; set; }

        [MaxLength(50)]
        public string? VideoCodec { get; set; }

        [MaxLength(50)]
        public string? AudioCodec { get; set; }

        public double? FrameRate { get; set; }

        public long? Bitrate { get; set; }

        [MaxLength(20)]
        public string? AspectRatio { get; set; }

        public byte[]? ThumbnailContent { get; set; }

        public TimeSpan? ThumbnailTimestamp { get; set; }

        public bool HasAudio { get; set; } = true;

        public bool HasVideo { get; set; } = true;

        public int? AudioChannels { get; set; }

        public int? AudioSampleRate { get; set; }

        [MaxLength(100)]
        public string? Container { get; set; }

        public bool IsHDR { get; set; } = false;

        [MaxLength(50)]
        public string? ColorSpace { get; set; }

        public double? RotationAngle { get; set; }

        public bool IsVR360 { get; set; } = false;

        public bool HasSubtitles { get; set; } = false;

        public int? ChapterCount { get; set; }

        // Video-specific validation
        public override ValidationResult ValidateFileType()
        {
            var errors = new List<string>();

            if (Width.HasValue && Width <= 0)
                errors.Add("Video width must be greater than 0");

            if (Height.HasValue && Height <= 0)
                errors.Add("Video height must be greater than 0");

            if (Duration.HasValue && Duration <= TimeSpan.Zero)
                errors.Add("Video duration must be greater than 0");

            if (FrameRate.HasValue && FrameRate <= 0)
                errors.Add("Frame rate must be greater than 0");

            if (Bitrate.HasValue && Bitrate <= 0)
                errors.Add("Bitrate must be greater than 0");

            if (AudioChannels.HasValue && AudioChannels <= 0)
                errors.Add("Audio channels must be greater than 0");

            if (AudioSampleRate.HasValue && AudioSampleRate <= 0)
                errors.Add("Audio sample rate must be greater than 0");

            return errors.Any() 
                ? new ValidationResult(string.Join("; ", errors))
                : ValidationResult.Success!;
        }

        // Video-specific processing
        public override async Task<bool> ProcessFileAsync()
        {
            try
            {
                await ExtractVideoMetadataAsync();
                await GenerateVideoThumbnailAsync();
                await ValidateVideoIntegrityAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task ExtractVideoMetadataAsync()
        {
            // Implementation for extracting video metadata using FFprobe or similar
            await Task.CompletedTask;
        }

        private async Task GenerateVideoThumbnailAsync()
        {
            // Implementation for generating video thumbnail at specific timestamp
            await Task.CompletedTask;
        }

        private async Task ValidateVideoIntegrityAsync()
        {
            // Implementation for validating video file integrity
            await Task.CompletedTask;
        }

        // Helper properties
        public string Resolution => Width.HasValue && Height.HasValue 
            ? $"{Width}x{Height}" 
            : string.Empty;

        public string FormattedDuration => Duration?.ToString(@"hh\:mm\:ss") ?? string.Empty;

        public string FormattedBitrate => Bitrate.HasValue 
            ? $"{Bitrate / 1000} kbps" 
            : string.Empty;

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
    }
}