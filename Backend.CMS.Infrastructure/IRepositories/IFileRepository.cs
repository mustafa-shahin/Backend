using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.IRepositories;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IFileRepository : IRepository<FileEntity>
    {
        Task<IEnumerable<FileEntity>> GetFilesByFolderIdAsync(int? folderId);
        Task<IEnumerable<FileEntity>> GetFilesByTypeAsync(FileType fileType);
        Task<IEnumerable<FileEntity>> GetPublicFilesAsync();
        Task<FileEntity?> GetByStoredFileNameAsync(string storedFileName);
        Task<FileEntity?> GetByHashAsync(string hash);
        Task<IEnumerable<FileEntity>> SearchFilesByNameAsync(string searchTerm);
        Task<IEnumerable<FileEntity>> GetFilesByExtensionAsync(string extension);
        Task<IEnumerable<FileEntity>> GetFilesByContentTypeAsync(string contentType);
        Task<IEnumerable<FileEntity>> GetUnprocessedFilesAsync();
        Task<IEnumerable<FileEntity>> GetFilesByUserIdAsync(int userId);
        Task<IEnumerable<FileEntity>> GetRecentFilesAsync(int count = 10);
        Task<IEnumerable<FileEntity>> GetLargeFilesAsync(long minSizeInBytes);
        Task<long> GetTotalFileSizeAsync();
        Task<long> GetTotalFileSizeByFolderAsync(int? folderId);
        Task<int> GetFileCountByTypeAsync(FileType fileType);
        Task<bool> FileExistsByHashAsync(string hash);
        Task<bool> UpdateDownloadCountAsync(int fileId);
        Task<bool> UpdateLastAccessedAsync(int fileId);
        Task<IEnumerable<FileEntity>> GetDuplicateFilesAsync();
        Task<IEnumerable<FileEntity>> GetOrphanedFilesAsync();
        Task<IEnumerable<FileEntity>> GetFilesByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<FileEntity>> GetFilesWithThumbnailsAsync();
        Task<IEnumerable<FileEntity>> GetFilesWithoutThumbnailsAsync();
    }
}