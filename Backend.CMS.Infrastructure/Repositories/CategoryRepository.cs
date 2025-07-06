using Backend.CMS.Application.DTOs;
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

        public override async Task<Category?> GetByIdAsync(int id)
        {
            return await _dbSet
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public override async Task<IEnumerable<Category>> GetAllAsync()
        {
            return await _dbSet
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
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
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
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
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
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

        #region Paginated Methods Implementation

        public async Task<(IEnumerable<Category> categories, int totalCount)> GetCategoriesPagedAsync(CategorySearchDto searchDto)
        {
            var query = BuildSearchQuery(searchDto);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply sorting and pagination
            query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

            var categories = await query
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .ToListAsync();

            return (categories, totalCount);
        }

        public async Task<(IEnumerable<Category> categories, int totalCount)> GetRootCategoriesPagedAsync(int pageNumber, int pageSize)
        {
            var query = _dbSet
                .Where(c => c.ParentCategoryId == null && !c.IsDeleted);

            var totalCount = await query.CountAsync();

            var categories = await query
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (categories, totalCount);
        }

        public async Task<(IEnumerable<Category> categories, int totalCount)> GetSubCategoriesPagedAsync(int parentCategoryId, int pageNumber, int pageSize)
        {
            var query = _dbSet
                .Where(c => c.ParentCategoryId == parentCategoryId && !c.IsDeleted);

            var totalCount = await query.CountAsync();

            var categories = await query
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (categories, totalCount);
        }

        public async Task<(IEnumerable<Category> categories, int totalCount)> SearchCategoriesPagedAsync(CategorySearchDto searchDto)
        {
            var query = BuildAdvancedSearchQuery(searchDto);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply sorting and pagination
            query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

            var categories = await query
                .Include(c => c.ParentCategory)
                .Include(c => c.Images.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.File)
                .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .ToListAsync();

            return (categories, totalCount);
        }

        #endregion

        #region Private Helper Methods

        private IQueryable<Category> BuildSearchQuery(CategorySearchDto searchDto)
        {
            var query = _dbSet.Where(c => !c.IsDeleted);

            // Apply basic filters
            if (searchDto.ParentCategoryId.HasValue)
            {
                query = query.Where(c => c.ParentCategoryId == searchDto.ParentCategoryId.Value);
            }

            if (searchDto.IsActive.HasValue)
            {
                query = query.Where(c => c.IsActive == searchDto.IsActive.Value);
            }

            if (searchDto.IsVisible.HasValue)
            {
                query = query.Where(c => c.IsVisible == searchDto.IsVisible.Value);
            }

            // Apply text search if provided
            if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
            {
                var searchTerm = searchDto.SearchTerm.Trim().ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(searchTerm) ||
                    (c.Description != null && c.Description.ToLower().Contains(searchTerm)) ||
                    c.Slug.ToLower().Contains(searchTerm) ||
                    (c.MetaKeywords != null && c.MetaKeywords.ToLower().Contains(searchTerm)));
            }

            return query;
        }

        private IQueryable<Category> BuildAdvancedSearchQuery(CategorySearchDto searchDto)
        {
            var query = _dbSet.Where(c => !c.IsDeleted);

            // Apply all filters from basic search
            query = BuildSearchQuery(searchDto);

            // Apply date range filters
            if (searchDto.CreatedFrom.HasValue)
            {
                query = query.Where(c => c.CreatedAt >= searchDto.CreatedFrom.Value);
            }

            if (searchDto.CreatedTo.HasValue)
            {
                query = query.Where(c => c.CreatedAt <= searchDto.CreatedTo.Value);
            }

            if (searchDto.UpdatedFrom.HasValue)
            {
                query = query.Where(c => c.UpdatedAt >= searchDto.UpdatedFrom.Value);
            }

            if (searchDto.UpdatedTo.HasValue)
            {
                query = query.Where(c => c.UpdatedAt <= searchDto.UpdatedTo.Value);
            }

            // Apply product count filters
            if (searchDto.MinProductCount.HasValue || searchDto.MaxProductCount.HasValue)
            {
                // This would require a more complex query involving product counts
                // For now, we'll apply it at the service level after getting results
            }

            // Apply image filter
            if (searchDto.HasImages.HasValue)
            {
                if (searchDto.HasImages.Value)
                {
                    query = query.Where(c => c.Images.Any(i => !i.IsDeleted));
                }
                else
                {
                    query = query.Where(c => !c.Images.Any(i => !i.IsDeleted));
                }
            }

            // Apply meta keywords filter
            if (!string.IsNullOrWhiteSpace(searchDto.MetaKeywords))
            {
                var keywords = searchDto.MetaKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim().ToLower()).ToList();

                foreach (var keyword in keywords)
                {
                    query = query.Where(c => c.MetaKeywords != null && c.MetaKeywords.ToLower().Contains(keyword));
                }
            }

            return query;
        }

        private IQueryable<Category> ApplySorting(IQueryable<Category> query, string sortBy, string sortDirection)
        {
            var isDescending = sortDirection.Equals("Desc", StringComparison.OrdinalIgnoreCase);

            return sortBy.ToLowerInvariant() switch
            {
                "name" => isDescending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
                "createdat" => isDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
                "updatedat" => isDescending ? query.OrderByDescending(c => c.UpdatedAt) : query.OrderBy(c => c.UpdatedAt),
                "sortorder" => isDescending ? query.OrderByDescending(c => c.SortOrder) : query.OrderBy(c => c.SortOrder),
                _ => query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            };
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

        #endregion
    }
}