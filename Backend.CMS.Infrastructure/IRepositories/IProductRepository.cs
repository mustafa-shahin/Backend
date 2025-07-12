using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.IRepositories
{
    /// <summary>
    /// Repository interface for Product entity operations
    /// </summary>
    public interface IProductRepository : IRepository<Product>
    {
        // Query operations
        Task<Product?> GetBySlugAsync(string slug);
        Task<Product?> GetWithDetailsAsync(int productId);
        Task<Product?> GetWithVariantsAsync(int productId);
        Task<Product?> GetWithCategoriesAsync(int productId);
        Task<Product?> GetWithImagesAsync(int productId);
        Task<Product?> GetWithAllRelationsAsync(int productId);

        // Validation operations
        Task<bool> SlugExistsAsync(string slug, int? excludeProductId = null);

        // Category-based queries
        Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId, int page, int pageSize);
        Task<int> GetCountByCategoryAsync(int categoryId);
        Task<PagedResult<Product>> GetPagedByCategoryAsync(int categoryId, int page, int pageSize);

        // Status-based queries
        Task<IEnumerable<Product>> GetByStatusAsync(ProductStatus status, int page, int pageSize);
        Task<int> GetCountByStatusAsync(ProductStatus status);
        Task<PagedResult<Product>> GetPagedByStatusAsync(ProductStatus status, int page, int pageSize);

        // Search operations
        Task<IEnumerable<Product>> SearchProductsAsync(ProductSearchDto searchDto);
        Task<int> GetSearchCountAsync(ProductSearchDto searchDto);
        Task<PagedResult<Product>> SearchProductsPagedAsync(ProductSearchDto searchDto);

        // Featured and special collections
        Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count);
        Task<PagedResult<Product>> GetFeaturedProductsPagedAsync(int page, int pageSize);
        Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int count);
        Task<PagedResult<Product>> GetRelatedProductsPagedAsync(int productId, int page, int pageSize);
        Task<IEnumerable<Product>> GetRecentProductsAsync(int count);
        Task<PagedResult<Product>> GetRecentProductsPagedAsync(int page, int pageSize);

        // Price operations
        Task<decimal> GetMinPriceAsync();
        Task<decimal> GetMaxPriceAsync();
        Task<(decimal min, decimal max)> GetPriceRangeAsync();
        Task<IEnumerable<Product>> GetProductsByPriceRangeAsync(decimal minPrice, decimal maxPrice);

        // Vendor and tag operations
        Task<IEnumerable<string>> GetVendorsAsync();
        Task<IEnumerable<Product>> GetByVendorAsync(string vendor);

        // Stock operations
        Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold = 5);
        Task<PagedResult<Product>> GetLowStockProductsPagedAsync(int threshold, int page, int pageSize);
        Task<IEnumerable<Product>> GetOutOfStockProductsAsync();
        Task<PagedResult<Product>> GetOutOfStockProductsPagedAsync(int page, int pageSize);
        Task<int> GetTotalStockAsync();
        Task<int> GetTotalStockByStatusAsync(ProductStatus status);

        // Statistics operations
        Task<Dictionary<string, int>> GetProductCountByStatusAsync();
        Task<Dictionary<string, int>> GetProductCountByCategoryAsync();

        // ProductCategory management methods
        Task AddProductCategoryAsync(ProductCategory productCategory);
        Task RemoveProductCategoriesAsync(int productId);
        Task<ProductCategory?> GetProductCategoryAsync(int productCategoryId);
        Task<IEnumerable<ProductCategory>> GetProductCategoriesAsync(int productId);
        Task RemoveProductCategoryAsync(ProductCategory productCategory);
        Task<bool> HasCategoryAsync(int productId, int categoryId);

        // Product image operations
        Task<IEnumerable<ProductImage>> GetProductImagesAsync(int productId);
        Task<ProductImage?> GetFeaturedImageAsync(int productId);
        Task<int> GetImageCountAsync(int productId);


        // Bulk operations
        Task<IEnumerable<Product>> GetProductsByIdsAsync(IEnumerable<int> productIds);
        Task<bool> BulkUpdateStatusAsync(IEnumerable<int> productIds, ProductStatus status);
        Task<bool> BulkUpdateCategoryAsync(IEnumerable<int> productIds, int categoryId);
        Task<int> BulkDeleteAsync(IEnumerable<int> productIds);

        // Advanced queries
        Task<IEnumerable<Product>> GetProductsWithNoVariantsAsync();
        Task<IEnumerable<Product>> GetProductsWithNoImagesAsync();
        Task<IEnumerable<Product>> GetProductsWithNoCategoriesAsync();
        Task<IEnumerable<Product>> GetDuplicateSlugProductsAsync();

        // Performance operations
        Task<bool> ProductExistsAsync(int productId);
        Task<int> GetProductCountAsync();
        Task<DateTime?> GetLastUpdatedAsync();
        Task<IEnumerable<Product>> GetRecentlyUpdatedAsync(int count);
    }
}