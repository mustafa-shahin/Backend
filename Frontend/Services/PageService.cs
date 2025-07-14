using Backend.CMS.Application.DTOs;
using Frontend.Interface;
using System;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frontend.Services
{
    public class PageService : IPageService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public PageService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
        }

        public async Task<PaginatedResult<PageListDto>> GetPagesAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                var query = $"/api/v1/page?pageNumber={page}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(search))
                {
                    query += $"&search={Uri.EscapeDataString(search)}";
                }

                var response = await _httpClient.GetAsync(query);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<PageListDto>>(_jsonOptions);
                    return result ?? new PaginatedResult<PageListDto>();
                }

                throw new HttpRequestException($"Failed to get pages: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting pages: {ex.Message}", ex);
            }
        }

        public async Task<PageDto?> GetPageByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/page/{id}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw new HttpRequestException($"Failed to get page: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting page {id}: {ex.Message}", ex);
            }
        }

        public async Task<PageDto?> CreatePageAsync(CreatePageDto createPageDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/v1/page", createPageDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create page: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating page: {ex.Message}", ex);
            }
        }

        public async Task<PageDto?> UpdatePageAsync(int id, UpdatePageDto updatePageDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/v1/page/{id}", updatePageDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update page: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating page {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeletePageAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/v1/page/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting page {id}: {ex.Message}", ex);
            }
        }

        public async Task<PageDto?> PublishPageAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/v1/page/{id}/publish", null);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to publish page: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error publishing page {id}: {ex.Message}", ex);
            }
        }

        public async Task<PageDto?> UnpublishPageAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/v1/page/{id}/unpublish", null);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to unpublish page: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error unpublishing page {id}: {ex.Message}", ex);
            }
        }

        public async Task<PageDto?> DuplicatePageAsync(int id, string newName)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"/api/v1/page/{id}/duplicate",
                    new DuplicatePageDto { NewName = newName }, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<PageDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to duplicate page: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error duplicating page {id}: {ex.Message}", ex);
            }
        }

        public async Task<List<PageVersionDto>> GetPageVersionsAsync(int pageId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/page/{pageId}/versions");

                if (response.IsSuccessStatusCode)
                {
                    var versions = await response.Content.ReadFromJsonAsync<List<PageVersionDto>>(_jsonOptions);
                    return versions ?? new List<PageVersionDto>();
                }

                throw new HttpRequestException($"Failed to get page versions: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting page versions for {pageId}: {ex.Message}", ex);
            }
        }

        public async Task<bool> ValidateSlugAsync(string slug, int? excludePageId = null)
        {
            try
            {
                var query = $"/api/v1.0/page/validate-slug?slug={Uri.EscapeDataString(slug)}";
                if (excludePageId.HasValue)
                {
                    query += $"&excludePageId={excludePageId.Value}";
                }

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
                throw new Exception($"Error validating slug: {ex.Message}", ex);
            }
        }
    }
}

