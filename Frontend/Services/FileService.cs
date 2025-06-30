using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Frontend.Interfaces;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;

namespace Frontend.Services
{
    public class FileService : IFileService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IJSRuntime _jsRuntime;

        public FileService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<PagedResult<FileDto>> GetFilesAsync(int page = 1, int pageSize = 20, int? folderId = null, string? search = null, FileType? fileType = null)
        {
            try
            {
                var query = $"/api/file?page={page}&pageSize={pageSize}";
                if (folderId.HasValue) query += $"&folderId={folderId}";
                if (!string.IsNullOrEmpty(search)) query += $"&search={Uri.EscapeDataString(search)}";
                if (fileType.HasValue) query += $"&fileType={fileType}";

                var response = await _httpClient.GetAsync(query);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<FileDto>>(_jsonOptions);
                    return result ?? new PagedResult<FileDto>();
                }
                throw new HttpRequestException($"Failed to get files: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting files: {ex.Message}", ex);
            }
        }

        public async Task<FileDto?> GetFileByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/file/{id}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FileDto>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting file {id}: {ex.Message}", ex);
            }
        }

        public async Task<FileDto?> UploadFileAsync(FileUploadDto uploadDto)
        {
            try
            {
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

                var response = await _httpClient.PostAsync("/api/file/upload", content);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FileDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to upload file: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error uploading file: {ex.Message}", ex);
            }
        }

        public async Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto)
        {
            try
            {
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

                var response = await _httpClient.PostAsync("/api/file/upload/multiple", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<FileDto>>(_jsonOptions);
                    return result ?? new List<FileDto>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to upload files: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error uploading files: {ex.Message}", ex);
            }
        }

        public async Task<FileDto?> UpdateFileAsync(int id, UpdateFileDto updateDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/file/{id}", updateDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FileDto>(_jsonOptions);
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
                var response = await _httpClient.DeleteAsync($"/api/file/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting file {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteMultipleFilesAsync(List<int> fileIds)
        {
            try
            {
                var bulkDto = new { FileIds = fileIds };
                var response = await _httpClient.PostAsJsonAsync("/api/file/bulk-delete", bulkDto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting files: {ex.Message}", ex);
            }
        }

        public async Task<FileDto?> MoveFileAsync(MoveFileDto moveDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/file/move", moveDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FileDto>(_jsonOptions);
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
                var response = await _httpClient.PostAsJsonAsync("/api/file/copy", copyDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FileDto>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error copying file: {ex.Message}", ex);
            }
        }

        public async Task<List<FileDto>> GetRecentFilesAsync(int count = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/file/recent?count={count}");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<FileDto>>(_jsonOptions);
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
                var response = await _httpClient.GetAsync("/api/file/statistics");
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
                var response = await _httpClient.PostAsync($"/api/file/{id}/generate-thumbnail", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating thumbnail for file {id}: {ex.Message}", ex);
            }
        }

        public async Task<string> GenerateDownloadTokenAsync(int fileId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/file/{fileId}/download-token", null);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    return result.GetProperty("token").GetString() ?? string.Empty;
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating download token: {ex.Message}", ex);
            }
        }

        public async Task DownloadFileAsync(int id)
        {
            try
            {
                // For public files, use direct download
                var fileInfo = await GetFileByIdAsync(id);
                if (fileInfo?.IsPublic == true)
                {
                    var downloadUrl = $"/api/file/{id}/download";
                    await _jsRuntime.InvokeVoidAsync("downloadFile", downloadUrl, fileInfo.OriginalFileName);
                }
                else
                {
                    // For private files, generate token first
                    var token = await GenerateDownloadTokenAsync(id);
                    if (!string.IsNullOrEmpty(token))
                    {
                        var downloadUrl = $"/api/file/download/{token}";
                        await _jsRuntime.InvokeVoidAsync("downloadFile", downloadUrl, fileInfo?.OriginalFileName ?? "file");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error downloading file {id}: {ex.Message}", ex);
            }
        }

        public async Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/file/{id}/download");
                if (response.IsSuccessStatusCode)
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                    var fileName = "file"; // Extract from headers if available
                    return (stream, contentType, fileName);
                }
                throw new HttpRequestException($"Failed to get file stream: {response.StatusCode}");
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
                var response = await _httpClient.GetAsync($"/api/file/{id}/thumbnail");
                if (response.IsSuccessStatusCode)
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";
                    var fileName = $"thumbnail_{id}";
                    return (stream, contentType, fileName);
                }
                throw new HttpRequestException($"Failed to get thumbnail: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting thumbnail {id}: {ex.Message}", ex);
            }
        }

        public async Task<FilePreviewDto> GetFilePreviewAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/file/{id}/preview");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<FilePreviewDto>(_jsonOptions);
                    return result ?? new FilePreviewDto();
                }
                throw new HttpRequestException($"Failed to get file preview: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting file preview {id}: {ex.Message}", ex);
            }
        }

        public async Task<List<FileDto>> SearchFilesAsync(FileSearchDto searchDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/file/search", searchDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<FileDto>>(_jsonOptions);
                    return result ?? new List<FileDto>();
                }
                return new List<FileDto>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error searching files: {ex.Message}", ex);
            }
        }

        public async Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto)
        {
            try
            {
                var bulkDto = new { FileIds = fileIds, UpdateDto = updateDto };
                var response = await _httpClient.PostAsJsonAsync("/api/file/bulk-update", bulkDto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error bulk updating files: {ex.Message}", ex);
            }
        }

        public async Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId)
        {
            try
            {
                var bulkDto = new { FileIds = fileIds, DestinationFolderId = destinationFolderId };
                var response = await _httpClient.PostAsJsonAsync("/api/file/bulk-move", bulkDto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error bulk moving files: {ex.Message}", ex);
            }
        }

        public async Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId)
        {
            try
            {
                var bulkDto = new { FileIds = fileIds, DestinationFolderId = destinationFolderId };
                var response = await _httpClient.PostAsJsonAsync("/api/file/bulk-copy", bulkDto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<FileDto>>(_jsonOptions);
                    return result ?? new List<FileDto>();
                }
                return new List<FileDto>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error bulk copying files: {ex.Message}", ex);
            }
        }
    }
}