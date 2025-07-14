using Backend.CMS.Application.DTOs;

namespace Frontend.Interfaces
{
    public interface IProductService
    {
        Task<PaginatedResult<ProductListDto>> GetProductsAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ProductDto?> GetProductByIdAsync(int id);
        Task<ProductDto?> GetProductBySlugAsync(string slug);
        Task<ProductDto?> CreateProductAsync(CreateProductDto createProductDto);
        Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto updateProductDto);
        Task<bool> DeleteProductAsync(int id);
        Task<ProductDto?> PublishProductAsync(int id);
        Task<ProductDto?> UnpublishProductAsync(int id);
        Task<ProductDto?> ArchiveProductAsync(int id);
        Task<ProductDto?> DuplicateProductAsync(int id, string newName);
        Task<PaginatedResult<ProductListDto>> SearchProductsAsync(ProductSearchDto searchDto);
        Task<PaginatedResult<ProductListDto>> GetProductsByCategoryAsync(int categoryId, int page = 1, int pageSize = 10);
        Task<PaginatedResult<ProductListDto>> GetFeaturedProductsAsync(int page = 1, int pageSize = 10);
        Task<PaginatedResult<ProductListDto>> GetRecentProductsAsync(int page = 1, int pageSize = 10);
        Task<PaginatedResult<ProductListDto>> GetRelatedProductsAsync(int id, int page = 1, int pageSize = 10);
        Task<PaginatedResult<ProductListDto>> GetLowStockProductsAsync(int threshold = 5, int page = 1, int pageSize = 10);
        Task<PaginatedResult<ProductListDto>> GetOutOfStockProductsAsync(int page = 1, int pageSize = 10);
        Task<bool> ValidateSlugAsync(string slug, int? excludeProductId = null);
        Task<Dictionary<string, object>> GetProductStatisticsAsync();
        Task<(decimal min, decimal max)> GetPriceRangeAsync();
        Task<List<string>> GetVendorsAsync();
        Task<List<ProductVariantDto>> GetProductVariantsAsync(int productId);
        Task<ProductVariantDto?> CreateProductVariantAsync(int productId, CreateProductVariantDto createVariantDto);
        Task<ProductVariantDto?> SetDefaultVariantAsync(int productId, int variantId);
    }
}