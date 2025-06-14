using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        public ProductRepository(ApplicationDbContext context) : base(context)
        {
        }

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
                 .Include(p => p.Options.Where(o => !o.IsDeleted))
                     .ThenInclude(o => o.Values.Where(v => !v.IsDeleted))
                 .FirstOrDefaultAsync(p => p.Slug == slug);
        }

        public async Task<Product?> GetBySKUAsync(string sku)
        {
            return await _dbSet.FirstOrDefaultAsync(p => p.SKU == sku);
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
                 .Include(p => p.Options.Where(o => !o.IsDeleted))
                     .ThenInclude(o => o.Values.Where(v => !v.IsDeleted))
                 .FirstOrDefaultAsync(p => p.Id == productId);
        }

        public async Task<Product?> GetWithVariantsAsync(int productId)
        {
            return await _dbSet
                 .Include(p => p.Variants.Where(v => !v.IsDeleted))
                     .ThenInclude(v => v.Images.Where(i => !i.IsDeleted))
                         .ThenInclude(i => i.File)
                 .FirstOrDefaultAsync(p => p.Id == productId);
        }

        public async Task<Product?> GetWithCategoriesAsync(int productId)
        {
            return await _dbSet
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .FirstOrDefaultAsync(p => p.Id == productId);
        }

        public async Task<Product?> GetWithImagesAsync(int productId)
        {
            return await _dbSet
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(p => p.Id == productId);
        }

        public async Task<bool> SlugExistsAsync(string slug, int? excludeProductId = null)
        {
            var query = _dbSet.Where(p => p.Slug == slug);

            if (excludeProductId.HasValue)
                query = query.Where(p => p.Id != excludeProductId.Value);

            return await query.AnyAsync();
        }

        public async Task<bool> SKUExistsAsync(string sku, int? excludeProductId = null)
        {
            var query = _dbSet.Where(p => p.SKU == sku);

            if (excludeProductId.HasValue)
                query = query.Where(p => p.Id != excludeProductId.Value);

            return await query.AnyAsync();
        }

        public async Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId, int page, int pageSize)
        {
            return await _dbSet
                .Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId && !pc.IsDeleted))
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetByStatusAsync(ProductStatus status, int page, int pageSize)
        {
            return await _dbSet
                .Where(p => p.Status == status)
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> SearchProductsAsync(ProductSearchDto searchDto)
        {
            var query = _dbSet.AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchDto.SearchTerm) ||
                                        p.Description!.Contains(searchDto.SearchTerm) ||
                                        p.SKU.Contains(searchDto.SearchTerm) ||
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

            if (searchDto.MinPrice.HasValue)
            {
                query = query.Where(p => p.Price >= searchDto.MinPrice.Value);
            }

            if (searchDto.MaxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= searchDto.MaxPrice.Value);
            }

            if (searchDto.HasVariants.HasValue)
            {
                query = query.Where(p => p.HasVariants == searchDto.HasVariants.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchDto.Vendor))
            {
                query = query.Where(p => p.Vendor == searchDto.Vendor);
            }

            if (searchDto.Tags.Any())
            {
                foreach (var tag in searchDto.Tags)
                {
                    query = query.Where(p => p.Tags!.Contains(tag));
                }
            }

            if (searchDto.IsAvailable.HasValue && searchDto.IsAvailable.Value)
            {
                query = query.Where(p => p.Status == ProductStatus.Active &&
                                        (p.Quantity > 0 || p.ContinueSellingWhenOutOfStock || p.HasVariants));
            }

            // Apply sorting
            query = searchDto.SortBy.ToLowerInvariant() switch
            {
                "name" => searchDto.SortDirection.ToLowerInvariant() == "desc"
                    ? query.OrderByDescending(p => p.Name)
                    : query.OrderBy(p => p.Name),
                "price" => searchDto.SortDirection.ToLowerInvariant() == "desc"
                    ? query.OrderByDescending(p => p.Price)
                    : query.OrderBy(p => p.Price),
                "createdat" => searchDto.SortDirection.ToLowerInvariant() == "desc"
                    ? query.OrderByDescending(p => p.CreatedAt)
                    : query.OrderBy(p => p.CreatedAt),
                "updatedat" => searchDto.SortDirection.ToLowerInvariant() == "desc"
                    ? query.OrderByDescending(p => p.UpdatedAt)
                    : query.OrderBy(p => p.UpdatedAt),
                _ => query.OrderBy(p => p.Name)
            };

            return await query
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                .Skip((searchDto.Page - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .ToListAsync();
        }

        public async Task<int> GetSearchCountAsync(ProductSearchDto searchDto)
        {
            var query = _dbSet.AsQueryable();

            // Apply same filters as SearchProductsAsync but without includes and pagination
            if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchDto.SearchTerm) ||
                                        p.Description!.Contains(searchDto.SearchTerm) ||
                                        p.SKU.Contains(searchDto.SearchTerm) ||
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

            if (searchDto.MinPrice.HasValue)
            {
                query = query.Where(p => p.Price >= searchDto.MinPrice.Value);
            }

            if (searchDto.MaxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= searchDto.MaxPrice.Value);
            }

            if (searchDto.HasVariants.HasValue)
            {
                query = query.Where(p => p.HasVariants == searchDto.HasVariants.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchDto.Vendor))
            {
                query = query.Where(p => p.Vendor == searchDto.Vendor);
            }

            if (searchDto.Tags.Any())
            {
                foreach (var tag in searchDto.Tags)
                {
                    query = query.Where(p => p.Tags!.Contains(tag));
                }
            }

            if (searchDto.IsAvailable.HasValue && searchDto.IsAvailable.Value)
            {
                query = query.Where(p => p.Status == ProductStatus.Active &&
                                        (p.Quantity > 0 || p.ContinueSellingWhenOutOfStock || p.HasVariants));
            }

            return await query.CountAsync();
        }

        public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count)
        {
            return await _dbSet
                .Where(p => p.Status == ProductStatus.Active)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                .OrderBy(p => Guid.NewGuid()) // Random order for featured products
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int count)
        {
            var product = await _dbSet
                .Include(p => p.ProductCategories.Where(pc => !pc.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null) return new List<Product>();

            var categoryIds = product.ProductCategories.Select(pc => pc.CategoryId).ToList();

            return await _dbSet
                .Where(p => p.Id != productId &&
                           p.Status == ProductStatus.Active &&
                           p.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId) && !pc.IsDeleted))
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                .OrderBy(p => Guid.NewGuid())
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetRecentProductsAsync(int count)
        {
            return await _dbSet
                .Where(p => p.Status == ProductStatus.Active)
                .Include(p => p.Images.Where(i => !i.IsDeleted))
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<decimal> GetMinPriceAsync()
        {
            return await _dbSet
                .Where(p => p.Status == ProductStatus.Active)
                .MinAsync(p => p.Price);
        }

        public async Task<decimal> GetMaxPriceAsync()
        {
            return await _dbSet
                .Where(p => p.Status == ProductStatus.Active)
                .MaxAsync(p => p.Price);
        }

        public async Task<IEnumerable<string>> GetVendorsAsync()
        {
            return await _dbSet
                .Where(p => !string.IsNullOrEmpty(p.Vendor))
                .Select(p => p.Vendor!)
                .Distinct()
                .OrderBy(v => v)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetTagsAsync()
        {
            var products = await _dbSet
                .Where(p => !string.IsNullOrEmpty(p.Tags))
                .Select(p => p.Tags!)
                .ToListAsync();

            var allTags = new HashSet<string>();
            foreach (var tagString in products)
            {
                var tags = tagString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim())
                                 .Where(t => !string.IsNullOrEmpty(t));
                foreach (var tag in tags)
                {
                    allTags.Add(tag);
                }
            }

            return allTags.OrderBy(t => t).ToList();
        }
        public async Task AddProductCategoryAsync(ProductCategory productCategory)
        {
            await _context.Set<ProductCategory>().AddAsync(productCategory);
        }

        public async Task RemoveProductCategoriesAsync(int productId)
        {
            var existingCategories = await _context.Set<ProductCategory>()
                .Where(pc => pc.ProductId == productId)
                .ToListAsync();

            _context.Set<ProductCategory>().RemoveRange(existingCategories);
        }

        public async Task<ProductCategory?> GetProductCategoryAsync(int productCategoryId)
        {
            return await _context.Set<ProductCategory>()
                .FirstOrDefaultAsync(pc => pc.Id == productCategoryId);
        }

        public async Task RemoveProductCategoryAsync(ProductCategory productCategory)
        {
            _context.Set<ProductCategory>().Remove(productCategory);
        }
    }
}