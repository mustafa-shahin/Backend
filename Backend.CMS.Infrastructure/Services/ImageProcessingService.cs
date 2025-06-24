using Backend.CMS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System.Collections.Concurrent;

namespace Backend.CMS.Infrastructure.Services
{
    public class ImageProcessingService : IImageProcessingService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImageProcessingService> _logger;
        private readonly int _thumbnailWidth;
        private readonly int _thumbnailHeight;
        private readonly int _maxImageWidth;
        private readonly int _maxImageHeight;
        private readonly int _imageQuality;
        private readonly SemaphoreSlim _processingLimiter;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _processingSemaphores;
        private readonly Timer _semaphoreCleanupTimer;
        private readonly object _semaphoreLock = new();
        private bool _disposed = false;

        // Supported image formats with priority order
        private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tga"
        };

        // Configuration for ImageSharp with performance optimizations
        private static readonly Configuration ImageSharpConfig = Configuration.Default.Clone();

        static ImageProcessingService()
        {
            // Configure ImageSharp for better performance and memory usage
            ImageSharpConfig.PreferContiguousImageBuffers = true;
            Image.SetDefaultConfiguration(ImageSharpConfig);
        }

        public ImageProcessingService(
            IConfiguration configuration,
            ILogger<ImageProcessingService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load configuration with validation
            _thumbnailWidth = GetConfigValue("FileStorage:ImageSettings:ThumbnailWidth", 300);
            _thumbnailHeight = GetConfigValue("FileStorage:ImageSettings:ThumbnailHeight", 300);
            _maxImageWidth = GetConfigValue("FileStorage:ImageSettings:MaxImageWidth", 2048);
            _maxImageHeight = GetConfigValue("FileStorage:ImageSettings:MaxImageHeight", 2048);
            _imageQuality = GetConfigValue("FileStorage:ImageSettings:ImageQuality", 85);

            // Validate configuration values
            ValidateConfiguration();

            // Initialize concurrency control
            var maxConcurrentOperations = Math.Max(1, Environment.ProcessorCount);
            _processingLimiter = new SemaphoreSlim(maxConcurrentOperations, maxConcurrentOperations);
            _processingSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

            // Cleanup unused semaphores every 10 minutes
            _semaphoreCleanupTimer = new Timer(CleanupUnusedSemaphores, null,
                TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

            _logger.LogInformation("ImageProcessingService initialized - ThumbnailSize: {Width}x{Height}, MaxSize: {MaxWidth}x{MaxHeight}, Quality: {Quality}%",
                _thumbnailWidth, _thumbnailHeight, _maxImageWidth, _maxImageHeight, _imageQuality);
        }

        public async Task<byte[]> GenerateThumbnailFromBytesAsync(byte[] imageBytes, int width = 0, int height = 0)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty");

            var targetWidth = width > 0 ? width : _thumbnailWidth;
            var targetHeight = height > 0 ? height : _thumbnailHeight;

            await _processingLimiter.WaitAsync();
            try
            {
                using var inputStream = new MemoryStream(imageBytes, false);
                return await ProcessImageAsync(inputStream, async (image, outputStream) =>
                {
                    var (newWidth, newHeight) = CalculateNewDimensions(
                        image.Width, image.Height, targetWidth, targetHeight, true);

                    image.Mutate(x => x.Resize(newWidth, newHeight));
                    await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = _imageQuality });

                    _logger.LogDebug("Thumbnail generated: {Width}x{Height} -> {NewWidth}x{NewHeight}",
                        image.Width, image.Height, newWidth, newHeight);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate thumbnail from bytes");
                throw new InvalidOperationException("Failed to generate thumbnail", ex);
            }
            finally
            {
                _processingLimiter.Release();
            }
        }

        public async Task<(int width, int height)> GetImageDimensionsFromBytesAsync(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty");

            await _processingLimiter.WaitAsync();
            try
            {
                using var inputStream = new MemoryStream(imageBytes, false);
                using var image = await LoadImageSafelyAsync(inputStream);

                return (image.Width, image.Height);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get image dimensions from bytes");
                throw new InvalidOperationException("Unable to read image dimensions", ex);
            }
            finally
            {
                _processingLimiter.Release();
            }
        }

        public async Task<byte[]> ResizeImageFromBytesAsync(byte[] imageBytes, int width, int height)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty");

            if (width <= 0 || height <= 0)
                throw new ArgumentException("Width and height must be positive values");

            await _processingLimiter.WaitAsync();
            try
            {
                using var inputStream = new MemoryStream(imageBytes, false);
                return await ProcessImageAsync(inputStream, async (image, outputStream) =>
                {
                    image.Mutate(x => x.Resize(width, height));
                    await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = _imageQuality });

                    _logger.LogDebug("Image resized: {OriginalWidth}x{OriginalHeight} -> {Width}x{Height}",
                        image.Width, image.Height, width, height);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resize image from bytes to {Width}x{Height}", width, height);
                throw new InvalidOperationException($"Failed to resize image to {width}x{height}", ex);
            }
            finally
            {
                _processingLimiter.Release();
            }
        }

        public async Task<bool> IsImageFromBytesAsync(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return false;

            // Quick format detection using magic bytes before trying to load the image
            if (!HasValidImageHeader(bytes))
                return false;

            await _processingLimiter.WaitAsync();
            try
            {
                using var inputStream = new MemoryStream(bytes, false);
                using var image = await LoadImageSafelyAsync(inputStream);

                // Additional validation
                return image.Width > 0 && image.Height > 0;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Bytes do not represent a valid image (size: {Size} bytes)", bytes.Length);
                return false;
            }
            finally
            {
                _processingLimiter.Release();
            }
        }

        public async Task<byte[]> ConvertImageFromBytesAsync(byte[] imageBytes, string targetFormat)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty");

            if (string.IsNullOrEmpty(targetFormat))
                throw new ArgumentException("Target format cannot be null or empty");

            var normalizedFormat = targetFormat.ToLowerInvariant().TrimStart('.');
            if (!IsFormatSupported(normalizedFormat))
                throw new NotSupportedException($"Image format '{normalizedFormat}' is not supported for conversion");

            await _processingLimiter.WaitAsync();
            try
            {
                using var inputStream = new MemoryStream(imageBytes, false);
                return await ProcessImageAsync(inputStream, async (image, outputStream) =>
                {
                    await SaveImageInFormatAsync(image, outputStream, normalizedFormat, _imageQuality);
                    _logger.LogDebug("Image converted to {Format}, original size: {OriginalSize} bytes, new size: {NewSize} bytes",
                        normalizedFormat, imageBytes.Length, outputStream.Length);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert image from bytes to {Format}", targetFormat);
                throw new InvalidOperationException($"Failed to convert image to {normalizedFormat}", ex);
            }
            finally
            {
                _processingLimiter.Release();
            }
        }

        public async Task<byte[]> OptimizeImageFromBytesAsync(byte[] imageBytes, int quality = 85)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty");

            if (quality < 1 || quality > 100)
                throw new ArgumentException("Quality must be between 1 and 100");

            await _processingLimiter.WaitAsync();
            try
            {
                using var inputStream = new MemoryStream(imageBytes, false);
                return await ProcessImageAsync(inputStream, async (image, outputStream) =>
                {
                    var originalWidth = image.Width;
                    var originalHeight = image.Height;

                    // Resize if image is larger than max dimensions
                    if (originalWidth > _maxImageWidth || originalHeight > _maxImageHeight)
                    {
                        var (newWidth, newHeight) = CalculateNewDimensions(
                            originalWidth, originalHeight, _maxImageWidth, _maxImageHeight, true);

                        image.Mutate(x => x.Resize(newWidth, newHeight));

                        _logger.LogDebug("Image resized during optimization: {OriginalWidth}x{OriginalHeight} -> {NewWidth}x{NewHeight}",
                            originalWidth, originalHeight, newWidth, newHeight);
                    }

                    await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = quality });

                    _logger.LogDebug("Image optimized with quality {Quality}%, size: {OriginalSize} -> {NewSize} bytes",
                        quality, imageBytes.Length, outputStream.Length);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize image from bytes");
                throw new InvalidOperationException("Failed to optimize image", ex);
            }
            finally
            {
                _processingLimiter.Release();
            }
        }

        // Private helper methods
        private async Task<Image> LoadImageSafelyAsync(Stream stream)
        {
            try
            {
                // Set a reasonable timeout for image loading
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                return await Image.LoadAsync(ImageSharpConfig, stream, cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("Image loading timed out - file may be corrupted or too large");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load image - file may be corrupted or in an unsupported format", ex);
            }
        }

        private async Task<byte[]> ProcessImageAsync(Stream inputStream, Func<Image, Stream, Task> processAction)
        {
            using var image = await LoadImageSafelyAsync(inputStream);
            using var outputStream = new MemoryStream();

            await processAction(image, outputStream);
            return outputStream.ToArray();
        }

        private static (int width, int height) CalculateNewDimensions(
            int originalWidth, int originalHeight, int maxWidth, int maxHeight, bool maintainAspectRatio = true)
        {
            if (!maintainAspectRatio)
            {
                return (maxWidth, maxHeight);
            }

            if (originalWidth <= maxWidth && originalHeight <= maxHeight)
            {
                return (originalWidth, originalHeight);
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

        private static async Task SaveImageInFormatAsync(Image image, Stream outputStream, string format, int quality)
        {
            switch (format)
            {
                case "jpg":
                case "jpeg":
                    await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = quality });
                    break;

                case "png":
                    await image.SaveAsPngAsync(outputStream, new PngEncoder
                    {
                        CompressionLevel = PngCompressionLevel.BestCompression
                    });
                    break;

                case "webp":
                    await image.SaveAsWebpAsync(outputStream, new WebpEncoder
                    {
                        Quality = quality,
                        Method = WebpEncodingMethod.BestQuality
                    });
                    break;

                case "bmp":
                    await image.SaveAsBmpAsync(outputStream);
                    break;

                case "gif":
                    await image.SaveAsGifAsync(outputStream);
                    break;

                default:
                    throw new NotSupportedException($"Image format '{format}' is not supported for conversion");
            }
        }

        private static bool HasValidImageHeader(byte[] bytes)
        {
            if (bytes.Length < 4)
                return false;

            // Check for common image format magic bytes
            var header = bytes.Take(12).ToArray();

            // JPEG
            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return true;

            // PNG
            if (header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                return true;

            // GIF
            if (header.Length >= 6 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46)
                return true;

            // BMP
            if (header.Length >= 2 && header[0] == 0x42 && header[1] == 0x4D)
                return true;

            // WebP
            if (header.Length >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                return true;

            return false;
        }

        private static bool IsFormatSupported(string format)
        {
            var supportedFormats = new[] { "jpg", "jpeg", "png", "webp", "bmp", "gif" };
            return supportedFormats.Contains(format);
        }

        private void ValidateConfiguration()
        {
            if (_thumbnailWidth <= 0 || _thumbnailHeight <= 0)
                throw new InvalidOperationException("Thumbnail dimensions must be positive values");

            if (_maxImageWidth <= 0 || _maxImageHeight <= 0)
                throw new InvalidOperationException("Maximum image dimensions must be positive values");

            if (_imageQuality < 1 || _imageQuality > 100)
                throw new InvalidOperationException("Image quality must be between 1 and 100");

            _logger.LogDebug("Image processing configuration validated successfully");
        }

        private int GetConfigValue(string key, int defaultValue)
        {
            var value = _configuration[key];
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            if (int.TryParse(value, out var result))
                return result;

            _logger.LogWarning("Invalid configuration value for {Key}: {Value}. Using default: {DefaultValue}",
                key, value, defaultValue);
            return defaultValue;
        }

        private SemaphoreSlim GetProcessingSemaphore(string key)
        {
            lock (_semaphoreLock)
            {
                return _processingSemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            }
        }

        private void CleanupUnusedSemaphores(object? state)
        {
            lock (_semaphoreLock)
            {
                try
                {
                    var keysToRemove = new List<string>();

                    foreach (var kvp in _processingSemaphores)
                    {
                        if (kvp.Value.CurrentCount == 1) // Not in use
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }

                    foreach (var key in keysToRemove.Take(50)) // Limit cleanup
                    {
                        if (_processingSemaphores.TryRemove(key, out var semaphore))
                        {
                            semaphore.Dispose();
                        }
                    }

                    if (keysToRemove.Count > 0)
                    {
                        _logger.LogDebug("Cleaned up {Count} unused processing semaphores",
                            Math.Min(keysToRemove.Count, 50));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during semaphore cleanup");
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _semaphoreCleanupTimer?.Dispose();
                _processingLimiter?.Dispose();

                // Dispose all processing semaphores
                foreach (var semaphore in _processingSemaphores.Values)
                {
                    semaphore.Dispose();
                }
                _processingSemaphores.Clear();

                _disposed = true;
                _logger.LogDebug("ImageProcessingService disposed");
            }
        }
    }
}