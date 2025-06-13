using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IProductRepository : IRepository<Product>
    {
        Task<Product?> GetBySlugAsync(string slug);
        Task<Product?> GetBySKUAsync(string sku);
        Task<Product?> GetWithDetailsAsync(int productId);
        Task<Product?> GetWithVariantsAsync(int productId);
        Task<Product?> GetWithCategoriesAsync(int productId);
        Task<Product?> GetWithImagesAsync(int productId);
        Task<bool> SlugExistsAsync(string slug, int? excludeProductId = null);
        Task<bool> SKUExistsAsync(string sku, int? excludeProductId = null);
        Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId, int page, int pageSize);
        Task<IEnumerable<Product>> GetByStatusAsync(ProductStatus status, int page, int pageSize);
        Task<IEnumerable<Product>> SearchProductsAsync(ProductSearchDto searchDto);
        Task<int> GetSearchCountAsync(ProductSearchDto searchDto);
        Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count);
        Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int count);
        Task<IEnumerable<Product>> GetRecentProductsAsync(int count);
        Task<decimal> GetMinPriceAsync();
        Task<decimal> GetMaxPriceAsync();
        Task<IEnumerable<string>> GetVendorsAsync();
        Task<IEnumerable<string>> GetTagsAsync();

        // ProductCategory management methods
        Task AddProductCategoryAsync(ProductCategory productCategory);
        Task RemoveProductCategoriesAsync(int productId);
        Task<ProductCategory?> GetProductCategoryAsync(int productCategoryId);
        Task RemoveProductCategoryAsync(ProductCategory productCategory);
    }
}