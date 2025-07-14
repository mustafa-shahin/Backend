using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for ProductVariant entity operations
    /// </summary>
    public class ProductVariantRepository : Repository<ProductVariant>, IProductVariantRepository
    {
        public ProductVariantRepository(ApplicationDbContext context) : base(context)
        {
        }

        #region Override Base Methods

        public override async Task<ProductVariant?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
        }

        public override async Task<IEnumerable<ProductVariant>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .Where(v => !v.IsDeleted)
                .OrderBy(v => v.Position)
                .ThenBy(v => v.Title)
                .ToListAsync();
        }

        #endregion

        #region Query Operations

        public async Task<ProductVariant?> GetWithImagesAsync(int variantId)
        {
            return await _dbSet
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted);
        }

        public async Task<ProductVariant?> GetWithProductAsync(int variantId)
        {
            return await _dbSet
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted);
        }

        public async Task<ProductVariant?> GetWithAllRelationsAsync(int variantId)
        {
            return await _dbSet
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted);
        }

        #endregion

        #region Product-related Queries

        public async Task<IEnumerable<ProductVariant>> GetByProductIdAsync(int productId)
        {
            return await _dbSet
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .Where(v => v.ProductId == productId && !v.IsDeleted)
                .OrderBy(v => v.Position)
                .ThenBy(v => v.Title)
                .ToListAsync();
        }

        public async Task<PaginatedResult<ProductVariant>> GetPagedByProductIdAsync(int productId, int page, int pageSize)
        {
            var query = _dbSet
                .Where(v => v.ProductId == productId && !v.IsDeleted)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(v => v.Position)
                .ThenBy(v => v.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<ProductVariant>(items, page, pageSize, totalCount);
        }

        public async Task<int> GetCountByProductIdAsync(int productId)
        {
            return await _dbSet
                .Where(v => v.ProductId == productId && !v.IsDeleted)
                .CountAsync();
        }

        public async Task<ProductVariant?> GetDefaultVariantAsync(int productId)
        {
            return await _dbSet
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(v => v.ProductId == productId && v.IsDefault && !v.IsDeleted) ??
                await _dbSet
                    .Include(v => v.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .Where(v => v.ProductId == productId && !v.IsDeleted)
                    .OrderBy(v => v.Position)
                    .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<ProductVariant>> GetVariantsByProductIdsAsync(IEnumerable<int> productIds)
        {
            return await _dbSet
                .Where(v => productIds.Contains(v.ProductId) && !v.IsDeleted)
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.ProductId)
                .ThenBy(v => v.Position)
                .ToListAsync();
        }

        #endregion

        #region Standalone Variant Operations

        public async Task<IEnumerable<ProductVariant>> GetStandaloneVariantsAsync()
        {
            return await _dbSet
                .Where(v => v.ProductId == 0 && !v.IsDeleted)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Position)
                .ThenBy(v => v.Title)
                .ToListAsync();
        }

        public async Task<PaginatedResult<ProductVariant>> GetStandaloneVariantsPagedAsync(int page, int pageSize)
        {
            var query = _dbSet
                .Where(v => v.ProductId == 0 && !v.IsDeleted)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(v => v.Position)
                .ThenBy(v => v.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<ProductVariant>(items, page, pageSize, totalCount);
        }

        public async Task<int> GetStandaloneVariantCountAsync()
        {
            return await _dbSet
                .Where(v => v.ProductId == 0 && !v.IsDeleted)
                .CountAsync();
        }

        #endregion

        #region Validation Operations


        public async Task<bool> IsDefaultVariantAsync(int variantId)
        {
            return await _dbSet
                .AnyAsync(v => v.Id == variantId && v.IsDefault && !v.IsDeleted);
        }

        public async Task<bool> HasProductVariantsAsync(int productId)
        {
            return await _dbSet
                .AnyAsync(v => v.ProductId == productId && !v.IsDeleted);
        }

        #endregion

        #region Stock Operations

        public async Task<IEnumerable<ProductVariant>> GetLowStockVariantsAsync(int threshold = 5)
        {
            return await _dbSet
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .Where(v => v.TrackQuantity && v.Quantity <= threshold && v.Quantity > 0 && !v.IsDeleted)
                .OrderBy(v => v.Quantity)
                .ThenBy(v => v.Title)
                .ToListAsync();
        }

        public async Task<PaginatedResult<ProductVariant>> GetLowStockVariantsPagedAsync(int threshold, int page, int pageSize)
        {
            var query = _dbSet
                .Where(v => v.TrackQuantity && v.Quantity <= threshold && v.Quantity > 0 && !v.IsDeleted)
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(v => v.Quantity)
                .ThenBy(v => v.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<ProductVariant>(items, page, pageSize, totalCount);
        }

        public async Task<IEnumerable<ProductVariant>> GetOutOfStockVariantsAsync()
        {
            return await _dbSet
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .Where(v => v.TrackQuantity && v.Quantity <= 0 && !v.IsDeleted)
                .OrderBy(v => v.Title)
                .ToListAsync();
        }

        public async Task<PaginatedResult<ProductVariant>> GetOutOfStockVariantsPagedAsync(int page, int pageSize)
        {
            var query = _dbSet
                .Where(v => v.TrackQuantity && v.Quantity <= 0 && !v.IsDeleted)
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(v => v.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<ProductVariant>(items, page, pageSize, totalCount);
        }

        public async Task UpdateStockAsync(int variantId, int newQuantity)
        {
            var variant = await _dbSet.FindAsync(variantId);
            if (variant != null && !variant.IsDeleted)
            {
                variant.Quantity = newQuantity;
                variant.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<int> GetTotalStockAsync(int productId)
        {
            return await _dbSet
                .Where(v => v.ProductId == productId && v.TrackQuantity && !v.IsDeleted)
                .SumAsync(v => v.Quantity);
        }

        public async Task<int> GetTotalStockForAllProductsAsync()
        {
            return await _dbSet
                .Where(v => v.TrackQuantity && !v.IsDeleted)
                .SumAsync(v => v.Quantity);
        }

        public async Task<IEnumerable<ProductVariant>> GetVariantsByStockRangeAsync(int minStock, int maxStock)
        {
            return await _dbSet
                .Where(v => v.TrackQuantity && v.Quantity >= minStock && v.Quantity <= maxStock && !v.IsDeleted)
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Quantity)
                .ToListAsync();
        }

        #endregion

        #region Price Operations

        public async Task<decimal> GetMinPriceAsync()
        {
            return await _dbSet
                .Where(v => !v.IsDeleted)
                .MinAsync(v => v.Price);
        }

        public async Task<decimal> GetMaxPriceAsync()
        {
            return await _dbSet
                .Where(v => !v.IsDeleted)
                .MaxAsync(v => v.Price);
        }

        public async Task<(decimal min, decimal max)> GetPriceRangeAsync()
        {
            var variants = _dbSet.Where(v => !v.IsDeleted);

            var min = await variants.MinAsync(v => v.Price);
            var max = await variants.MaxAsync(v => v.Price);

            return (min, max);
        }

        public async Task<IEnumerable<ProductVariant>> GetVariantsByPriceRangeAsync(decimal minPrice, decimal maxPrice)
        {
            return await _dbSet
                .Where(v => v.Price >= minPrice && v.Price <= maxPrice && !v.IsDeleted)
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Price)
                .ToListAsync();
        }

        public async Task<decimal> GetAveragePriceAsync()
        {
            return await _dbSet
                .Where(v => !v.IsDeleted)
                .AverageAsync(v => v.Price);
        }

        #endregion

        #region Sorting and Ordering

        public async Task<IEnumerable<ProductVariant>> GetVariantsByPositionAsync(int productId)
        {
            return await _dbSet
                .Where(v => v.ProductId == productId && !v.IsDeleted)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Position)
                .ToListAsync();
        }

        public async Task<bool> UpdatePositionsAsync(List<(int VariantId, int Position)> variantPositions)
        {
            try
            {
                var variantIds = variantPositions.Select(vp => vp.VariantId).ToList();
                var variants = await _dbSet
                    .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
                    .ToListAsync();

                foreach (var variant in variants)
                {
                    var newPosition = variantPositions.First(vp => vp.VariantId == variant.Id).Position;
                    variant.Position = newPosition;
                    variant.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> GetNextPositionAsync(int productId)
        {
            var maxPosition = await _dbSet
                .Where(v => v.ProductId == productId && !v.IsDeleted)
                .MaxAsync(v => (int?)v.Position);

            return (maxPosition ?? 0) + 1;
        }

        public async Task<bool> ReorderVariantsAsync(int productId, List<int> variantIds)
        {
            try
            {
                var variants = await _dbSet
                    .Where(v => v.ProductId == productId && variantIds.Contains(v.Id) && !v.IsDeleted)
                    .ToListAsync();

                for (int i = 0; i < variantIds.Count; i++)
                {
                    var variant = variants.FirstOrDefault(v => v.Id == variantIds[i]);
                    if (variant != null)
                    {
                        variant.Position = i;
                        variant.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Default Variant Management

        public async Task<bool> SetDefaultVariantAsync(int variantId)
        {
            try
            {
                var variant = await _dbSet
                    .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted);

                if (variant == null || variant.ProductId <= 0)
                    return false;

                // Remove default flag from other variants of the same product
                var allVariants = await _dbSet
                    .Where(v => v.ProductId == variant.ProductId && !v.IsDeleted)
                    .ToListAsync();

                foreach (var v in allVariants)
                {
                    v.IsDefault = v.Id == variantId;
                    v.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveDefaultVariantAsync(int productId)
        {
            try
            {
                var defaultVariant = await _dbSet
                    .FirstOrDefaultAsync(v => v.ProductId == productId && v.IsDefault && !v.IsDeleted);

                if (defaultVariant != null)
                {
                    defaultVariant.IsDefault = false;
                    defaultVariant.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> HasDefaultVariantAsync(int productId)
        {
            return await _dbSet
                .AnyAsync(v => v.ProductId == productId && v.IsDefault && !v.IsDeleted);
        }

        #endregion

        #region Image Operations

        public async Task<IEnumerable<ProductVariantImage>> GetVariantImagesAsync(int variantId)
        {
            return await _context.Set<ProductVariantImage>()
                .Where(vi => vi.ProductVariantId == variantId && !vi.IsDeleted)
                .Include(vi => vi.File)
                .OrderBy(vi => vi.Position)
                .ToListAsync();
        }

        public async Task<ProductVariantImage?> GetFeaturedImageAsync(int variantId)
        {
            return await _context.Set<ProductVariantImage>()
                .Where(vi => vi.ProductVariantId == variantId && vi.IsFeatured && !vi.IsDeleted)
                .Include(vi => vi.File)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetImageCountAsync(int variantId)
        {
            return await _context.Set<ProductVariantImage>()
                .Where(vi => vi.ProductVariantId == variantId && !vi.IsDeleted)
                .CountAsync();
        }

        #endregion

        #region Option-based Queries

        public async Task<IEnumerable<ProductVariant>> GetVariantsByOptionAsync(string optionName, string optionValue)
        {
            return await _dbSet
                .Where(v => !v.IsDeleted &&
                           ((optionName.ToLower() == "option1" && v.Option1 == optionValue) ||
                            (optionName.ToLower() == "option2" && v.Option2 == optionValue) ||
                            (optionName.ToLower() == "option3" && v.Option3 == optionValue)))
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Title)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProductVariant>> GetVariantsByOptionsAsync(Dictionary<string, string> options)
        {
            var query = _dbSet.Where(v => !v.IsDeleted);

            foreach (var option in options)
            {
                var optionName = option.Key.ToLower();
                var optionValue = option.Value;

                if (optionName == "option1")
                    query = query.Where(v => v.Option1 == optionValue);
                else if (optionName == "option2")
                    query = query.Where(v => v.Option2 == optionValue);
                else if (optionName == "option3")
                    query = query.Where(v => v.Option3 == optionValue);
            }

            return await query
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Title)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetUniqueOption1ValuesAsync(int productId)
        {
            return await _dbSet
                .Where(v => v.ProductId == productId && !string.IsNullOrEmpty(v.Option1) && !v.IsDeleted)
                .Select(v => v.Option1!)
                .Distinct()
                .OrderBy(o => o)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetUniqueOption2ValuesAsync(int productId)
        {
            return await _dbSet
                .Where(v => v.ProductId == productId && !string.IsNullOrEmpty(v.Option2) && !v.IsDeleted)
                .Select(v => v.Option2!)
                .Distinct()
                .OrderBy(o => o)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetUniqueOption3ValuesAsync(int productId)
        {
            return await _dbSet
                .Where(v => v.ProductId == productId && !string.IsNullOrEmpty(v.Option3) && !v.IsDeleted)
                .Select(v => v.Option3!)
                .Distinct()
                .OrderBy(o => o)
                .ToListAsync();
        }

        #endregion

        #region Bulk Operations

        public async Task<IEnumerable<ProductVariant>> GetVariantsByIdsAsync(IEnumerable<int> variantIds)
        {
            return await _dbSet
                .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Position)
                .ToListAsync();
        }

        public async Task<bool> BulkUpdateStockAsync(Dictionary<int, int> variantStockUpdates)
        {
            try
            {
                var variantIds = variantStockUpdates.Keys.ToList();
                var variants = await _dbSet
                    .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
                    .ToListAsync();

                foreach (var variant in variants)
                {
                    if (variantStockUpdates.TryGetValue(variant.Id, out var newStock))
                    {
                        variant.Quantity = newStock;
                        variant.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> BulkUpdatePricesAsync(Dictionary<int, decimal> variantPriceUpdates)
        {
            try
            {
                var variantIds = variantPriceUpdates.Keys.ToList();
                var variants = await _dbSet
                    .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
                    .ToListAsync();

                foreach (var variant in variants)
                {
                    if (variantPriceUpdates.TryGetValue(variant.Id, out var newPrice))
                    {
                        variant.Price = newPrice;
                        variant.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> BulkDeleteAsync(IEnumerable<int> variantIds)
        {
            var variants = await _dbSet
                .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
                .ToListAsync();

            foreach (var variant in variants)
            {
                variant.IsDeleted = true;
                variant.DeletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return variants.Count;
        }

        public async Task<bool> BulkMoveToProductAsync(IEnumerable<int> variantIds, int targetProductId)
        {
            try
            {
                var variants = await _dbSet
                    .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
                    .ToListAsync();

                foreach (var variant in variants)
                {
                    variant.ProductId = targetProductId;
                    variant.UpdatedAt = DateTime.UtcNow;
                    variant.IsDefault = false; // Reset default status when moving
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Statistics Operations

        public async Task<Dictionary<string, int>> GetStockStatisticsAsync()
        {
            var variants = await _dbSet
                .Where(v => v.TrackQuantity && !v.IsDeleted)
                .ToListAsync();

            return new Dictionary<string, int>
            {
                ["TotalStock"] = variants.Sum(v => v.Quantity),
                ["InStockVariants"] = variants.Count(v => v.Quantity > 0),
                ["OutOfStockVariants"] = variants.Count(v => v.Quantity <= 0),
                ["LowStockVariants"] = variants.Count(v => v.Quantity <= 5 && v.Quantity > 0)
            };
        }

        public async Task<Dictionary<string, decimal>> GetPriceStatisticsAsync()
        {
            var variants = await _dbSet
                .Where(v => !v.IsDeleted)
                .ToListAsync();

            return new Dictionary<string, decimal>
            {
                ["MinPrice"] = variants.Any() ? variants.Min(v => v.Price) : 0,
                ["MaxPrice"] = variants.Any() ? variants.Max(v => v.Price) : 0,
                ["AveragePrice"] = variants.Any() ? variants.Average(v => v.Price) : 0,
                ["TotalValue"] = variants.Sum(v => v.Price * v.Quantity)
            };
        }

        public async Task<Dictionary<int, int>> GetVariantCountByProductAsync()
        {
            var stats = await _dbSet
                .Where(v => v.ProductId > 0 && !v.IsDeleted)
                .GroupBy(v => v.ProductId)
                .Select(g => new { ProductId = g.Key, Count = g.Count() })
                .ToListAsync();

            return stats.ToDictionary(s => s.ProductId, s => s.Count);
        }

        #endregion

        #region Search Operations

        public async Task<IEnumerable<ProductVariant>> SearchVariantsAsync(string searchTerm)
        {
            return await _dbSet
                .Where(v => !v.IsDeleted &&
                           (v.Title.Contains(searchTerm) ||
                            v.Option1!.Contains(searchTerm) ||
                            v.Option2!.Contains(searchTerm) ||
                            v.Option3!.Contains(searchTerm)))
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Title)
                .ToListAsync();
        }

        public async Task<PaginatedResult<ProductVariant>> SearchVariantsPagedAsync(string searchTerm, int page, int pageSize)
        {
            var query = _dbSet
                .Where(v => !v.IsDeleted &&
                           (v.Title.Contains(searchTerm) ||
                            v.Option1!.Contains(searchTerm) ||
                            v.Option2!.Contains(searchTerm) ||
                            v.Option3!.Contains(searchTerm)))
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(v => v.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<ProductVariant>(items, page, pageSize, totalCount);
        }

        public async Task<int> GetSearchCountAsync(string searchTerm)
        {
            return await _dbSet
                .Where(v => !v.IsDeleted &&
                           (v.Title.Contains(searchTerm) ||
                            v.Option1!.Contains(searchTerm) ||
                            v.Option2!.Contains(searchTerm) ||
                            v.Option3!.Contains(searchTerm)))
                .CountAsync();
        }

        #endregion

        #region Availability Operations

        public async Task<IEnumerable<ProductVariant>> GetAvailableVariantsAsync(int productId)
        {
            return await _dbSet
                .Where(v => v.ProductId == productId &&
                           (v.Quantity > 0 || v.ContinueSellingWhenOutOfStock) &&
                           !v.IsDeleted)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Position)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProductVariant>> GetUnavailableVariantsAsync(int productId)
        {
            return await _dbSet
                .Where(v => v.ProductId == productId &&
                           v.Quantity <= 0 &&
                           !v.ContinueSellingWhenOutOfStock &&
                           !v.IsDeleted)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Position)
                .ToListAsync();
        }

        public async Task<bool> IsVariantAvailableAsync(int variantId)
        {
            var variant = await _dbSet
                .FirstOrDefaultAsync(v => v.Id == variantId && !v.IsDeleted);

            if (variant == null) return false;

            return variant.Quantity > 0 || variant.ContinueSellingWhenOutOfStock;
        }

        #endregion

        #region Weight and Shipping Operations

        public async Task<IEnumerable<ProductVariant>> GetVariantsByWeightRangeAsync(decimal minWeight, decimal maxWeight)
        {
            return await _dbSet
                .Where(v => v.Weight >= minWeight && v.Weight <= maxWeight && !v.IsDeleted)
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Weight)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProductVariant>> GetShippableVariantsAsync()
        {
            return await _dbSet
                .Where(v => v.RequiresShipping && !v.IsDeleted)
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Title)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProductVariant>> GetNonShippableVariantsAsync()
        {
            return await _dbSet
                .Where(v => !v.RequiresShipping && !v.IsDeleted)
                .Include(v => v.Product)
                .Include(v => v.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(v => v.Title)
                .ToListAsync();
        }

        #endregion

        #region Advanced Queries

        public async Task<IEnumerable<ProductVariant>> GetVariantsWithNoImagesAsync()
        {
            return await _dbSet
                .Where(v => !v.Images.Any(i => !i.IsDeleted) && !v.IsDeleted)
                .Include(v => v.Product)
                .OrderBy(v => v.Title)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProductVariant>> GetVariantsWithInvalidDataAsync()
        {
            return await _dbSet
                .Where(v => !v.IsDeleted &&
                           (string.IsNullOrEmpty(v.Title) ||
                            v.Price < 0 ||
                            v.Quantity < 0))
                .Include(v => v.Product)
                .OrderBy(v => v.Title)
                .ToListAsync();
        }

        #endregion
    }
}