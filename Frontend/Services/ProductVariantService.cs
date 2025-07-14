using Backend.CMS.Application.DTOs;
using Frontend.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frontend.Services
{
    public class ProductVariantService : IProductVariantService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ProductVariantService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
        }

        public async Task<PaginatedResult<ProductVariantDto>> GetVariantsAsync(int page = 1, int pageSize = 10, bool standaloneOnly = false)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/productvariant?page={page}&pageSize={pageSize}&standaloneOnly={standaloneOnly}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ProductVariantDto>>(_jsonOptions);
                    return result ?? new PaginatedResult<ProductVariantDto>();
                }

                throw new HttpRequestException($"Failed to get variants: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting variants: {ex.Message}", ex);
            }
        }

        public async Task<PaginatedResult<ProductVariantDto>> GetStandaloneVariantsAsync(int page = 1, int pageSize = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/productvariant/standalone?page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ProductVariantDto>>(_jsonOptions);
                    return result ?? new PaginatedResult<ProductVariantDto>();
                }

                throw new HttpRequestException($"Failed to get standalone variants: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting standalone variants: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> GetVariantByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/productvariant/{id}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantDto>(_jsonOptions);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw new HttpRequestException($"Failed to get variant: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting variant {id}: {ex.Message}", ex);
            }
        }

        public async Task<List<ProductVariantDto>> GetVariantsByProductIdAsync(int productId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/productvariant/product/{productId}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<ProductVariantDto>>(_jsonOptions);
                    return result ?? new List<ProductVariantDto>();
                }

                throw new HttpRequestException($"Failed to get variants by product: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting variants for product {productId}: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> GetDefaultVariantAsync(int productId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/productvariant/product/{productId}/default");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantDto>(_jsonOptions);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw new HttpRequestException($"Failed to get default variant: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting default variant for product {productId}: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> CreateVariantAsync(int productId, CreateProductVariantDto createVariantDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"/api/v1/productvariant/product/{productId}", createVariantDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create variant: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating variant: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> CreateStandaloneVariantAsync(CreateProductVariantDto createVariantDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/v1/productvariant", createVariantDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create standalone variant: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating standalone variant: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> AssignVariantToProductAsync(int variantId, int productId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/v1/productvariant/{variantId}/assign-to-product/{productId}", null);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to assign variant to product: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error assigning variant to product: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> UpdateVariantAsync(int id, UpdateProductVariantDto updateVariantDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/v1/productvariant/{id}", updateVariantDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update variant: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating variant {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteVariantAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/v1/productvariant/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting variant {id}: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> SetDefaultVariantAsync(int id)
        {
            try
            {
                var response = await _httpClient.PutAsync($"/api/v1/productvariant/{id}/set-default", null);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to set default variant: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error setting default variant {id}: {ex.Message}", ex);
            }
        }

        public async Task<List<ProductVariantDto>> ReorderVariantsAsync(List<(int Id, int Position)> variantOrders)
        {
            try
            {
                var reorderDto = new ReorderVariantsDto
                {
                    Variants = variantOrders.Select(v => new VariantOrderDto { Id = v.Id, Position = v.Position }).ToList()
                };

                var response = await _httpClient.PostAsJsonAsync("/api/v1/productvariant/reorder", reorderDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<ProductVariantDto>>(_jsonOptions);
                    return result ?? new List<ProductVariantDto>();
                }

                throw new HttpRequestException($"Failed to reorder variants: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reordering variants: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> UpdateStockAsync(int id, int newQuantity)
        {
            try
            {
                var updateStockDto = new UpdateVariantStockDto { NewQuantity = newQuantity };
                var response = await _httpClient.PostAsJsonAsync($"/api/v1/productvariant/{id}/stock", updateStockDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update stock: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating stock for variant {id}: {ex.Message}", ex);
            }
        }

        public async Task<PaginatedResult<ProductVariantDto>> GetLowStockVariantsAsync(int threshold = 5, int page = 1, int pageSize = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/productvariant/low-stock?threshold={threshold}&page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ProductVariantDto>>(_jsonOptions);
                    return result ?? new PaginatedResult<ProductVariantDto>();
                }

                throw new HttpRequestException($"Failed to get low stock variants: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting low stock variants: {ex.Message}", ex);
            }
        }

        public async Task<PaginatedResult<ProductVariantDto>> GetOutOfStockVariantsAsync(int page = 1, int pageSize = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/productvariant/out-of-stock?page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ProductVariantDto>>(_jsonOptions);
                    return result ?? new PaginatedResult<ProductVariantDto>();
                }

                throw new HttpRequestException($"Failed to get out of stock variants: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting out of stock variants: {ex.Message}", ex);
            }
        }

        public async Task<int> GetTotalStockAsync(int productId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/productvariant/product/{productId}/total-stock");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    return result.GetProperty("totalStock").GetInt32();
                }

                throw new HttpRequestException($"Failed to get total stock: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting total stock for product {productId}: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantImageDto?> AddVariantImageAsync(int id, CreateProductVariantImageDto createImageDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"/api/v1/productvariant/{id}/images", createImageDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantImageDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to add variant image: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error adding image to variant {id}: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantImageDto?> UpdateVariantImageAsync(int imageId, UpdateProductVariantImageDto updateImageDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/v1/productvariant/images/{imageId}", updateImageDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantImageDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update variant image: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating variant image {imageId}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteVariantImageAsync(int imageId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/v1/productvariant/images/{imageId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting variant image {imageId}: {ex.Message}", ex);
            }
        }

        public async Task<List<ProductVariantImageDto>> ReorderVariantImagesAsync(int id, List<(int ImageId, int Position)> imageOrders)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"/api/v1/productvariant/{id}/images/reorder", imageOrders, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<ProductVariantImageDto>>(_jsonOptions);
                    return result ?? new List<ProductVariantImageDto>();
                }

                throw new HttpRequestException($"Failed to reorder variant images: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reordering images for variant {id}: {ex.Message}", ex);
            }
        }

        // Helper DTOs for reordering variants
        private class ReorderVariantsDto
        {
            public List<VariantOrderDto> Variants { get; set; } = new();
        }

        private class VariantOrderDto
        {
            public int Id { get; set; }
            public int Position { get; set; }
        }
    }
}