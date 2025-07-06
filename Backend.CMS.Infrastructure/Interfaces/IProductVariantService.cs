using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IProductVariantService
    {
        // Core CRUD operations
        Task<ProductVariantDto> GetVariantByIdAsync(int variantId);
        Task<PagedResult<ProductVariantDto>> GetVariantsAsync(int page = 1, int pageSize = 10, bool standaloneOnly = false);
        Task<PagedResult<ProductVariantDto>> GetStandaloneVariantsAsync(int page = 1, int pageSize = 10);
        Task<ProductVariantDto?> GetVariantBySKUAsync(string sku);
        Task<List<ProductVariantDto>> GetVariantsByProductIdAsync(int productId);
        Task<ProductVariantDto?> GetDefaultVariantAsync(int productId);
        Task<ProductVariantDto> CreateVariantAsync(int productId, CreateProductVariantDto createVariantDto);
        Task<ProductVariantDto> UpdateVariantAsync(int variantId, UpdateProductVariantDto updateVariantDto);
        Task<bool> DeleteVariantAsync(int variantId);

        // Variant management
        Task<ProductVariantDto> SetDefaultVariantAsync(int variantId);
        Task<List<ProductVariantDto>> ReorderVariantsAsync(List<(int VariantId, int Position)> variantOrders);

        // Stock management
        Task<ProductVariantDto> UpdateStockAsync(int variantId, int newQuantity);
        Task<PagedResult<ProductVariantDto>> GetLowStockVariantsAsync(int threshold = 5, int page = 1, int pageSize = 10);
        Task<PagedResult<ProductVariantDto>> GetOutOfStockVariantsAsync(int page = 1, int pageSize = 10);
        Task<int> GetTotalStockAsync(int productId);

        // Validation
        Task<bool> ValidateSKUAsync(string sku, int? excludeVariantId = null);

        // Image management
        Task<ProductVariantImageDto> AddVariantImageAsync(int variantId, CreateProductVariantImageDto createImageDto);
        Task<ProductVariantImageDto> UpdateVariantImageAsync(int imageId, UpdateProductVariantImageDto updateImageDto);
        Task<bool> DeleteVariantImageAsync(int imageId);
        Task<List<ProductVariantImageDto>> ReorderVariantImagesAsync(int variantId, List<(int ImageId, int Position)> imageOrders);
    }
}