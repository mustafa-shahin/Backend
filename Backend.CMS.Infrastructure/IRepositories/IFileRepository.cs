using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Domain.Enums;
namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IFileRepository : IRepository<BaseFileEntity>
    {
        // Basic file operations
        Task<IEnumerable<BaseFileEntity>> GetFilesByFolderIdAsync(int? folderId);
        Task<IEnumerable<BaseFileEntity>> GetFilesByTypeAsync(FileType fileType);
        Task<IEnumerable<BaseFileEntity>> GetPublicFilesAsync();
        Task<BaseFileEntity?> GetByStoredFileNameAsync(string storedFileName);
        Task<BaseFileEntity?> GetByHashAsync(string hash);
        Task<IEnumerable<BaseFileEntity>> SearchFilesByNameAsync(string searchTerm);
        Task<IEnumerable<BaseFileEntity>> GetFilesByExtensionAsync(string extension);
        Task<IEnumerable<BaseFileEntity>> GetFilesByContentTypeAsync(string contentType);
        Task<IEnumerable<BaseFileEntity>> GetUnprocessedFilesAsync();
        Task<IEnumerable<BaseFileEntity>> GetFilesByUserIdAsync(int userId);
        Task<IEnumerable<BaseFileEntity>> GetRecentFilesAsync(int count = 10);
        Task<IEnumerable<BaseFileEntity>> GetLargeFilesAsync(long minSizeInBytes);
        
        // Statistics and analytics
        Task<long> GetTotalFileSizeAsync();
        Task<long> GetTotalFileSizeByFolderAsync(int? folderId);
        Task<int> GetFileCountByTypeAsync(FileType fileType);
        Task<bool> FileExistsByHashAsync(string hash);
        Task<bool> UpdateDownloadCountAsync(int fileId);
        Task<bool> UpdateLastAccessedAsync(int fileId);
        Task<IEnumerable<BaseFileEntity>> GetDuplicateFilesAsync();
        Task<IEnumerable<BaseFileEntity>> GetOrphanedFilesAsync();
        Task<IEnumerable<BaseFileEntity>> GetFilesByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<BaseFileEntity>> GetFilesWithThumbnailsAsync();
        Task<IEnumerable<BaseFileEntity>> GetFilesWithoutThumbnailsAsync();

        // Type-specific queries
        Task<IEnumerable<ImageFileEntity>> GetImageFilesAsync(int? folderId = null);
        Task<IEnumerable<ImageFileEntity>> GetImagesByDimensionsAsync(int? minWidth = null, int? maxWidth = null, int? minHeight = null, int? maxHeight = null);
        Task<IEnumerable<ImageFileEntity>> GetImagesWithGeoLocationAsync();
        
        Task<IEnumerable<VideoFileEntity>> GetVideoFilesAsync(int? folderId = null);
        Task<IEnumerable<VideoFileEntity>> GetVideosByQualityAsync(int minWidth, int minHeight);
        Task<IEnumerable<VideoFileEntity>> GetVideosByDurationAsync(TimeSpan? minDuration = null, TimeSpan? maxDuration = null);
        
        Task<IEnumerable<AudioFileEntity>> GetAudioFilesAsync(int? folderId = null);
        Task<IEnumerable<AudioFileEntity>> GetAudioByArtistAsync(string artist);
        Task<IEnumerable<AudioFileEntity>> GetAudioByAlbumAsync(string album);
        Task<IEnumerable<AudioFileEntity>> GetAudioByGenreAsync(string genre);
        
        Task<IEnumerable<DocumentFileEntity>> GetDocumentFilesAsync(int? folderId = null);
        Task<IEnumerable<DocumentFileEntity>> GetDocumentsByAuthorAsync(string author);
        Task<IEnumerable<DocumentFileEntity>> GetPasswordProtectedDocumentsAsync();
        
        Task<IEnumerable<ArchiveFileEntity>> GetArchiveFilesAsync(int? folderId = null);
        Task<IEnumerable<ArchiveFileEntity>> GetCorruptedArchivesAsync();
        Task<IEnumerable<ArchiveFileEntity>> GetPasswordProtectedArchivesAsync();
        
        Task<IEnumerable<OtherFileEntity>> GetOtherFilesAsync(int? folderId = null);
        Task<IEnumerable<OtherFileEntity>> GetExecutableFilesAsync();
        Task<IEnumerable<OtherFileEntity>> GetPotentiallyDangerousFilesAsync();
    }
}