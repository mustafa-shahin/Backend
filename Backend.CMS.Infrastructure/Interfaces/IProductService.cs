using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IProductService
    {
        // Basic CRUD operations
        Task<ProductDto?> GetProductByIdAsync(int productId);
        Task<ProductDto?> GetProductBySlugAsync(string slug);
        Task<PaginatedResult<ProductDto>> GetProductsAsync(int page = 1, int pageSize = 10);
        Task<ProductDto> CreateProductAsync(CreateProductDto createProductDto);
        Task<ProductDto> UpdateProductAsync(int productId, UpdateProductDto updateProductDto);
        Task<bool> DeleteProductAsync(int productId);

        // Category operations
        Task<PaginatedResult<ProductDto>> GetProductsByCategoryAsync(int categoryId, int page = 1, int pageSize = 10);

        // Search operations
        Task<PaginatedResult<ProductDto>> SearchProductsAsync(ProductSearchDto searchDto);
        Task<int> GetSearchCountAsync(ProductSearchDto searchDto);

        // Status operations
        Task<ProductDto> PublishProductAsync(int productId);
        Task<ProductDto> UnpublishProductAsync(int productId);
        Task<ProductDto> ArchiveProductAsync(int productId);

        // Utility operations
        Task<ProductDto> DuplicateProductAsync(int productId, string newName);
        Task<bool> ValidateSlugAsync(string slug, int? excludeProductId = null);

        // Collection operations
        Task<PaginatedResult<ProductDto>> GetFeaturedProductsAsync(int page = 1, int pageSize = 10);
        Task<PaginatedResult<ProductDto>> GetRelatedProductsAsync(int productId, int page = 1, int pageSize = 10);
        Task<PaginatedResult<ProductDto>> GetRecentProductsAsync(int page = 1, int pageSize = 10);

        Task<PaginatedResult<ProductDto>> GetLowStockProductsAsync(int threshold = 5, int page = 1, int pageSize = 10);
        Task<PaginatedResult<ProductDto>> GetOutOfStockProductsAsync(int page = 1, int pageSize = 10);

        // Statistics and aggregations
        Task<Dictionary<string, object>> GetProductStatisticsAsync();
        Task<(decimal min, decimal max)> GetPriceRangeAsync();
        Task<List<string>> GetVendorsAsync();

        // Image management
        Task<ProductImageDto> AddProductImageAsync(int productId, CreateProductImageDto createImageDto);
        Task<ProductImageDto> UpdateProductImageAsync(int imageId, UpdateProductImageDto updateImageDto);
        Task<bool> DeleteProductImageAsync(int imageId);
        Task<List<ProductImageDto>> ReorderProductImagesAsync(int productId, List<(int ImageId, int Position)> imageOrders);
    }
    public class PriceRange

    {
        public decimal Min { get; set; }
        public decimal Max { get; set; }
    }
}