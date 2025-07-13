using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class CategoryRepository : Repository<Category>, ICategoryRepository
    {
        public CategoryRepository(ApplicationDbContext context, ILogger<CategoryRepository> logger) 
            : base(context, logger)
        {
        }

        public CategoryRepository(ApplicationDbContext context) : base(context)
        {
        }

        public override async Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var category = await _dbSet
                    .Include(c => c.ParentCategory)
                    .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);

                _logger.LogDebug("Retrieved category by ID {CategoryId}: {Found}", id, category != null ? "Found" : "Not found");
                return category;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category by ID {CategoryId}", id);
                throw;
            }
        }

        public override async Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var categories = await _dbSet
                    .Include(c => c.ParentCategory)
                    .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .Where(c => !c.IsDeleted)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .ToListAsync(cancellationToken);

                _logger.LogDebug("Retrieved {Count} categories", categories.Count);
                return categories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all categories");
                throw;
            }
        }

        public async Task<Category?> GetBySlugAsync(string slug)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                    throw new ArgumentException("Slug cannot be null or empty", nameof(slug));

                var category = await _dbSet
                    .Include(c => c.ParentCategory)
                    .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .FirstOrDefaultAsync(c => c.Slug == slug && !c.IsDeleted);

                _logger.LogDebug("Retrieved category by slug {Slug}: {Found}", slug, category != null ? "Found" : "Not found");
                return category;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category by slug {Slug}", slug);
                throw;
            }
        }

        public async Task<IEnumerable<Category>> GetCategoryTreeAsync()
        {
            try
            {
                var categories = await _dbSet
                    .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .Where(c => !c.IsDeleted && c.ParentCategoryId == null)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .ToListAsync();

                _logger.LogDebug("Retrieved category tree with {Count} root categories", categories.Count);
                return categories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category tree");
                throw;
            }
        }

        public async Task<IEnumerable<Category>> GetRootCategoriesAsync()
        {
            try
            {
                var categories = await _dbSet
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .Where(c => !c.IsDeleted && c.ParentCategoryId == null)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} root categories", categories.Count);
                return categories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving root categories");
                throw;
            }
        }

        public async Task<IEnumerable<Category>> GetSubCategoriesAsync(int parentCategoryId)
        {
            try
            {
                var categories = await _dbSet
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .Where(c => !c.IsDeleted && c.ParentCategoryId == parentCategoryId)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} subcategories for parent {ParentCategoryId}", categories.Count, parentCategoryId);
                return categories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subcategories for parent {ParentCategoryId}", parentCategoryId);
                throw;
            }
        }

        public async Task<Category?> GetWithSubCategoriesAsync(int categoryId)
        {
            try
            {
                var category = await _dbSet
                   .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                   .Include(c => c.Images.Where(i => !i.IsDeleted))
                       .ThenInclude(i => i.File)
                   .FirstOrDefaultAsync(c => c.Id == categoryId && !c.IsDeleted);

                _logger.LogDebug("Retrieved category with subcategories {CategoryId}: {Found}", categoryId, category != null ? "Found" : "Not found");
                return category;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category with subcategories {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<Category?> GetWithProductsAsync(int categoryId)
        {
            try
            {
                var category = await _dbSet
                    .Include(c => c.ProductCategories.Where(pc => !pc.IsDeleted))
                        .ThenInclude(pc => pc.Product)
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .FirstOrDefaultAsync(c => c.Id == categoryId && !c.IsDeleted);

                _logger.LogDebug("Retrieved category with products {CategoryId}: {Found}", categoryId, category != null ? "Found" : "Not found");
                return category;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category with products {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<bool> SlugExistsAsync(string slug, int? excludeCategoryId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                    throw new ArgumentException("Slug cannot be null or empty", nameof(slug));

                var query = _dbSet.Where(c => !c.IsDeleted && c.Slug == slug);

                if (excludeCategoryId.HasValue)
                    query = query.Where(c => c.Id != excludeCategoryId.Value);

                var exists = await query.AnyAsync();
                _logger.LogDebug("Slug exists check for '{Slug}': {Exists}", slug, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if slug exists '{Slug}'", slug);
                throw;
            }
        }

        public async Task<IEnumerable<Category>> SearchCategoriesAsync(string searchTerm, int page, int pageSize)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));

                ValidatePagination(page, pageSize);

                var categories = await _dbSet
                    .Where(c => !c.IsDeleted && (
                        c.Name.Contains(searchTerm) ||
                        (c.Description != null && c.Description.Contains(searchTerm)) ||
                        c.Slug.Contains(searchTerm)))
                    .Include(c => c.ParentCategory)
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .OrderBy(c => c.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogDebug("Found {Count} categories matching search term '{SearchTerm}'", categories.Count, searchTerm);
                return categories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching categories with term '{SearchTerm}'", searchTerm);
                throw;
            }
        }

        public async Task<int> GetProductCountAsync(int categoryId, bool includeSubCategories = false)
        {
            try
            {
                if (!includeSubCategories)
                {
                    var count = await _context.Set<ProductCategory>()
                        .Where(pc => pc.CategoryId == categoryId && !pc.IsDeleted)
                        .CountAsync();

                    _logger.LogDebug("Product count for category {CategoryId}: {Count}", categoryId, count);
                    return count;
                }

                // Get all descendant category IDs
                var categoryIds = await GetAllDescendantCategoryIdsAsync(categoryId);
                categoryIds.Add(categoryId);

                var totalCount = await _context.Set<ProductCategory>()
                    .Where(pc => categoryIds.Contains(pc.CategoryId) && !pc.IsDeleted)
                    .CountAsync();

                _logger.LogDebug("Product count for category {CategoryId} (including subcategories): {Count}", categoryId, totalCount);
                return totalCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product count for category {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<bool> HasSubCategoriesAsync(int categoryId)
        {
            try
            {
                var hasSubCategories = await _dbSet.AnyAsync(c => !c.IsDeleted && c.ParentCategoryId == categoryId);
                _logger.LogDebug("Category {CategoryId} has subcategories: {HasSubCategories}", categoryId, hasSubCategories);
                return hasSubCategories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if category has subcategories {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<bool> CanDeleteAsync(int categoryId)
        {
            try
            {
                // Check if category has products
                var hasProducts = await _context.Set<ProductCategory>()
                    .AnyAsync(pc => pc.CategoryId == categoryId && !pc.IsDeleted);

                if (hasProducts)
                {
                    _logger.LogDebug("Category {CategoryId} cannot be deleted: has products", categoryId);
                    return false;
                }

                // Check if category has subcategories
                var hasSubCategories = await HasSubCategoriesAsync(categoryId);

                if (hasSubCategories)
                {
                    _logger.LogDebug("Category {CategoryId} cannot be deleted: has subcategories", categoryId);
                    return false;
                }

                _logger.LogDebug("Category {CategoryId} can be deleted", categoryId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if category can be deleted {CategoryId}", categoryId);
                throw;
            }
        }

        // PagedResult methods
        public async Task<PagedResult<Category>> GetCategoriesPagedAsync(CategorySearchDto searchDto)
        {
            try
            {
                ValidatePagination(searchDto.PageNumber, searchDto.PageSize);

                // Start with base query - only filter by IsDeleted initially
                var query = _dbSet.Where(c => !c.IsDeleted);
                var test = query.ToList().Where(c => c.IsDeleted);
                // Apply filters only if they are explicitly specified
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

                // Apply search term filter
                if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
                {
                    var searchTerm = searchDto.SearchTerm.Trim().ToLower();
                    query = query.Where(c =>
                        c.Name.ToLower().Contains(searchTerm) ||
                        (c.Description != null && c.Description.ToLower().Contains(searchTerm)) ||
                        c.Slug.ToLower().Contains(searchTerm) ||
                        (c.MetaKeywords != null && c.MetaKeywords.ToLower().Contains(searchTerm)));
                }

                // Apply date filters
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

                // Get total count before applying pagination
                var totalCount = await query.CountAsync();

                _logger.LogDebug("Query built successfully. Total count before pagination: {TotalCount}", totalCount);

                // Apply sorting
                query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

                // Apply pagination
                var categories = await query
                    .Include(c => c.ParentCategory)
                    .Include(c => c.SubCategories.Where(sc => !sc.IsDeleted))
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToListAsync();
                var result = new PagedResult<Category>
                {
                    Data = categories,
                    PageNumber = searchDto.PageNumber,
                    PageSize = searchDto.PageSize,
                    TotalCount = totalCount
                };

                _logger.LogDebug("Retrieved paged categories: {Count} results out of {TotalCount} total",
                    categories.Count, totalCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged categories with search criteria: {@SearchDto}", searchDto);
                throw;
            }
        }

        public async Task<PagedResult<Category>> GetRootCategoriesPagedAsync(int pageNumber, int pageSize)
        {
            try
            {
                ValidatePagination(pageNumber, pageSize);

                var query = _dbSet.Where(c => !c.IsDeleted && c.ParentCategoryId == null);

                var totalCount = await query.CountAsync();

                var categories = await query
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = new PagedResult<Category>
                {
                    Data = categories,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };

                _logger.LogDebug("Retrieved paged root categories: {Count} results", categories.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged root categories");
                throw;
            }
        }

        public async Task<PagedResult<Category>> GetSubCategoriesPagedAsync(int parentCategoryId, int pageNumber, int pageSize)
        {
            try
            {
                ValidatePagination(pageNumber, pageSize);

                var query = _dbSet.Where(c => !c.IsDeleted && c.ParentCategoryId == parentCategoryId);

                var totalCount = await query.CountAsync();

                var categories = await query
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = new PagedResult<Category>
                {
                    Data = categories,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };

                _logger.LogDebug("Retrieved paged subcategories for parent {ParentCategoryId}: {Count} results", parentCategoryId, categories.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged subcategories for parent {ParentCategoryId}", parentCategoryId);
                throw;
            }
        }

        public async Task<PagedResult<Category>> SearchCategoriesPagedAsync(CategorySearchDto searchDto)
        {
            try
            {
                ValidatePagination(searchDto.PageNumber, searchDto.PageSize);

                var query = BuildAdvancedSearchQuery(searchDto);

                var totalCount = await query.CountAsync();

                query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

                var categories = await query
                    .Include(c => c.ParentCategory)
                    .Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File)
                    .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToListAsync();

                var result = new PagedResult<Category>
                {
                    Data = categories,
                    PageNumber = searchDto.PageNumber,
                    PageSize = searchDto.PageSize,
                    TotalCount = totalCount
                };

                _logger.LogDebug("Searched categories with advanced criteria: {Count} results", categories.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching categories with advanced criteria");
                throw;
            }
        }

        #region Private Helper Methods

        private IQueryable<Category> BuildSearchQuery(CategorySearchDto searchDto)
        {
            var query = _dbSet.Where(c => !c.IsDeleted);

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
            var query = BuildSearchQuery(searchDto);

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
                .Where(c => !c.IsDeleted && c.ParentCategoryId == categoryId)
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

        private static void ValidatePagination(int page, int pageSize)
        {
            if (page < 1)
                throw new ArgumentException("Page number must be greater than 0", nameof(page));
            
            if (pageSize < 1)
                throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));
            
            if (pageSize > 1000)
                throw new ArgumentException("Page size cannot exceed 1000", nameof(pageSize));
        }

        #endregion
    }
}