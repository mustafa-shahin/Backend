using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;

namespace Frontend.Interfaces
{
    public interface IFolderService
    {
        Task<List<FolderDto>> GetFoldersAsync(int? parentFolderId = null);
        Task<FolderDto?> GetFolderByIdAsync(int id);
        Task<FolderDto?> CreateFolderAsync(CreateFolderDto createFolderDto);
        Task<FolderDto?> UpdateFolderAsync(int id, UpdateFolderDto updateFolderDto);
        Task<bool> DeleteFolderAsync(int id, bool deleteFiles = false);
        Task<FolderDto?> MoveFolderAsync(MoveFolderDto moveFolderDto);
        Task<bool> RenameFolderAsync(int id, string newName);
        Task<FolderDto?> CopyFolderAsync(int folderId, int? destinationFolderId, string? newName);
        Task<string?> GetFolderPathAsync(int id);
        Task<List<FolderDto>> GetFolderBreadcrumbsAsync(int id);
        Task<FolderDto?> GetFolderByPathAsync(string path);
        Task<List<FolderDto>> SearchFoldersAsync(string searchTerm);
        Task<bool> ValidateFolderNameAsync(string name, int? parentFolderId = null, int? excludeFolderId = null);
        Task<Dictionary<string, object>> GetFolderStatisticsAsync(int id);
        Task<FolderDto?> GetOrCreateSystemFolderAsync(FolderType folderType);
        Task<FolderDto?> GetUserAvatarFolderAsync(int userId);
        Task<FolderDto?> GetCompanyAssetsFolderAsync();
        Task<bool> FolderExistsAsync(int id);
        Task<FolderTreeDto> GetFolderTreeAsync(int? rootFolderId = null);
    }
}