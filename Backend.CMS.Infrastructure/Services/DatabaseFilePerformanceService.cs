using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    /// <summary>
    /// Performance optimization service for database file storage
    /// </summary>
    public class DatabaseFilePerformanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseFilePerformanceService> _logger;

        public DatabaseFilePerformanceService(
            ApplicationDbContext context,
            ILogger<DatabaseFilePerformanceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Configure Entity Framework for optimal file storage performance
        /// Call this in your DbContext OnConfiguring method
        /// </summary>
        public static void ConfigureForFileStorage(DbContextOptionsBuilder optionsBuilder)
        {
            // Enable command timeout for large file operations
            optionsBuilder.UseNpgsql(options =>
            {
                options.CommandTimeout(300); // 5 minutes for file operations
            });

            // Enable lazy loading for better file metadata queries
            optionsBuilder.UseLazyLoadingProxies();
        }

        /// <summary>
        /// Get file metadata without loading file content (performance optimization)
        /// </summary>
        public async Task<IQueryable<FileEntity>> GetFileMetadataQuery()
        {
            return _context.Set<FileEntity>()
                .Select(f => new FileEntity
                {
                    Id = f.Id,
                    OriginalFileName = f.OriginalFileName,
                    StoredFileName = f.StoredFileName,
                    ContentType = f.ContentType,
                    FileSize = f.FileSize,
                    FileExtension = f.FileExtension,
                    FileType = f.FileType,
                    Description = f.Description,
                    Alt = f.Alt,
                    Metadata = f.Metadata,
                    IsPublic = f.IsPublic,
                    FolderId = f.FolderId,
                    DownloadCount = f.DownloadCount,
                    LastAccessedAt = f.LastAccessedAt,
                    Width = f.Width,
                    Height = f.Height,
                    Duration = f.Duration,
                    IsProcessed = f.IsProcessed,
                    ProcessingStatus = f.ProcessingStatus,
                    Tags = f.Tags,
                    CreatedAt = f.CreatedAt,
                    UpdatedAt = f.UpdatedAt,
                    Hash = f.Hash,
                    // Explicitly exclude FileContent and ThumbnailContent
                    FileContent = null!,
                    ThumbnailContent = null
                });
        }

        /// <summary>
        /// Analyze database file storage statistics
        /// </summary>
        public async Task<DatabaseFileStorageStats> GetStorageStatisticsAsync()
        {
            var stats = await _context.Database.SqlQueryRaw<DatabaseFileStorageStats>(@"
                SELECT 
                    COUNT(*) as TotalFiles,
                    SUM(octet_length(""FileContent"")) as TotalFileSize,
                    AVG(octet_length(""FileContent"")) as AverageFileSize,
                    MAX(octet_length(""FileContent"")) as LargestFileSize,
                    COUNT(CASE WHEN ""ThumbnailContent"" IS NOT NULL THEN 1 END) as FilesWithThumbnails,
                    SUM(CASE WHEN ""ThumbnailContent"" IS NOT NULL THEN octet_length(""ThumbnailContent"") ELSE 0 END) as TotalThumbnailSize
                FROM ""Files""
                WHERE ""IsDeleted"" = false
            ").FirstOrDefaultAsync();

            return stats ?? new DatabaseFileStorageStats();
        }

        /// <summary>
        /// Clean up orphaned thumbnails and optimize storage
        /// </summary>
        public async Task<int> CleanupOrphanedDataAsync()
        {
            var cleanedUp = 0;

            try
            {
                // Remove thumbnails for non-image files
                var nonImageFilesWithThumbnails = await _context.Set<FileEntity>()
                    .Where(f => f.FileType != Domain.Enums.FileType.Image && f.ThumbnailContent != null)
                    .ToListAsync();

                foreach (var file in nonImageFilesWithThumbnails)
                {
                    file.ThumbnailContent = null;
                    cleanedUp++;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} orphaned thumbnails", cleanedUp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }

            return cleanedUp;
        }

        /// <summary>
        /// Archive old files by compressing them (if compression is available)
        /// </summary>
        public async Task<int> ArchiveOldFilesAsync(DateTime cutoffDate)
        {
            try
            {
                // Find files older than cutoff that haven't been accessed recently
                var oldFiles = await _context.Set<FileEntity>()
                    .Where(f => f.CreatedAt < cutoffDate &&
                               (f.LastAccessedAt == null || f.LastAccessedAt < cutoffDate.AddDays(-30)))
                    .ToListAsync();

                var archivedCount = 0;

                foreach (var file in oldFiles)
                {
                    try
                    {
                        // Compress file content if it's not already compressed
                        if (file.FileContent.Length > 1024) // Only compress files larger than 1KB
                        {
                            var compressedContent = CompressFileContent(file.FileContent);
                            if (compressedContent.Length < file.FileContent.Length * 0.9) // Only if compression saves at least 10%
                            {
                                file.FileContent = compressedContent;
                                file.Metadata["compressed"] = true;
                                file.Metadata["originalSize"] = file.FileSize;
                                archivedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to compress file {FileId}", file.Id);
                    }
                }

                if (archivedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Archived {Count} old files", archivedCount);
                }

                return archivedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file archiving");
                return 0;
            }
        }

        private byte[] CompressFileContent(byte[] content)
        {
            using var output = new MemoryStream();
            using var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress);
            gzip.Write(content, 0, content.Length);
            gzip.Close();
            return output.ToArray();
        }

        private byte[] DecompressFileContent(byte[] compressedContent)
        {
            using var input = new MemoryStream(compressedContent);
            using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }

    public class DatabaseFileStorageStats
    {
        public long TotalFiles { get; set; }
        public long TotalFileSize { get; set; }
        public long AverageFileSize { get; set; }
        public long LargestFileSize { get; set; }
        public long FilesWithThumbnails { get; set; }
        public long TotalThumbnailSize { get; set; }

        public string TotalFileSizeFormatted => FormatFileSize(TotalFileSize);
        public string AverageFileSizeFormatted => FormatFileSize(AverageFileSize);
        public string LargestFileSizeFormatted => FormatFileSize(LargestFileSize);
        public string TotalThumbnailSizeFormatted => FormatFileSize(TotalThumbnailSize);

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

