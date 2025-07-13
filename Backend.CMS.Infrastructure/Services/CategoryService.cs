using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using static System.Net.Mime.MediaTypeNames;

namespace Backend.CMS.Infrastructure.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<CategoryService> _logger;
        private readonly IFileUrlBuilder _fileUrlBuilder;
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 100;
        private const int MinPageSize = 1;

        public CategoryService(
            IMapper mapper,
            ILogger<CategoryService> logger,
            IUnitOfWork unitOfWork,
            IFileUrlBuilder fileUrlBuilder)
        {
            _mapper = mapper;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _fileUrlBuilder = fileUrlBuilder;
        }

        #region Paginated Methods

        /// <summary>
        /// Get paginated categories with filtering and sorting - Service-level pagination
        /// </summary>
        public async Task<PagedResult<CategoryDto>> GetCategoriesPagedAsync(CategorySearchDto searchDto)
        {
            try
            {
                // Normalize and validate pagination parameters at service level
                searchDto = NormalizeSearchDto(searchDto);

                // Get data directly from repository
                var pagedResult = await _unitOfWork.Categories.GetCategoriesPagedAsync(searchDto);

                if (!pagedResult.Data.Any())
                {
                    _logger.LogDebug("No categories found for search criteria on page {PageNumber}", searchDto.PageNumber);
                    return new PagedResult<CategoryDto>
                    {
                        Data = new List<CategoryDto>(),
                        PageNumber = searchDto.PageNumber,
                        PageSize = searchDto.PageSize,
                        TotalCount = 0
                    };
                }

                var categoryDtos = _mapper.Map<List<CategoryDto>>(pagedResult.Data);

                // Service-level enrichment with product counts
                await EnrichCategoriesWithProductCountsAsync(categoryDtos);

                // Apply additional service-level filters if needed
                categoryDtos = await ApplyAdditionalFiltersAsync(categoryDtos, searchDto);

                _logger.LogDebug("Retrieved {CategoryCount} categories (page {PageNumber}/{TotalPages})",
                    categoryDtos.Count, pagedResult.PageNumber, pagedResult.TotalPages);

                return new PagedResult<CategoryDto>
                {
                    Data = categoryDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = pagedResult.TotalCount
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
        public async Task<PagedResult<CategoryDto>> GetRootCategoriesPagedAsync(int pageNumber, int pageSize)
        {
            try
            {
                // Service-level validation and normalization
                var normalizedPageNumber = Math.Max(MinPageSize, pageNumber);
                var normalizedPageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

                // Override with default page size for consistency
                normalizedPageSize = DefaultPageSize;

                // Get data directly from repository
                var pagedResult = await _unitOfWork.Categories.GetRootCategoriesPagedAsync(normalizedPageNumber, normalizedPageSize);

                if (!pagedResult.Data.Any())
                {
                    _logger.LogDebug("No root categories found on page {PageNumber}", normalizedPageNumber);
                    return new PagedResult<CategoryDto>
                    {
                        Data = new List<CategoryDto>(),
                        PageNumber = normalizedPageNumber,
                        PageSize = normalizedPageSize,
                        TotalCount = 0
                    };
                }

                var categoryDtos = _mapper.Map<List<CategoryDto>>(pagedResult.Data);
                await EnrichCategoriesWithProductCountsAsync(categoryDtos);

                _logger.LogDebug("Retrieved {CategoryCount} root categories (page {PageNumber}/{TotalPages})",
                    categoryDtos.Count, pagedResult.PageNumber, pagedResult.TotalPages);

                return new PagedResult<CategoryDto>
                {
                    Data = categoryDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = pagedResult.TotalCount
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
        public async Task<PagedResult<CategoryDto>> GetSubCategoriesPagedAsync(int parentCategoryId, int pageNumber, int pageSize)
        {
            try
            {
                // Service-level validation
                if (parentCategoryId <= 0)
                    throw new ArgumentException("Parent category ID must be greater than 0", nameof(parentCategoryId));

                var normalizedPageNumber = Math.Max(MinPageSize, pageNumber);
                var normalizedPageSize = DefaultPageSize; // Force consistent page size

                // Validate parent category exists
                var parentExists = await _unitOfWork.Categories.GetByIdAsync(parentCategoryId);
                if (parentExists == null)
                {
                    _logger.LogWarning("Parent category {ParentCategoryId} not found", parentCategoryId);
                    return new PagedResult<CategoryDto>
                    {
                        Data = new List<CategoryDto>(),
                        PageNumber = normalizedPageNumber,
                        PageSize = normalizedPageSize,
                        TotalCount = 0
                    };
                }

                // Get data directly from repository
                var pagedResult = await _unitOfWork.Categories.GetSubCategoriesPagedAsync(parentCategoryId, normalizedPageNumber, normalizedPageSize);

                if (!pagedResult.Data.Any())
                {
                    _logger.LogDebug("No subcategories found for parent {ParentCategoryId} on page {PageNumber}",
                        parentCategoryId, normalizedPageNumber);
                    return new PagedResult<CategoryDto>
                    {
                        Data = new List<CategoryDto>(),
                        PageNumber = normalizedPageNumber,
                        PageSize = normalizedPageSize,
                        TotalCount = 0
                    };
                }

                var categoryDtos = _mapper.Map<List<CategoryDto>>(pagedResult.Data);
                await EnrichCategoriesWithProductCountsAsync(categoryDtos);

                _logger.LogDebug("Retrieved {CategoryCount} subcategories for parent {ParentCategoryId} (page {PageNumber}/{TotalPages})",
                    categoryDtos.Count, parentCategoryId, pagedResult.PageNumber, pagedResult.TotalPages);

                return new PagedResult<CategoryDto>
                {
                    Data = categoryDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = pagedResult.TotalCount
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
        public async Task<PagedResult<CategoryDto>> SearchCategoriesPagedAsync(CategorySearchDto searchDto)
        {
            try
            {
                // Service-level validation and normalization
                searchDto = NormalizeSearchDto(searchDto);

                if (string.IsNullOrWhiteSpace(searchDto.SearchTerm))
                {
                    _logger.LogWarning("Search performed with empty search term");
                    // Fallback to regular paginated categories
                    return await GetCategoriesPagedAsync(searchDto);
                }

                // Get data directly from repository
                var pagedResult = await _unitOfWork.Categories.SearchCategoriesPagedAsync(searchDto);

                if (!pagedResult.Data.Any())
                {
                    _logger.LogDebug("No categories found for search term '{SearchTerm}' on page {PageNumber}",
                        searchDto.SearchTerm, searchDto.PageNumber);
                    return new PagedResult<CategoryDto>
                    {
                        Data = new List<CategoryDto>(),
                        PageNumber = searchDto.PageNumber,
                        PageSize = searchDto.PageSize,
                        TotalCount = 0
                    };
                }

                var categoryDtos = _mapper.Map<List<CategoryDto>>(pagedResult.Data);
                await EnrichCategoriesWithProductCountsAsync(categoryDtos);

                // Service-level post-processing filters
                categoryDtos = await ApplyAdvancedSearchFiltersAsync(categoryDtos, searchDto);

                // Recalculate total count after service-level filtering
                var finalTotalCount = categoryDtos.Count < searchDto.PageSize ?
                    ((searchDto.PageNumber - 1) * searchDto.PageSize) + categoryDtos.Count :
                    pagedResult.TotalCount;

                _logger.LogDebug("Search '{SearchTerm}' found {CategoryCount} categories (page {PageNumber}/{TotalPages})",
                    searchDto.SearchTerm, categoryDtos.Count, searchDto.PageNumber, Math.Ceiling((double)finalTotalCount / searchDto.PageSize));

                return new PagedResult<CategoryDto>
                {
                    Data = categoryDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = finalTotalCount
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
                if (categoryId <= 0)
                    throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

                var category = await _unitOfWork.Categories.GetWithSubCategoriesAsync(categoryId);
                if (category == null)
                {
                    _logger.LogDebug("Category with ID {CategoryId} not found", categoryId);
                    throw new ArgumentException($"Category with ID {categoryId} not found");
                }

                var categoryDto = _mapper.Map<CategoryDto>(category);
                categoryDto.ProductCount = await _unitOfWork.Categories.GetProductCountAsync(categoryId, true);

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
                categoryDto.ProductCount = await _unitOfWork.Categories.GetProductCountAsync(category.Id, true);

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
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

            if (updateCategoryDto == null)
                throw new ArgumentNullException(nameof(updateCategoryDto));

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
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

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
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

            var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
            if (category == null)
                throw new ArgumentException($"Category with ID {categoryId} not found");

            if (newParentCategoryId.HasValue)
            {
                if (newParentCategoryId.Value == categoryId)
                    throw new ArgumentException("Category cannot be its own parent");

                var parentExists = await _unitOfWork.Categories.GetByIdAsync(newParentCategoryId.Value);
                if (parentExists == null)
                    throw new ArgumentException($"Parent category with ID {newParentCategoryId.Value} not found");

                if (await WouldCreateCircularReferenceAsync(categoryId, newParentCategoryId.Value))
                    throw new ArgumentException("Cannot create circular reference in category hierarchy");
            }

            try
            {
                category.ParentCategoryId = newParentCategoryId;
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
            if (categoryOrders == null || !categoryOrders.Any())
                throw new ArgumentException("Category orders cannot be null or empty", nameof(categoryOrders));

            try
            {
                var categoryIds = categoryOrders.Select(co => co.CategoryId).ToList();
                var categories = await _unitOfWork.Categories.FindAsync(c => categoryIds.Contains(c.Id));

                foreach (var category in categories)
                {
                    var newOrder = categoryOrders.First(co => co.CategoryId == category.Id).SortOrder;
                    category.SortOrder = newOrder;
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
                if (categoryId <= 0)
                    return false;

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
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

            if (createImageDto == null)
                throw new ArgumentNullException(nameof(createImageDto));

            var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
            if (category == null)
                throw new ArgumentException($"Category with ID {categoryId} not found");

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
            if (categoryId <= 0)
                throw new ArgumentException("Category ID must be greater than 0", nameof(categoryId));

            if (imageOrders == null || !imageOrders.Any())
                throw new ArgumentException("Image orders cannot be null or empty", nameof(imageOrders));

            try
            {
                var imageIds = imageOrders.Select(io => io.ImageId).ToList();
                var images = await _unitOfWork.GetRepository<CategoryImage>().FindAsync(i => i.CategoryId == categoryId && imageIds.Contains(i.Id));

                foreach (var image in images)
                {
                    var newPosition = imageOrders.First(io => io.ImageId == image.Id).Position;
                    image.Position = newPosition;
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
                    categoryDto.ProductCount = await _unitOfWork.Categories.GetProductCountAsync(categoryDto.Id, true);
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
                    ProductCount = await _unitOfWork.Categories.GetProductCountAsync(category.Id, true),
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

            var validationTasks = fileIds.Select(ValidateImageAsync);
            await Task.WhenAll(validationTasks);
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