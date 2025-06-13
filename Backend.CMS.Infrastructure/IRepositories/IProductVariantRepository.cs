using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IProductVariantRepository : IRepository<ProductVariant>
    {
        Task<ProductVariant?> GetBySKUAsync(string sku);
        Task<IEnumerable<ProductVariant>> GetByProductIdAsync(int productId);
        Task<ProductVariant?> GetDefaultVariantAsync(int productId);
        Task<bool> SKUExistsAsync(string sku, int? excludeVariantId = null);
        Task<IEnumerable<ProductVariant>> GetLowStockVariantsAsync(int threshold = 5);
        Task<IEnumerable<ProductVariant>> GetOutOfStockVariantsAsync();
        Task UpdateStockAsync(int variantId, int newQuantity);
        Task<int> GetTotalStockAsync(int productId);
    }
}