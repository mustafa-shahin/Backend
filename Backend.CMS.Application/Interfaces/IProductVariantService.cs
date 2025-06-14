using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Application.Interfaces
{
    public interface IProductVariantService
    {
        Task<ProductVariantDto> GetVariantByIdAsync(int variantId);
        Task<PagedResult<ProductVariantDto>> GetVariantsPagedAsync(int page = 1, int pageSize = 20);
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
        Task<ProductVariantImageDto> AddVariantImageAsync(int variantId, CreateProductVariantImageDto createImageDto);
        Task<ProductVariantImageDto> UpdateVariantImageAsync(int imageId, UpdateProductVariantImageDto updateImageDto);
        Task<bool> DeleteVariantImageAsync(int imageId);
        Task<List<ProductVariantImageDto>> ReorderVariantImagesAsync(int variantId, List<(int ImageId, int Position)> imageOrders);
    }
}