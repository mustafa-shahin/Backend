﻿namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IFileCachingService
    {
        Task<byte[]?> GetFileContentAsync(int fileId);
        Task SetFileContentAsync(int fileId, byte[] content, TimeSpan? expiration = null);
        Task<byte[]?> GetThumbnailContentAsync(int fileId);
        Task SetThumbnailContentAsync(int fileId, byte[] content, TimeSpan? expiration = null);
        Task InvalidateFileAsync(int fileId);
        Task<T?> GetFileMetadataAsync<T>(string key) where T : class;
        Task SetFileMetadataAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task InvalidateFileMetadataAsync(string key);
    }
}
