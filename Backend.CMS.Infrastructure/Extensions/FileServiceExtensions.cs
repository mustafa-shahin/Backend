
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
           

            // Register type-specific services
            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<IVideoService, VideoService>();
            services.AddScoped<IAudioService, AudioService>();
            services.AddScoped<IDocumentService, DocumentService>();
            services.AddScoped<IArchiveService, ArchiveService>();
            services.AddScoped<IOtherFileService, OtherFileService>();


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
            services.AddScoped<IFileAggregatorService, FileAggregatorService>();
            
            // Register file URL builder if not already registered
            services.AddScoped<IFileUrlService, FileUrlService>();

            return services;
        }
    }
}