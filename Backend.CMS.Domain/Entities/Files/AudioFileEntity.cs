using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Domain.Entities.Files
{
    public class AudioFileEntity : BaseFileEntity
    {
        public override FileType FileType => FileType.Audio;

        public TimeSpan? Duration { get; set; }

        [MaxLength(50)]
        public string? AudioCodec { get; set; }

        public long? Bitrate { get; set; }

        public int? SampleRate { get; set; }

        public int? Channels { get; set; }

        [MaxLength(20)]
        public string? BitDepth { get; set; }

        [MaxLength(100)]
        public string? Artist { get; set; }

        [MaxLength(100)]
        public string? Album { get; set; }

        [MaxLength(100)]
        public string? Title { get; set; }

        [MaxLength(50)]
        public string? Genre { get; set; }

        public int? Year { get; set; }

        public int? TrackNumber { get; set; }

        public int? TotalTracks { get; set; }

        [MaxLength(100)]
        public string? Composer { get; set; }

        [MaxLength(100)]
        public string? AlbumArtist { get; set; }

        public byte[]? AlbumArt { get; set; }

        [MaxLength(20)]
        public string? AlbumArtFormat { get; set; }

        public bool IsLossless { get; set; } = false;

        public bool HasLyrics { get; set; } = false;

        [MaxLength(5000)]
        public string? Lyrics { get; set; }

        [MaxLength(100)]
        public string? Copyright { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public double? ReplayGain { get; set; }

        public double? Peak { get; set; }

        // Audio-specific validation
        public override ValidationResult ValidateFileType()
        {
            var errors = new List<string>();

            if (Duration.HasValue && Duration <= TimeSpan.Zero)
                errors.Add("Audio duration must be greater than 0");

            if (Bitrate.HasValue && Bitrate <= 0)
                errors.Add("Bitrate must be greater than 0");

            if (SampleRate.HasValue && SampleRate <= 0)
                errors.Add("Sample rate must be greater than 0");

            if (Channels.HasValue && (Channels <= 0 || Channels > 8))
                errors.Add("Channels must be between 1 and 8");

            if (Year.HasValue && (Year < 1900 || Year > DateTime.Now.Year + 1))
                errors.Add("Year must be valid");

            if (TrackNumber.HasValue && TrackNumber <= 0)
                errors.Add("Track number must be greater than 0");

            if (TotalTracks.HasValue && TotalTracks <= 0)
                errors.Add("Total tracks must be greater than 0");

            if (TrackNumber.HasValue && TotalTracks.HasValue && TrackNumber > TotalTracks)
                errors.Add("Track number cannot be greater than total tracks");

            return errors.Any() 
                ? new ValidationResult(string.Join("; ", errors))
                : ValidationResult.Success!;
        }

        // Audio-specific processing
        public override async Task<bool> ProcessFileAsync()
        {
            try
            {
                await ExtractAudioMetadataAsync();
                await ExtractAlbumArtAsync();
                await AnalyzeAudioQualityAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task ExtractAudioMetadataAsync()
        {
            // Implementation for extracting audio metadata (ID3 tags, etc.)
            await Task.CompletedTask;
        }

        private async Task ExtractAlbumArtAsync()
        {
            // Implementation for extracting embedded album art
            await Task.CompletedTask;
        }

        private async Task AnalyzeAudioQualityAsync()
        {
            // Implementation for analyzing audio quality and calculating ReplayGain
            await Task.CompletedTask;
        }

        // Helper properties
        public string FormattedDuration => Duration?.ToString(@"mm\:ss") ?? string.Empty;

        public string FormattedBitrate => Bitrate.HasValue 
            ? $"{Bitrate} kbps" 
            : string.Empty;

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

        public bool HasAlbumArt => AlbumArt?.Length > 0;

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
}