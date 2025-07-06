using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IFolderService
    {
        // Paginated operations
        Task<PagedResult<FolderDto>> GetFoldersPagedAsync(int? parentFolderId = null, int pageNumber = 1, int pageSize = 10);
        Task<PagedResult<FolderDto>> SearchFoldersPagedAsync(string searchTerm, int pageNumber = 1, int pageSize = 10);

        // CRUD operations
        Task<FolderDto> CreateFolderAsync(CreateFolderDto createDto);
        Task<FolderDto> UpdateFolderAsync(int folderId, UpdateFolderDto updateDto);
        Task<bool> DeleteFolderAsync(int folderId, bool deleteFiles = false);

        // Single folder operations
        Task<FolderDto> GetFolderByIdAsync(int folderId);
        Task<List<FolderDto>> GetFoldersAsync(int? parentFolderId = null);
        Task<FolderTreeDto> GetFolderTreeAsync(int? rootFolderId = null);
        Task<FolderDto?> GetFolderByPathAsync(string path);

        // Folder management operations
        Task<FolderDto> MoveFolderAsync(MoveFolderDto moveDto);
        Task<bool> RenameFolderAsync(int folderId, string newName);
        Task<FolderDto> CopyFolderAsync(int folderId, int? destinationFolderId, string? newName = null);

        // Navigation and hierarchy
        Task<string> GetFolderPathAsync(int folderId);
        Task<List<FolderDto>> GetFolderBreadcrumbsAsync(int folderId);
        Task<List<FolderDto>> SearchFoldersAsync(string searchTerm);

        // Validation and utilities
        Task<bool> FolderExistsAsync(int folderId);
        Task<bool> ValidateFolderNameAsync(string name, int? parentFolderId = null, int? excludeFolderId = null);
        Task<bool> IsSubFolderOfAsync(int childFolderId, int parentFolderId);
        Task<Dictionary<string, object>> GetFolderStatisticsAsync(int folderId);

        // System folder operations
        Task<FolderDto> GetOrCreateSystemFolderAsync(FolderType folderType);
        Task<FolderDto> GetUserAvatarFolderAsync(int userId);
        Task<FolderDto> GetCompanyAssetsFolderAsync();
    }
}