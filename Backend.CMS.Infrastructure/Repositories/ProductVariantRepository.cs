using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class ProductVariantRepository : Repository<ProductVariant>, IProductVariantRepository
    {
        public ProductVariantRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<ProductVariant?> GetBySKUAsync(string sku)
        {
            return await _dbSet
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.SKU == sku);
        }

        public async Task<IEnumerable<ProductVariant>> GetByProductIdAsync(int productId)
        {
            return await _dbSet
                .Where(v => v.ProductId == productId)
                .OrderBy(v => v.Position)
                .ThenBy(v => v.Title)
                .ToListAsync();
        }

        public async Task<ProductVariant?> GetDefaultVariantAsync(int productId)
        {
            return await _dbSet
                .FirstOrDefaultAsync(v => v.ProductId == productId && v.IsDefault) ??
                await _dbSet
                    .Where(v => v.ProductId == productId)
                    .OrderBy(v => v.Position)
                    .FirstOrDefaultAsync();
        }

        public async Task<bool> SKUExistsAsync(string sku, int? excludeVariantId = null)
        {
            var query = _dbSet.Where(v => v.SKU == sku);

            if (excludeVariantId.HasValue)
                query = query.Where(v => v.Id != excludeVariantId.Value);

            return await query.AnyAsync();
        }

        public async Task<IEnumerable<ProductVariant>> GetLowStockVariantsAsync(int threshold = 5)
        {
            return await _dbSet
                .Include(v => v.Product)
                .Where(v => v.TrackQuantity && v.Quantity <= threshold && v.Quantity > 0)
                .OrderBy(v => v.Quantity)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProductVariant>> GetOutOfStockVariantsAsync()
        {
            return await _dbSet
                .Include(v => v.Product)
                .Where(v => v.TrackQuantity && v.Quantity <= 0)
                .OrderBy(v => v.Product.Name)
                .ThenBy(v => v.Title)
                .ToListAsync();
        }

        public async Task UpdateStockAsync(int variantId, int newQuantity)
        {
            var variant = await _dbSet.FindAsync(variantId);
            if (variant != null)
            {
                variant.Quantity = newQuantity;
                variant.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<int> GetTotalStockAsync(int productId)
        {
            return await _dbSet
                .Where(v => v.ProductId == productId && v.TrackQuantity)
                .SumAsync(v => v.Quantity);
        }
    }
}