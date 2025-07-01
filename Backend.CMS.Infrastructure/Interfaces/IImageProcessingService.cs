namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IImageProcessingService
    {
        // Byte array methods for database storage
        Task<byte[]> GenerateThumbnailFromBytesAsync(byte[] imageBytes, int width = 300, int height = 300);
        Task<(int width, int height)> GetImageDimensionsFromBytesAsync(byte[] imageBytes);
        Task<byte[]> ResizeImageFromBytesAsync(byte[] imageBytes, int width, int height);
        Task<bool> IsImageFromBytesAsync(byte[] bytes);
        Task<byte[]> ConvertImageFromBytesAsync(byte[] imageBytes, string targetFormat);
        Task<byte[]> OptimizeImageFromBytesAsync(byte[] imageBytes, int quality = 85);
    }
}