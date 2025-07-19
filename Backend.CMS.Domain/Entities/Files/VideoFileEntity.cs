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

        public byte[]? ThumbnailContent { get; set; }

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

            return errors.Any() 
                ? new ValidationResult(string.Join("; ", errors))
                : ValidationResult.Success!;
        }

        // Video-specific processing
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
        public string Resolution => Width.HasValue && Height.HasValue 
            ? $"{Width}x{Height}" 
            : string.Empty;

        public string FormattedDuration => Duration?.ToString(@"hh\:mm\:ss") ?? string.Empty;
    }
}