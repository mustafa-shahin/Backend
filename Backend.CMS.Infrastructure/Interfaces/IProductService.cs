using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IProductService
    {
        Task<ProductDto?> GetProductByIdAsync(int productId);
        Task<ProductDto?> GetProductBySlugAsync(string slug);
        Task<List<ProductDto>> GetProductsAsync();
        Task<List<ProductDto>> GetProductsByCategoryAsync(int categoryId, int page = 1, int pageSize = 20);
        Task<ProductDto> CreateProductAsync(CreateProductDto createProductDto);
        Task<ProductDto> UpdateProductAsync(int productId, UpdateProductDto updateProductDto);
        Task<bool> DeleteProductAsync(int productId);
        Task<List<ProductDto>> SearchProductsAsync(ProductSearchDto searchDto);
        Task<int> GetSearchCountAsync(ProductSearchDto searchDto);
        Task<bool> ValidateSlugAsync(string slug, int? excludeProductId = null);
        Task<bool> ValidateSKUAsync(string sku, int? excludeProductId = null);
        Task<ProductDto> PublishProductAsync(int productId);
        Task<ProductDto> UnpublishProductAsync(int productId);
        Task<ProductDto> ArchiveProductAsync(int productId);
        Task<ProductDto> DuplicateProductAsync(int productId, string newName);
        Task<List<ProductDto>> GetFeaturedProductsAsync(int count = 10);
        Task<List<ProductDto>> GetRelatedProductsAsync(int productId, int count = 4);
        Task<List<ProductDto>> GetRecentProductsAsync(int count = 10);
        Task<Dictionary<string, object>> GetProductStatisticsAsync();
        Task<(decimal min, decimal max)> GetPriceRangeAsync();
        Task<List<string>> GetVendorsAsync();
        Task<List<string>> GetTagsAsync();
        Task UpdateStockAsync(int productId, int? variantId, int newQuantity);
        Task<List<ProductDto>> GetLowStockProductsAsync(int threshold = 5);
        Task<List<ProductDto>> GetOutOfStockProductsAsync();
        Task<ProductImageDto> AddProductImageAsync(int productId, CreateProductImageDto createImageDto);
        Task<ProductImageDto> UpdateProductImageAsync(int imageId, UpdateProductImageDto updateImageDto);
        Task<bool> DeleteProductImageAsync(int imageId);
        Task<List<ProductImageDto>> ReorderProductImagesAsync(int productId, List<(int ImageId, int Position)> imageOrders);
    }
}