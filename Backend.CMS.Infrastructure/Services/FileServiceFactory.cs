using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.CMS.Infrastructure.Services
{
    /// <summary>
    /// Factory for creating type-specific file services
    /// </summary>
    public class FileServiceFactory : IFileServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public FileServiceFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Gets the appropriate service for the specified file type
        /// </summary>
        public T GetService<T>() where T : class
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Gets the image file service
        /// </summary>
        public IImageFileService GetImageService()
        {
            return _serviceProvider.GetRequiredService<IImageFileService>();
        }

        /// <summary>
        /// Gets the video file service
        /// </summary>
        public IVideoFileService GetVideoService()
        {
            return _serviceProvider.GetRequiredService<IVideoFileService>();
        }

        /// <summary>
        /// Gets the audio file service
        /// </summary>
        public IAudioFileService GetAudioService()
        {
            return _serviceProvider.GetRequiredService<IAudioFileService>();
        }

        /// <summary>
        /// Gets the document file service
        /// </summary>
        public IDocumentFileService GetDocumentService()
        {
            return _serviceProvider.GetRequiredService<IDocumentFileService>();
        }

        /// <summary>
        /// Gets the archive file service
        /// </summary>
        public IArchiveFileService GetArchiveService()
        {
            return _serviceProvider.GetRequiredService<IArchiveFileService>();
        }

        /// <summary>
        /// Gets the appropriate service based on file type
        /// </summary>
        public object GetServiceByFileType(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => GetImageService(),
                FileType.Video => GetVideoService(),
                FileType.Audio => GetAudioService(),
                FileType.Document => GetDocumentService(),
                FileType.Archive => GetArchiveService(),
                _ => throw new ArgumentException($"No service available for file type: {fileType}")
            };
        }

        /// <summary>
        /// Gets the appropriate service based on content type
        /// </summary>
        public object GetServiceByContentType(string contentType)
        {
            var fileType = DetermineFileTypeFromContentType(contentType);
            return GetServiceByFileType(fileType);
        }

        /// <summary>
        /// Determines file type from content type
        /// </summary>
        private static FileType DetermineFileTypeFromContentType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return FileType.Other;

            var contentTypeLower = contentType.ToLowerInvariant();

            if (contentTypeLower.StartsWith("image/"))
                return FileType.Image;

            if (contentTypeLower.StartsWith("video/"))
                return FileType.Video;

            if (contentTypeLower.StartsWith("audio/"))
                return FileType.Audio;

            // Document types
            if (contentTypeLower.Contains("pdf") ||
                contentTypeLower.Contains("document") ||
                contentTypeLower.Contains("word") ||
                contentTypeLower.Contains("excel") ||
                contentTypeLower.Contains("powerpoint") ||
                contentTypeLower.Contains("text/"))
                return FileType.Document;

            // Archive types
            if (contentTypeLower.Contains("zip") ||
                contentTypeLower.Contains("rar") ||
                contentTypeLower.Contains("7z") ||
                contentTypeLower.Contains("tar") ||
                contentTypeLower.Contains("gzip"))
                return FileType.Archive;

            return FileType.Other;
        }
    }

    /// <summary>
    /// Interface for the file service factory
    /// </summary>
    public interface IFileServiceFactory
    {
        T GetService<T>() where T : class;
        IImageFileService GetImageService();
        IVideoFileService GetVideoService();
        IAudioFileService GetAudioService();
        IDocumentFileService GetDocumentService();
        IArchiveFileService GetArchiveService();
        object GetServiceByFileType(FileType fileType);
        object GetServiceByContentType(string contentType);
    }
}