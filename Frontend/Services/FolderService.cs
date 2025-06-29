using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Frontend.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;

namespace Frontend.Services
{
    public class FolderService : IFolderService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public FolderService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<List<FolderDto>> GetFoldersAsync(int? parentFolderId = null)
        {
            try
            {
                var query = "/api/folder/all";
                if (parentFolderId.HasValue)
                    query += $"?parentFolderId={parentFolderId}";

                var response = await _httpClient.GetAsync(query);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<FolderDto>>(_jsonOptions);
                    return result ?? new List<FolderDto>();
                }
                return new List<FolderDto>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting folders: {ex.Message}", ex);
            }
        }

        public async Task<FolderDto?> GetFolderByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/folder/{id}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting folder {id}: {ex.Message}", ex);
            }
        }

        public async Task<FolderDto?> CreateFolderAsync(CreateFolderDto createFolderDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/folder", createFolderDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create folder: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating folder: {ex.Message}", ex);
            }
        }

        public async Task<FolderDto?> UpdateFolderAsync(int id, UpdateFolderDto updateFolderDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/folder/{id}", updateFolderDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update folder: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating folder {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteFolderAsync(int id, bool deleteFiles = false)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/folder/{id}?deleteFiles={deleteFiles}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting folder {id}: {ex.Message}", ex);
            }
        }

        public async Task<FolderDto?> MoveFolderAsync(MoveFolderDto moveFolderDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/folder/move", moveFolderDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error moving folder: {ex.Message}", ex);
            }
        }

        public async Task<bool> RenameFolderAsync(int id, string newName)
        {
            try
            {
                var renameDto = new { NewName = newName };
                var response = await _httpClient.PostAsJsonAsync($"/api/folder/{id}/rename", renameDto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error renaming folder {id}: {ex.Message}", ex);
            }
        }

        public async Task<FolderDto?> CopyFolderAsync(int folderId, int? destinationFolderId, string? newName)
        {
            try
            {
                var copyDto = new { FolderId = folderId, DestinationFolderId = destinationFolderId, NewName = newName };
                var response = await _httpClient.PostAsJsonAsync("/api/folder/copy", copyDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error copying folder {folderId}: {ex.Message}", ex);
            }
        }

        public async Task<string?> GetFolderPathAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/folder/{id}/path");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    return result.GetProperty("path").GetString();
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting folder path {id}: {ex.Message}", ex);
            }
        }

        public async Task<List<FolderDto>> GetFolderBreadcrumbsAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/folder/{id}/breadcrumbs");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<FolderDto>>(_jsonOptions);
                    return result ?? new List<FolderDto>();
                }
                return new List<FolderDto>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting folder breadcrumbs {id}: {ex.Message}", ex);
            }
        }

        public async Task<FolderDto?> GetFolderByPathAsync(string path)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/folder/by-path?path={Uri.EscapeDataString(path)}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting folder by path {path}: {ex.Message}", ex);
            }
        }

        public async Task<List<FolderDto>> SearchFoldersAsync(string searchTerm)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/folder/search?searchTerm={Uri.EscapeDataString(searchTerm)}");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<FolderDto>>(_jsonOptions);
                    return result ?? new List<FolderDto>();
                }
                return new List<FolderDto>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error searching folders: {ex.Message}", ex);
            }
        }

        public async Task<bool> ValidateFolderNameAsync(string name, int? parentFolderId = null, int? excludeFolderId = null)
        {
            try
            {
                var query = $"/api/folder/validate-name?name={Uri.EscapeDataString(name)}";
                if (parentFolderId.HasValue) query += $"&parentFolderId={parentFolderId}";
                if (excludeFolderId.HasValue) query += $"&excludeFolderId={excludeFolderId}";

                var response = await _httpClient.GetAsync(query);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    return result.GetProperty("isValid").GetBoolean();
                }
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error validating folder name: {ex.Message}", ex);
            }
        }

        public async Task<Dictionary<string, object>> GetFolderStatisticsAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/folder/{id}/statistics");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
                    return result ?? new Dictionary<string, object>();
                }
                return new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting folder statistics {id}: {ex.Message}", ex);
            }
        }

        public async Task<FolderDto?> GetOrCreateSystemFolderAsync(FolderType folderType)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/folder/system/{folderType}", null);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting/creating system folder {folderType}: {ex.Message}", ex);
            }
        }

        public async Task<FolderDto?> GetUserAvatarFolderAsync(int userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/folder/user-avatars/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting user avatar folder {userId}: {ex.Message}", ex);
            }
        }

        public async Task<FolderDto?> GetCompanyAssetsFolderAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/folder/company-assets");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting company assets folder: {ex.Message}", ex);
            }
        }

        public async Task<bool> FolderExistsAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/folder/{id}/exists");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    return result.GetProperty("exists").GetBoolean();
                }
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking folder existence {id}: {ex.Message}", ex);
            }
        }

        public async Task<FolderTreeDto> GetFolderTreeAsync(int? rootFolderId = null)
        {
            try
            {
                var query = "/api/folder/tree";
                if (rootFolderId.HasValue) query += $"?rootFolderId={rootFolderId}";

                var response = await _httpClient.GetAsync(query);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<FolderTreeDto>(_jsonOptions);
                    return result ?? new FolderTreeDto();
                }
                return new FolderTreeDto();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting folder tree: {ex.Message}", ex);
            }
        }
    }
}