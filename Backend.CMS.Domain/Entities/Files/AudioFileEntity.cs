using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Domain.Entities.Files
{
    public class AudioFileEntity : BaseFileEntity
    {
        public override FileType FileType => FileType.Audio;

        public TimeSpan? Duration { get; set; }

        // Audio-specific validation
        public override ValidationResult ValidateFileType()
        {
            var errors = new List<string>();

            if (Duration.HasValue && Duration <= TimeSpan.Zero)
                errors.Add("Audio duration must be greater than 0");

            return errors.Any() 
                ? new ValidationResult(string.Join("; ", errors))
                : ValidationResult.Success!;
        }

        // Audio-specific processing
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
        public string FormattedDuration => Duration?.ToString(@"mm\:ss") ?? string.Empty;
    }
}