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
    /// <summary>
    /// Frontend file service implementation with file management
    /// </summary>
    public class FileService : IFileService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IJSRuntime _jsRuntime;
        private readonly string _backendBaseUrl;
        private readonly SemaphoreSlim _downloadSemaphore;
        private readonly ConcurrentDictionary<int, FileDto> _fileCache;
        private readonly Timer _cacheCleanupTimer;

        public FileService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));

            // Extract backend base URL from HttpClient configuration
            _backendBaseUrl = GetBackendBaseUrl(httpClient);

            _downloadSemaphore = new SemaphoreSlim(5, 5); // Limit concurrent downloads
            _fileCache = new ConcurrentDictionary<int, FileDto>();

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            // Clean cache every 10 minutes
            _cacheCleanupTimer = new Timer(CleanupCache, null,
                TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        }

        private string GetBackendBaseUrl(HttpClient httpClient)
        {
            // Try to get from HttpClient base address
            if (httpClient.BaseAddress != null)
            {
                return httpClient.BaseAddress.ToString().TrimEnd('/');
            }

            // Fallback based on environment
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
                ? "https://localhost:7206"  // Development backend URL
                : "https://api.domain.com"; // Production backend URL
        }

        #region Core File Operations with Pagination

        public async Task<PagedResult<FileDto>> GetFilesAsync(
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
                var queryParams = new List<string>
                {
                    $"pageNumber={pageNumber}",
                    $"pageSize={Math.Clamp(pageSize, 1, 100)}", // Ensure page size is within bounds
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

                var query = $"api/v1/file?{string.Join("&", queryParams)}";

                var response = await _httpClient.GetAsync(query);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<FileDto>>(_jsonOptions);

                    // Cache the files for performance
                    if (result?.Data != null)
                    {
                        foreach (var file in result.Data)
                        {
                            _fileCache.TryAdd(file.Id, file);
                        }
                    }

                    return result ?? PagedResult<FileDto>.Empty(pageNumber, pageSize);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get files: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting files: {ex.Message}", ex);
            }
        }

        public async Task<PagedResult<FileDto>> SearchFilesAsync(FileSearchDto searchDto)
        {
            try
            {
                if (searchDto == null)
                    throw new ArgumentNullException(nameof(searchDto));

                // Ensure pagination parameters are valid
                searchDto.PageNumber = Math.Max(1, searchDto.PageNumber);
                searchDto.PageSize = Math.Clamp(searchDto.PageSize, 1, 100);

                var response = await _httpClient.PostAsJsonAsync("api/v1/file/search", searchDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<FileDto>>(_jsonOptions);

                    // Cache the files
                    if (result?.Data != null)
                    {
                        foreach (var file in result.Data)
                        {
                            _fileCache.TryAdd(file.Id, file);
                        }
                    }

                    return result ?? PagedResult<FileDto>.Empty(searchDto.PageNumber, searchDto.PageSize);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to search files: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error searching files: {ex.Message}", ex);
            }
        }

        public async Task<PagedResult<FileDto>> GetFilesByFolderAsync(int? folderId, int pageNumber = 1, int pageSize = 10)
        {
            return await GetFilesAsync(pageNumber, pageSize, folderId);
        }

        public async Task<FileDto?> GetFileByIdAsync(int id)
        {
            try
            {
                // Check cache first
                if (_fileCache.TryGetValue(id, out var cachedFile))
                {
                    return cachedFile;
                }

                var response = await _httpClient.GetAsync($"api/v1/file/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var file = await response.Content.ReadFromJsonAsync<FileDto>(_jsonOptions);
                    if (file != null)
                    {
                        _fileCache.TryAdd(file.Id, file);
                    }
                    return file;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get file: {response.StatusCode} - {errorContent}");
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

                // Add the file
                var fileContent = new StreamContent(uploadDto.File.OpenReadStream());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(uploadDto.File.ContentType);
                content.Add(fileContent, "File", uploadDto.File.Name);

                // Add other properties
                if (!string.IsNullOrEmpty(uploadDto.Description))
                    content.Add(new StringContent(uploadDto.Description), "Description");

                if (!string.IsNullOrEmpty(uploadDto.Alt))
                    content.Add(new StringContent(uploadDto.Alt), "Alt");

                if (uploadDto.FolderId.HasValue)
                    content.Add(new StringContent(uploadDto.FolderId.Value.ToString()), "FolderId");

                content.Add(new StringContent(uploadDto.IsPublic.ToString().ToLower()), "IsPublic");
                content.Add(new StringContent(uploadDto.GenerateThumbnail.ToString().ToLower()), "GenerateThumbnail");
                content.Add(new StringContent(uploadDto.ProcessImmediately.ToString().ToLower()), "ProcessImmediately");

                // Add tags if any
                if (uploadDto.Tags?.Any() == true)
                {
                    foreach (var tag in uploadDto.Tags)
                    {
                        content.Add(new StringContent(tag.Value?.ToString() ?? ""), $"Tags[{tag.Key}]");
                    }
                }

                var response = await _httpClient.PostAsync("api/v1/file/upload", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<FileUploadResultDto>(_jsonOptions);

                    // Cache the uploaded file
                    if (result?.File != null)
                    {
                        _fileCache.TryAdd(result.File.Id, result.File);
                    }

                    return result;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return new FileUploadResultDto
                {
                    Success = false,
                    ErrorMessage = $"Upload failed: {response.StatusCode} - {errorContent}"
                };
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

                // Add files
                foreach (var file in uploadDto.Files)
                {
                    var fileContent = new StreamContent(file.OpenReadStream());
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                    content.Add(fileContent, "Files", file.Name);
                }

                // Add other properties
                if (uploadDto.FolderId.HasValue)
                    content.Add(new StringContent(uploadDto.FolderId.Value.ToString()), "FolderId");

                content.Add(new StringContent(uploadDto.IsPublic.ToString().ToLower()), "IsPublic");
                content.Add(new StringContent(uploadDto.GenerateThumbnails.ToString().ToLower()), "GenerateThumbnails");
                content.Add(new StringContent(uploadDto.ProcessImmediately.ToString().ToLower()), "ProcessImmediately");
                content.Add(new StringContent(uploadDto.ProcessInParallel.ToString().ToLower()), "ProcessInParallel");

                var response = await _httpClient.PostAsync("api/v1/file/upload/multiple", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<BulkOperationResultDto>(_jsonOptions);

                    // Cache successful files
                    if (result?.SuccessfulFiles != null)
                    {
                        foreach (var file in result.SuccessfulFiles)
                        {
                            _fileCache.TryAdd(file.Id, file);
                        }
                    }

                    return result ?? new BulkOperationResultDto();
                }

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
                if (response.IsSuccessStatusCode)
                {
                    var file = await response.Content.ReadFromJsonAsync<FileDto>(_jsonOptions);
                    if (file != null)
                    {
                        // Update cache
                        _fileCache.AddOrUpdate(file.Id, file, (key, oldValue) => file);
                    }
                    return file;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update file: {response.StatusCode} - {errorContent}");
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
                    // Remove from cache
                    _fileCache.TryRemove(id, out _);
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

                    // Remove successful deletions from cache
                    if (result.IsCompleteSuccess || result.IsPartialSuccess)
                    {
                        foreach (var fileId in fileIds)
                        {
                            _fileCache.TryRemove(fileId, out _);
                        }
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
                        // Update cache
                        _fileCache.AddOrUpdate(file.Id, file, (key, oldValue) => file);
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
                        // Add to cache
                        _fileCache.TryAdd(file.Id, file);
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

                    // Cache the files
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
                    // Invalidate cache for this file so it gets refreshed
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
                // Get file info first to check if it's public
                var fileInfo = await GetFileByIdAsync(id);
                if (fileInfo == null)
                {
                    throw new Exception("File not found");
                }

                string downloadUrl;
                if (fileInfo.IsPublic)
                {
                    // For public files, use direct download
                    downloadUrl = fileInfo.Urls.DirectAccess ?? fileInfo.Urls.Download;
                }
                else
                {
                    // For private files, generate token first
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

                    // Try to get filename from Content-Disposition header
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

                    // Invalidate cache for updated files
                    if (result.IsCompleteSuccess || result.IsPartialSuccess)
                    {
                        foreach (var fileId in fileIds)
                        {
                            _fileCache.TryRemove(fileId, out _);
                        }
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

                    // Invalidate cache for moved files
                    if (result.IsCompleteSuccess || result.IsPartialSuccess)
                    {
                        foreach (var fileId in fileIds)
                        {
                            _fileCache.TryRemove(fileId, out _);
                        }
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

                    // Cache new files
                    if (result.SuccessfulFiles != null)
                    {
                        foreach (var file in result.SuccessfulFiles)
                        {
                            _fileCache.TryAdd(file.Id, file);
                        }
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
            // Try to get from cache first - use the backend provided URL
            if (_fileCache.TryGetValue(fileId, out var cachedFile) && !string.IsNullOrEmpty(cachedFile.Urls.Download))
            {
                return cachedFile.Urls.Download;
            }

            // Fallback to constructed URL only if not available from backend
            return $"{_backendBaseUrl}/api/v1/file/{fileId}/download";
        }

        public string GetThumbnailUrl(int fileId)
        {
            // Try to get from cache first - use the backend provided URL
            if (_fileCache.TryGetValue(fileId, out var cachedFile) && !string.IsNullOrEmpty(cachedFile.Urls.Thumbnail))
            {
                return cachedFile.Urls.Thumbnail;
            }

            // Fallback to constructed URL only if not available from backend
            return $"{_backendBaseUrl}/api/v1/file/{fileId}/thumbnail";
        }

        public string GetPreviewUrl(int fileId)
        {
            // Try to get from cache first - use the backend provided URL
            if (_fileCache.TryGetValue(fileId, out var cachedFile) && !string.IsNullOrEmpty(cachedFile.Urls.Preview))
            {
                return cachedFile.Urls.Preview;
            }

            // Fallback to constructed URL only if not available from backend
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

        #region Private Methods

        private void CleanupCache(object? state)
        {
            try
            {
                const int maxCacheSize = 1000;
                if (_fileCache.Count <= maxCacheSize)
                    return;

                // Remove 20% of oldest entries
                var itemsToRemove = _fileCache.Count / 5;
                var keysToRemove = _fileCache.Keys.Take(itemsToRemove).ToList();

                foreach (var key in keysToRemove)
                {
                    _fileCache.TryRemove(key, out _);
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
            _downloadSemaphore?.Dispose();
            _cacheCleanupTimer?.Dispose();
            _fileCache?.Clear();
        }

        #endregion
    }
}