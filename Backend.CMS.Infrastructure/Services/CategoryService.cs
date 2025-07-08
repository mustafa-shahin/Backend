using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.Caching.Services;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IRepository<CategoryImage> _categoryImageRepository;
        private readonly IRepository<FileEntity> _fileRepository;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly IMapper _mapper;
        private readonly ILogger<CategoryService> _logger;

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 100;
        private const int MinPageSize = 1;

        public CategoryService(
            ICategoryRepository categoryRepository,
            IRepository<CategoryImage> categoryImageRepository,
            IRepository<FileEntity> fileRepository,
            ICacheService cacheService,
            ICacheInvalidationService cacheInvalidationService,
            ICacheKeyService cacheKeyService,
            IMapper mapper,
            ILogger<CategoryService> logger)
        {
            _categoryRepository = categoryRepository;
            _categoryImageRepository = categoryImageRepository;
            _fileRepository = fileRepository;
            _cacheService = cacheService;
            _cacheInvalidationService = cacheInvalidationService;
            _cacheKeyService = cacheKeyService;
            _mapper = mapper;
            _logger = logger;
        }

        #region Paginated Methods

        /// <summary>
        /// Get paginated categories with filtering and sorting - Service-level pagination
        /// </summary>
        public async Task<PagedResult<CategoryDto>> GetCategoriesPagedAsync(CategorySearchDto searchDto)
        {
            // Normalize and validate pagination parameters at service level
            searchDto = NormalizeSearchDto(searchDto);

            var cacheKey = _cacheKeyService.GetQueryKey<Category>("paged_categories", new
            {
                searchDto.PageNumber,
                searchDto.PageSize,
                searchDto.SearchTerm,
                searchDto.ParentCategoryId,
                searchDto.IsActive,
                searchDto.IsVisible,
                searchDto.SortBy,
                searchDto.SortDirection,
                searchDto.CreatedFrom,
                searchDto.CreatedTo,
                searchDto.UpdatedFrom,
                searchDto.UpdatedTo,
                searchDto.MinProductCount,
                searchDto.MaxProductCount,
                searchDto.HasImages,
                searchDto.MetaKeywords
            });

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    var (categories, totalCount) = await _categoryRepository.GetCategoriesPagedAsync(searchDto);

                    if (!categories.Any())
                    {
                        _logger.LogDebug("No categories found for search criteria on page {PageNumber}", searchDto.PageNumber);
                        return PagedResult<CategoryDto>.Empty(searchDto.PageNumber, searchDto.PageSize);
                    }

                    var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

                    // Service-level enrichment with product counts
                    await EnrichCategoriesWithProductCountsAsync(categoryDtos);

                    // Apply additional service-level filters if needed
                    categoryDtos = await ApplyAdditionalFiltersAsync(categoryDtos, searchDto);

                    _logger.LogDebug("Retrieved {CategoryCount} categories (page {PageNumber}/{TotalPages})",
                        categoryDtos.Count, searchDto.PageNumber, Math.Ceiling((double)totalCount / searchDto.PageSize));

                    return new PagedResult<CategoryDto>(categoryDtos, searchDto.PageNumber, searchDto.PageSize, totalCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving paginated categories for page {PageNumber}", searchDto.PageNumber);
                    throw;
                }
            }, CacheExpiration.ForDataType(CacheDataVolatility.SemiStatic));

            return result ?? PagedResult<CategoryDto>.Empty(searchDto.PageNumber, searchDto.PageSize);
        }

        /// <summary>
        /// Get paginated root categories - Service-level pagination
        /// </summary>
        public async Task<PagedResult<CategoryDto>> GetRootCategoriesPagedAsync(int pageNumber, int pageSize)
        {
            // Service-level validation and normalization
            var normalizedPageNumber = Math.Max(MinPageSize, pageNumber);
            var normalizedPageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

            // Override with default page size for consistency
            normalizedPageSize = DefaultPageSize;

            var cacheKey = _cacheKeyService.GetCollectionKey<Category>("root_categories_paged",
                new object[] { normalizedPageNumber, normalizedPageSize });

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    var (categories, totalCount) = await _categoryRepository.GetRootCategoriesPagedAsync(normalizedPageNumber, normalizedPageSize);

                    if (!categories.Any())
                    {
                        _logger.LogDebug("No root categories found on page {PageNumber}", normalizedPageNumber);
                        return PagedResult<CategoryDto>.Empty(normalizedPageNumber, normalizedPageSize);
                    }

                    var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);
                    await EnrichCategoriesWithProductCountsAsync(categoryDtos);

                    _logger.LogDebug("Retrieved {CategoryCount} root categories (page {PageNumber}/{TotalPages})",
                        categoryDtos.Count, normalizedPageNumber, Math.Ceiling((double)totalCount / normalizedPageSize));

                    return new PagedResult<CategoryDto>(categoryDtos, normalizedPageNumber, normalizedPageSize, totalCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving paginated root categories for page {PageNumber}", normalizedPageNumber);
                    throw;
                }
            }, CacheExpiration.ForDataType(CacheDataVolatility.SemiStatic));

            return result ?? PagedResult<CategoryDto>.Empty(normalizedPageNumber, normalizedPageSize);
        }

        /// <summary>
        /// Get paginated subcategories - Service-level pagination
        /// </summary>
        public async Task<PagedResult<CategoryDto>> GetSubCategoriesPagedAsync(int parentCategoryId, int pageNumber, int pageSize)
        {
            // Service-level validation
            if (parentCategoryId <= 0)
                throw new ArgumentException("Parent category ID must be greater than 0", nameof(parentCategoryId));

            var normalizedPageNumber = Math.Max(MinPageSize, pageNumber);
            var normalizedPageSize = DefaultPageSize; // Force consistent page size

            var cacheKey = _cacheKeyService.GetCollectionKey<Category>("subcategories_paged",
                new object[] { parentCategoryId, normalizedPageNumber, normalizedPageSize });

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    // Validate parent category exists
                    var parentExists = await _categoryRepository.GetByIdAsync(parentCategoryId);
                    if (parentExists == null)
                    {
                        _logger.LogWarning("Parent category {ParentCategoryId} not found", parentCategoryId);
                        return PagedResult<CategoryDto>.Empty(normalizedPageNumber, normalizedPageSize);
                    }

                    var (categories, totalCount) = await _categoryRepository.GetSubCategoriesPagedAsync(parentCategoryId, normalizedPageNumber, normalizedPageSize);

                    if (!categories.Any())
                    {
                        _logger.LogDebug("No subcategories found for parent {ParentCategoryId} on page {PageNumber}",
                            parentCategoryId, normalizedPageNumber);
                        return PagedResult<CategoryDto>.Empty(normalizedPageNumber, normalizedPageSize);
                    }

                    var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);
                    await EnrichCategoriesWithProductCountsAsync(categoryDtos);

                    _logger.LogDebug("Retrieved {CategoryCount} subcategories for parent {ParentCategoryId} (page {PageNumber}/{TotalPages})",
                        categoryDtos.Count, parentCategoryId, normalizedPageNumber, Math.Ceiling((double)totalCount / normalizedPageSize));

                    return new PagedResult<CategoryDto>(categoryDtos, normalizedPageNumber, normalizedPageSize, totalCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving paginated subcategories for parent {ParentCategoryId} on page {PageNumber}",
                        parentCategoryId, normalizedPageNumber);
                    throw;
                }
            }, CacheExpiration.ForDataType(CacheDataVolatility.SemiStatic));

            return result ?? PagedResult<CategoryDto>.Empty(normalizedPageNumber, normalizedPageSize);
        }

        /// <summary>
        /// Search categories with pagination - Service-level pagination and filtering
        /// </summary>
        public async Task<PagedResult<CategoryDto>> SearchCategoriesPagedAsync(CategorySearchDto searchDto)
        {
            // Service-level validation and normalization
            searchDto = NormalizeSearchDto(searchDto);

            if (string.IsNullOrWhiteSpace(searchDto.SearchTerm))
            {
                _logger.LogWarning("Search performed with empty search term");
                // Fallback to regular paginated categories
                return await GetCategoriesPagedAsync(searchDto);
            }

            var cacheKey = _cacheKeyService.GetQueryKey<Category>("search_categories_paged", searchDto);

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    var (categories, totalCount) = await _categoryRepository.SearchCategoriesPagedAsync(searchDto);

                    if (!categories.Any())
                    {
                        _logger.LogDebug("No categories found for search term '{SearchTerm}' on page {PageNumber}",
                            searchDto.SearchTerm, searchDto.PageNumber);
                        return PagedResult<CategoryDto>.Empty(searchDto.PageNumber, searchDto.PageSize);
                    }

                    var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);
                    await EnrichCategoriesWithProductCountsAsync(categoryDtos);

                    // Service-level post-processing filters
                    categoryDtos = await ApplyAdvancedSearchFiltersAsync(categoryDtos, searchDto);

                    // Recalculate total count after service-level filtering
                    var finalTotalCount = categoryDtos.Count < searchDto.PageSize ?
                        ((searchDto.PageNumber - 1) * searchDto.PageSize) + categoryDtos.Count :
                        totalCount;

                    _logger.LogDebug("Search '{SearchTerm}' found {CategoryCount} categories (page {PageNumber}/{TotalPages})",
                        searchDto.SearchTerm, categoryDtos.Count, searchDto.PageNumber, Math.Ceiling((double)finalTotalCount / searchDto.PageSize));

                    return new PagedResult<CategoryDto>(categoryDtos, searchDto.PageNumber, searchDto.PageSize, finalTotalCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error performing paginated search for term '{SearchTerm}' on page {PageNumber}",
                        searchDto.SearchTerm, searchDto.PageNumber);
                    throw;
                }
            }, CacheExpiration.ForDataType(CacheDataVolatility.Volatile));

            return result ?? PagedResult<CategoryDto>.Empty(searchDto.PageNumber, searchDto.PageSize);
        }

        #endregion

        #region Individual Category Operations

        /// <summary>
        /// Get category by ID with full details
        /// </summary>
        public async Task<CategoryDto> GetCategoryByIdAsync(int categoryId)
        {
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

            var cacheKey = CacheKeys.CategoryById(categoryId);

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    var category = await _categoryRepository.GetWithSubCategoriesAsync(categoryId);
                    if (category == null)
                    {
                        _logger.LogDebug("Category with ID {CategoryId} not found", categoryId);
                        return null;
                    }

                    var categoryDto = _mapper.Map<CategoryDto>(category);
                    categoryDto.ProductCount = await _categoryRepository.GetProductCountAsync(categoryId, true);

                    _logger.LogDebug("Retrieved category {CategoryName} (ID: {CategoryId})", category.Name, categoryId);
                    return categoryDto;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving category with ID {CategoryId}", categoryId);
                    throw;
                }
            }, CacheExpiration.ForDataType(CacheDataVolatility.SemiStatic));

            if (result == null)
                throw new ArgumentException($"Category with ID {categoryId} not found");

            return result;
        }

        /// <summary>
        /// Get category by slug
        /// </summary>
        public async Task<CategoryDto?> GetCategoryBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                throw new ArgumentException("Slug cannot be null or empty", nameof(slug));

            var normalizedSlug = slug.ToLowerInvariant().Trim();
            var cacheKey = CacheKeys.CategoryBySlug(normalizedSlug);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    var category = await _categoryRepository.GetBySlugAsync(normalizedSlug);
                    if (category == null)
                    {
                        _logger.LogDebug("Category with slug '{Slug}' not found", normalizedSlug);
                        return null;
                    }

                    var categoryDto = _mapper.Map<CategoryDto>(category);
                    categoryDto.ProductCount = await _categoryRepository.GetProductCountAsync(category.Id, true);

                    _logger.LogDebug("Retrieved category {CategoryName} by slug '{Slug}'", category.Name, normalizedSlug);
                    return categoryDto;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving category by slug '{Slug}'", normalizedSlug);
                    throw;
                }
            }, CacheExpiration.ForDataType(CacheDataVolatility.SemiStatic));
        }

        /// <summary>
        /// Get category tree structure (non-paginated for tree display)
        /// </summary>
        public async Task<List<CategoryTreeDto>> GetCategoryTreeAsync()
        {
            var cacheKey = CacheKeys.CategoryTree;

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    var rootCategories = await _categoryRepository.GetCategoryTreeAsync();
                    if (!rootCategories.Any())
                    {
                        _logger.LogDebug("No categories found for tree structure");
                        return new List<CategoryTreeDto>();
                    }

                    var treeStructure = await BuildCategoryTreeAsync(rootCategories);
                    _logger.LogDebug("Built category tree with {RootCount} root categories", treeStructure.Count);
                    return treeStructure;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error building category tree");
                    throw;
                }
            }, CacheExpiration.ForDataType(CacheDataVolatility.SemiStatic));

            return result ?? new List<CategoryTreeDto>();
        }

        #endregion

        #region CRUD Operations

        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto createCategoryDto)
        {
            if (createCategoryDto == null)
                throw new ArgumentNullException(nameof(createCategoryDto));

            if (string.IsNullOrWhiteSpace(createCategoryDto.Name))
                throw new ArgumentException("Category name is required", nameof(createCategoryDto));

            if (string.IsNullOrWhiteSpace(createCategoryDto.Slug))
                throw new ArgumentException("Category slug is required", nameof(createCategoryDto));

            // Validate slug uniqueness
            if (await _categoryRepository.SlugExistsAsync(createCategoryDto.Slug))
                throw new ArgumentException($"Category with slug '{createCategoryDto.Slug}' already exists");

            // Validate parent category if specified
            if (createCategoryDto.ParentCategoryId.HasValue)
            {
                var parentExists = await _categoryRepository.GetByIdAsync(createCategoryDto.ParentCategoryId.Value);
                if (parentExists == null)
                    throw new ArgumentException($"Parent category with ID {createCategoryDto.ParentCategoryId.Value} not found");
            }

            // Validate images
            if (createCategoryDto.Images.Any())
            {
                await ValidateImagesAsync(createCategoryDto.Images.Select(i => i.FileId).ToList());
            }

            try
            {
                var category = _mapper.Map<Category>(createCategoryDto);
                await _categoryRepository.AddAsync(category);
                await _categoryRepository.SaveChangesAsync();

                // Add images
                if (createCategoryDto.Images.Any())
                {
                    await AddCategoryImagesAsync(category.Id, createCategoryDto.Images);
                }

                await InvalidateCategoryCacheAsync();

                _logger.LogInformation("Created category: {CategoryName} (ID: {CategoryId})", category.Name, category.Id);

                // Return the complete category with all relations
                var createdCategory = await _categoryRepository.GetWithSubCategoriesAsync(category.Id);
                return _mapper.Map<CategoryDto>(createdCategory!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category: {CategoryName}", createCategoryDto.Name);
                throw;
            }
        }

        public async Task<CategoryDto> UpdateCategoryAsync(int categoryId, UpdateCategoryDto updateCategoryDto)
        {
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

            if (updateCategoryDto == null)
                throw new ArgumentNullException(nameof(updateCategoryDto));

            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category == null)
                throw new ArgumentException($"Category with ID {categoryId} not found");

            // Validate slug uniqueness
            if (await _categoryRepository.SlugExistsAsync(updateCategoryDto.Slug, categoryId))
                throw new ArgumentException($"Category with slug '{updateCategoryDto.Slug}' already exists");

            // Validate parent category if specified
            if (updateCategoryDto.ParentCategoryId.HasValue)
            {
                if (updateCategoryDto.ParentCategoryId.Value == categoryId)
                    throw new ArgumentException("Category cannot be its own parent");

                var parentExists = await _categoryRepository.GetByIdAsync(updateCategoryDto.ParentCategoryId.Value);
                if (parentExists == null)
                    throw new ArgumentException($"Parent category with ID {updateCategoryDto.ParentCategoryId.Value} not found");

                // Check for circular reference
                if (await WouldCreateCircularReferenceAsync(categoryId, updateCategoryDto.ParentCategoryId.Value))
                    throw new ArgumentException("Cannot create circular reference in category hierarchy");
            }

            // Validate images
            if (updateCategoryDto.Images.Any())
            {
                await ValidateImagesAsync(updateCategoryDto.Images.Select(i => i.FileId).ToList());
            }

            try
            {
                _mapper.Map(updateCategoryDto, category);
                _categoryRepository.Update(category);

                // Update images
                await UpdateCategoryImagesAsync(categoryId, updateCategoryDto.Images);

                await _categoryRepository.SaveChangesAsync();
                await InvalidateCategoryCacheAsync();

                _logger.LogInformation("Updated category: {CategoryName} (ID: {CategoryId})", category.Name, category.Id);

                // Return the complete updated category
                var updatedCategory = await _categoryRepository.GetWithSubCategoriesAsync(category.Id);
                return _mapper.Map<CategoryDto>(updatedCategory!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category: {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<bool> DeleteCategoryAsync(int categoryId)
        {
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category == null)
            {
                _logger.LogWarning("Attempted to delete non-existent category: {CategoryId}", categoryId);
                return false;
            }

            if (!await _categoryRepository.CanDeleteAsync(categoryId))
                throw new InvalidOperationException("Cannot delete category that has products or subcategories");

            try
            {
                await _categoryRepository.SoftDeleteAsync(category);
                await InvalidateCategoryCacheAsync();

                _logger.LogInformation("Deleted category: {CategoryName} (ID: {CategoryId})", category.Name, category.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category: {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<CategoryDto> MoveCategoryAsync(int categoryId, int? newParentCategoryId)
        {
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category == null)
                throw new ArgumentException($"Category with ID {categoryId} not found");

            if (newParentCategoryId.HasValue)
            {
                if (newParentCategoryId.Value == categoryId)
                    throw new ArgumentException("Category cannot be its own parent");

                var parentExists = await _categoryRepository.GetByIdAsync(newParentCategoryId.Value);
                if (parentExists == null)
                    throw new ArgumentException($"Parent category with ID {newParentCategoryId.Value} not found");

                if (await WouldCreateCircularReferenceAsync(categoryId, newParentCategoryId.Value))
                    throw new ArgumentException("Cannot create circular reference in category hierarchy");
            }

            try
            {
                category.ParentCategoryId = newParentCategoryId;
                _categoryRepository.Update(category);
                await _categoryRepository.SaveChangesAsync();

                await InvalidateCategoryCacheAsync();

                _logger.LogInformation("Moved category {CategoryId} to parent {NewParentId}", categoryId, newParentCategoryId);
                return _mapper.Map<CategoryDto>(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving category {CategoryId} to parent {NewParentId}", categoryId, newParentCategoryId);
                throw;
            }
        }

        public async Task<List<CategoryDto>> ReorderCategoriesAsync(List<(int CategoryId, int SortOrder)> categoryOrders)
        {
            if (categoryOrders == null || !categoryOrders.Any())
                throw new ArgumentException("Category orders cannot be null or empty", nameof(categoryOrders));

            try
            {
                var categoryIds = categoryOrders.Select(co => co.CategoryId).ToList();
                var categories = await _categoryRepository.FindAsync(c => categoryIds.Contains(c.Id));

                foreach (var category in categories)
                {
                    var newOrder = categoryOrders.First(co => co.CategoryId == category.Id).SortOrder;
                    category.SortOrder = newOrder;
                }

                _categoryRepository.UpdateRange(categories);
                await _categoryRepository.SaveChangesAsync();

                await InvalidateCategoryCacheAsync();

                _logger.LogInformation("Reordered {CategoryCount} categories", categories.Count());
                return _mapper.Map<List<CategoryDto>>(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering categories");
                throw;
            }
        }

        #endregion

        #region Validation Operations

        public async Task<bool> ValidateSlugAsync(string slug, int? excludeCategoryId = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return false;

            var normalizedSlug = slug.ToLowerInvariant().Trim();
            var validationKey = _cacheKeyService.GetCustomKey("category_slug_validation", normalizedSlug, excludeCategoryId?.ToString() ?? "null");

            var result = await _cacheService.GetOrAddAsync(validationKey, async () =>
            {
                var exists = await _categoryRepository.SlugExistsAsync(normalizedSlug, excludeCategoryId);
                return new BoolWrapper(!exists);
            }, CacheExpiration.ForDataType(CacheDataVolatility.HighlyVolatile));

            return result?.Value ?? false;
        }

        public async Task<bool> CanDeleteAsync(int categoryId)
        {
            if (categoryId <= 0)
                return false;

            var deleteValidationKey = _cacheKeyService.GetCustomKey("category_can_delete", categoryId.ToString());

            var result = await _cacheService.GetOrAddAsync(deleteValidationKey, async () =>
            {
                var canDelete = await _categoryRepository.CanDeleteAsync(categoryId);
                return new BoolWrapper(canDelete);
            }, CacheExpiration.ForDataType(CacheDataVolatility.HighlyVolatile));

            return result?.Value ?? false;
        }

        #endregion

        #region Image Management

        public async Task<CategoryImageDto> AddCategoryImageAsync(int categoryId, CreateCategoryImageDto createImageDto)
        {
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

            if (createImageDto == null)
                throw new ArgumentNullException(nameof(createImageDto));

            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category == null)
                throw new ArgumentException($"Category with ID {categoryId} not found");

            await ValidateImageAsync(createImageDto.FileId);

            try
            {
                var categoryImage = _mapper.Map<CategoryImage>(createImageDto);
                categoryImage.CategoryId = categoryId;

                // If this is set as featured, remove featured flag from other images
                if (createImageDto.IsFeatured)
                {
                    await RemoveFeaturedFlagFromOtherImagesAsync(categoryId);
                }

                await _categoryImageRepository.AddAsync(categoryImage);
                await _categoryImageRepository.SaveChangesAsync();

                await InvalidateCategoryCacheAsync();

                _logger.LogInformation("Added image to category {CategoryId}: FileId {FileId}", categoryId, createImageDto.FileId);
                return _mapper.Map<CategoryImageDto>(categoryImage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding image to category {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<CategoryImageDto> UpdateCategoryImageAsync(int imageId, UpdateCategoryImageDto updateImageDto)
        {
            if (imageId <= 0)
                throw new ArgumentException("Image ID must be greater than 0", nameof(imageId));

            if (updateImageDto == null)
                throw new ArgumentNullException(nameof(updateImageDto));

            var categoryImage = await _categoryImageRepository.GetByIdAsync(imageId);
            if (categoryImage == null)
                throw new ArgumentException($"Category image with ID {imageId} not found");

            await ValidateImageAsync(updateImageDto.FileId);

            try
            {
                var oldIsFeatured = categoryImage.IsFeatured;
                _mapper.Map(updateImageDto, categoryImage);

                // If this image is being set as featured, remove featured flag from other images
                if (updateImageDto.IsFeatured && !oldIsFeatured)
                {
                    await RemoveFeaturedFlagFromOtherImagesAsync(categoryImage.CategoryId, imageId);
                }

                _categoryImageRepository.Update(categoryImage);
                await _categoryImageRepository.SaveChangesAsync();

                await InvalidateCategoryCacheAsync();

                _logger.LogInformation("Updated category image {ImageId}", imageId);
                return _mapper.Map<CategoryImageDto>(categoryImage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category image {ImageId}", imageId);
                throw;
            }
        }

        public async Task<bool> DeleteCategoryImageAsync(int imageId)
        {
            if (imageId <= 0)
                throw new ArgumentException("Image ID must be greater than 0", nameof(imageId));

            var categoryImage = await _categoryImageRepository.GetByIdAsync(imageId);
            if (categoryImage == null)
            {
                _logger.LogWarning("Attempted to delete non-existent category image: {ImageId}", imageId);
                return false;
            }

            try
            {
                var categoryId = categoryImage.CategoryId;
                await _categoryImageRepository.SoftDeleteAsync(categoryImage);

                await InvalidateCategoryCacheAsync();

                _logger.LogInformation("Deleted category image {ImageId} from category {CategoryId}", imageId, categoryId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category image {ImageId}", imageId);
                throw;
            }
        }

        public async Task<List<CategoryImageDto>> ReorderCategoryImagesAsync(int categoryId, List<(int ImageId, int Position)> imageOrders)
        {
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

            if (imageOrders == null || !imageOrders.Any())
                throw new ArgumentException("Image orders cannot be null or empty", nameof(imageOrders));

            try
            {
                var imageIds = imageOrders.Select(io => io.ImageId).ToList();
                var images = await _categoryImageRepository.FindAsync(i => i.CategoryId == categoryId && imageIds.Contains(i.Id));

                foreach (var image in images)
                {
                    var newPosition = imageOrders.First(io => io.ImageId == image.Id).Position;
                    image.Position = newPosition;
                }

                _categoryImageRepository.UpdateRange(images);
                await _categoryImageRepository.SaveChangesAsync();

                await InvalidateCategoryCacheAsync();

                _logger.LogInformation("Reordered {ImageCount} images for category {CategoryId}", images.Count(), categoryId);
                return _mapper.Map<List<CategoryImageDto>>(images.OrderBy(i => i.Position));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering images for category {CategoryId}", categoryId);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Normalize and validate search DTO parameters
        /// </summary>
        private CategorySearchDto NormalizeSearchDto(CategorySearchDto searchDto)
        {
            if (searchDto == null)
                throw new ArgumentNullException(nameof(searchDto));

            // Normalize pagination parameters
            searchDto.PageNumber = Math.Max(MinPageSize, searchDto.PageNumber);
            searchDto.PageSize = DefaultPageSize; // Force consistent page size

            // Normalize search term
            if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
            {
                searchDto.SearchTerm = searchDto.SearchTerm.Trim();
            }

            // Validate and normalize sort parameters
            var validSortFields = new[] { "Name", "CreatedAt", "UpdatedAt", "SortOrder" };
            if (string.IsNullOrWhiteSpace(searchDto.SortBy) ||
                !validSortFields.Contains(searchDto.SortBy, StringComparer.OrdinalIgnoreCase))
            {
                searchDto.SortBy = "Name";
            }

            var validSortDirections = new[] { "Asc", "Desc" };
            if (string.IsNullOrWhiteSpace(searchDto.SortDirection) ||
                !validSortDirections.Contains(searchDto.SortDirection, StringComparer.OrdinalIgnoreCase))
            {
                searchDto.SortDirection = "Asc";
            }

            // Validate date ranges
            if (searchDto.CreatedFrom.HasValue && searchDto.CreatedTo.HasValue &&
                searchDto.CreatedFrom.Value > searchDto.CreatedTo.Value)
            {
                // Swap if from date is after to date
                (searchDto.CreatedFrom, searchDto.CreatedTo) = (searchDto.CreatedTo, searchDto.CreatedFrom);
            }

            if (searchDto.UpdatedFrom.HasValue && searchDto.UpdatedTo.HasValue &&
                searchDto.UpdatedFrom.Value > searchDto.UpdatedTo.Value)
            {
                // Swap if from date is after to date
                (searchDto.UpdatedFrom, searchDto.UpdatedTo) = (searchDto.UpdatedTo, searchDto.UpdatedFrom);
            }

            // Validate product count range
            if (searchDto.MinProductCount.HasValue && searchDto.MaxProductCount.HasValue &&
                searchDto.MinProductCount.Value > searchDto.MaxProductCount.Value)
            {
                // Swap if min is greater than max
                (searchDto.MinProductCount, searchDto.MaxProductCount) = (searchDto.MaxProductCount, searchDto.MinProductCount);
            }

            return searchDto;
        }

        /// <summary>
        /// Apply additional service-level filters that couldn't be applied at repository level
        /// </summary>
        private async Task<List<CategoryDto>> ApplyAdditionalFiltersAsync(List<CategoryDto> categoryDtos, CategorySearchDto searchDto)
        {
            var filteredCategories = categoryDtos;

            // Apply product count filters at service level
            if (searchDto.MinProductCount.HasValue)
            {
                filteredCategories = filteredCategories.Where(c => c.ProductCount >= searchDto.MinProductCount.Value).ToList();
            }

            if (searchDto.MaxProductCount.HasValue)
            {
                filteredCategories = filteredCategories.Where(c => c.ProductCount <= searchDto.MaxProductCount.Value).ToList();
            }

            return filteredCategories;
        }

        /// <summary>
        /// Apply advanced search filters at service level
        /// </summary>
        private async Task<List<CategoryDto>> ApplyAdvancedSearchFiltersAsync(List<CategoryDto> categoryDtos, CategorySearchDto searchDto)
        {
            var filteredCategories = await ApplyAdditionalFiltersAsync(categoryDtos, searchDto);

            // Apply any additional advanced filters here if needed
            // This method allows for complex business logic that can't be easily expressed in SQL

            return filteredCategories;
        }

        /// <summary>
        /// Enrich categories with product counts efficiently
        /// </summary>
        private async Task EnrichCategoriesWithProductCountsAsync(List<CategoryDto> categoryDtos)
        {
            if (!categoryDtos.Any()) return;

            try
            {
                var productCountTasks = categoryDtos.Select(async categoryDto =>
                {
                    categoryDto.ProductCount = await _categoryRepository.GetProductCountAsync(categoryDto.Id, true);
                });

                await Task.WhenAll(productCountTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching categories with product counts");
                // Don't throw, just log and continue with zero counts
                foreach (var category in categoryDtos)
                {
                    category.ProductCount = 0;
                }
            }
        }

        /// <summary>
        /// Build hierarchical category tree structure
        /// </summary>
        private async Task<List<CategoryTreeDto>> BuildCategoryTreeAsync(IEnumerable<Category> categories)
        {
            var treeDtos = new List<CategoryTreeDto>();

            foreach (var category in categories)
            {
                var treeDto = new CategoryTreeDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    Slug = category.Slug,
                    ParentCategoryId = category.ParentCategoryId,
                    IsActive = category.IsActive,
                    SortOrder = category.SortOrder,
                    FeaturedImageUrl = category.FeaturedImageUrl,
                    ProductCount = await _categoryRepository.GetProductCountAsync(category.Id, true),
                    Children = await BuildCategoryTreeAsync(category.SubCategories),
                    Level = 0, // Will be calculated separately if needed
                    Path = category.Slug,
                    TotalDescendants = category.SubCategories.Count
                };

                treeDtos.Add(treeDto);
            }

            return treeDtos.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToList();
        }

        /// <summary>
        /// Check for circular reference in category hierarchy
        /// </summary>
        private async Task<bool> WouldCreateCircularReferenceAsync(int categoryId, int potentialParentId)
        {
            var circularCheckKey = _cacheKeyService.GetCustomKey("category_circular_check", categoryId.ToString(), potentialParentId.ToString());

            var result = await _cacheService.GetOrAddAsync(circularCheckKey, async () =>
            {
                var currentCategory = await _categoryRepository.GetByIdAsync(potentialParentId);

                while (currentCategory?.ParentCategoryId.HasValue == true)
                {
                    if (currentCategory.ParentCategoryId.Value == categoryId)
                        return new BoolWrapper(true);

                    currentCategory = await _categoryRepository.GetByIdAsync(currentCategory.ParentCategoryId.Value);
                }

                return new BoolWrapper(false);
            }, CacheExpiration.ForDataType(CacheDataVolatility.HighlyVolatile));

            return result?.Value ?? false;
        }

        /// <summary>
        /// Validate image files
        /// </summary>
        private async Task ValidateImagesAsync(List<int> fileIds)
        {
            if (!fileIds.Any()) return;

            var validationTasks = fileIds.Select(ValidateImageAsync);
            await Task.WhenAll(validationTasks);
        }

        /// <summary>
        /// Validate single image file
        /// </summary>
        private async Task ValidateImageAsync(int fileId)
        {
            var validationKey = _cacheKeyService.GetCustomKey("file_image_validation", fileId.ToString());

            var result = await _cacheService.GetOrAddAsync(validationKey, async () =>
            {
                var file = await _fileRepository.GetByIdAsync(fileId);
                if (file == null)
                    throw new ArgumentException($"File with ID {fileId} not found");

                if (file.FileType != Domain.Enums.FileType.Image)
                    throw new ArgumentException($"File with ID {fileId} is not an image");

                return new BoolWrapper(true);
            }, CacheExpiration.ForDataType(CacheDataVolatility.Static));

            if (result?.Value != true)
                throw new ArgumentException($"File validation failed for ID {fileId}");
        }

        /// <summary>
        /// Add category images during creation
        /// </summary>
        private async Task AddCategoryImagesAsync(int categoryId, List<CreateCategoryImageDto> images)
        {
            if (!images.Any()) return;

            foreach (var imageDto in images)
            {
                var categoryImage = _mapper.Map<CategoryImage>(imageDto);
                categoryImage.CategoryId = categoryId;

                await _categoryImageRepository.AddAsync(categoryImage);
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedImageAsync(categoryId);
            await _categoryImageRepository.SaveChangesAsync();
        }

        /// <summary>
        /// Update category images during update
        /// </summary>
        private async Task UpdateCategoryImagesAsync(int categoryId, List<UpdateCategoryImageDto> images)
        {
            // Remove existing images (soft delete)
            var existingImages = await _categoryImageRepository.FindAsync(i => i.CategoryId == categoryId);
            if (existingImages.Any())
            {
                await _categoryImageRepository.SoftDeleteRangeAsync(existingImages);
            }

            // Add new images
            foreach (var imageDto in images)
            {
                var categoryImage = new CategoryImage
                {
                    CategoryId = categoryId,
                    FileId = imageDto.FileId,
                    Alt = imageDto.Alt,
                    Caption = imageDto.Caption,
                    Position = imageDto.Position,
                    IsFeatured = imageDto.IsFeatured
                };

                await _categoryImageRepository.AddAsync(categoryImage);
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedImageAsync(categoryId);
        }

        /// <summary>
        /// Remove featured flag from other images when setting a new featured image
        /// </summary>
        private async Task RemoveFeaturedFlagFromOtherImagesAsync(int categoryId, int? excludeImageId = null)
        {
            var otherImages = await _categoryImageRepository.FindAsync(i =>
                i.CategoryId == categoryId &&
                i.IsFeatured &&
                (excludeImageId == null || i.Id != excludeImageId));

            if (otherImages.Any())
            {
                foreach (var image in otherImages)
                {
                    image.IsFeatured = false;
                }

                _categoryImageRepository.UpdateRange(otherImages);
            }
        }

        /// <summary>
        /// Ensure only one image is marked as featured per category
        /// </summary>
        private async Task EnsureSingleFeaturedImageAsync(int categoryId)
        {
            var featuredImages = await _categoryImageRepository.FindAsync(i => i.CategoryId == categoryId && i.IsFeatured);
            var featuredImagesList = featuredImages.ToList();

            if (featuredImagesList.Count > 1)
            {
                // Keep only the first one as featured
                for (int i = 1; i < featuredImagesList.Count; i++)
                {
                    featuredImagesList[i].IsFeatured = false;
                }

                _categoryImageRepository.UpdateRange(featuredImagesList.Skip(1));
            }
            else if (!featuredImagesList.Any())
            {
                // If no featured image, make the first one featured
                var firstImage = await _categoryImageRepository.FirstOrDefaultAsync(i => i.CategoryId == categoryId);
                if (firstImage != null)
                {
                    firstImage.IsFeatured = true;
                    _categoryImageRepository.Update(firstImage);
                }
            }
        }

        /// <summary>
        /// cache invalidation for category-related data
        /// </summary>
        private async Task InvalidateCategoryCacheAsync()
        {
            try
            {
                // Invalidate all category-related cache entries using the invalidation service
                await _cacheInvalidationService.InvalidateEntityTypeAsync<Category>();

                // Invalidate specific patterns that might be affected
                await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.CategoryPattern);

                // Invalidate search results and other related cache entries
                await _cacheInvalidationService.InvalidateByPatternAsync("category_*");
                await _cacheInvalidationService.InvalidateByPatternAsync("*category*");

                _logger.LogDebug("Category cache invalidated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating category cache");
                // Don't throw as this is not critical for the operation
            }
        }

        #endregion
    }
}