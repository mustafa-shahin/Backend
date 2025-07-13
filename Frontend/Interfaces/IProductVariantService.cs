using Backend.CMS.Application.DTOs;

namespace Frontend.Interfaces
{
    public interface IProductVariantService
    {
        Task<PagedResult<ProductVariantDto>> GetVariantsAsync(int page = 1, int pageSize = 10, bool standaloneOnly = false);
        Task<PagedResult<ProductVariantDto>> GetStandaloneVariantsAsync(int page = 1, int pageSize = 10);
        Task<ProductVariantDto?> GetVariantByIdAsync(int id);
        Task<List<ProductVariantDto>> GetVariantsByProductIdAsync(int productId);
        Task<ProductVariantDto?> GetDefaultVariantAsync(int productId);
        Task<ProductVariantDto?> CreateVariantAsync(int productId, CreateProductVariantDto createVariantDto);
        Task<ProductVariantDto?> CreateStandaloneVariantAsync(CreateProductVariantDto createVariantDto);
        Task<ProductVariantDto?> AssignVariantToProductAsync(int variantId, int productId);
        Task<ProductVariantDto?> UpdateVariantAsync(int id, UpdateProductVariantDto updateVariantDto);
        Task<bool> DeleteVariantAsync(int id);
        Task<ProductVariantDto?> SetDefaultVariantAsync(int id);
        Task<List<ProductVariantDto>> ReorderVariantsAsync(List<(int Id, int Position)> variantOrders);
        Task<ProductVariantDto?> UpdateStockAsync(int id, int newQuantity);
        Task<PagedResult<ProductVariantDto>> GetLowStockVariantsAsync(int threshold = 5, int page = 1, int pageSize = 10);
        Task<PagedResult<ProductVariantDto>> GetOutOfStockVariantsAsync(int page = 1, int pageSize = 10);
        Task<int> GetTotalStockAsync(int productId);
        Task<ProductVariantImageDto?> AddVariantImageAsync(int id, CreateProductVariantImageDto createImageDto);
        Task<ProductVariantImageDto?> UpdateVariantImageAsync(int imageId, UpdateProductVariantImageDto updateImageDto);
        Task<bool> DeleteVariantImageAsync(int imageId);
        Task<List<ProductVariantImageDto>> ReorderVariantImagesAsync(int id, List<(int ImageId, int Position)> imageOrders);
    }
}