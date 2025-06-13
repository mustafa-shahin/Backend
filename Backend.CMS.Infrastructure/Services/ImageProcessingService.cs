using Backend.CMS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Backend.CMS.Infrastructure.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImageProcessingService> _logger;
        private readonly int _thumbnailWidth;
        private readonly int _thumbnailHeight;
        private readonly int _maxImageWidth;
        private readonly int _maxImageHeight;
        private readonly int _imageQuality;

        // Supported image formats
        private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tga"
        };

        public ImageProcessingService(
            IConfiguration configuration,
            ILogger<ImageProcessingService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _thumbnailWidth = int.Parse(configuration["FileStorage:ImageSettings:ThumbnailWidth"] ?? "300");
            _thumbnailHeight = int.Parse(configuration["FileStorage:ImageSettings:ThumbnailHeight"] ?? "300");
            _maxImageWidth = int.Parse(configuration["FileStorage:ImageSettings:MaxImageWidth"] ?? "2048");
            _maxImageHeight = int.Parse(configuration["FileStorage:ImageSettings:MaxImageHeight"] ?? "2048");
            _imageQuality = int.Parse(configuration["FileStorage:ImageSettings:ImageQuality"] ?? "85");
        }

        // Byte array methods for database storage
        public async Task<byte[]> GenerateThumbnailFromBytesAsync(byte[] imageBytes, int width = 300, int height = 300)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty");

            if (!await IsImageFromBytesAsync(imageBytes))
                throw new ArgumentException("Data is not a valid image");

            try
            {
                var thumbnailWidth = width > 0 ? width : _thumbnailWidth;
                var thumbnailHeight = height > 0 ? height : _thumbnailHeight;

                using var inputStream = new MemoryStream(imageBytes);
                using var image = await Image.LoadAsync(inputStream);

                var (newWidth, newHeight) = CalculateNewDimensions(
                    image.Width, image.Height, thumbnailWidth, thumbnailHeight, true);

                image.Mutate(x => x.Resize(newWidth, newHeight));

                using var outputStream = new MemoryStream();
                await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = _imageQuality });

                _logger.LogInformation("Thumbnail generated from bytes: {Width}x{Height}", newWidth, newHeight);

                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate thumbnail from bytes");
                throw;
            }
        }

        public async Task<(int width, int height)> GetImageDimensionsFromBytesAsync(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty");

            try
            {
                using var inputStream = new MemoryStream(imageBytes);
                using var image = await Image.LoadAsync(inputStream);
                return (image.Width, image.Height);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get image dimensions from bytes");
                throw new InvalidOperationException("Unable to read image dimensions", ex);
            }
        }

        public async Task<byte[]> ResizeImageFromBytesAsync(byte[] imageBytes, int width, int height)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty");

            if (width <= 0 || height <= 0)
                throw new ArgumentException("Width and height must be positive values");

            try
            {
                using var inputStream = new MemoryStream(imageBytes);
                using var image = await Image.LoadAsync(inputStream);

                image.Mutate(x => x.Resize(width, height));

                using var outputStream = new MemoryStream();
                await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = _imageQuality });

                _logger.LogInformation("Image resized from bytes to {Width}x{Height}", width, height);

                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resize image from bytes to {Width}x{Height}", width, height);
                throw;
            }
        }

        public async Task<bool> IsImageFromBytesAsync(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return false;

            try
            {
                using var inputStream = new MemoryStream(bytes);
                using var image = await Image.LoadAsync(inputStream);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<byte[]> ConvertImageFromBytesAsync(byte[] imageBytes, string targetFormat)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty");

            if (string.IsNullOrEmpty(targetFormat))
                throw new ArgumentException("Target format cannot be null or empty");

            if (!await IsImageFromBytesAsync(imageBytes))
                throw new ArgumentException("Data is not a valid image");

            try
            {
                var normalizedFormat = targetFormat.ToLowerInvariant().TrimStart('.');

                using var inputStream = new MemoryStream(imageBytes);
                using var image = await Image.LoadAsync(inputStream);

                using var outputStream = new MemoryStream();
                await SaveImageInFormat(image, outputStream, normalizedFormat, _imageQuality);

                _logger.LogInformation("Image converted from bytes to {Format}", normalizedFormat);

                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert image from bytes to {Format}", targetFormat);
                throw;
            }
        }

        public async Task<byte[]> OptimizeImageFromBytesAsync(byte[] imageBytes, int quality = 85)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty");

            if (quality < 1 || quality > 100)
                throw new ArgumentException("Quality must be between 1 and 100");

            if (!await IsImageFromBytesAsync(imageBytes))
                throw new ArgumentException("Data is not a valid image");

            try
            {
                using var inputStream = new MemoryStream(imageBytes);
                using var image = await Image.LoadAsync(inputStream);

                // Resize if image is larger than max dimensions
                if (image.Width > _maxImageWidth || image.Height > _maxImageHeight)
                {
                    var (newWidth, newHeight) = CalculateNewDimensions(
                        image.Width, image.Height, _maxImageWidth, _maxImageHeight, true);

                    image.Mutate(x => x.Resize(newWidth, newHeight));
                }

                using var outputStream = new MemoryStream();
                await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = quality });

                _logger.LogInformation("Image optimized from bytes with quality {Quality}", quality);

                return outputStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize image from bytes");
                throw;
            }
        }
 

        // Private helper methods
        private static (int width, int height) CalculateNewDimensions(
            int originalWidth, int originalHeight, int maxWidth, int maxHeight, bool maintainAspectRatio = true)
        {
            if (!maintainAspectRatio)
            {
                return (maxWidth, maxHeight);
            }

            var aspectRatio = (double)originalWidth / originalHeight;

            int newWidth, newHeight;

            if (aspectRatio > 1) // Landscape
            {
                newWidth = Math.Min(maxWidth, originalWidth);
                newHeight = (int)(newWidth / aspectRatio);

                if (newHeight > maxHeight)
                {
                    newHeight = maxHeight;
                    newWidth = (int)(newHeight * aspectRatio);
                }
            }
            else // Portrait or square
            {
                newHeight = Math.Min(maxHeight, originalHeight);
                newWidth = (int)(newHeight * aspectRatio);

                if (newWidth > maxWidth)
                {
                    newWidth = maxWidth;
                    newHeight = (int)(newWidth / aspectRatio);
                }
            }

            return (Math.Max(1, newWidth), Math.Max(1, newHeight));
        }

        private static async Task SaveImageInFormat(Image image, Stream outputStream, string format, int quality)
        {
            switch (format)
            {
                case "jpg":
                case "jpeg":
                    await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = quality });
                    break;

                case "png":
                    await image.SaveAsPngAsync(outputStream, new PngEncoder());
                    break;

                case "webp":
                    await image.SaveAsWebpAsync(outputStream, new WebpEncoder { Quality = quality });
                    break;

                case "bmp":
                    await image.SaveAsBmpAsync(outputStream);
                    break;

                default:
                    throw new NotSupportedException($"Image format '{format}' is not supported for conversion");
            }
        }
    }
}