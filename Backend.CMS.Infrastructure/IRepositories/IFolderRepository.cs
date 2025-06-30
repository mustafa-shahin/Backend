using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.IRepositories;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IFolderRepository : IRepository<Folder>
    {
        Task<IEnumerable<Folder>> GetRootFoldersAsync();
        Task<IEnumerable<Folder>> GetSubFoldersAsync(int parentFolderId);
        Task<Folder?> GetByPathAsync(string path);
        Task<IEnumerable<Folder>> GetFoldersByTypeAsync(FolderType folderType);
        Task<IEnumerable<Folder>> GetPublicFoldersAsync();
        Task<IEnumerable<Folder>> SearchFoldersByNameAsync(string searchTerm);
        Task<IEnumerable<Folder>> GetFolderHierarchyAsync(int folderId);
        Task<IEnumerable<Folder>> GetFoldersByUserIdAsync(int userId);
        Task<string> GenerateUniquePathAsync(string basePath, int? parentFolderId = null);
        Task<bool> IsPathUniqueAsync(string path, int? excludeFolderId = null);
        Task<bool> HasSubFoldersAsync(int folderId);
        Task<bool> HasFilesAsync(int folderId);
        Task<int> GetTotalFileCountAsync(int folderId, bool includeSubfolders = false);
        Task<long> GetTotalSizeAsync(int folderId, bool includeSubfolders = false);
        Task<int> GetDepthAsync(int folderId);
        Task<IEnumerable<Folder>> GetAncestorsAsync(int folderId);
        Task<IEnumerable<Folder>> GetDescendantsAsync(int folderId);
        Task<bool> IsDescendantOfAsync(int childFolderId, int ancestorFolderId);
        Task<IEnumerable<Folder>> GetEmptyFoldersAsync();
        Task<IEnumerable<Folder>> GetFoldersByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<bool> CanDeleteFolderAsync(int folderId);
        Task<bool> MoveFolderAsync(int folderId, int? newParentFolderId);
    }
}