using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Domain.Entities.Files
{
    public class ImageFileEntity : BaseFileEntity
    {
        public override FileType FileType => FileType.Image;

        public int? Width { get; set; }

        public int? Height { get; set; }

        public byte[]? ThumbnailContent { get; set; }

        // Image-specific validation
        public override ValidationResult ValidateFileType()
        {
            var errors = new List<string>();

            if (Width.HasValue && Width <= 0)
                errors.Add("Image width must be greater than 0");

            if (Height.HasValue && Height <= 0)
                errors.Add("Image height must be greater than 0");


            return errors.Any() 
                ? new ValidationResult(string.Join("; ", errors))
                : ValidationResult.Success!;
        }

        // Image-specific processing
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
        public string Dimensions => Width.HasValue && Height.HasValue 
            ? $"{Width}x{Height}" 
            : string.Empty;
    }
}