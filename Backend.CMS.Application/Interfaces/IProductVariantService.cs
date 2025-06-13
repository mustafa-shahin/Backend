using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Application.Interfaces
{
    public interface IProductVariantService
    {
        Task<ProductVariantDto> GetVariantByIdAsync(int variantId);
        Task<ProductVariantDto?> GetVariantBySKUAsync(string sku);
        Task<List<ProductVariantDto>> GetVariantsByProductIdAsync(int productId);
        Task<ProductVariantDto?> GetDefaultVariantAsync(int productId);
        Task<ProductVariantDto> CreateVariantAsync(int productId, CreateProductVariantDto createVariantDto);
        Task<ProductVariantDto> UpdateVariantAsync(int variantId, UpdateProductVariantDto updateVariantDto);
        Task<bool> DeleteVariantAsync(int variantId);
        Task<bool> ValidateSKUAsync(string sku, int? excludeVariantId = null);
        Task<ProductVariantDto> SetDefaultVariantAsync(int variantId);
        Task<List<ProductVariantDto>> ReorderVariantsAsync(List<(int VariantId, int Position)> variantOrders);
        Task<ProductVariantDto> UpdateStockAsync(int variantId, int newQuantity);
        Task<List<ProductVariantDto>> GetLowStockVariantsAsync(int threshold = 5);
        Task<List<ProductVariantDto>> GetOutOfStockVariantsAsync();
        Task<int> GetTotalStockAsync(int productId);
    }
}