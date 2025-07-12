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

        public async Task<PagedResult<ProductVariantDto>> GetProductVariantsAsync(int pageNumber = 1, int pageSize = 10, int? productId = null, string? searchTerm = null)
        {
            try
            {
                var query = $"/api/v1/productvariant?pageNumber={pageNumber}&pageSize={pageSize}";
                if (productId.HasValue)
                {
                    query += $"&productId={productId.Value}";
                }
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
                }

                var response = await _httpClient.GetAsync(query);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductVariantDto>>(_jsonOptions);
                    return result ?? new PagedResult<ProductVariantDto>();
                }

                throw new HttpRequestException($"Failed to get product variants: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting product variants: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> GetProductVariantByIdAsync(int id)
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

                throw new HttpRequestException($"Failed to get product variant: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting product variant {id}: {ex.Message}", ex);
            }
        }

        public async Task<ProductVariantDto?> CreateProductVariantAsync(CreateProductVariantDto createProductVariantDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/v1/productvariant", createProductVariantDto, _jsonOptions);

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

        public async Task<ProductVariantDto?> UpdateProductVariantAsync(int id, UpdateProductVariantDto updateProductVariantDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/v1/productvariant/{id}", updateProductVariantDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProductVariantDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update product variant: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating product variant {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteProductVariantAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/v1/productvariant/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting product variant {id}: {ex.Message}", ex);
            }
        }
    }
}