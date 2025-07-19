using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Domain.Entities.Files
{
    public class DocumentFileEntity : BaseFileEntity
    {
        public override FileType FileType => FileType.Document;

        public int? PageCount { get; set; }

        public byte[]? ThumbnailContent { get; set; }

        // Document-specific validation
        public override ValidationResult ValidateFileType()
        {
            var errors = new List<string>();

            if (PageCount.HasValue && PageCount <= 0)
                errors.Add("Page count must be greater than 0");

            return errors.Any() 
                ? new ValidationResult(string.Join("; ", errors))
                : ValidationResult.Success!;
        }

        // Document-specific processing
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
        public bool HasThumbnail => ThumbnailContent?.Length > 0;
    }
}