using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Frontend.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Frontend.Services
{
    /// <summary>
    /// Frontend folder service implementation with pagination support and caching
    /// </summary>
    public class FolderService : IFolderService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _baseUrl;
        private readonly ConcurrentDictionary<int, FolderDto> _folderCache;
        private readonly Timer _cacheCleanupTimer;
        private readonly SemaphoreSlim _requestSemaphore;
        private bool _disposed = false;

        public FolderService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _baseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "";
            _folderCache = new ConcurrentDictionary<int, FolderDto>();
            _requestSemaphore = new SemaphoreSlim(10, 10); 

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            // Clean cache every 15 minutes
            _cacheCleanupTimer = new Timer(CleanupCache, null,
                TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
        }

        #region Paginated Operations

        public async Task<PaginatedResult<FolderDto>> GetFoldersPagedAsync(int? parentFolderId = null, int pageNumber = 1, int pageSize = 10)
        {
            // Validate pagination parameters
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            await _requestSemaphore.WaitAsync();
            try
            {
                var queryParams = new List<string>
                {
                    $"pageNumber={pageNumber}",
                    $"pageSize={pageSize}"
                };

                if (parentFolderId.HasValue)
                    queryParams.Add($"parentFolderId={parentFolderId}");

                var query = $"api/v1/folder?{string.Join("&", queryParams)}";

                var response = await _httpClient.GetAsync(query);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<FolderDto>>(_jsonOptions);

                    // Cache the folders for performance
                    if (result?.Data != null)
                    {
                        foreach (var folder in result.Data)
                        {
                            _folderCache.TryAdd(folder.Id, folder);
                        }
                    }

                    return result ?? PaginatedResult<FolderDto>.Empty(pageNumber, pageSize);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get folders: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting folders: {ex.Message}", ex);
            }
            finally
            {
                _requestSemaphore.Release();
            }
        }

        public async Task<PaginatedResult<FolderDto>> SearchFoldersPagedAsync(string searchTerm, int pageNumber = 1, int pageSize = 10)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return PaginatedResult<FolderDto>.Empty(pageNumber, pageSize);

            // Validate pagination parameters
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            await _requestSemaphore.WaitAsync();
            try
            {
                var queryParams = new List<string>
                {
                    $"searchTerm={Uri.EscapeDataString(searchTerm)}",
                    $"pageNumber={pageNumber}",
                    $"pageSize={pageSize}"
                };

                var query = $"api/v1/folder/search?{string.Join("&", queryParams)}";

                var response = await _httpClient.GetAsync(query);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<FolderDto>>(_jsonOptions);

                    // Cache the folders
                    if (result?.Data != null)
                    {
                        foreach (var folder in result.Data)
                        {
                            _folderCache.TryAdd(folder.Id, folder);
                        }
                    }

                    return result ?? PaginatedResult<FolderDto>.Empty(pageNumber, pageSize);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to search folders: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error searching folders: {ex.Message}", ex);
            }
            finally
            {
                _requestSemaphore.Release();
            }
        }

        #endregion

        #region Core Folder Operations

        public async Task<List<FolderDto>> GetFoldersAsync(int? parentFolderId = null)
        {
            try
            {
                var query = "api/v1/folder/all";
                if (parentFolderId.HasValue)
                    query += $"?parentFolderId={parentFolderId}";

                var response = await _httpClient.GetAsync(query);
                if (response.IsSuccessStatusCode)
                {
                    var folders = await response.Content.ReadFromJsonAsync<List<FolderDto>>(_jsonOptions);

                    // Cache the folders
                    if (folders != null)
                    {
                        foreach (var folder in folders)
                        {
                            _folderCache.TryAdd(folder.Id, folder);
                        }
                    }

                    return folders ?? new List<FolderDto>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get folders: {response.StatusCode} - {errorContent}");
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
                // Check cache first
                if (_folderCache.TryGetValue(id, out var cachedFolder))
                {
                    return cachedFolder;
                }

                var response = await _httpClient.GetAsync($"api/v1/folder/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var folder = await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                    if (folder != null)
                    {
                        _folderCache.TryAdd(folder.Id, folder);
                    }
                    return folder;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get folder: {response.StatusCode} - {errorContent}");
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
                if (createFolderDto == null)
                    throw new ArgumentNullException(nameof(createFolderDto));

                var response = await _httpClient.PostAsJsonAsync("api/v1/folder", createFolderDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    var folder = await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                    if (folder != null)
                    {
                        _folderCache.TryAdd(folder.Id, folder);
                    }
                    return folder;
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
                if (updateFolderDto == null)
                    throw new ArgumentNullException(nameof(updateFolderDto));

                var response = await _httpClient.PutAsJsonAsync($"api/v1/folder/{id}", updateFolderDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    var folder = await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                    if (folder != null)
                    {
                        // Update cache
                        _folderCache.AddOrUpdate(folder.Id, folder, (key, oldValue) => folder);
                    }
                    return folder;
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
                var response = await _httpClient.DeleteAsync($"api/v1/folder/{id}?deleteFiles={deleteFiles}");
                var success = response.IsSuccessStatusCode;

                if (success)
                {
                    // Remove from cache
                    _folderCache.TryRemove(id, out _);
                }

                return success;
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
                if (moveFolderDto == null)
                    throw new ArgumentNullException(nameof(moveFolderDto));

                var response = await _httpClient.PostAsJsonAsync("api/v1/folder/move", moveFolderDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    var folder = await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                    if (folder != null)
                    {
                        // Update cache
                        _folderCache.AddOrUpdate(folder.Id, folder, (key, oldValue) => folder);
                    }
                    return folder;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to move folder: {response.StatusCode} - {errorContent}");
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
                if (string.IsNullOrWhiteSpace(newName))
                    throw new ArgumentException("New name cannot be empty", nameof(newName));

                var renameDto = new RenameFolderDto { NewName = newName };
                var response = await _httpClient.PostAsJsonAsync($"api/v1/folder/{id}/rename", renameDto, _jsonOptions);
                var success = response.IsSuccessStatusCode;

                if (success)
                {
                    // Invalidate cache for this folder
                    _folderCache.TryRemove(id, out _);
                }

                return success;
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
                var copyDto = new CopyFolderDto
                {
                    FolderId = folderId,
                    DestinationFolderId = destinationFolderId,
                    NewName = newName
                };

                var response = await _httpClient.PostAsJsonAsync("api/v1/folder/copy", copyDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    var folder = await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                    if (folder != null)
                    {
                        // Add to cache
                        _folderCache.TryAdd(folder.Id, folder);
                    }
                    return folder;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to copy folder: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error copying folder {folderId}: {ex.Message}", ex);
            }
        }

        #endregion

        #region Navigation and Hierarchy

        public async Task<string?> GetFolderPathAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/v1/folder/{id}/path");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    return result.GetProperty("path").GetString();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get folder path: {response.StatusCode} - {errorContent}");
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
                var response = await _httpClient.GetAsync($"api/v1/folder/{id}/breadcrumbs");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<FolderDto>>(_jsonOptions);
                    return result ?? new List<FolderDto>();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new List<FolderDto>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get folder breadcrumbs: {response.StatusCode} - {errorContent}");
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
                if (string.IsNullOrWhiteSpace(path))
                    return null;

                var response = await _httpClient.GetAsync($"api/v1/folder/by-path?path={Uri.EscapeDataString(path)}");
                if (response.IsSuccessStatusCode)
                {
                    var folder = await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                    if (folder != null)
                    {
                        _folderCache.TryAdd(folder.Id, folder);
                    }
                    return folder;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get folder by path: {response.StatusCode} - {errorContent}");
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
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return new List<FolderDto>();

                // Use paginated search and get all results
                var pageSize = 50;
                var allFolders = new List<FolderDto>();
                var currentPage = 1;
                PaginatedResult<FolderDto> result;

                do
                {
                    result = await SearchFoldersPagedAsync(searchTerm, currentPage, pageSize);
                    allFolders.AddRange(result.Data);
                    currentPage++;
                } while (result.HasNextPage);

                return allFolders;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error searching folders: {ex.Message}", ex);
            }
        }

        public async Task<FolderTreeDto> GetFolderTreeAsync(int? rootFolderId = null)
        {
            try
            {
                var query = "api/v1/folder/tree";
                if (rootFolderId.HasValue)
                    query += $"?rootFolderId={rootFolderId}";

                var response = await _httpClient.GetAsync(query);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<FolderTreeDto>(_jsonOptions);
                    return result ?? new FolderTreeDto();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get folder tree: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting folder tree: {ex.Message}", ex);
            }
        }

        #endregion

        #region Validation and Utilities

        public async Task<bool> ValidateFolderNameAsync(string name, int? parentFolderId = null, int? excludeFolderId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return false;

                var query = $"api/v1/folder/validate-name?name={Uri.EscapeDataString(name)}";
                if (parentFolderId.HasValue)
                    query += $"&parentFolderId={parentFolderId}";
                if (excludeFolderId.HasValue)
                    query += $"&excludeFolderId={excludeFolderId}";

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
                var response = await _httpClient.GetAsync($"api/v1/folder/{id}/statistics");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
                    return result ?? new Dictionary<string, object>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get folder statistics: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting folder statistics {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> FolderExistsAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/v1/folder/{id}/exists");
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

        #endregion

        #region System Folder Operations

        public async Task<FolderDto?> GetOrCreateSystemFolderAsync(FolderType folderType)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/v1/folder/system/{folderType}", null);
                if (response.IsSuccessStatusCode)
                {
                    var folder = await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                    if (folder != null)
                    {
                        _folderCache.TryAdd(folder.Id, folder);
                    }
                    return folder;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get/create system folder: {response.StatusCode} - {errorContent}");
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
                var response = await _httpClient.GetAsync($"api/v1/folder/user-avatars/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    var folder = await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                    if (folder != null)
                    {
                        _folderCache.TryAdd(folder.Id, folder);
                    }
                    return folder;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get user avatar folder: {response.StatusCode} - {errorContent}");
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
                var response = await _httpClient.GetAsync("api/v1/folder/company-assets");
                if (response.IsSuccessStatusCode)
                {
                    var folder = await response.Content.ReadFromJsonAsync<FolderDto>(_jsonOptions);
                    if (folder != null)
                    {
                        _folderCache.TryAdd(folder.Id, folder);
                    }
                    return folder;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get company assets folder: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting company assets folder: {ex.Message}", ex);
            }
        }

        #endregion

        #region Folder Type Information

        public List<FolderTypeInfo> GetFolderTypeInfos()
        {
            return new List<FolderTypeInfo>
            {
                new FolderTypeInfo
                {
                    Type = FolderType.General,
                    Name = "General",
                    Icon = "fas fa-folder",
                    ColorClass = "text-blue-600 dark:text-blue-400",
                    Description = "General purpose folder for mixed content",
                    Features = new[] { "Flexible organization", "No restrictions" }
                },
                new FolderTypeInfo
                {
                    Type = FolderType.Images,
                    Name = "Images",
                    Icon = "fas fa-images",
                    ColorClass = "text-green-600 dark:text-green-400",
                    Description = "Optimized for image files and galleries",
                    Features = new[] { "Image previews", "EXIF data support", "Thumbnail generation" }
                },
                new FolderTypeInfo
                {
                    Type = FolderType.Documents,
                    Name = "Documents",
                    Icon = "fas fa-file-alt",
                    ColorClass = "text-red-600 dark:text-red-400",
                    Description = "Document storage with preview support",
                    Features = new[] { "Document previews", "Search indexing", "Version control" }
                },
                new FolderTypeInfo
                {
                    Type = FolderType.Videos,
                    Name = "Videos",
                    Icon = "fas fa-video",
                    ColorClass = "text-blue-600 dark:text-blue-400",
                    Description = "Video files with streaming capabilities",
                    Features = new[] { "Video previews", "Duration tracking", "Thumbnail generation" }
                },
                new FolderTypeInfo
                {
                    Type = FolderType.Audio,
                    Name = "Audio",
                    Icon = "fas fa-music",
                    ColorClass = "text-purple-600 dark:text-purple-400",
                    Description = "Audio files with metadata support",
                    Features = new[] { "Audio previews", "Metadata extraction", "Playlist support" }
                },
                new FolderTypeInfo
                {
                    Type = FolderType.UserAvatars,
                    Name = "User Avatars",
                    Icon = "fas fa-user-circle",
                    ColorClass = "text-indigo-600 dark:text-indigo-400",
                    Description = "User profile pictures and avatars",
                    Features = new[] { "Automatic resizing", "Multiple formats", "CDN optimization" }
                },
                new FolderTypeInfo
                {
                    Type = FolderType.CompanyAssets,
                    Name = "Company Assets",
                    Icon = "fas fa-building",
                    ColorClass = "text-gray-600 dark:text-gray-400",
                    Description = "Company branding and official assets",
                    Features = new[] { "Brand compliance", "Version control", "Usage tracking" }
                },
                new FolderTypeInfo
                {
                    Type = FolderType.Temporary,
                    Name = "Temporary",
                    Icon = "fas fa-clock",
                    ColorClass = "text-orange-600 dark:text-orange-400",
                    Description = "Temporary files with automatic cleanup",
                    Features = new[] { "Auto-deletion", "Short-term storage", "Quick access" }
                }
            };
        }

        #endregion

        #region Cache and Performance

        public async Task ClearCacheAsync()
        {
            await Task.Run(() =>
            {
                _folderCache.Clear();
            });
        }

        public async Task PreloadFoldersAsync(List<int> folderIds)
        {
            if (folderIds?.Any() != true)
                return;

            var semaphore = new SemaphoreSlim(5, 5); // Limit concurrent preloads
            var tasks = folderIds.Where(id => !_folderCache.ContainsKey(id))
                                .Select(async id =>
                                {
                                    await semaphore.WaitAsync();
                                    try
                                    {
                                        await GetFolderByIdAsync(id);
                                    }
                                    catch
                                    {
                                        // Ignore errors during preloading
                                    }
                                    finally
                                    {
                                        semaphore.Release();
                                    }
                                });

            await Task.WhenAll(tasks);
            semaphore.Dispose();
        }

        #endregion

        #region Private Methods

        private void CleanupCache(object? state)
        {
            try
            {
                const int maxCacheSize = 500;
                if (_folderCache.Count <= maxCacheSize)
                    return;

                // Remove 20% of oldest entries (simple cleanup strategy)
                var itemsToRemove = _folderCache.Count / 5;
                var keysToRemove = _folderCache.Keys.Take(itemsToRemove).ToList();

                foreach (var key in keysToRemove)
                {
                    _folderCache.TryRemove(key, out _);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _cacheCleanupTimer?.Dispose();
                _requestSemaphore?.Dispose();
                _folderCache?.Clear();
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Folder type information for UI display
    /// </summary>
    public class FolderTypeInfo
    {
        public FolderType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string ColorClass { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] Features { get; set; } = Array.Empty<string>();
    }
}