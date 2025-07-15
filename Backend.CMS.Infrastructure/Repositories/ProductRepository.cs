using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for Product entity operations
    /// </summary>
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        public ProductRepository(ApplicationDbContext context) : base(context)
        {
        }

        #region Override Base Methods

        public override async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        }

        public override async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        #endregion

        #region Query Operations

        public async Task<Product?> GetBySlugAsync(string slug)
        {
            return await _dbSet
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDeleted);
        }

        public async Task<Product?> GetWithDetailsAsync(int productId)
        {
            return await _dbSet
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
        }

        public async Task<Product?> GetWithVariantsAsync(int productId)
        {
            return await _dbSet
                .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .ThenInclude(v => v.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
        }

        public async Task<Product?> GetWithCategoriesAsync(int productId)
        {
            return await _dbSet
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
        }

        public async Task<Product?> GetWithImagesAsync(int productId)
        {
            return await _dbSet
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
        }

        public async Task<Product?> GetWithAllRelationsAsync(int productId)
        {
            return await GetWithDetailsAsync(productId);
        }

        #endregion

        #region Validation Operations

        public async Task<bool> SlugExistsAsync(string slug, int? excludeProductId = null)
        {
            var query = _dbSet.Where(p => p.Slug == slug && !p.IsDeleted);

            if (excludeProductId.HasValue)
                query = query.Where(p => p.Id != excludeProductId.Value);

            return await query.AnyAsync();
        }

        #endregion

        #region Category-based Queries

        public async Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId, int skip = 0, int take = int.MaxValue)
        {
            var query = _dbSet
                .Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId && !pc.IsDeleted) && !p.IsDeleted)
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(p => p.Name);

            if (skip > 0)
                query = (IOrderedQueryable<Product>)query.Skip(skip);

            if (take != int.MaxValue)
                query = (IOrderedQueryable<Product>)query.Take(take);

            return await query.ToListAsync();
        }

        public async Task<int> GetCountByCategoryAsync(int categoryId)
        {
            return await _dbSet
                .Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId && !pc.IsDeleted) && !p.IsDeleted)
                .CountAsync();
        }

        #endregion

        #region Status-based Queries

        public async Task<IEnumerable<Product>> GetByStatusAsync(ProductStatus status, int skip = 0, int take = int.MaxValue)
        {
            var query = _dbSet
                .Where(p => p.Status == status && !p.IsDeleted)
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(p => p.Name);

            if (skip > 0)
                query = (IOrderedQueryable<Product>)query.Skip(skip);

            if (take != int.MaxValue)
                query = (IOrderedQueryable<Product>)query.Take(take);

            return await query.ToListAsync();
        }

        public async Task<int> GetCountByStatusAsync(ProductStatus status)
        {
            return await _dbSet
                .Where(p => p.Status == status && !p.IsDeleted)
                .CountAsync();
        }

        #endregion

        #region Search Operations

        public async Task<IEnumerable<Product>> SearchProductsAsync(ProductSearchDto searchDto, int skip = 0, int take = int.MaxValue)
        {
            var query = BuildSearchQuery(searchDto);

            // Apply sorting
            query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

            // Apply includes
            query = query
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File);

            if (skip > 0)
                query = query.Skip(skip);

            if (take != int.MaxValue)
                query = query.Take(take);

            return await query.ToListAsync();
        }

        public async Task<int> GetSearchCountAsync(ProductSearchDto searchDto)
        {
            var query = BuildSearchQuery(searchDto);
            return await query.CountAsync();
        }

        private IQueryable<Product> BuildSearchQuery(ProductSearchDto searchDto)
        {
            var query = _dbSet.Where(p => !p.IsDeleted);

            // Apply filters
            if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchDto.SearchTerm) ||
                                        p.Description!.Contains(searchDto.SearchTerm) ||
                                        p.SearchKeywords!.Contains(searchDto.SearchTerm));
            }

            if (searchDto.Status.HasValue)
            {
                query = query.Where(p => p.Status == searchDto.Status.Value);
            }

            if (searchDto.Type.HasValue)
            {
                query = query.Where(p => p.Type == searchDto.Type.Value);
            }

            if (searchDto.CategoryIds.Any())
            {
                query = query.Where(p => p.ProductCategories.Any(pc => searchDto.CategoryIds.Contains(pc.CategoryId) && !pc.IsDeleted));
            }

            if (searchDto.HasVariants.HasValue)
            {
                query = query.Where(p => p.HasVariants == searchDto.HasVariants.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchDto.Vendor))
            {
                query = query.Where(p => p.Vendor == searchDto.Vendor);
            }

            if (searchDto.IsAvailable.HasValue)
            {
                if (searchDto.IsAvailable.Value)
                {
                    query = query.Where(p => p.Status == ProductStatus.Active && p.Variants.Any(v => v.Quantity > 0));
                }
                else
                {
                    query = query.Where(p => p.Status != ProductStatus.Active || p.Variants.All(v => v.Quantity <= 0));
                }
            }

            return query;
        }

        private IQueryable<Product> ApplySorting(IQueryable<Product> query, string sortBy, string sortDirection)
        {
            return sortBy.ToLowerInvariant() switch
            {
                "name" => sortDirection.ToLowerInvariant() == "desc"
                    ? query.OrderByDescending(p => p.Name)
                    : query.OrderBy(p => p.Name),
                "createdat" => sortDirection.ToLowerInvariant() == "desc"
                    ? query.OrderByDescending(p => p.CreatedAt)
                    : query.OrderBy(p => p.CreatedAt),
                "updatedat" => sortDirection.ToLowerInvariant() == "desc"
                    ? query.OrderByDescending(p => p.UpdatedAt)
                    : query.OrderBy(p => p.UpdatedAt),
                _ => query.OrderBy(p => p.Name)
            };
        }

        #endregion

        #region Featured and Special Collections

        public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int skip = 0, int take = int.MaxValue)
        {
            var query = _dbSet
                .Where(p => p.Status == ProductStatus.Active && !p.IsDeleted)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(p => Guid.NewGuid()); // Random order for featured products

            if (skip > 0)
                query = (IOrderedQueryable<Product>)query.Skip(skip);

            if (take != int.MaxValue)
                query = (IOrderedQueryable<Product>)query.Take(take);

            return await query.ToListAsync();
        }

        public async Task<int> GetFeaturedProductsCountAsync()
        {
            return await _dbSet
                .Where(p => p.Status == ProductStatus.Active && !p.IsDeleted)
                .CountAsync();
        }

        public async Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int skip = 0, int take = int.MaxValue)
        {
            var product = await _dbSet
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);

            if (product == null) return new List<Product>();

            var categoryIds = product.ProductCategories.Select(pc => pc.CategoryId).ToList();

            var query = _dbSet
                .Where(p => p.Id != productId &&
                           p.Status == ProductStatus.Active &&
                           !p.IsDeleted &&
                           p.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId) && !pc.IsDeleted))
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(p => Guid.NewGuid());

            if (skip > 0)
                query = (IOrderedQueryable<Product>)query.Skip(skip);

            if (take != int.MaxValue)
                query = (IOrderedQueryable<Product>)query.Take(take);

            return await query.ToListAsync();
        }

        public async Task<int> GetRelatedProductsCountAsync(int productId)
        {
            var product = await _dbSet
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);

            if (product == null) return 0;

            var categoryIds = product.ProductCategories.Select(pc => pc.CategoryId).ToList();

            if (!categoryIds.Any()) return 0;

            return await _dbSet
                .Where(p => p.Id != productId &&
                           p.Status == ProductStatus.Active &&
                           !p.IsDeleted &&
                           p.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId) && !pc.IsDeleted))
                .CountAsync();
        }

        public async Task<IEnumerable<Product>> GetRecentProductsAsync(int skip = 0, int take = int.MaxValue)
        {
            var query = _dbSet
                .Where(p => p.Status == ProductStatus.Active && !p.IsDeleted)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderByDescending(p => p.CreatedAt);

            if (skip > 0)
                query = (IOrderedQueryable<Product>)query.Skip(skip);

            if (take != int.MaxValue)
                query = (IOrderedQueryable<Product>)query.Take(take);

            return await query.ToListAsync();
        }

        public async Task<int> GetRecentProductsCountAsync()
        {
            return await _dbSet
                .Where(p => p.Status == ProductStatus.Active && !p.IsDeleted)
                .CountAsync();
        }

        #endregion

        #region Price Operations

        public async Task<decimal> GetMinPriceAsync()
        {
            return await _dbSet
                .Where(p => p.Status == ProductStatus.Active && !p.IsDeleted)
                .SelectMany(p => p.Variants)
                .MinAsync(v => v.Price);
        }

        public async Task<decimal> GetMaxPriceAsync()
        {
            return await _dbSet
                .Where(p => p.Status == ProductStatus.Active && !p.IsDeleted)
                .SelectMany(p => p.Variants)
                .MaxAsync(v => v.Price);
        }

        public async Task<(decimal min, decimal max)> GetPriceRangeAsync()
        {
            var variantPrices = await _dbSet
                .Where(p => p.Status == ProductStatus.Active && !p.IsDeleted)
                .SelectMany(p => p.Variants.Select(v => v.Price))
                .ToListAsync();

            if (!variantPrices.Any())
                return (0m, 0m);

            var min = variantPrices.Min();
            var max = variantPrices.Max();

            return (min, max);
        }

        public async Task<IEnumerable<Product>> GetProductsByPriceRangeAsync(decimal minPrice, decimal maxPrice)
        {
            return await _dbSet
                .Where(p => !p.IsDeleted &&
                            p.Status == ProductStatus.Active &&
                            p.Variants.Any(v => v.Price >= minPrice && v.Price <= maxPrice))
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(p => p.Variants.Min(v => v.Price))
                .ToListAsync();
        }

        #endregion

        #region Vendor and Tag Operations

        public async Task<IEnumerable<string>> GetVendorsAsync()
        {
            return await _dbSet
                .Where(p => !string.IsNullOrEmpty(p.Vendor) && !p.IsDeleted)
                .Select(p => p.Vendor!)
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetByVendorAsync(string vendor)
        {
            return await _dbSet
                .Where(p => p.Vendor == vendor && !p.IsDeleted)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        #endregion

        #region Stock Operations

        public async Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold = 5, int skip = 0, int take = int.MaxValue)
        {
            var query = _dbSet
                .Where(p => !p.IsDeleted &&
                            p.Variants.Any(v => v.Quantity > 0 && v.Quantity <= threshold))
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(p => p.Variants.Min(v => v.Quantity))
                .ThenBy(p => p.Name);

            if (skip > 0)
                query = (IOrderedQueryable<Product>)query.Skip(skip);

            if (take != int.MaxValue)
                query = (IOrderedQueryable<Product>)query.Take(take);

            return await query.ToListAsync();
        }

        public async Task<int> GetLowStockProductsCountAsync(int threshold = 5)
        {
            return await _dbSet
                .Where(p => !p.IsDeleted &&
                            p.Variants.Any(v => v.Quantity > 0 && v.Quantity <= threshold))
                .CountAsync();
        }

        public async Task<IEnumerable<Product>> GetOutOfStockProductsAsync(int skip = 0, int take = int.MaxValue)
        {
            var query = _dbSet
                .Where(p => !p.IsDeleted &&
                            p.Variants.All(v => v.Quantity <= 0))
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(p => p.Name);

            if (skip > 0)
                query = (IOrderedQueryable<Product>)query.Skip(skip);

            if (take != int.MaxValue)
                query = (IOrderedQueryable<Product>)query.Take(take);

            return await query.ToListAsync();
        }

        public async Task<int> GetOutOfStockProductsCountAsync()
        {
            return await _dbSet
                .Where(p => !p.IsDeleted &&
                            p.Variants.All(v => v.Quantity <= 0))
                .CountAsync();
        }

        public async Task<int> GetTotalStockAsync()
        {
            return await _dbSet
                .Where(p => !p.IsDeleted)
                .SelectMany(p => p.Variants)
                .SumAsync(v => v.Quantity);
        }

        public async Task<int> GetTotalStockByStatusAsync(ProductStatus status)
        {
            return await _dbSet
                .Where(p => p.Status == status && !p.IsDeleted)
                .SelectMany(p => p.Variants)
                .SumAsync(v => v.Quantity);
        }

        #endregion

        #region Statistics Operations

        public async Task<Dictionary<string, int>> GetProductCountByStatusAsync()
        {
            var stats = await _dbSet
                .Where(p => !p.IsDeleted)
                .GroupBy(p => p.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();

            return stats.ToDictionary(s => s.Status, s => s.Count);
        }

        public async Task<Dictionary<string, int>> GetProductCountByCategoryAsync()
        {
            var stats = await _context.Set<ProductCategory>()
                .Where(pc => !pc.IsDeleted && !pc.Product.IsDeleted)
                .Include(pc => pc.Category)
                .GroupBy(pc => pc.Category.Name)
                .Select(g => new { CategoryName = g.Key, Count = g.Count() })
                .ToListAsync();

            return stats.ToDictionary(s => s.CategoryName, s => s.Count);
        }

        #endregion

        #region ProductCategory Management

        public async Task AddProductCategoryAsync(ProductCategory productCategory)
        {
            await _context.Set<ProductCategory>().AddAsync(productCategory);
        }

        public async Task RemoveProductCategoriesAsync(int productId)
        {
            var existingCategories = await _context.Set<ProductCategory>()
                .Where(pc => pc.ProductId == productId && !pc.IsDeleted)
                .ToListAsync();

            foreach (var category in existingCategories)
            {
                category.IsDeleted = true;
                category.DeletedAt = DateTime.UtcNow;
            }
        }

        public async Task<ProductCategory?> GetProductCategoryAsync(int productCategoryId)
        {
            return await _context.Set<ProductCategory>()
                .FirstOrDefaultAsync(pc => pc.Id == productCategoryId && !pc.IsDeleted);
        }

        public async Task<IEnumerable<ProductCategory>> GetProductCategoriesAsync(int productId)
        {
            return await _context.Set<ProductCategory>()
                .Where(pc => pc.ProductId == productId && !pc.IsDeleted)
                .Include(pc => pc.Category)
                .OrderBy(pc => pc.SortOrder)
                .ToListAsync();
        }

        public async Task RemoveProductCategoryAsync(ProductCategory productCategory)
        {
            productCategory.IsDeleted = true;
            productCategory.DeletedAt = DateTime.UtcNow;
        }

        public async Task<bool> HasCategoryAsync(int productId, int categoryId)
        {
            return await _context.Set<ProductCategory>()
                .AnyAsync(pc => pc.ProductId == productId && pc.CategoryId == categoryId && !pc.IsDeleted);
        }

        #endregion

        #region Product Image Operations

        public async Task<IEnumerable<ProductImage>> GetProductImagesAsync(int productId)
        {
            return await _context.Set<ProductImage>()
                .Where(pi => pi.ProductId == productId && !pi.IsDeleted)
                .Include(pi => pi.File)
                .OrderBy(pi => pi.Position)
                .ToListAsync();
        }

        public async Task<ProductImage?> GetFeaturedImageAsync(int productId)
        {
            return await _context.Set<ProductImage>()
                .Where(pi => pi.ProductId == productId && pi.IsFeatured && !pi.IsDeleted)
                .Include(pi => pi.File)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetImageCountAsync(int productId)
        {
            return await _context.Set<ProductImage>()
                .Where(pi => pi.ProductId == productId && !pi.IsDeleted)
                .CountAsync();
        }

        #endregion

        #region Bulk Operations

        public async Task<IEnumerable<Product>> GetProductsByIdsAsync(IEnumerable<int> productIds)
        {
            return await _dbSet
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .ToListAsync();
        }

        public async Task<bool> BulkUpdateStatusAsync(IEnumerable<int> productIds, ProductStatus status)
        {
            try
            {
                var products = await _dbSet
                    .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                    .ToListAsync();

                foreach (var product in products)
                {
                    product.Status = status;
                    product.UpdatedAt = DateTime.UtcNow;
                    if (status == ProductStatus.Active)
                        product.PublishedAt = DateTime.UtcNow;
                    else if (status != ProductStatus.Active)
                        product.PublishedAt = null;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> BulkUpdateCategoryAsync(IEnumerable<int> productIds, int categoryId)
        {
            try
            {
                // Remove existing categories
                var existingCategories = await _context.Set<ProductCategory>()
                    .Where(pc => productIds.Contains(pc.ProductId) && !pc.IsDeleted)
                    .ToListAsync();

                foreach (var pc in existingCategories)
                {
                    pc.IsDeleted = true;
                    pc.DeletedAt = DateTime.UtcNow;
                }

                // Add new categories
                var newCategories = productIds.Select((productId, index) => new ProductCategory
                {
                    ProductId = productId,
                    CategoryId = categoryId,
                    SortOrder = index,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                await _context.Set<ProductCategory>().AddRangeAsync(newCategories);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> BulkDeleteAsync(IEnumerable<int> productIds)
        {
            var products = await _dbSet
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                .ToListAsync();

            foreach (var product in products)
            {
                product.IsDeleted = true;
                product.DeletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return products.Count;
        }

        #endregion

        #region Advanced Queries

        public async Task<IEnumerable<Product>> GetProductsWithNoVariantsAsync()
        {
            return await _dbSet
                .Where(p => !p.HasVariants && !p.IsDeleted)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetProductsWithNoImagesAsync()
        {
            return await _dbSet
                .Where(p => !p.Images.Any(i => !i.IsDeleted) && !p.IsDeleted)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetProductsWithNoCategoriesAsync()
        {
            return await _dbSet
                .Where(p => !p.ProductCategories.Any(pc => !pc.IsDeleted) && !p.IsDeleted)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetDuplicateSlugProductsAsync()
        {
            var duplicateSlugs = await _dbSet
                .Where(p => !p.IsDeleted)
                .GroupBy(p => p.Slug)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToListAsync();

            return await _dbSet
                .Where(p => duplicateSlugs.Contains(p.Slug) && !p.IsDeleted)
                .OrderBy(p => p.Slug)
                .ToListAsync();
        }

        #endregion

        #region Performance Operations

        public async Task<bool> ProductExistsAsync(int productId)
        {
            return await _dbSet.AnyAsync(p => p.Id == productId && !p.IsDeleted);
        }

        public async Task<int> GetProductCountAsync()
        {
            return await _dbSet.Where(p => !p.IsDeleted).CountAsync();
        }

        public async Task<DateTime?> GetLastUpdatedAsync()
        {
            return await _dbSet
                .Where(p => !p.IsDeleted)
                .MaxAsync(p => (DateTime?)p.UpdatedAt);
        }

        public async Task<IEnumerable<Product>> GetRecentlyUpdatedAsync(int count)
        {
            return await _dbSet
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.UpdatedAt)
                .Take(count)
                .ToListAsync();
        }

        #endregion
    }
}