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
                    .AsNoTracking()
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
                    .AsNoTracking()
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
                    .AsNoTracking()
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
                    .AsNoTracking()
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
                    .AsNoTracking()
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
                    .AsNoTracking()
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
                   .AsNoTracking()
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
                    .AsNoTracking()
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

                var query = _dbSet.AsNoTracking().Where(c => !c.IsDeleted && c.Slug == slug);

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
                    .AsNoTracking()
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
                    var count = await _dbSet
                        .AsNoTracking()
                        .Where(c => c.Id == categoryId && !c.IsDeleted)
                        .SelectMany(c => c.ProductCategories)
                        .Where(pc => !pc.IsDeleted)
                        .CountAsync();

                    _logger.LogDebug("Product count for category {CategoryId}: {Count}", categoryId, count);
                    return count;
                }

                var categoryIds = await GetAllDescendantCategoryIdsAsync(categoryId);
                categoryIds.Add(categoryId);

                var totalCount = await _dbSet
                    .AsNoTracking()
                    .Where(c => categoryIds.Contains(c.Id) && !c.IsDeleted)
                    .SelectMany(c => c.ProductCategories)
                    .Where(pc => !pc.IsDeleted)
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

        public async Task<Dictionary<int, int>> GetProductCountsAsync(IEnumerable<int> categoryIds, bool includeSubCategories = false)
        {
            try
            {
                var categoryIdsList = categoryIds.ToList();
                if (!categoryIdsList.Any())
                {
                    return new Dictionary<int, int>();
                }

                if (!includeSubCategories)
                {
                    var directCounts = await _dbSet
                        .AsNoTracking()
                        .Where(c => categoryIdsList.Contains(c.Id) && !c.IsDeleted)
                        .Select(c => new { c.Id, ProductCount = c.ProductCategories.Count(pc => !pc.IsDeleted) })
                        .ToDictionaryAsync(x => x.Id, x => x.ProductCount);

                    _logger.LogDebug("Retrieved direct product counts for {Count} categories", categoryIdsList.Count);
                    return directCounts;
                }

                var result = new Dictionary<int, int>();
                foreach (var categoryId in categoryIdsList)
                {
                    var allIds = await GetAllDescendantCategoryIdsAsync(categoryId);
                    allIds.Add(categoryId);

                    var count = await _dbSet
                        .AsNoTracking()
                        .Where(c => allIds.Contains(c.Id) && !c.IsDeleted)
                        .SelectMany(c => c.ProductCategories)
                        .Where(pc => !pc.IsDeleted)
                        .CountAsync();

                    result[categoryId] = count;
                }

                _logger.LogDebug("Retrieved hierarchical product counts for {Count} categories", categoryIdsList.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product counts for multiple categories");
                throw;
            }
        }

        public async Task<bool> HasSubCategoriesAsync(int categoryId)
        {
            try
            {
                var hasSubCategories = await _dbSet.AsNoTracking().AnyAsync(c => !c.IsDeleted && c.ParentCategoryId == categoryId);
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
                var hasProducts = await _dbSet
                    .AsNoTracking()
                    .Where(c => c.Id == categoryId && !c.IsDeleted)
                    .SelectMany(c => c.ProductCategories)
                    .AnyAsync(pc => !pc.IsDeleted);

                if (hasProducts)
                {
                    _logger.LogDebug("Category {CategoryId} cannot be deleted: has products", categoryId);
                    return false;
                }

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

        #region New IQueryable Methods for Service-Level Pagination

        public IQueryable<Category> GetCategoriesQueryable(CategorySearchDto searchDto)
        {
            try
            {
                var query = _dbSet.AsNoTracking().Where(c => !c.IsDeleted);

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

                _logger.LogDebug("Built categories query with filters: Parent={ParentId}, Active={IsActive}, Visible={IsVisible}, SearchTerm='{SearchTerm}'",
                    searchDto.ParentCategoryId, searchDto.IsActive, searchDto.IsVisible, searchDto.SearchTerm);

                return query;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building categories query");
                throw;
            }
        }

        public IQueryable<Category> GetRootCategoriesQueryable()
        {
            try
            {
                var query = _dbSet.AsNoTracking()
                    .Where(c => !c.IsDeleted && c.ParentCategoryId == null);

                _logger.LogDebug("Built root categories query");
                return query;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building root categories query");
                throw;
            }
        }

        public IQueryable<Category> GetSubCategoriesQueryable(int parentCategoryId)
        {
            try
            {
                var query = _dbSet.AsNoTracking()
                    .Where(c => !c.IsDeleted && c.ParentCategoryId == parentCategoryId);

                _logger.LogDebug("Built subcategories query for parent {ParentCategoryId}", parentCategoryId);
                return query;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building subcategories query for parent {ParentCategoryId}", parentCategoryId);
                throw;
            }
        }

        public IQueryable<Category> SearchCategoriesQueryable(CategorySearchDto searchDto)
        {
            try
            {
                var query = BuildAdvancedSearchQuery(searchDto);
                _logger.LogDebug("Built search categories query for term '{SearchTerm}'", searchDto.SearchTerm);
                return query;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building search categories query");
                throw;
            }
        }

        public async Task<int> GetQueryCountAsync(IQueryable<Category> query)
        {
            try
            {
                var count = await query.CountAsync();
                _logger.LogDebug("Query count: {Count}", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting query count");
                throw;
            }
        }

        public IQueryable<Category> ApplyIncludes(IQueryable<Category> query, bool includeImages = true, bool includeParent = true, bool includeSubCategories = false)
        {
            try
            {
                if (includeParent)
                {
                    query = query.Include(c => c.ParentCategory);
                }

                if (includeSubCategories)
                {
                    query = query.Include(c => c.SubCategories.Where(sc => !sc.IsDeleted));
                }

                if (includeImages)
                {
                    query = query.Include(c => c.Images.Where(i => !i.IsDeleted))
                        .ThenInclude(i => i.File);
                }

                _logger.LogDebug("Applied includes: Images={IncludeImages}, Parent={IncludeParent}, SubCategories={IncludeSubCategories}",
                    includeImages, includeParent, includeSubCategories);

                return query;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying includes to query");
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        private IQueryable<Category> BuildSearchQuery(CategorySearchDto searchDto)
        {
            var query = _dbSet.AsNoTracking().Where(c => !c.IsDeleted);

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
                .AsNoTracking()
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