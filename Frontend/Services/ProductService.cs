using Backend.CMS.Application.DTOs;
using Frontend.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frontend.Services
{
    public class ProductService : IProductService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ProductService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
        }

        public async Task<PagedResult<ProductListDto>> GetProductsAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                var query = $"/api/v1/product?page={page}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(search))
                {
                    query += $"&search={Uri.EscapeDataString(search)}";
                }

                var response = await _httpClient.GetAsync(query);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductListDto>>(_jsonOptions);
                    return result ?? new PagedResult<ProductListDto>();
                }

                throw new HttpRequestException($"Failed to get products: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting products: {ex.Message}", ex);
            }
        }

        public async Task<ProductDto?> GetProductByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/product/{id}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw new HttpRequestException($"Failed to get product: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting product {id}: {ex.Message}", ex);
            }
        }

        public async Task<ProductDto?> GetProductBySlugAsync(string slug)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/product/by-slug/{Uri.EscapeDataString(slug)}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw new HttpRequestException($"Failed to get product by slug: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting product by slug {slug}: {ex.Message}", ex);
            }
        }

        public async Task<ProductDto?> CreateProductAsync(CreateProductDto createProductDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/v1/product", createProductDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create product: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating product: {ex.Message}", ex);
            }
        }

        public async Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto updateProductDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/v1/product/{id}", updateProductDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update product: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating product {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/v1/product/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting product {id}: {ex.Message}", ex);
            }
        }

        public async Task<ProductDto?> PublishProductAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/v1/product/{id}/publish", null);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to publish product: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error publishing product {id}: {ex.Message}", ex);
            }
        }

        public async Task<ProductDto?> UnpublishProductAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/v1/product/{id}/unpublish", null);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to unpublish product: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error unpublishing product {id}: {ex.Message}", ex);
            }
        }

        public async Task<ProductDto?> ArchiveProductAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/v1/product/{id}/archive", null);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to archive product: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error archiving product {id}: {ex.Message}", ex);
            }
        }

        public async Task<ProductDto?> DuplicateProductAsync(int id, string newName)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"/api/v1/product/{id}/duplicate",
                    new DuplicateProductDto { NewName = newName }, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to duplicate product: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error duplicating product {id}: {ex.Message}", ex);
            }
        }

        public async Task<PagedResult<ProductListDto>> SearchProductsAsync(ProductSearchDto searchDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/v1/product/search", searchDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductListDto>>(_jsonOptions);
                    return result ?? new PagedResult<ProductListDto>();
                }

                throw new HttpRequestException($"Failed to search products: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error searching products: {ex.Message}", ex);
            }
        }

        public async Task<PagedResult<ProductListDto>> GetProductsByCategoryAsync(int categoryId, int page = 1, int pageSize = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/product/category/{categoryId}?page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductListDto>>(_jsonOptions);
                    return result ?? new PagedResult<ProductListDto>();
                }

                throw new HttpRequestException($"Failed to get products by category: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting products by category {categoryId}: {ex.Message}", ex);
            }
        }

        public async Task<PagedResult<ProductListDto>> GetFeaturedProductsAsync(int page = 1, int pageSize = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/product/featured?page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductListDto>>(_jsonOptions);
                    return result ?? new PagedResult<ProductListDto>();
                }

                throw new HttpRequestException($"Failed to get featured products: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting featured products: {ex.Message}", ex);
            }
        }

        public async Task<PagedResult<ProductListDto>> GetRecentProductsAsync(int page = 1, int pageSize = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/product/recent?page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductListDto>>(_jsonOptions);
                    return result ?? new PagedResult<ProductListDto>();
                }

                throw new HttpRequestException($"Failed to get recent products: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting recent products: {ex.Message}", ex);
            }
        }

        public async Task<PagedResult<ProductListDto>> GetRelatedProductsAsync(int id, int page = 1, int pageSize = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/product/{id}/related?page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductListDto>>(_jsonOptions);
                    return result ?? new PagedResult<ProductListDto>();
                }

                throw new HttpRequestException($"Failed to get related products: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting related products for {id}: {ex.Message}", ex);
            }
        }

        public async Task<PagedResult<ProductListDto>> GetLowStockProductsAsync(int threshold = 5, int page = 1, int pageSize = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/product/low-stock?threshold={threshold}&page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductListDto>>(_jsonOptions);
                    return result ?? new PagedResult<ProductListDto>();
                }

                throw new HttpRequestException($"Failed to get low stock products: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting low stock products: {ex.Message}", ex);
            }
        }

        public async Task<PagedResult<ProductListDto>> GetOutOfStockProductsAsync(int page = 1, int pageSize = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/product/out-of-stock?page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductListDto>>(_jsonOptions);
                    return result ?? new PagedResult<ProductListDto>();
                }

                throw new HttpRequestException($"Failed to get out of stock products: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting out of stock products: {ex.Message}", ex);
            }
        }

        public async Task<bool> ValidateSlugAsync(string slug, int? excludeProductId = null)
        {
            try
            {
                var query = $"/api/v1/product/validate-slug?slug={Uri.EscapeDataString(slug)}";
                if (excludeProductId.HasValue)
                {
                    query += $"&excludeProductId={excludeProductId.Value}";
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

        public async Task<Dictionary<string, object>> GetProductStatisticsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/v1/product/statistics");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions);
                    return result ?? new Dictionary<string, object>();
                }

                throw new HttpRequestException($"Failed to get product statistics: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting product statistics: {ex.Message}", ex);
            }
        }

        public async Task<(decimal min, decimal max)> GetPriceRangeAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/v1/product/price-range");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    return (
                        result.GetProperty("minPrice").GetDecimal(),
                        result.GetProperty("maxPrice").GetDecimal()
                    );
                }

                throw new HttpRequestException($"Failed to get price range: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting price range: {ex.Message}", ex);
            }
        }

        public async Task<List<string>> GetVendorsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/v1/product/vendors");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions);
                    return result ?? new List<string>();
                }

                throw new HttpRequestException($"Failed to get vendors: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting vendors: {ex.Message}", ex);
            }
        }

        public async Task<List<ProductVariantDto>> GetProductVariantsAsync(int productId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/v1/product/{productId}/variants");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<ProductVariantDto>>(_jsonOptions);
                    return result ?? new List<ProductVariantDto>();
                }

                throw new HttpRequestException($"Failed to get product variants: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting product variants for {productId}: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> CreateProductVariantAsync(int productId, CreateProductVariantDto createVariantDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"/api/v1/product/{productId}/variant", createVariantDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create product variant: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating product variant: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> SetDefaultVariantAsync(int productId, int variantId)
        {
            try
            {
                var response = await _httpClient.PutAsync($"/api/v1/product/{productId}/variant/{variantId}/set-default", null);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to set default variant: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error setting default variant: {ex.Message}", ex);
            }
        }
    }
}