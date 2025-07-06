using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IProductService
    {
        // Core CRUD operations
        Task<ProductDto?> GetProductByIdAsync(int productId);
        Task<ProductDto?> GetProductBySlugAsync(string slug);
        Task<PagedResult<ProductDto>> GetProductsAsync(int page = 1, int pageSize = 10);
        Task<PagedResult<ProductDto>> GetProductsByCategoryAsync(int categoryId, int page = 1, int pageSize = 10);
        Task<ProductDto> CreateProductAsync(CreateProductDto createProductDto);
        Task<ProductDto> UpdateProductAsync(int productId, UpdateProductDto updateProductDto);
        Task<bool> DeleteProductAsync(int productId);

        // Search and filtering
        Task<PagedResult<ProductDto>> SearchProductsAsync(ProductSearchDto searchDto);
        Task<int> GetSearchCountAsync(ProductSearchDto searchDto);

        // Product collections
        Task<PagedResult<ProductDto>> GetFeaturedProductsAsync(int page = 1, int pageSize = 10);
        Task<PagedResult<ProductDto>> GetRelatedProductsAsync(int productId, int page = 1, int pageSize = 10);
        Task<PagedResult<ProductDto>> GetRecentProductsAsync(int page = 1, int pageSize = 10);

        // Product status operations
        Task<ProductDto> PublishProductAsync(int productId);
        Task<ProductDto> UnpublishProductAsync(int productId);
        Task<ProductDto> ArchiveProductAsync(int productId);
        Task<ProductDto> DuplicateProductAsync(int productId, string newName);

        // Validation
        Task<bool> ValidateSlugAsync(string slug, int? excludeProductId = null);
        Task<bool> ValidateSKUAsync(string sku, int? excludeProductId = null);

        // Statistics and utilities
        Task<Dictionary<string, object>> GetProductStatisticsAsync();
        Task<(decimal min, decimal max)> GetPriceRangeAsync();
        Task<List<string>> GetVendorsAsync();
        Task<List<string>> GetTagsAsync();

        // Stock management
        Task UpdateStockAsync(int productId, int? variantId, int newQuantity);
        Task<PagedResult<ProductDto>> GetLowStockProductsAsync(int threshold = 5, int page = 1, int pageSize = 10);
        Task<PagedResult<ProductDto>> GetOutOfStockProductsAsync(int page = 1, int pageSize = 10);

        // Image management
        Task<ProductImageDto> AddProductImageAsync(int productId, CreateProductImageDto createImageDto);
        Task<ProductImageDto> UpdateProductImageAsync(int imageId, UpdateProductImageDto updateImageDto);
        Task<bool> DeleteProductImageAsync(int imageId);
        Task<List<ProductImageDto>> ReorderProductImagesAsync(int productId, List<(int ImageId, int Position)> imageOrders);
    }

    // Helper class for caching primitive return types
    //public class CountWrapper
    //{
    //    public int Value { get; set; }
    //}

    // Helper class for caching price range
    public class PriceRange
    {
        public decimal Min { get; set; }
        public decimal Max { get; set; }
    }
}