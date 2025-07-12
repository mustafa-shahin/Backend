using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
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

        public async Task<PagedResult<ProductListDto>> GetProductsAsync(int pageNumber = 1, int pageSize = 10, string? searchTerm = null, ProductStatus? status = null, ProductType? type = null, List<int>? categoryIds = null, bool? hasVariants = null, bool? isAvailable = null, string? vendor = null)
        {
            try
            {
                var query = $"/api/v1/product?pageNumber={pageNumber}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
                }
                if (status.HasValue)
                {
                    query += $"&status={status.Value}";
                }
                if (type.HasValue)
                {
                    query += $"&type={type.Value}";
                }
                if (categoryIds != null && categoryIds.Any())
                {
                    query += $"&{string.Join("&", categoryIds.Select(id => $"categoryIds={id}"))}";
                }
                if (hasVariants.HasValue)
                {
                    query += $"&hasVariants={hasVariants.Value}";
                }
                if (isAvailable.HasValue)
                {
                    query += $"&isAvailable={isAvailable.Value}";
                }
                if (!string.IsNullOrEmpty(vendor))
                {
                    query += $"&vendor={Uri.EscapeDataString(vendor)}";
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

        public async Task<bool> ValidateSlugAsync(string slug, int? excludeId = null)
        {
            try
            {
                var query = $"/api/v1/product/validate-slug?slug={Uri.EscapeDataString(slug)}";
                if (excludeId.HasValue)
                {
                    query += $"&excludeId={excludeId.Value}";
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