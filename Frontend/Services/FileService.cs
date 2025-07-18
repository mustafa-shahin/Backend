using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Frontend.Interfaces;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Frontend.Services
{
    public class FileService : IFileService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IJSRuntime _jsRuntime;
        private readonly string _backendBaseUrl;
        private readonly SemaphoreSlim _downloadSemaphore;
        private readonly ConcurrentDictionary<int, FileDto> _fileCache;
        private readonly ConcurrentDictionary<string, PaginatedResult<FileDto>> _paginationCache;
        private readonly Timer _cacheCleanupTimer;

        public FileService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));

            _backendBaseUrl = GetBackendBaseUrl(httpClient);
            _downloadSemaphore = new SemaphoreSlim(5, 5);
            _fileCache = new ConcurrentDictionary<int, FileDto>();
            _paginationCache = new ConcurrentDictionary<string, PaginatedResult<FileDto>>();

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            _cacheCleanupTimer = new Timer(CleanupCache, null,
                TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        }

        private string GetBackendBaseUrl(HttpClient httpClient)
        {
            if (httpClient.BaseAddress != null)
            {
                return httpClient.BaseAddress.ToString().TrimEnd('/');
            }

            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
                ? "https://localhost:7206"
                : "https://api.domain.com";
        }

        #region Enhanced Core File Operations with Pagination

        public async Task<PaginatedResult<FileDto>> GetFilesAsync(
            int pageNumber = 1,
            int pageSize = 10,
            int? folderId = null,
            string? search = null,
            FileType? fileType = null,
            bool? isPublic = null,
            string sortBy = "CreatedAt",
            string sortDirection = "Desc")
        {
            try
            {
                // Validate and normalize parameters
                pageNumber = Math.Max(1, pageNumber);
                pageSize = Math.Clamp(pageSize, 1, 100);

                // Create cache key for pagination
                var cacheKey = CreatePaginationCacheKey(pageNumber, pageSize, folderId, search, fileType, isPublic, sortBy, sortDirection);

                // Check cache first (only for short-term caching)
                if (_paginationCache.TryGetValue(cacheKey, out var cachedResult))
                {
                    var cacheAge = DateTime.UtcNow - cachedResult.Data.FirstOrDefault()?.CreatedAt;
                    if (cacheAge?.TotalMinutes < 2) // Only cache for 2 minutes
                    {
                        return cachedResult;
                    }
                    _paginationCache.TryRemove(cacheKey, out _);
                }

                var queryParams = BuildQueryParameters(pageNumber, pageSize, folderId, search, fileType, isPublic, sortBy, sortDirection);
                var query = $"api/v1/file?{string.Join("&", queryParams)}";

                var response = await _httpClient.GetAsync(query);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to get files: {response.StatusCode} - {errorContent}");
                }

                var result = await response.Content.ReadFromJsonAsync<PaginatedResult<FileDto>>(_jsonOptions);

                if (result == null)
                {
                    return PaginatedResult<FileDto>.Empty(pageNumber, pageSize);
                }

                // Validate pagination data integrity
                if (result.TotalCount < 0 || result.PageNumber != pageNumber || result.PageSize != pageSize)
                {
                    throw new InvalidOperationException("Invalid pagination data received from server");
                }

                // Cache files individually and pagination result
                if (result.Data?.Any() == true)
                {
                    foreach (var file in result.Data)
                    {
                        _fileCache.TryAdd(file.Id, file);
                    }
                    _paginationCache.TryAdd(cacheKey, result);
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting files: {ex.Message}", ex);
            }
        }

        public async Task<PaginatedResult<FileDto>> SearchFilesAsync(FileSearchDto searchDto)
        {
            try
            {
                if (searchDto == null)
                    throw new ArgumentNullException(nameof(searchDto));

                // Validate and normalize search parameters
                searchDto.PageNumber = Math.Max(1, searchDto.PageNumber);
                searchDto.PageSize = Math.Clamp(searchDto.PageSize, 1, 100);

                var response = await _httpClient.PostAsJsonAsync("api/v1/file/search", searchDto, _jsonOptions);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to search files: {response.StatusCode} - {errorContent}");
                }

                var result = await response.Content.ReadFromJsonAsync<PaginatedResult<FileDto>>(_jsonOptions);

                if (result == null)
                {
                    return PaginatedResult<FileDto>.Empty(searchDto.PageNumber, searchDto.PageSize);
                }

                // Cache the search results
                if (result.Data?.Any() == true)
                {
                    foreach (var file in result.Data)
                    {
                        _fileCache.TryAdd(file.Id, file);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error searching files: {ex.Message}", ex);
            }
        }

        public async Task<PaginatedResult<FileDto>> GetFilesByFolderAsync(int? folderId, int pageNumber = 1, int pageSize = 10)
        {
            return await GetFilesAsync(pageNumber, pageSize, folderId);
        }

        public async Task<FileDto?> GetFileByIdAsync(int id)
        {
            try
            {
                if (_fileCache.TryGetValue(id, out var cachedFile))
                {
                    return cachedFile;
                }

                var response = await _httpClient.GetAsync($"api/v1/file/{id}");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to get file: {response.StatusCode} - {errorContent}");
                }

                var file = await response.Content.ReadFromJsonAsync<FileDto>(_jsonOptions);
                if (file != null)
                {
                    _fileCache.TryAdd(file.Id, file);
                }
                return file;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting file {id}: {ex.Message}", ex);
            }
        }

        #endregion

        #region File Upload Operations

        public async Task<FileUploadResultDto?> UploadFileAsync(FileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.File == null)
                    throw new ArgumentException("File is required");

                using var content = new MultipartFormDataContent();

                var fileContent = new StreamContent(uploadDto.File.OpenReadStream());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(uploadDto.File.ContentType);
                content.Add(fileContent, "File", uploadDto.File.Name);

                AddFormParameters(content, uploadDto);

                var response = await _httpClient.PostAsync("api/v1/file/upload", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new FileUploadResultDto
                    {
                        Success = false,
                        ErrorMessage = $"Upload failed: {response.StatusCode} - {errorContent}"
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<FileUploadResultDto>(_jsonOptions);

                if (result?.File != null)
                {
                    _fileCache.TryAdd(result.File.Id, result.File);
                    InvalidatePaginationCache();
                }

                return result;
            }
            catch (Exception ex)
            {
                return new FileUploadResultDto
                {
                    Success = false,
                    ErrorMessage = $"Error uploading file: {ex.Message}"
                };
            }
        }

        public async Task<BulkOperationResultDto> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.Files == null || !uploadDto.Files.Any())
                {
                    return new BulkOperationResultDto
                    {
                        TotalRequested = 0,
                        SuccessCount = 0,
                        FailureCount = 0
                    };
                }

                using var content = new MultipartFormDataContent();

                foreach (var file in uploadDto.Files)
                {
                    var fileContent = new StreamContent(file.OpenReadStream());
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                    content.Add(fileContent, "Files", file.Name);
                }

                AddMultipleUploadParameters(content, uploadDto);

                var response = await _httpClient.PostAsync("api/v1/file/upload/multiple", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new BulkOperationResultDto
                    {
                        TotalRequested = uploadDto.Files.Count,
                        SuccessCount = 0,
                        FailureCount = uploadDto.Files.Count,
                        Errors = new List<BulkOperationErrorDto>
                        {
                            new() { ErrorMessage = $"Upload failed: {response.StatusCode} - {errorContent}" }
                        }
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<BulkOperationResultDto>(_jsonOptions);

                if (result?.SuccessfulFiles != null)
                {
                    foreach (var file in result.SuccessfulFiles)
                    {
                        _fileCache.TryAdd(file.Id, file);
                    }
                    InvalidatePaginationCache();
                }

                return result ?? new BulkOperationResultDto();
            }
            catch (Exception ex)
            {
                return new BulkOperationResultDto
                {
                    TotalRequested = uploadDto?.Files?.Count ?? 0,
                    SuccessCount = 0,
                    FailureCount = uploadDto?.Files?.Count ?? 0,
                    Errors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = $"Error uploading files: {ex.Message}" }
                    }
                };
            }
        }

        #endregion

        #region File Management Operations

        public async Task<FileDto?> UpdateFileAsync(int id, UpdateFileDto updateDto)
        {
            try
            {
                if (updateDto == null)
                    throw new ArgumentNullException(nameof(updateDto));

                var response = await _httpClient.PutAsJsonAsync($"api/v1/file/{id}", updateDto, _jsonOptions);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to update file: {response.StatusCode} - {errorContent}");
                }

                var file = await response.Content.ReadFromJsonAsync<FileDto>(_jsonOptions);
                if (file != null)
                {
                    _fileCache.AddOrUpdate(file.Id, file, (key, oldValue) => file);
                    InvalidatePaginationCache();
                }
                return file;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating file {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteFileAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/v1/file/{id}");
                var success = response.IsSuccessStatusCode;

                if (success)
                {
                    _fileCache.TryRemove(id, out _);
                    InvalidatePaginationCache();
                }

                return success;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting file {id}: {ex.Message}", ex);
            }
        }

        public async Task<BulkOperationResultDto> DeleteMultipleFilesAsync(List<int> fileIds)
        {
            try
            {
                if (fileIds?.Any() != true)
                {
                    return new BulkOperationResultDto();
                }

                var bulkDto = new { FileIds = fileIds };
                var response = await _httpClient.PostAsJsonAsync("api/v1/file/bulk-delete", bulkDto, _jsonOptions);

                BulkOperationResultDto result;
                if (response.IsSuccessStatusCode)
                {
                    result = await response.Content.ReadFromJsonAsync<BulkOperationResultDto>(_jsonOptions)
                        ?? new BulkOperationResultDto();

                    if (result.IsCompleteSuccess || result.IsPartialSuccess)
                    {
                        foreach (var fileId in fileIds)
                        {
                            _fileCache.TryRemove(fileId, out _);
                        }
                        InvalidatePaginationCache();
                    }
                }
                else
                {
                    result = new BulkOperationResultDto
                    {
                        TotalRequested = fileIds.Count,
                        FailureCount = fileIds.Count,
                        Errors = new List<BulkOperationErrorDto>
                        {
                            new() { ErrorMessage = $"Delete failed: {response.StatusCode}" }
                        }
                    };
                }

                return result;
            }
            catch (Exception ex)
            {
                return new BulkOperationResultDto
                {
                    TotalRequested = fileIds?.Count ?? 0,
                    FailureCount = fileIds?.Count ?? 0,
                    Errors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = $"Error deleting files: {ex.Message}" }
                    }
                };
            }
        }

        public async Task<FileDto?> MoveFileAsync(MoveFileDto moveDto)
        {
            try
            {
                if (moveDto == null)
                    throw new ArgumentNullException(nameof(moveDto));

                var response = await _httpClient.PostAsJsonAsync("api/v1/file/move", moveDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    var file = await response.Content.ReadFromJsonAsync<FileDto>(_jsonOptions);
                    if (file != null)
                    {
                        _fileCache.AddOrUpdate(file.Id, file, (key, oldValue) => file);
                        InvalidatePaginationCache();
                    }
                    return file;
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error moving file: {ex.Message}", ex);
            }
        }

        public async Task<FileDto?> CopyFileAsync(CopyFileDto copyDto)
        {
            try
            {
                if (copyDto == null)
                    throw new ArgumentNullException(nameof(copyDto));

                var response = await _httpClient.PostAsJsonAsync("api/v1/file/copy", copyDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    var file = await response.Content.ReadFromJsonAsync<FileDto>(_jsonOptions);
                    if (file != null)
                    {
                        _fileCache.TryAdd(file.Id, file);
                        InvalidatePaginationCache();
                    }
                    return file;
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error copying file: {ex.Message}", ex);
            }
        }

        #endregion

        #region File Access and Preview

        public async Task<FilePreviewDto> GetFilePreviewAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/v1/file/{id}/preview");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<FilePreviewDto>(_jsonOptions);
                    return result ?? new FilePreviewDto();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get file preview: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting file preview {id}: {ex.Message}", ex);
            }
        }

        public async Task<List<FileDto>> GetRecentFilesAsync(int count = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/v1/file/recent?count={Math.Clamp(count, 1, 50)}");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<FileDto>>(_jsonOptions);

                    if (result != null)
                    {
                        foreach (var file in result)
                        {
                            _fileCache.TryAdd(file.Id, file);
                        }
                    }

                    return result ?? new List<FileDto>();
                }
                return new List<FileDto>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting recent files: {ex.Message}", ex);
            }
        }

        public async Task<Dictionary<string, object>> GetFileStatisticsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/v1/file/statistics");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
                    return result ?? new Dictionary<string, object>();
                }
                return new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting file statistics: {ex.Message}", ex);
            }
        }

        public async Task<bool> GenerateThumbnailAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/v1/file/{id}/generate-thumbnail", null);
                var success = response.IsSuccessStatusCode;

                if (success)
                {
                    _fileCache.TryRemove(id, out _);
                }

                return success;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating thumbnail for file {id}: {ex.Message}", ex);
            }
        }

        #endregion

        #region Download and Streaming

        public async Task<string> GenerateDownloadTokenAsync(int fileId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/v1/file/{fileId}/download-token", null);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    return result.GetProperty("token").GetString() ?? string.Empty;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to generate download token: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating download token: {ex.Message}", ex);
            }
        }

        public async Task DownloadFileAsync(int id)
        {
            await _downloadSemaphore.WaitAsync();
            try
            {
                var fileInfo = await GetFileByIdAsync(id);
                if (fileInfo == null)
                {
                    throw new Exception("File not found");
                }

                string downloadUrl;
                if (fileInfo.IsPublic)
                {
                    downloadUrl = fileInfo.Urls.DirectAccess ?? fileInfo.Urls.Download;
                }
                else
                {
                    var token = await GenerateDownloadTokenAsync(id);
                    if (!string.IsNullOrEmpty(token))
                    {
                        downloadUrl = $"{_backendBaseUrl}/api/v1/file/download/{token}";
                    }
                    else
                    {
                        throw new Exception("Failed to generate download token");
                    }
                }

                await _jsRuntime.InvokeVoidAsync("downloadFileWithAuth", downloadUrl, fileInfo.OriginalFileName);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error downloading file {id}: {ex.Message}", ex);
            }
            finally
            {
                _downloadSemaphore.Release();
            }
        }

        public async Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/v1/file/{id}/download");
                if (response.IsSuccessStatusCode)
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

                    var fileName = "file";
                    if (response.Content.Headers.ContentDisposition?.FileName != null)
                    {
                        fileName = response.Content.Headers.ContentDisposition.FileName.Trim('"');
                    }

                    return (stream, contentType, fileName);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get file stream: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting file stream {id}: {ex.Message}", ex);
            }
        }

        public async Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/v1/file/{id}/thumbnail");
                if (response.IsSuccessStatusCode)
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";
                    var fileName = $"thumbnail_{id}";
                    return (stream, contentType, fileName);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new FileNotFoundException("Thumbnail not found");
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get thumbnail: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting thumbnail {id}: {ex.Message}", ex);
            }
        }

        #endregion

        #region Video and Audio Streaming

        public string GetStreamingUrl(int fileId)
        {
            // Always return the streaming endpoint for video/audio files
            return $"{_backendBaseUrl}/api/v1/file/{fileId}/stream";
        }

        public async Task<string> GetStreamingUrlWithTokenAsync(int fileId)
        {
            try
            {
                var fileInfo = await GetFileByIdAsync(fileId);
                if (fileInfo?.IsPublic == true)
                {
                    // Public files don't need tokens
                    return GetStreamingUrl(fileId);
                }

                // Generate token for private files
                var token = await GenerateDownloadTokenAsync(fileId);
                if (!string.IsNullOrEmpty(token))
                {
                    return $"{_backendBaseUrl}/api/v1/file/stream/{token}";
                }

                throw new Exception("Failed to generate streaming token");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting streaming URL with token for file {fileId}: {ex.Message}", ex);
            }
        }

        #endregion

        #region Bulk Operations

        public async Task<BulkOperationResultDto> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto)
        {
            try
            {
                if (fileIds?.Any() != true || updateDto == null)
                {
                    return new BulkOperationResultDto();
                }

                var bulkDto = new { FileIds = fileIds, UpdateDto = updateDto };
                var response = await _httpClient.PostAsJsonAsync("api/v1/file/bulk-update", bulkDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<BulkOperationResultDto>(_jsonOptions)
                        ?? new BulkOperationResultDto();

                    if (result.IsCompleteSuccess || result.IsPartialSuccess)
                    {
                        foreach (var fileId in fileIds)
                        {
                            _fileCache.TryRemove(fileId, out _);
                        }
                        InvalidatePaginationCache();
                    }

                    return result;
                }

                return new BulkOperationResultDto
                {
                    TotalRequested = fileIds.Count,
                    FailureCount = fileIds.Count,
                    Errors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = $"Bulk update failed: {response.StatusCode}" }
                    }
                };
            }
            catch (Exception ex)
            {
                return new BulkOperationResultDto
                {
                    TotalRequested = fileIds?.Count ?? 0,
                    FailureCount = fileIds?.Count ?? 0,
                    Errors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = $"Error bulk updating files: {ex.Message}" }
                    }
                };
            }
        }

        public async Task<BulkOperationResultDto> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId)
        {
            try
            {
                if (fileIds?.Any() != true)
                {
                    return new BulkOperationResultDto();
                }

                var bulkDto = new { FileIds = fileIds, DestinationFolderId = destinationFolderId };
                var response = await _httpClient.PostAsJsonAsync("api/v1/file/bulk-move", bulkDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<BulkOperationResultDto>(_jsonOptions)
                        ?? new BulkOperationResultDto();

                    if (result.IsCompleteSuccess || result.IsPartialSuccess)
                    {
                        foreach (var fileId in fileIds)
                        {
                            _fileCache.TryRemove(fileId, out _);
                        }
                        InvalidatePaginationCache();
                    }

                    return result;
                }

                return new BulkOperationResultDto
                {
                    TotalRequested = fileIds.Count,
                    FailureCount = fileIds.Count,
                    Errors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = $"Bulk move failed: {response.StatusCode}" }
                    }
                };
            }
            catch (Exception ex)
            {
                return new BulkOperationResultDto
                {
                    TotalRequested = fileIds?.Count ?? 0,
                    FailureCount = fileIds?.Count ?? 0,
                    Errors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = $"Error bulk moving files: {ex.Message}" }
                    }
                };
            }
        }

        public async Task<BulkOperationResultDto> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId)
        {
            try
            {
                if (fileIds?.Any() != true)
                {
                    return new BulkOperationResultDto();
                }

                var bulkDto = new { FileIds = fileIds, DestinationFolderId = destinationFolderId };
                var response = await _httpClient.PostAsJsonAsync("api/v1/file/bulk-copy", bulkDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<BulkOperationResultDto>(_jsonOptions)
                        ?? new BulkOperationResultDto();

                    if (result.SuccessfulFiles != null)
                    {
                        foreach (var file in result.SuccessfulFiles)
                        {
                            _fileCache.TryAdd(file.Id, file);
                        }
                        InvalidatePaginationCache();
                    }

                    return result;
                }

                return new BulkOperationResultDto
                {
                    TotalRequested = fileIds.Count,
                    FailureCount = fileIds.Count,
                    Errors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = $"Bulk copy failed: {response.StatusCode}" }
                    }
                };
            }
            catch (Exception ex)
            {
                return new BulkOperationResultDto
                {
                    TotalRequested = fileIds?.Count ?? 0,
                    FailureCount = fileIds?.Count ?? 0,
                    Errors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = $"Error bulk copying files: {ex.Message}" }
                    }
                };
            }
        }

        #endregion

        #region Utility Methods

        public string GetFileUrl(int fileId)
        {
            // Check cache for file info to determine if it's streamable
            if (_fileCache.TryGetValue(fileId, out var cachedFile))
            {
                // For video and audio files, return streaming URL
                if (IsStreamableFileType(cachedFile))
                {
                    return GetStreamingUrl(fileId);
                }

                // For other files, return download URL if available
                if (!string.IsNullOrEmpty(cachedFile.Urls.Download))
                {
                    return cachedFile.Urls.Download;
                }
            }

            // Default fallback to download endpoint
            return $"{_backendBaseUrl}/api/v1/file/{fileId}/download";
        }

        public string GetThumbnailUrl(int fileId)
        {
            if (_fileCache.TryGetValue(fileId, out var cachedFile) && !string.IsNullOrEmpty(cachedFile.Urls.Thumbnail))
            {
                return cachedFile.Urls.Thumbnail;
            }

            return $"{_backendBaseUrl}/api/v1/file/{fileId}/thumbnail";
        }

        public string GetPreviewUrl(int fileId)
        {
            if (_fileCache.TryGetValue(fileId, out var cachedFile) && !string.IsNullOrEmpty(cachedFile.Urls.Preview))
            {
                return cachedFile.Urls.Preview;
            }

            return $"{_backendBaseUrl}/api/v1/file/{fileId}/preview";
        }

        public string FormatFileSize(long bytes)
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

        public string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return duration.ToString(@"h\:mm\:ss");
            }
            return duration.ToString(@"m\:ss");
        }

        #endregion

        #region Diagnostics and Integrity

        public async Task<bool> VerifyFileIntegrityAsync(int fileId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/v1/file/{fileId}/verify-integrity", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error verifying file integrity {fileId}: {ex.Message}", ex);
            }
        }

        public async Task<object> GetFileDiagnosticInfoAsync(int fileId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/v1/file/{fileId}/diagnostic");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<object>(_jsonOptions);
                    return result ?? new object();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get diagnostic info: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting diagnostic info for file {fileId}: {ex.Message}", ex);
            }
        }

        #endregion

        #region Cache and Performance

        public async Task ClearCacheAsync()
        {
            await Task.Run(() =>
            {
                _fileCache.Clear();
                _paginationCache.Clear();
            });
        }

        public async Task PreloadFilesAsync(List<int> fileIds)
        {
            if (fileIds?.Any() != true)
                return;

            var tasks = fileIds.Where(id => !_fileCache.ContainsKey(id))
                              .Select(async id =>
                              {
                                  try
                                  {
                                      await GetFileByIdAsync(id);
                                  }
                                  catch
                                  {
                                      // Ignore errors during preloading
                                  }
                              });

            await Task.WhenAll(tasks);
        }

        #endregion

        #region Private Helper Methods

        private bool IsStreamableFileType(FileDto file)
        {
            if (file.FileType == Backend.CMS.Domain.Enums.FileType.Video)
            {
                return IsStreamableVideo(file.ContentType);
            }

            if (file.FileType == Backend.CMS.Domain.Enums.FileType.Audio)
            {
                return IsStreamableAudio(file.ContentType);
            }

            return false;
        }

        private static bool IsStreamableVideo(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            var streamableTypes = new[]
            {
                "video/mp4",
                "video/webm",
                "video/ogg",
                "video/quicktime",
                "video/x-msvideo", // AVI
                "video/x-ms-wmv",  // WMV
                "video/x-flv",     // FLV
                "video/3gpp",      // 3GP
                "video/x-matroska" // MKV
            };

            return streamableTypes.Any(type => contentType.Contains(type, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsStreamableAudio(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            var streamableTypes = new[]
            {
                "audio/mpeg",
                "audio/mp4",
                "audio/ogg",
                "audio/wav",
                "audio/webm",
                "audio/x-ms-wma",
                "audio/x-wav"
            };

            return streamableTypes.Any(type => contentType.Contains(type, StringComparison.OrdinalIgnoreCase));
        }

        private List<string> BuildQueryParameters(int pageNumber, int pageSize, int? folderId, string? search,
            FileType? fileType, bool? isPublic, string sortBy, string sortDirection)
        {
            var queryParams = new List<string>
            {
                $"pageNumber={pageNumber}",
                $"pageSize={pageSize}",
                $"sortBy={Uri.EscapeDataString(sortBy)}",
                $"sortDirection={Uri.EscapeDataString(sortDirection)}"
            };

            if (folderId.HasValue)
                queryParams.Add($"folderId={folderId}");

            if (!string.IsNullOrEmpty(search))
                queryParams.Add($"search={Uri.EscapeDataString(search)}");

            if (fileType.HasValue)
                queryParams.Add($"fileType={fileType}");

            if (isPublic.HasValue)
                queryParams.Add($"isPublic={isPublic.Value.ToString().ToLower()}");

            return queryParams;
        }

        private string CreatePaginationCacheKey(int pageNumber, int pageSize, int? folderId, string? search,
            FileType? fileType, bool? isPublic, string sortBy, string sortDirection)
        {
            return $"files_{pageNumber}_{pageSize}_{folderId}_{search}_{fileType}_{isPublic}_{sortBy}_{sortDirection}";
        }

        private void AddFormParameters(MultipartFormDataContent content, FileUploadDto uploadDto)
        {
            if (!string.IsNullOrEmpty(uploadDto.Description))
                content.Add(new StringContent(uploadDto.Description), "Description");

            if (!string.IsNullOrEmpty(uploadDto.Alt))
                content.Add(new StringContent(uploadDto.Alt), "Alt");

            if (uploadDto.FolderId.HasValue)
                content.Add(new StringContent(uploadDto.FolderId.Value.ToString()), "FolderId");

            content.Add(new StringContent(uploadDto.IsPublic.ToString().ToLower()), "IsPublic");
            content.Add(new StringContent(uploadDto.GenerateThumbnail.ToString().ToLower()), "GenerateThumbnail");
            content.Add(new StringContent(uploadDto.ProcessImmediately.ToString().ToLower()), "ProcessImmediately");

            if (uploadDto.Tags?.Any() == true)
            {
                foreach (var tag in uploadDto.Tags)
                {
                    content.Add(new StringContent(tag.Value?.ToString() ?? ""), $"Tags[{tag.Key}]");
                }
            }
        }

        private void AddMultipleUploadParameters(MultipartFormDataContent content, MultipleFileUploadDto uploadDto)
        {
            if (uploadDto.FolderId.HasValue)
                content.Add(new StringContent(uploadDto.FolderId.Value.ToString()), "FolderId");

            content.Add(new StringContent(uploadDto.IsPublic.ToString().ToLower()), "IsPublic");
            content.Add(new StringContent(uploadDto.GenerateThumbnails.ToString().ToLower()), "GenerateThumbnails");
            content.Add(new StringContent(uploadDto.ProcessImmediately.ToString().ToLower()), "ProcessImmediately");
            content.Add(new StringContent(uploadDto.ProcessInParallel.ToString().ToLower()), "ProcessInParallel");
        }

        private void InvalidatePaginationCache()
        {
            _paginationCache.Clear();
        }

        private void CleanupCache(object? state)
        {
            try
            {
                const int maxCacheSize = 1000;

                if (_fileCache.Count > maxCacheSize)
                {
                    var itemsToRemove = _fileCache.Count / 5;
                    var keysToRemove = _fileCache.Keys.Take(itemsToRemove).ToList();

                    foreach (var key in keysToRemove)
                    {
                        _fileCache.TryRemove(key, out _);
                    }
                }

                // Clean pagination cache (keep only recent entries)
                if (_paginationCache.Count > 50)
                {
                    _paginationCache.Clear();
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #endregion

        public async Task<List<FileDto>> GetFilesForEntityAsync(string entityType, int entityId, FileType? fileType = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0)
                {
                    return new List<FileDto>();
                }

                // Create cache key for entity files
                var cacheKey = $"entity_files_{entityType}_{entityId}_{fileType}";

                // Check if we have a recent cached result
                if (_entityFileCache.TryGetValue(cacheKey, out var cachedFiles))
                {
                    var cacheAge = DateTime.UtcNow - cachedFiles.CachedAt;
                    if (cacheAge.TotalMinutes < 5) // Cache for 5 minutes
                    {
                        return cachedFiles.Files;
                    }
                    _entityFileCache.TryRemove(cacheKey, out _);
                }

                // Use the existing API endpoint for entity files
                var queryParams = new List<string>
                {
                    $"entityType={Uri.EscapeDataString(entityType)}",
                    $"entityId={entityId}"
                };

                if (fileType.HasValue)
                {
                    queryParams.Add($"fileType={fileType}");
                }

                var query = $"api/v1/file/entity?{string.Join("&", queryParams)}";
                var response = await _httpClient.GetAsync(query);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Failed to get entity files: {response.StatusCode} - {errorContent}");
                }

                var entityFiles = await response.Content.ReadFromJsonAsync<List<FileDto>>(_jsonOptions);
                var files = entityFiles ?? new List<FileDto>();

                // Cache individual files and the entity result
                foreach (var file in files)
                {
                    _fileCache.TryAdd(file.Id, file);
                }

                _entityFileCache.TryAdd(cacheKey, new EntityFilesCacheEntry
                {
                    Files = files,
                    CachedAt = DateTime.UtcNow
                });

                return files;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting files for entity {entityType}:{entityId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Upload file and link it to a specific entity with immediate association
        /// </summary>
        /// <param name="uploadDto">Upload data with entity information</param>
        /// <returns>Upload result with file DTO</returns>
        public async Task<FileUploadResultDto?> UploadFileForEntityAsync(FileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.File == null)
                    throw new ArgumentException("File is required");

                // Ensure entity information is properly set
                if (!string.IsNullOrEmpty(uploadDto.EntityType) && uploadDto.EntityId.HasValue)
                {
                    uploadDto.Tags = uploadDto.Tags ?? new Dictionary<string, object>();
                    uploadDto.Tags["EntityType"] = uploadDto.EntityType;
                    uploadDto.Tags["EntityId"] = uploadDto.EntityId.Value.ToString();
                }

                using var content = new MultipartFormDataContent();

                var fileContent = new StreamContent(uploadDto.File.OpenReadStream());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(uploadDto.File.ContentType);
                content.Add(fileContent, "File", uploadDto.File.Name);

                // Add all form parameters including entity information
                AddFormParametersWithEntity(content, uploadDto);

                var response = await _httpClient.PostAsync("api/v1/file/upload-for-entity", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new FileUploadResultDto
                    {
                        Success = false,
                        ErrorMessage = $"Upload failed: {response.StatusCode} - {errorContent}"
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<FileUploadResultDto>(_jsonOptions);

                if (result?.File != null)
                {
                    // Cache the uploaded file
                    _fileCache.TryAdd(result.File.Id, result.File);

                    // Invalidate entity file cache for this entity
                    InvalidateEntityFileCache(uploadDto.EntityType!, uploadDto.EntityId!.Value);

                    // Invalidate general pagination cache
                    InvalidatePaginationCache();
                }

                return result;
            }
            catch (Exception ex)
            {
                return new FileUploadResultDto
                {
                    Success = false,
                    ErrorMessage = $"Error uploading file for entity: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Upload multiple files and link them to a specific entity
        /// </summary>
        /// <param name="uploadDto">Multiple upload data with entity information</param>
        /// <returns>Bulk operation result</returns>
        public async Task<BulkOperationResultDto> UploadMultipleFilesForEntityAsync(MultipleFileUploadDto uploadDto)
        {
            try
            {
                if (uploadDto?.Files == null || !uploadDto.Files.Any())
                {
                    return new BulkOperationResultDto
                    {
                        TotalRequested = 0,
                        SuccessCount = 0,
                        FailureCount = 0
                    };
                }

                using var content = new MultipartFormDataContent();

                foreach (var file in uploadDto.Files)
                {
                    var fileContent = new StreamContent(file.OpenReadStream());
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                    content.Add(fileContent, "Files", file.Name);
                }

                // Add entity parameters
                AddMultipleUploadParametersWithEntity(content, uploadDto);

                var response = await _httpClient.PostAsync("api/v1/file/upload-multiple-for-entity", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new BulkOperationResultDto
                    {
                        TotalRequested = uploadDto.Files.Count,
                        SuccessCount = 0,
                        FailureCount = uploadDto.Files.Count,
                        Errors = new List<BulkOperationErrorDto>
                        {
                            new() { ErrorMessage = $"Upload failed: {response.StatusCode} - {errorContent}" }
                        }
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<BulkOperationResultDto>(_jsonOptions);

                if (result?.SuccessfulFiles != null)
                {
                    // Cache uploaded files
                    foreach (var file in result.SuccessfulFiles)
                    {
                        _fileCache.TryAdd(file.Id, file);
                    }

                    // Invalidate caches
                    if (!string.IsNullOrEmpty(uploadDto.EntityType) && uploadDto.EntityId.HasValue)
                    {
                        InvalidateEntityFileCache(uploadDto.EntityType, uploadDto.EntityId.Value);
                    }
                    InvalidatePaginationCache();
                }

                return result ?? new BulkOperationResultDto();
            }
            catch (Exception ex)
            {
                return new BulkOperationResultDto
                {
                    TotalRequested = uploadDto?.Files?.Count ?? 0,
                    SuccessCount = 0,
                    FailureCount = uploadDto?.Files?.Count ?? 0,
                    Errors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = $"Error uploading files for entity: {ex.Message}" }
                    }
                };
            }
        }

        /// <summary>
        /// Delete all files linked to a specific entity
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="entityId">Entity identifier</param>
        /// <returns>Bulk operation result</returns>
        public async Task<BulkOperationResultDto> DeleteFilesForEntityAsync(string entityType, int entityId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0)
                {
                    return new BulkOperationResultDto();
                }

                var deleteDto = new
                {
                    EntityType = entityType,
                    EntityId = entityId
                };

                var response = await _httpClient.PostAsJsonAsync("api/v1/file/delete-for-entity", deleteDto, _jsonOptions);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new BulkOperationResultDto
                    {
                        TotalRequested = 0,
                        FailureCount = 0,
                        Errors = new List<BulkOperationErrorDto>
                        {
                            new() { ErrorMessage = $"Delete failed: {response.StatusCode} - {errorContent}" }
                        }
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<BulkOperationResultDto>(_jsonOptions);

                if (result?.IsCompleteSuccess == true || result?.IsPartialSuccess == true)
                {
                    // Remove files from cache and invalidate entity cache
                    InvalidateEntityFileCache(entityType, entityId);
                    InvalidatePaginationCache();
                }

                return result ?? new BulkOperationResultDto();
            }
            catch (Exception ex)
            {
                return new BulkOperationResultDto
                {
                    TotalRequested = 0,
                    FailureCount = 0,
                    Errors = new List<BulkOperationErrorDto>
                    {
                        new() { ErrorMessage = $"Error deleting files for entity: {ex.Message}" }
                    }
                };
            }
        }
        public async Task<int> CountFilesForEntityAsync(string entityType, int entityId, FileType? fileType = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0)
                {
                    return 0;
                }

                var queryParams = new List<string>
                {
                    $"entityType={Uri.EscapeDataString(entityType)}",
                    $"entityId={entityId}"
                };

                if (fileType.HasValue)
                {
                    queryParams.Add($"fileType={fileType}");
                }

                var query = $"api/v1/file/entity/count?{string.Join("&", queryParams)}";
                var response = await _httpClient.GetAsync(query);

                if (!response.IsSuccessStatusCode)
                {
                    return 0;
                }

                var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                return result.GetProperty("count").GetInt32();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error counting files for entity {entityType}:{entityId}: {ex.Message}", ex);
            }
        }


        #region Enhanced Private Methods for Entity Support

        // Entity file cache for better performance
        private readonly ConcurrentDictionary<string, EntityFilesCacheEntry> _entityFileCache = new();

        private class EntityFilesCacheEntry
        {
            public List<FileDto> Files { get; set; } = new();
            public DateTime CachedAt { get; set; }
        }

        private void AddFormParametersWithEntity(MultipartFormDataContent content, FileUploadDto uploadDto)
        {
            // Add standard form parameters
            AddFormParameters(content, uploadDto);

            // Add entity-specific parameters
            if (!string.IsNullOrEmpty(uploadDto.EntityType))
                content.Add(new StringContent(uploadDto.EntityType), "EntityType");

            if (uploadDto.EntityId.HasValue)
                content.Add(new StringContent(uploadDto.EntityId.Value.ToString()), "EntityId");
        }

        private void AddMultipleUploadParametersWithEntity(MultipartFormDataContent content, MultipleFileUploadDto uploadDto)
        {
            // Add standard parameters
            AddMultipleUploadParameters(content, uploadDto);

            // Add entity-specific parameters
            if (!string.IsNullOrEmpty(uploadDto.EntityType))
                content.Add(new StringContent(uploadDto.EntityType), "EntityType");

            if (uploadDto.EntityId.HasValue)
                content.Add(new StringContent(uploadDto.EntityId.Value.ToString()), "EntityId");
        }

        private void InvalidateEntityFileCache(string entityType, int entityId)
        {
            var keysToRemove = _entityFileCache.Keys
                .Where(key => key.Contains($"entity_files_{entityType}_{entityId}"))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _entityFileCache.TryRemove(key, out _);
            }
        }

        private void CleanupEntityFileCache(object? state)
        {
            try
            {
                const int maxCacheSize = 500;

                if (_entityFileCache.Count > maxCacheSize)
                {
                    var expiredEntries = _entityFileCache
                        .Where(kvp => DateTime.UtcNow - kvp.Value.CachedAt > TimeSpan.FromMinutes(10))
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in expiredEntries)
                    {
                        _entityFileCache.TryRemove(key, out _);
                    }

                    // If still too many, remove oldest entries
                    if (_entityFileCache.Count > maxCacheSize)
                    {
                        var oldestEntries = _entityFileCache
                            .OrderBy(kvp => kvp.Value.CachedAt)
                            .Take(_entityFileCache.Count - maxCacheSize)
                            .Select(kvp => kvp.Key)
                            .ToList();

                        foreach (var key in oldestEntries)
                        {
                            _entityFileCache.TryRemove(key, out _);
                        }
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #endregion

        #region Updated Dispose Method

        public void Dispose()
        {
            _downloadSemaphore?.Dispose();
            _cacheCleanupTimer?.Dispose();
            _fileCache?.Clear();
            _paginationCache?.Clear();
            _entityFileCache?.Clear();
        }

        #endregion
    }
}