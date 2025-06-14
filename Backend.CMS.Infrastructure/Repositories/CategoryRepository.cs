using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class CategoryRepository : Repository<Category>, ICategoryRepository
    {
        public CategoryRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Category?> GetBySlugAsync(string slug)
        {
            return await _dbSet
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(c => c.Slug == slug);
        }

        public async Task<IEnumerable<Category>> GetCategoryTreeAsync()
        {
            return await _dbSet
     .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
     .Include(c => c.Images.Where(i => !i.IsDeleted))
         .ThenInclude(i => i.File)
     .Where(c => c.ParentCategoryId == null)
     .OrderBy(c => c.SortOrder)
     .ThenBy(c => c.Name)
     .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetRootCategoriesAsync()
        {
            return await _dbSet
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .Where(c => c.ParentCategoryId == null)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetSubCategoriesAsync(int parentCategoryId)
        {
            return await _dbSet
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .Where(c => c.ParentCategoryId == parentCategoryId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Category?> GetWithSubCategoriesAsync(int categoryId)
        {
            return await _dbSet
               .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
               .Include(c => c.Images.Where(i => !i.IsDeleted))
                   .ThenInclude(i => i.File)
               .FirstOrDefaultAsync(c => c.Id == categoryId);
        }

        public async Task<Category?> GetWithProductsAsync(int categoryId)
        {
            return await _dbSet
                .Include(c => c.ProductCategories.Where(pc => !pc.IsDeleted))
                    .ThenInclude(pc => pc.Product)
                .FirstOrDefaultAsync(c => c.Id == categoryId);
        }

        public async Task<bool> SlugExistsAsync(string slug, int? excludeCategoryId = null)
        {
            var query = _dbSet.Where(c => c.Slug == slug);

            if (excludeCategoryId.HasValue)
                query = query.Where(c => c.Id != excludeCategoryId.Value);

            return await query.AnyAsync();
        }

        public async Task<IEnumerable<Category>> SearchCategoriesAsync(string searchTerm, int page, int pageSize)
        {
            return await _dbSet
                .Where(c => c.Name.Contains(searchTerm) ||
                           c.Description!.Contains(searchTerm) ||
                           c.Slug.Contains(searchTerm))
                .Include(c => c.ParentCategory)
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetProductCountAsync(int categoryId, bool includeSubCategories = false)
        {
            if (!includeSubCategories)
            {
                return await _context.Set<ProductCategory>()
                    .Where(pc => pc.CategoryId == categoryId && !pc.IsDeleted)
                    .CountAsync();
            }

            // Get all descendant category IDs
            var categoryIds = await GetAllDescendantCategoryIdsAsync(categoryId);
            categoryIds.Add(categoryId);

            return await _context.Set<ProductCategory>()
                .Where(pc => categoryIds.Contains(pc.CategoryId) && !pc.IsDeleted)
                .CountAsync();
        }

        public async Task<bool> HasSubCategoriesAsync(int categoryId)
        {
            return await _dbSet.AnyAsync(c => c.ParentCategoryId == categoryId);
        }

        public async Task<bool> CanDeleteAsync(int categoryId)
        {
            // Check if category has products
            var hasProducts = await _context.Set<ProductCategory>()
                .AnyAsync(pc => pc.CategoryId == categoryId && !pc.IsDeleted);

            if (hasProducts) return false;

            // Check if category has subcategories
            var hasSubCategories = await HasSubCategoriesAsync(categoryId);

            return !hasSubCategories;
        }

        private async Task<List<int>> GetAllDescendantCategoryIdsAsync(int categoryId)
        {
            var descendantIds = new List<int>();
            var childIds = await _dbSet
                .Where(c => c.ParentCategoryId == categoryId)
                .Select(c => c.Id)
                .ToListAsync();

            foreach (var childId in childIds)
            {
                descendantIds.Add(childId);
                var grandChildIds = await GetAllDescendantCategoryIdsAsync(childId);
                descendantIds.AddRange(grandChildIds);
            }

            return descendantIds;
        }
    }
}