using Backend.CMS.Application.DTOs;
using Frontend.Interfaces;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frontend.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IJSRuntime _jsRuntime;
        public CategoryService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };

        }

        public async Task<PaginatedResult<CategoryDto>> GetCategoriesAsync(CategorySearchDto? searchDto = null)
        {
            try
            {
                if (searchDto != null)
                {
                    var response = await _httpClient.PostAsJsonAsync("/api/v1/category/search", searchDto, _jsonOptions);

                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<CategoryDto>>(_jsonOptions);
                        return result ?? new PaginatedResult<CategoryDto>();
                    }

                    throw new HttpRequestException($"Failed to search categories: {response.StatusCode}");
                }
                else
                {
                    var response = await _httpClient.GetAsync("/api/v1/category");

                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<CategoryDto>>(_jsonOptions);
                        return result ?? new PaginatedResult<CategoryDto>();
                    }

                    throw new HttpRequestException($"Failed to get categories: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting categories: {ex.Message}", ex);
            }
        }

        public async Task<List<CategoryTreeDto>> GetCategoryTreeAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/v1/category/tree");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<CategoryTreeDto>>(_jsonOptions);
                    return result ?? new List<CategoryTreeDto>();
                }

                throw new HttpRequestException($"Failed to get category tree: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting category tree: {ex.Message}", ex);
            }
        }

        public async Task<PaginatedResult<CategoryDto>> GetRootCategoriesAsync(int page = 1, int pageSize = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/category/root?pageNumber={page}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<CategoryDto>>(_jsonOptions);
                    return result ?? new PaginatedResult<CategoryDto>();
                }

                throw new HttpRequestException($"Failed to get root categories: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting root categories: {ex.Message}", ex);
            }
        }

        public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/category/{id}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CategoryDto>(_jsonOptions);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw new HttpRequestException($"Failed to get category: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting category {id}: {ex.Message}", ex);
            }
        }

        public async Task<CategoryDto?> GetCategoryBySlugAsync(string slug)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/category/by-slug/{Uri.EscapeDataString(slug)}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CategoryDto>(_jsonOptions);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw new HttpRequestException($"Failed to get category by slug: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting category by slug {slug}: {ex.Message}", ex);
            }
        }

        public async Task<PaginatedResult<CategoryDto>> GetSubCategoriesAsync(int id, int page = 1, int pageSize = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/category/{id}/subcategories?pageNumber={page}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<CategoryDto>>(_jsonOptions);
                    return result ?? new PaginatedResult<CategoryDto>();
                }

                throw new HttpRequestException($"Failed to get subcategories: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting subcategories for {id}: {ex.Message}", ex);
            }
        }

        public async Task<PaginatedResult<CategoryDto>> SearchCategoriesAsync(CategorySearchDto searchDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/v1/category/search", searchDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<CategoryDto>>(_jsonOptions);
                    return result ?? new PaginatedResult<CategoryDto>();
                }

                throw new HttpRequestException($"Failed to search categories: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error searching categories: {ex.Message}", ex);
            }
        }

        public async Task<CategoryDto?> CreateCategoryAsync(CreateCategoryDto createCategoryDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/v1/category", createCategoryDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CategoryDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create category: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating category: {ex.Message}", ex);
            }
        }

        public async Task<CategoryDto?> UpdateCategoryAsync(int id, UpdateCategoryDto updateCategoryDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/v1/category/{id}", updateCategoryDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CategoryDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();

                // Log to browser console
                await _jsRuntime.InvokeVoidAsync("console.error", $"Failed to update category: {response.StatusCode} - {errorContent}");

                throw new HttpRequestException($"Failed to update category: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                await _jsRuntime.InvokeVoidAsync("console.error", $"Exception while updating category {id}: {ex.Message}");
                throw;
            }
        }


        public async Task<bool> DeleteCategoryAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/v1/category/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting category {id}: {ex.Message}", ex);
            }
        }

        public async Task<CategoryDto?> MoveCategoryAsync(int id, int? newParentCategoryId)
        {
            try
            {
                var moveDto = new MoveCategoryDto { NewParentCategoryId = newParentCategoryId };
                var response = await _httpClient.PostAsJsonAsync($"/api/v1/category/{id}/move", moveDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CategoryDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to move category: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error moving category {id}: {ex.Message}", ex);
            }
        }

        public async Task<List<CategoryDto>> ReorderCategoriesAsync(List<(int Id, int SortOrder)> categoryOrders)
        {
            try
            {
                var reorderDto = new ReorderCategoriesDto
                {
                    Categories = categoryOrders.Select(c => new CategoryOrderDto { Id = c.Id, SortOrder = c.SortOrder }).ToList()
                };

                var response = await _httpClient.PostAsJsonAsync("/api/v1/category/reorder", reorderDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<CategoryDto>>(_jsonOptions);
                    return result ?? new List<CategoryDto>();
                }

                throw new HttpRequestException($"Failed to reorder categories: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reordering categories: {ex.Message}", ex);
            }
        }

        public async Task<bool> ValidateSlugAsync(string slug, int? excludeCategoryId = null)
        {
            try
            {
                var query = $"/api/v1/category/validate-slug?slug={Uri.EscapeDataString(slug)}";
                if (excludeCategoryId.HasValue)
                {
                    query += $"&excludeCategoryId={excludeCategoryId.Value}";
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

        public async Task<bool> CanDeleteAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/category/{id}/can-delete");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    return result.GetProperty("canDelete").GetBoolean();
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking if category can be deleted: {ex.Message}", ex);
            }
        }

        public async Task<CategoryImageDto?> AddCategoryImageAsync(int id, CreateCategoryImageDto createImageDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"/api/v1/category/{id}/images", createImageDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CategoryImageDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to add category image: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error adding image to category {id}: {ex.Message}", ex);
            }
        }

        public async Task<CategoryImageDto?> UpdateCategoryImageAsync(int imageId, UpdateCategoryImageDto updateImageDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/v1/category/images/{imageId}", updateImageDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CategoryImageDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update category image: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating category image {imageId}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteCategoryImageAsync(int imageId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/v1/category/images/{imageId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting category image {imageId}: {ex.Message}", ex);
            }
        }

        public async Task<List<CategoryImageDto>> ReorderCategoryImagesAsync(int id, List<(int Id, int Position)> imageOrders)
        {
            try
            {
                var reorderDto = new ReorderCategoryImagesDto
                {
                    Images = imageOrders.Select(i => new CategoryImageOrderDto { Id = i.Id, Position = i.Position }).ToList()
                };

                var response = await _httpClient.PostAsJsonAsync($"/api/v1/category/{id}/images/reorder", reorderDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<CategoryImageDto>>(_jsonOptions);
                    return result ?? new List<CategoryImageDto>();
                }

                throw new HttpRequestException($"Failed to reorder category images: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reordering images for category {id}: {ex.Message}", ex);
            }
        }

        // Helper DTOs that should match the controller DTOs
        private class MoveCategoryDto
        {
            public int? NewParentCategoryId { get; set; }
        }

        private class ReorderCategoriesDto
        {
            public List<CategoryOrderDto> Categories { get; set; } = new();
        }

        private class CategoryOrderDto
        {
            public int Id { get; set; }
            public int SortOrder { get; set; }
        }

        private class ReorderCategoryImagesDto
        {
            public List<CategoryImageOrderDto> Images { get; set; } = new();
        }

        private class CategoryImageOrderDto
        {
            public int Id { get; set; }
            public int Position { get; set; }
        }
    }
}