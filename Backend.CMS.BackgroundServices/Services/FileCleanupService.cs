using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.BackgroundServices.Services
{
    public interface IFileCleanupService
    {
        Task CleanupTempFilesAsync();
        Task CleanupOldLogsAsync();
        Task OptimizeStorageAsync();
    }

    public class FileCleanupService : IFileCleanupService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileCleanupService> _logger;

        public FileCleanupService(IConfiguration configuration, ILogger<FileCleanupService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task CleanupTempFilesAsync()
        {
            try
            {
                var tempPath = Path.GetTempPath();
                var cutoffDate = DateTime.UtcNow.AddDays(-1); // Clean files older than 1 day

                var tempFiles = Directory.GetFiles(tempPath, "cms_*", SearchOption.TopDirectoryOnly)
                    .Where(f => File.GetCreationTime(f) < cutoffDate)
                    .ToList();

                foreach (var file in tempFiles)
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogDebug("Deleted temp file: {File}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temp file: {File}", file);
                    }
                }

                if (tempFiles.Any())
                {
                    _logger.LogInformation("Cleaned up {Count} temporary files", tempFiles.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during temp file cleanup");
            }
        }

        public async Task CleanupOldLogsAsync()
        {
            try
            {
                var logPath = _configuration.GetValue<string>("Logging:LogFilePath", "Logs");
                var logDirectory = Path.GetDirectoryName(logPath) ?? "Logs";
                
                if (!Directory.Exists(logDirectory))
                    return;

                var maxLogFiles = _configuration.GetValue<int>("Logging:MaxLogFiles", 10);
                var logFiles = Directory.GetFiles(logDirectory, "*.log")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .Skip(maxLogFiles)
                    .ToList();

                foreach (var file in logFiles)
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogDebug("Deleted old log file: {File}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete log file: {File}", file);
                    }
                }

                if (logFiles.Any())
                {
                    _logger.LogInformation("Cleaned up {Count} old log files", logFiles.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during log file cleanup");
            }
        }

        public async Task OptimizeStorageAsync()
        {
            try
            {
                var uploadsPath = _configuration.GetValue<string>("FileStorage:BasePath", "wwwroot/uploads");
                
                if (!Directory.Exists(uploadsPath))
                    return;

                // Check for orphaned files (files not referenced in database)
                // This would require comparing file system with database records
                
                // Compress old files
                var compressionCutoff = DateTime.UtcNow.AddDays(-30);
                var oldFiles = Directory.GetFiles(uploadsPath, "*", SearchOption.AllDirectories)
                    .Where(f => File.GetLastAccessTime(f) < compressionCutoff && !f.EndsWith(".gz"))
                    .ToList();

                foreach (var file in oldFiles.Take(100)) // Limit batch size
                {
                    try
                    {
                        await CompressFileAsync(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to compress file: {File}", file);
                    }
                }

                if (oldFiles.Any())
                {
                    _logger.LogInformation("Compressed {Count} old files", Math.Min(oldFiles.Count, 100));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during storage optimization");
            }
        }

        private async Task CompressFileAsync(string filePath)
        {
            // Simple compression example - in production, consider more sophisticated compression
            var compressedPath = filePath + ".gz";
            
            using var originalFile = File.OpenRead(filePath);
            using var compressedFile = File.Create(compressedPath);
            using var compressionStream = new System.IO.Compression.GZipStream(compressedFile, System.IO.Compression.CompressionMode.Compress);
            
            await originalFile.CopyToAsync(compressionStream);
            
            // Replace original with compressed version
            File.Delete(filePath);
            File.Move(compressedPath, filePath);
        }
    }
}