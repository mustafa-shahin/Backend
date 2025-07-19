
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.CMS.Infrastructure.Extensions
{
    /// <summary>
    /// Extension methods for registering file services
    /// </summary>
    public static class FileServiceExtensions
    {
        /// <summary>
        /// Registers all file services with dependency injection
        /// </summary>
        public static IServiceCollection AddFileServices(this IServiceCollection services)
        {
            // Register base file service (keep the existing one for backward compatibility)
            services.AddScoped<FileService>();

            // Register type-specific services
            services.AddScoped<IImageFileService, ImageFileService>();
            services.AddScoped<IVideoFileService, VideoFileService>();
            services.AddScoped<IAudioFileService, AudioFileService>();
            services.AddScoped<IDocumentFileService, DocumentFileService>();
            services.AddScoped<IArchiveFileService, ArchiveFileService>();
            services.AddScoped<IOtherFileService, OtherFileService>();

            // Register the service factory
            services.AddScoped<IFileServiceFactory, FileServiceFactory>();

            return services;
        }

        /// <summary>
        /// Registers file processing services
        /// </summary>
        public static IServiceCollection AddFileProcessingServices(this IServiceCollection services)
        {
            // Register image processing service if not already registered
            services.AddScoped<IImageProcessingService, ImageProcessingService>();
            
            // Register file validation service if not already registered
            services.AddScoped<IFileValidationService, FileValidationService>();
            
            // Register file URL builder if not already registered
            services.AddScoped<IFileUrlBuilder, FileUrlBuilder>();

            return services;
        }
    }
}