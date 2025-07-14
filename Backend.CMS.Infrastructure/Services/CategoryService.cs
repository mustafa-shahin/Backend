using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<CategoryService> _logger;
        private readonly IFileUrlBuilder _fileUrlBuilder;
        private readonly IScopedDbContextService _scopedDbContextService;
        private readonly IUserSessionService _userSessionService;
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 100;
        private const int MinPageSize = 1;

        public CategoryService(
            IMapper mapper,
            ILogger<CategoryService> logger,
            IUnitOfWork unitOfWork,
            IFileUrlBuilder fileUrlBuilder,
            IUserSessionService userSessionService,
            IScopedDbContextService scopedDbContextService)
        {
            _mapper = mapper;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _fileUrlBuilder = fileUrlBuilder;
            _userSessionService = userSessionService;
            _scopedDbContextService = scopedDbContextService;

        }

        #region Paginated Methods - Service-Level Pagination

        /// <summary>
        /// Get paginated categories with filtering and sorting - Service-level pagination
        /// </summary>
        public async Task<PaginatedResult<CategoryDto>> GetCategoriesPaginatedAsync(CategorySearchDto searchDto)
        {
            try
            {
                // Normalize and validate pagination parameters at service level
                searchDto = NormalizeSearchDto(searchDto);

                // Get base query from repository
                var query = _unitOfWork.Categories.GetCategoriesQueryable(searchDto);

                // Get total count before pagination
                var totalCount = await _unitOfWork.Categories.GetQueryCountAsync(query);

                if (totalCount == 0)
                {
                    _logger.LogDebug("No categories found for search criteria on page {PageNumber}", searchDto.PageNumber);
                    return new PaginatedResult<CategoryDto>
                    {
                        Data = new List<CategoryDto>(),
                        PageNumber = searchDto.PageNumber,
                        PageSize = searchDto.PageSize,
                        TotalCount = 0
                    };
                }

                // Apply sorting
                query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

                // Apply pagination
                query = query.Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                            .Take(searchDto.PageSize);

                // Apply includes and execute query
                query = _unitOfWork.Categories.ApplyIncludes(query, includeImages: true, includeParent: true, includeSubCategories: false);
                var categories = await query.ToListAsync();

                // Map to DTOs
                var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

                await EnrichCategoriesWithProductCountsBatchAsync(categoryDtos);

                // Apply additional service-level post-processing
                categoryDtos = await ApplyPostProcessingFiltersAsync(categoryDtos, searchDto);

                _logger.LogDebug("Retrieved {CategoryCount} categories (page {PageNumber}/{TotalPages}) with service-level pagination",
                    categoryDtos.Count, searchDto.PageNumber, Math.Ceiling((double)totalCount / searchDto.PageSize));

                return new PaginatedResult<CategoryDto>
                {
                    Data = categoryDtos,
                    PageNumber = searchDto.PageNumber,
                    PageSize = searchDto.PageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated categories for page {PageNumber}", searchDto.PageNumber);
                throw;
            }
        }

        /// <summary>
        /// Get paginated root categories - Service-level pagination
        /// </summary>
        public async Task<PaginatedResult<CategoryDto>> GetRootCategoriesPaginatedAsync(int pageNumber, int pageSize)
        {
            try
            {
                // Service-level validation and normalization
                var normalizedPageNumber = Math.Max(MinPageSize, pageNumber);
                var normalizedPageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

                // Get base query from repository
                var query = _unitOfWork.Categories.GetRootCategoriesQueryable();

                // Get total count before pagination
                var totalCount = await _unitOfWork.Categories.GetQueryCountAsync(query);

                if (totalCount == 0)
                {
                    _logger.LogDebug("No root categories found on page {PageNumber}", normalizedPageNumber);
                    return new PaginatedResult<CategoryDto>
                    {
                        Data = new List<CategoryDto>(),
                        PageNumber = normalizedPageNumber,
                        PageSize = normalizedPageSize,
                        TotalCount = 0
                    };
                }

                // Apply default sorting for root categories
                query = query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name);

                // Apply pagination
                query = query.Skip((normalizedPageNumber - 1) * normalizedPageSize)
                            .Take(normalizedPageSize);

                // Apply includes and execute query
                query = _unitOfWork.Categories.ApplyIncludes(query, includeImages: true, includeParent: false, includeSubCategories: false);
                var categories = await query.ToListAsync();

                // Map to DTOs
                var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

                await EnrichCategoriesWithProductCountsBatchAsync(categoryDtos);

                _logger.LogDebug("Retrieved {CategoryCount} root categories (page {PageNumber}/{TotalPages}) with service-level pagination",
                    categoryDtos.Count, normalizedPageNumber, Math.Ceiling((double)totalCount / normalizedPageSize));

                return new PaginatedResult<CategoryDto>
                {
                    Data = categoryDtos,
                    PageNumber = normalizedPageNumber,
                    PageSize = normalizedPageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated root categories for page {PageNumber}", pageNumber);
                throw;
            }
        }

        /// <summary>
        /// Get paginated subcategories - Service-level pagination
        /// </summary>
        public async Task<PaginatedResult<CategoryDto>> GetSubCategoriesPaginatedAsync(int parentCategoryId, int pageNumber, int pageSize)
        {
            try
            {
                var normalizedPageNumber = Math.Max(MinPageSize, pageNumber);
                var normalizedPageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

                // Validate parent category exists
                var parentExists = await _unitOfWork.Categories.GetByIdAsync(parentCategoryId);
                if (parentExists == null)
                {
                    _logger.LogWarning("Parent category {ParentCategoryId} not found", parentCategoryId);
                    return new PaginatedResult<CategoryDto>
                    {
                        Data = new List<CategoryDto>(),
                        PageNumber = normalizedPageNumber,
                        PageSize = normalizedPageSize,
                        TotalCount = 0
                    };
                }

                // Get base query from repository
                var query = _unitOfWork.Categories.GetSubCategoriesQueryable(parentCategoryId);

                // Get total count before pagination
                var totalCount = await _unitOfWork.Categories.GetQueryCountAsync(query);

                if (totalCount == 0)
                {
                    _logger.LogDebug("No subcategories found for parent {ParentCategoryId} on page {PageNumber}",
                        parentCategoryId, normalizedPageNumber);
                    return new PaginatedResult<CategoryDto>
                    {
                        Data = new List<CategoryDto>(),
                        PageNumber = normalizedPageNumber,
                        PageSize = normalizedPageSize,
                        TotalCount = 0
                    };
                }

                // Apply default sorting for subcategories
                query = query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name);

                // Apply pagination
                query = query.Skip((normalizedPageNumber - 1) * normalizedPageSize)
                            .Take(normalizedPageSize);

                // Apply includes and execute query
                query = _unitOfWork.Categories.ApplyIncludes(query, includeImages: true, includeParent: false, includeSubCategories: false);
                var categories = await query.ToListAsync();

                // Map to DTOs
                var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

                await EnrichCategoriesWithProductCountsBatchAsync(categoryDtos);

                _logger.LogDebug("Retrieved {CategoryCount} subcategories for parent {ParentCategoryId} (page {PageNumber}/{TotalPages}) with service-level pagination",
                    categoryDtos.Count, parentCategoryId, normalizedPageNumber, Math.Ceiling((double)totalCount / normalizedPageSize));

                return new PaginatedResult<CategoryDto>
                {
                    Data = categoryDtos,
                    PageNumber = normalizedPageNumber,
                    PageSize = normalizedPageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated subcategories for parent {ParentCategoryId} on page {PageNumber}",
                    parentCategoryId, pageNumber);
                throw;
            }
        }

        /// <summary>
        /// Search categories with pagination - Service-level pagination and filtering
        /// </summary>
        public async Task<PaginatedResult<CategoryDto>> SearchCategoriesPaginatedAsync(CategorySearchDto searchDto)
        {
            try
            {
                // Service-level validation and normalization
                searchDto = NormalizeSearchDto(searchDto);

                if (string.IsNullOrWhiteSpace(searchDto.SearchTerm))
                {
                    _logger.LogWarning("Search performed with empty search term");
                    // Fallback to regular paginated categories
                    return await GetCategoriesPaginatedAsync(searchDto);
                }

                // Get base query from repository
                var query = _unitOfWork.Categories.SearchCategoriesQueryable(searchDto);

                // Get total count before pagination
                var totalCount = await _unitOfWork.Categories.GetQueryCountAsync(query);

                if (totalCount == 0)
                {
                    _logger.LogDebug("No categories found for search term '{SearchTerm}' on page {PageNumber}",
                        searchDto.SearchTerm, searchDto.PageNumber);
                    return new PaginatedResult<CategoryDto>
                    {
                        Data = new List<CategoryDto>(),
                        PageNumber = searchDto.PageNumber,
                        PageSize = searchDto.PageSize,
                        TotalCount = 0
                    };
                }

                // Apply sorting
                query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

                // Apply pagination
                query = query.Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                            .Take(searchDto.PageSize);

                // Apply includes and execute query
                query = _unitOfWork.Categories.ApplyIncludes(query, includeImages: true, includeParent: true, includeSubCategories: false);
                var categories = await query.ToListAsync();

                // Map to DTOs
                var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

                // Service-level enrichment
                await EnrichCategoriesWithProductCountsBatchAsync(categoryDtos);

                // Apply service-level post-processing
                categoryDtos = await ApplySearchPostProcessingAsync(categoryDtos, searchDto);

                _logger.LogDebug("Search '{SearchTerm}' found {CategoryCount} categories (page {PageNumber}/{TotalPages}) with service-level pagination",
                    searchDto.SearchTerm, categoryDtos.Count, searchDto.PageNumber, Math.Ceiling((double)totalCount / searchDto.PageSize));

                return new PaginatedResult<CategoryDto>
                {
                    Data = categoryDtos,
                    PageNumber = searchDto.PageNumber,
                    PageSize = searchDto.PageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing paginated search for term '{SearchTerm}' on page {PageNumber}",
                    searchDto.SearchTerm, searchDto.PageNumber);
                throw;
            }
        }

        #endregion

        #region Individual Category Operations

        /// <summary>
        /// Get category by ID with full details
        /// </summary>
        public async Task<CategoryDto> GetCategoryByIdAsync(int categoryId)
        {
            try
            {
                var category = await _unitOfWork.Categories.GetWithSubCategoriesAsync(categoryId);
                if (category == null)
                {
                    _logger.LogDebug("Category with ID {CategoryId} not found", categoryId);
                    throw new ArgumentException($"Category with ID {categoryId} not found");
                }

                var categoryDto = _mapper.Map<CategoryDto>(category);

                // Get product count using scoped context to avoid threading issues
                categoryDto.ProductCount = await _scopedDbContextService.ExecuteWithNewContextAsync(async context =>
                {
                    return await context.Set<ProductCategory>()
                        .AsNoTracking()
                        .Where(pc => pc.CategoryId == categoryId && !pc.IsDeleted)
                        .CountAsync();
                });

                _logger.LogDebug("Retrieved category {CategoryName} (ID: {CategoryId})", category.Name, categoryId);
                return categoryDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category with ID {CategoryId}", categoryId);
                throw;
            }
        }

        /// <summary>
        /// Get category by slug
        /// </summary>
        public async Task<CategoryDto?> GetCategoryBySlugAsync(string slug)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                    throw new ArgumentException("Slug cannot be null or empty", nameof(slug));

                var normalizedSlug = slug.ToLowerInvariant().Trim();

                var category = await _unitOfWork.Categories.GetBySlugAsync(normalizedSlug);
                if (category == null)
                {
                    _logger.LogDebug("Category with slug '{Slug}' not found", normalizedSlug);
                    return null;
                }

                var categoryDto = _mapper.Map<CategoryDto>(category);

                // Get product count using scoped context
                categoryDto.ProductCount = await _scopedDbContextService.ExecuteWithNewContextAsync(async context =>
                {
                    return await context.Set<ProductCategory>()
                        .AsNoTracking()
                        .Where(pc => pc.CategoryId == category.Id && !pc.IsDeleted)
                        .CountAsync();
                });

                _logger.LogDebug("Retrieved category {CategoryName} by slug '{Slug}'", category.Name, normalizedSlug);
                return categoryDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category by slug '{Slug}'", slug);
                throw;
            }
        }

        /// <summary>
        /// Get category tree structure (non-paginated for tree display)
        /// </summary>
        public async Task<List<CategoryTreeDto>> GetCategoryTreeAsync()
        {
            try
            {
                var rootCategories = await _unitOfWork.Categories.GetCategoryTreeAsync();
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
        }

        #endregion

        #region CRUD Operations

        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto createCategoryDto)
        {
            ArgumentNullException.ThrowIfNull(createCategoryDto);

            if (string.IsNullOrWhiteSpace(createCategoryDto.Name))
                throw new ArgumentException("Category name is required", nameof(createCategoryDto));

            if (string.IsNullOrWhiteSpace(createCategoryDto.Slug))
                throw new ArgumentException("Category slug is required", nameof(createCategoryDto));

            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Validate slug uniqueness
                    if (await _unitOfWork.Categories.SlugExistsAsync(createCategoryDto.Slug))
                        throw new ArgumentException($"Category with slug '{createCategoryDto.Slug}' already exists");

                    // Validate parent category if specified
                    if (createCategoryDto.ParentCategoryId.HasValue)
                    {
                        var parentExists = await _unitOfWork.Categories.GetByIdAsync(createCategoryDto.ParentCategoryId.Value) ?? throw new ArgumentException($"Parent category with ID {createCategoryDto.ParentCategoryId.Value} not found");
                    }

                    // Validate images
                    if (createCategoryDto.Images.Count != 0)
                    {
                        await ValidateImagesAsync(createCategoryDto.Images.Select(i => i.FileId).ToList());
                    }

                    // Map DTO to entity
                    var category = _mapper.Map<Category>(createCategoryDto);
                    category.CreatedAt = DateTime.UtcNow;
                    category.CreatedByUserId = _userSessionService.GetCurrentUserId(); ;
                    await _unitOfWork.Categories.AddAsync(category);
                    await _unitOfWork.SaveChangesAsync();

                    if (createCategoryDto.Images.Count != 0)
                    {
                        await AddCategoryImagesAsync(category.Id, createCategoryDto.Images);
                        var featured = createCategoryDto.Images.FirstOrDefault(p => p.IsFeatured);
                        if (featured != null)
                        {
                            category.FeaturedImageUrl = _fileUrlBuilder.GenerateThumbnailUrl(
                                featured.FileId,
                                Domain.Enums.FileType.Image,
                                true
                            );
                        }
                        await _unitOfWork.SaveChangesAsync(); // Save image entities
                    }

                    _logger.LogInformation("Created category: {CategoryName} (ID: {CategoryId})", category.Name, category.Id);

                    // Load the complete category with relations
                    var createdCategory = await _unitOfWork.Categories.GetWithSubCategoriesAsync(category.Id);

                    return _mapper.Map<CategoryDto>(createdCategory!);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category: {CategoryName}", createCategoryDto.Name);
                throw;
            }
        }

        public async Task<CategoryDto> UpdateCategoryAsync(int categoryId, UpdateCategoryDto updateCategoryDto)
        {

            ArgumentNullException.ThrowIfNull(updateCategoryDto);

            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);


                    if (category == null)
                        throw new ArgumentException($"Category with ID {categoryId} not found");

                    // Validate slug uniqueness
                    if (await _unitOfWork.Categories.SlugExistsAsync(updateCategoryDto.Slug, categoryId))
                        throw new ArgumentException($"Category with slug '{updateCategoryDto.Slug}' already exists");

                    // Validate parent category if specified
                    if (updateCategoryDto.ParentCategoryId.HasValue)
                    {
                        if (updateCategoryDto.ParentCategoryId.Value == categoryId)
                            throw new ArgumentException("Category cannot be its own parent");

                        var parentExists = await _unitOfWork.Categories.GetByIdAsync(updateCategoryDto.ParentCategoryId.Value);
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
                    category.UpdatedByUserId = _userSessionService.GetCurrentUserId();
                    category.UpdatedAt = DateTime.UtcNow;
                    _mapper.Map(updateCategoryDto, category);
                    _unitOfWork.Categories.Update(category);

                    // Update images - properly handle existing vs new images
                    await UpdateCategoryImagesAsync(categoryId, updateCategoryDto.Images);

                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Updated category: {CategoryName} (ID: {CategoryId})", category.Name, category.Id);

                    // Clear any change tracking to avoid conflicts
                    _unitOfWork.ClearChangeTracker();

                    // Return the complete updated category
                    var updatedCategory = await _unitOfWork.Categories.GetWithSubCategoriesAsync(category.Id);
                    return _mapper.Map<CategoryDto>(updatedCategory!);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category: {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<bool> DeleteCategoryAsync(int categoryId)
        {
            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
                    if (category == null)
                    {
                        _logger.LogWarning("Attempted to delete non-existent category: {CategoryId}", categoryId);
                        return false;
                    }

                    if (!await _unitOfWork.Categories.CanDeleteAsync(categoryId))
                        throw new InvalidOperationException("Cannot delete category that has products or subcategories");

                    // Delete associated images first
                    var existingImages = await _unitOfWork.GetRepository<CategoryImage>()
                        .FindAsync(i => i.CategoryId == categoryId);

                    if (existingImages.Any())
                    {
                        await _unitOfWork.GetRepository<CategoryImage>()
                            .SoftDeleteRangeAsync(existingImages);
                    }
                    category.UpdatedByUserId = _userSessionService.GetCurrentUserId();
                    category.UpdatedAt = DateTime.UtcNow;
                    // Now delete the category
                    await _unitOfWork.Categories.SoftDeleteAsync(category);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Deleted category: {CategoryName} (ID: {CategoryId})", category.Name, category.Id);
                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category: {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<CategoryDto> MoveCategoryAsync(int categoryId, int? newParentCategoryId)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(categoryId) ?? throw new ArgumentException($"Category with ID {categoryId} not found");
            if (newParentCategoryId.HasValue)
            {
                if (newParentCategoryId.Value == categoryId)
                    throw new ArgumentException("Category cannot be its own parent");
                _ = await _unitOfWork.Categories.GetByIdAsync(newParentCategoryId.Value) ?? throw new ArgumentException($"Parent category with ID {newParentCategoryId.Value} not found");
                if (await WouldCreateCircularReferenceAsync(categoryId, newParentCategoryId.Value))
                    throw new ArgumentException("Cannot create circular reference in category hierarchy");
            }

            try
            {
                category.ParentCategoryId = newParentCategoryId;
                category.UpdatedByUserId = _userSessionService.GetCurrentUserId();
                category.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Categories.Update(category);
                await _unitOfWork.SaveChangesAsync();

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
            if (categoryOrders == null || categoryOrders.Count == 0)
                throw new ArgumentException("Category orders cannot be null or empty", nameof(categoryOrders));

            try
            {
                var categoryIds = categoryOrders.Select(co => co.CategoryId).ToList();
                var categories = await _unitOfWork.Categories.FindAsync(c => categoryIds.Contains(c.Id));

                foreach (var category in categories)
                {
                    var newOrder = categoryOrders.First(co => co.CategoryId == category.Id).SortOrder;
                    category.SortOrder = newOrder;
                    category.UpdatedByUserId = _userSessionService.GetCurrentUserId();
                    category.UpdatedAt = DateTime.UtcNow;
                }
                _unitOfWork.Categories.UpdateRange(categories);
                await _unitOfWork.SaveChangesAsync();

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
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                    return false;

                var normalizedSlug = slug.ToLowerInvariant().Trim();
                var exists = await _unitOfWork.Categories.SlugExistsAsync(normalizedSlug, excludeCategoryId);
                return !exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating slug '{Slug}'", slug);
                return false;
            }
        }

        public async Task<bool> CanDeleteAsync(int categoryId)
        {
            try
            {
                var canDelete = await _unitOfWork.Categories.CanDeleteAsync(categoryId);
                return canDelete;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if category {CategoryId} can be deleted", categoryId);
                return false;
            }
        }

        #endregion

        #region Image Management

        public async Task<CategoryImageDto> AddCategoryImageAsync(int categoryId, CreateCategoryImageDto createImageDto)
        {
            ArgumentNullException.ThrowIfNull(createImageDto);

            var category = await _unitOfWork.Categories.GetByIdAsync(categoryId) ?? throw new ArgumentException($"Category with ID {categoryId} not found");
            await ValidateImageAsync(createImageDto.FileId);
            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var categoryImage = _mapper.Map<CategoryImage>(createImageDto);
                    categoryImage.CategoryId = categoryId;

                    // If this is set as featured, remove featured flag from other images
                    if (createImageDto.IsFeatured)
                    {
                        await RemoveFeaturedFlagFromOtherImagesAsync(categoryId);
                    }
                    categoryImage.UpdatedByUserId = _userSessionService.GetCurrentUserId();
                    categoryImage.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.GetRepository<CategoryImage>().AddAsync(categoryImage);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Added image to category {CategoryId}: FileId {FileId}", categoryId, createImageDto.FileId);
                    return _mapper.Map<CategoryImageDto>(categoryImage);
                });
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

            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Use a fresh context query to get the image without tracking conflicts
                    var categoryImage = await _unitOfWork.GetRepository<CategoryImage>()
                        .FirstOrDefaultAsync(ci => ci.Id == imageId);

                    if (categoryImage == null)
                        throw new ArgumentException($"Category image with ID {imageId} not found");

                    await ValidateImageAsync(updateImageDto.FileId);

                    var oldIsFeatured = categoryImage.IsFeatured;

                    // Update properties
                    categoryImage.FileId = updateImageDto.FileId;
                    categoryImage.Alt = updateImageDto.Alt;
                    categoryImage.Caption = updateImageDto.Caption;
                    categoryImage.Position = updateImageDto.Position;
                    categoryImage.IsFeatured = updateImageDto.IsFeatured;

                    // If this image is being set as featured, remove featured flag from other images
                    if (updateImageDto.IsFeatured && !oldIsFeatured)
                    {
                        await RemoveFeaturedFlagFromOtherImagesAsync(categoryImage.CategoryId, imageId);
                    }
                    categoryImage.UpdatedByUserId = _userSessionService.GetCurrentUserId();
                    categoryImage.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.GetRepository<CategoryImage>().Update(categoryImage);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Updated category image {ImageId}", imageId);
                    return _mapper.Map<CategoryImageDto>(categoryImage);
                });
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

            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var categoryImage = await _unitOfWork.GetRepository<CategoryImage>()
                        .FirstOrDefaultAsync(ci => ci.Id == imageId);

                    if (categoryImage == null)
                    {
                        _logger.LogWarning("Attempted to delete non-existent category image: {ImageId}", imageId);
                        return false;
                    }

                    var categoryId = categoryImage.CategoryId;
                    categoryImage.UpdatedByUserId = _userSessionService.GetCurrentUserId();
                    categoryImage.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.GetRepository<CategoryImage>().SoftDeleteAsync(categoryImage);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Deleted category image {ImageId} from category {CategoryId}", imageId, categoryId);
                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category image {ImageId}", imageId);
                throw;
            }
        }

        public async Task<List<CategoryImageDto>> ReorderCategoryImagesAsync(int categoryId, List<(int ImageId, int Position)> imageOrders)
        {
            if (imageOrders == null || imageOrders.Count == 0)
                throw new ArgumentException("Image orders cannot be null or empty", nameof(imageOrders));

            try
            {
                var imageIds = imageOrders.Select(io => io.ImageId).ToList();
                var images = await _unitOfWork.GetRepository<CategoryImage>().FindAsync(i => i.CategoryId == categoryId && imageIds.Contains(i.Id));

                foreach (var image in images)
                {
                    var newPosition = imageOrders.First(io => io.ImageId == image.Id).Position;
                    image.Position = newPosition;
                    image.UpdatedByUserId = _userSessionService.GetCurrentUserId();
                    image.UpdatedAt = DateTime.UtcNow;
                }

                _unitOfWork.GetRepository<CategoryImage>().UpdateRange(images);
                await _unitOfWork.SaveChangesAsync();

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
            ArgumentNullException.ThrowIfNull(searchDto);

            // Normalize pagination parameters
            searchDto.PageNumber = Math.Max(MinPageSize, searchDto.PageNumber);
            searchDto.PageSize = Math.Clamp(searchDto.PageSize, MinPageSize, MaxPageSize);

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
        /// Apply sorting to query
        /// </summary>
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

        /// <summary>
        /// Apply post-processing filters after mapping to DTOs
        /// </summary>
        private async Task<List<CategoryDto>> ApplyPostProcessingFiltersAsync(List<CategoryDto> categoryDtos, CategorySearchDto searchDto)
        {
            var filteredCategories = categoryDtos;

            // Apply product count filters (after enrichment)
            if (searchDto.MinProductCount.HasValue)
            {
                filteredCategories = filteredCategories.Where(c => c.ProductCount >= searchDto.MinProductCount.Value).ToList();
            }

            if (searchDto.MaxProductCount.HasValue)
            {
                filteredCategories = filteredCategories.Where(c => c.ProductCount <= searchDto.MaxProductCount.Value).ToList();
            }

            // Apply any other filters here
            // For example, permission-based filtering, custom business rules, etc.

            await Task.CompletedTask; // Placeholder for async operations
            return filteredCategories;
        }

        /// <summary>
        /// Apply search-specific post-processing
        /// </summary>
        private async Task<List<CategoryDto>> ApplySearchPostProcessingAsync(List<CategoryDto> categoryDtos, CategorySearchDto searchDto)
        {
            var processedCategories = await ApplyPostProcessingFiltersAsync(categoryDtos, searchDto);

            // Apply search-specific processing like relevance scoring, highlighting, etc.
            // For example, calculate relevance scores based on search term matches

            await Task.CompletedTask; // Placeholder for async operations
            return processedCategories;
        }

        /// <summary>
        /// Enrich categories with product counts
        /// </summary>
        private async Task EnrichCategoriesWithProductCountsBatchAsync(List<CategoryDto> categoryDtos)
        {
            if (!categoryDtos.Any()) return;

            try
            {
                // Get all category IDs
                var categoryIds = categoryDtos.Select(c => c.Id).ToList();

                // Use the new batch method from repository which is thread-safe
                var productCounts = await _unitOfWork.Categories.GetProductCountsAsync(categoryIds, true);

                // Apply the counts to the DTOs
                foreach (var categoryDto in categoryDtos)
                {
                    categoryDto.ProductCount = productCounts.GetValueOrDefault(categoryDto.Id, 0);
                }

                _logger.LogDebug("Enriched {Count} categories with product counts using batch operation", categoryDtos.Count);
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
                // Use scoped context for product count to avoid threading issues
                var productCount = await _scopedDbContextService.ExecuteWithNewContextAsync(async context =>
                {
                    return await context.Set<ProductCategory>()
                        .AsNoTracking()
                        .Where(pc => pc.CategoryId == category.Id && !pc.IsDeleted)
                        .CountAsync();
                });

                var treeDto = new CategoryTreeDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    Slug = category.Slug,
                    ParentCategoryId = category.ParentCategoryId,
                    IsActive = category.IsActive,
                    SortOrder = category.SortOrder,
                    FeaturedImageUrl = category.FeaturedImageUrl,
                    ProductCount = productCount,
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
            try
            {
                var currentCategory = await _unitOfWork.Categories.GetByIdAsync(potentialParentId);

                while (currentCategory?.ParentCategoryId.HasValue == true)
                {
                    if (currentCategory.ParentCategoryId.Value == categoryId)
                        return true;

                    currentCategory = await _unitOfWork.Categories.GetByIdAsync(currentCategory.ParentCategoryId.Value);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking circular reference for category {CategoryId} with parent {PotentialParentId}",
                    categoryId, potentialParentId);
                return false;
            }
        }

        /// <summary>
        /// Validate image files
        /// </summary>
        private async Task ValidateImagesAsync(List<int> fileIds)
        {
            if (!fileIds.Any()) return;

            // Validate sequentially to avoid threading issues
            foreach (var fileId in fileIds)
            {
                await ValidateImageAsync(fileId);
            }
        }

        /// <summary>
        /// Validate single image file
        /// </summary>
        private async Task ValidateImageAsync(int fileId)
        {
            try
            {
                var file = await _unitOfWork.Files.GetByIdAsync(fileId);
                if (file == null)
                    throw new ArgumentException($"File with ID {fileId} not found");

                if (file.FileType != Domain.Enums.FileType.Image)
                    throw new ArgumentException($"File with ID {fileId} is not an image");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating image file {FileId}", fileId);
                throw;
            }
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

                await _unitOfWork.GetRepository<CategoryImage>().AddAsync(categoryImage);
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedImageAsync(categoryId);
        }

        /// <summary>
        /// Update category images during update - handle existing vs new images properly
        /// </summary>
        private async Task UpdateCategoryImagesAsync(int categoryId, List<UpdateCategoryImageDto> images)
        {
            // Get existing images
            var existingImages = await _unitOfWork.GetRepository<CategoryImage>()
                .FindAsync(i => i.CategoryId == categoryId);

            // Remove images that are not in the update list
            var imagesToKeep = images.Where(i => i.Id > 0).Select(i => i.Id).ToList();
            var imagesToRemove = existingImages.Where(i => !imagesToKeep.Contains(i.Id));

            if (imagesToRemove.Any())
            {
                await _unitOfWork.GetRepository<CategoryImage>().SoftDeleteRangeAsync(imagesToRemove);
            }

            // Update existing images and add new ones
            foreach (var imageDto in images)
            {
                if (imageDto.Id > 0)
                {
                    // Update existing image
                    var existingImage = existingImages.FirstOrDefault(i => i.Id == imageDto.Id);
                    if (existingImage != null)
                    {
                        existingImage.FileId = imageDto.FileId;
                        existingImage.Alt = imageDto.Alt;
                        existingImage.Caption = imageDto.Caption;
                        existingImage.Position = imageDto.Position;
                        existingImage.IsFeatured = imageDto.IsFeatured;

                        _unitOfWork.GetRepository<CategoryImage>().Update(existingImage);
                    }
                }
                else
                {
                    // Add new image
                    var newImage = new CategoryImage
                    {
                        CategoryId = categoryId,
                        FileId = imageDto.FileId,
                        Alt = imageDto.Alt,
                        Caption = imageDto.Caption,
                        Position = imageDto.Position,
                        IsFeatured = imageDto.IsFeatured
                    };

                    await _unitOfWork.GetRepository<CategoryImage>().AddAsync(newImage);
                }
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedImageAsync(categoryId);
        }

        /// <summary>
        /// Remove featured flag from other images when setting a new featured image
        /// </summary>
        private async Task RemoveFeaturedFlagFromOtherImagesAsync(int categoryId, int? excludeImageId = null)
        {
            var otherImages = await _unitOfWork.GetRepository<CategoryImage>().FindAsync(i =>
                i.CategoryId == categoryId &&
                i.IsFeatured &&
                (excludeImageId == null || i.Id != excludeImageId));

            if (otherImages.Any())
            {
                foreach (var image in otherImages)
                {
                    image.IsFeatured = false;
                }

                _unitOfWork.GetRepository<CategoryImage>().UpdateRange(otherImages);
            }
        }

        /// <summary>
        /// Ensure only one image is marked as featured per category
        /// </summary>
        private async Task EnsureSingleFeaturedImageAsync(int categoryId)
        {
            var featuredImages = await _unitOfWork.GetRepository<CategoryImage>()
                .FindAsync(i => i.CategoryId == categoryId && i.IsFeatured);
            var featuredImagesList = featuredImages.ToList();

            if (featuredImagesList.Count > 1)
            {
                // Keep only the first one as featured
                for (int i = 1; i < featuredImagesList.Count; i++)
                {
                    featuredImagesList[i].IsFeatured = false;
                }

                _unitOfWork.GetRepository<CategoryImage>().UpdateRange(featuredImagesList.Skip(1));
            }
            else if (!featuredImagesList.Any())
            {
                // If no featured image, make the first one featured
                var firstImage = await _unitOfWork.GetRepository<CategoryImage>()
                    .FirstOrDefaultAsync(i => i.CategoryId == categoryId);
                if (firstImage != null)
                {
                    firstImage.IsFeatured = true;
                    _unitOfWork.GetRepository<CategoryImage>().Update(firstImage);
                }
            }
        }

        #endregion
    }
}