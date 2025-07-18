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

        [MaxLength(50)]
        public string? ColorProfile { get; set; }

        public int? DPI { get; set; }

        public bool HasTransparency { get; set; } = false;

        [MaxLength(100)]
        public string? CameraModel { get; set; }

        [MaxLength(100)]
        public string? CameraMake { get; set; }

        public DateTime? DateTaken { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [MaxLength(50)]
        public string? Orientation { get; set; }

        public double? ExposureTime { get; set; }

        public double? FNumber { get; set; }

        public int? ISO { get; set; }

        public double? FocalLength { get; set; }

        public bool IsAnimated { get; set; } = false;

        public int? FrameCount { get; set; }

        // Image-specific validation
        public override ValidationResult ValidateFileType()
        {
            var errors = new List<string>();

            if (Width.HasValue && Width <= 0)
                errors.Add("Image width must be greater than 0");

            if (Height.HasValue && Height <= 0)
                errors.Add("Image height must be greater than 0");

            if (DPI.HasValue && DPI <= 0)
                errors.Add("DPI must be greater than 0");

            if (Latitude.HasValue && (Latitude < -90 || Latitude > 90))
                errors.Add("Latitude must be between -90 and 90");

            if (Longitude.HasValue && (Longitude < -180 || Longitude > 180))
                errors.Add("Longitude must be between -180 and 180");

            return errors.Any() 
                ? new ValidationResult(string.Join("; ", errors))
                : ValidationResult.Success!;
        }

        // Image-specific processing
        public override async Task<bool> ProcessFileAsync()
        {
            try
            {
                // Extract image metadata, generate thumbnail, etc.
                await ExtractImageMetadataAsync();
                await GenerateThumbnailAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task ExtractImageMetadataAsync()
        {
            // Implementation for extracting EXIF data, dimensions, etc.
            await Task.CompletedTask;
        }

        private async Task GenerateThumbnailAsync()
        {
            // Implementation for thumbnail generation
            await Task.CompletedTask;
        }

        // Helper properties
        public string AspectRatio => Width.HasValue && Height.HasValue && Height > 0 
            ? $"{Width}:{Height}" 
            : string.Empty;

        public string Dimensions => Width.HasValue && Height.HasValue 
            ? $"{Width}x{Height}" 
            : string.Empty;

        public bool HasGeoLocation => Latitude.HasValue && Longitude.HasValue;

        public long PixelCount => (Width ?? 0) * (Height ?? 0);
    }
}