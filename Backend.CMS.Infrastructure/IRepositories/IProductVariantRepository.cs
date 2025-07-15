using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    /// <summary>
    /// Repository interface for ProductVariant entity operations
    /// </summary>
    public interface IProductVariantRepository : IRepository<ProductVariant>
    {
        // Query operations
        Task<ProductVariant?> GetWithImagesAsync(int variantId);
        Task<ProductVariant?> GetWithProductAsync(int variantId);
        Task<ProductVariant?> GetWithAllRelationsAsync(int variantId);

        // Product-related queries
        Task<IEnumerable<ProductVariant>> GetByProductIdAsync(int productId, int skip = 0, int take = int.MaxValue);
        Task<int> GetCountByProductIdAsync(int productId);
        Task<ProductVariant?> GetDefaultVariantAsync(int productId);
        Task<IEnumerable<ProductVariant>> GetVariantsByProductIdsAsync(IEnumerable<int> productIds);

        // Standalone variant operations
        Task<IEnumerable<ProductVariant>> GetStandaloneVariantsAsync(int skip = 0, int take = int.MaxValue);
        Task<int> GetStandaloneVariantCountAsync();

        // Validation operations
        Task<bool> IsDefaultVariantAsync(int variantId);
        Task<bool> HasProductVariantsAsync(int productId);

        // Stock operations
        Task<IEnumerable<ProductVariant>> GetLowStockVariantsAsync(int threshold = 5, int skip = 0, int take = int.MaxValue);
        Task<int> GetLowStockVariantsCountAsync(int threshold = 5);
        Task<IEnumerable<ProductVariant>> GetOutOfStockVariantsAsync(int skip = 0, int take = int.MaxValue);
        Task<int> GetOutOfStockVariantsCountAsync();
        Task UpdateStockAsync(int variantId, int newQuantity);
        Task<int> GetTotalStockAsync(int productId);
        Task<int> GetTotalStockForAllProductsAsync();
        Task<IEnumerable<ProductVariant>> GetVariantsByStockRangeAsync(int minStock, int maxStock);

        // Price operations
        Task<decimal> GetMinPriceAsync();
        Task<decimal> GetMaxPriceAsync();
        Task<(decimal min, decimal max)> GetPriceRangeAsync();
        Task<IEnumerable<ProductVariant>> GetVariantsByPriceRangeAsync(decimal minPrice, decimal maxPrice);
        Task<decimal> GetAveragePriceAsync();

        // Sorting and ordering
        Task<IEnumerable<ProductVariant>> GetVariantsByPositionAsync(int productId);
        Task<bool> UpdatePositionsAsync(List<(int VariantId, int Position)> variantPositions);
        Task<int> GetNextPositionAsync(int productId);
        Task<bool> ReorderVariantsAsync(int productId, List<int> variantIds);

        // Default variant management
        Task<bool> SetDefaultVariantAsync(int variantId);
        Task<bool> RemoveDefaultVariantAsync(int productId);
        Task<bool> HasDefaultVariantAsync(int productId);

        // Image operations
        Task<IEnumerable<ProductVariantImage>> GetVariantImagesAsync(int variantId);
        Task<ProductVariantImage?> GetFeaturedImageAsync(int variantId);
        Task<int> GetImageCountAsync(int variantId);

        // Option-based queries
        Task<IEnumerable<ProductVariant>> GetVariantsByOptionAsync(string optionName, string optionValue);
        Task<IEnumerable<ProductVariant>> GetVariantsByOptionsAsync(Dictionary<string, string> options);
        Task<IEnumerable<string>> GetUniqueOption1ValuesAsync(int productId);
        Task<IEnumerable<string>> GetUniqueOption2ValuesAsync(int productId);
        Task<IEnumerable<string>> GetUniqueOption3ValuesAsync(int productId);

        // Bulk operations
        Task<IEnumerable<ProductVariant>> GetVariantsByIdsAsync(IEnumerable<int> variantIds);
        Task<bool> BulkUpdateStockAsync(Dictionary<int, int> variantStockUpdates);
        Task<bool> BulkUpdatePricesAsync(Dictionary<int, decimal> variantPriceUpdates);
        Task<int> BulkDeleteAsync(IEnumerable<int> variantIds);
        Task<bool> BulkMoveToProductAsync(IEnumerable<int> variantIds, int targetProductId);

        // Statistics operations
        Task<Dictionary<string, int>> GetStockStatisticsAsync();
        Task<Dictionary<string, decimal>> GetPriceStatisticsAsync();
        Task<Dictionary<int, int>> GetVariantCountByProductAsync();

        // Search operations
        Task<IEnumerable<ProductVariant>> SearchVariantsAsync(string searchTerm, int skip = 0, int take = int.MaxValue);
        Task<int> GetSearchCountAsync(string searchTerm);

        // Availability operations
        Task<IEnumerable<ProductVariant>> GetAvailableVariantsAsync(int productId);
        Task<IEnumerable<ProductVariant>> GetUnavailableVariantsAsync(int productId);
        Task<bool> IsVariantAvailableAsync(int variantId);

        // Weight and shipping operations
        Task<IEnumerable<ProductVariant>> GetVariantsByWeightRangeAsync(decimal minWeight, decimal maxWeight);
        Task<IEnumerable<ProductVariant>> GetShippableVariantsAsync();
        Task<IEnumerable<ProductVariant>> GetNonShippableVariantsAsync();

        // Advanced queries
        Task<IEnumerable<ProductVariant>> GetVariantsWithNoImagesAsync();
        Task<IEnumerable<ProductVariant>> GetVariantsWithInvalidDataAsync();
    }
}