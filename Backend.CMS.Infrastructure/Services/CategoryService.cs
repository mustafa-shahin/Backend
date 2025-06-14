using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Caching;
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
        private readonly IMapper _mapper;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(
            ICategoryRepository categoryRepository,
            IRepository<CategoryImage> categoryImageRepository,
            IRepository<FileEntity> fileRepository,
            ICacheService cacheService,
            IMapper mapper,
            ILogger<CategoryService> logger)
        {
            _categoryRepository = categoryRepository;
            _categoryImageRepository = categoryImageRepository;
            _fileRepository = fileRepository;
            _cacheService = cacheService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<CategoryDto> GetCategoryByIdAsync(int categoryId)
        {
            var cacheKey = CacheKeys.CategoryById(categoryId);
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var category = await _categoryRepository.GetWithSubCategoriesAsync(categoryId);
                if (category == null)
                    throw new ArgumentException($"Category with ID {categoryId} not found");

                var categoryDto = _mapper.Map<CategoryDto>(category);
                categoryDto.ProductCount = await _categoryRepository.GetProductCountAsync(categoryId, true);
                return categoryDto;
            },cacheEmptyCollections: false) ?? new CategoryDto();
        }

        public async Task<CategoryDto?> GetCategoryBySlugAsync(string slug)
        {
            var cacheKey = CacheKeys.CategoryBySlug(slug);
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var category = await _categoryRepository.GetBySlugAsync(slug);
                if (category == null) return null;

                var categoryDto = _mapper.Map<CategoryDto>(category);
                categoryDto.ProductCount = await _categoryRepository.GetProductCountAsync(category.Id, true);
                return categoryDto;
            } ,cacheEmptyCollections: false);
        }
        public async Task<List<CategoryDto>> GetCategoriesAsync()
        {
            var cacheKey = CacheKeys.AllCategories;

            // Don't cache empty collections for categories
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var categories = await _categoryRepository.GetAllAsync();
                var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

                foreach (var categoryDto in categoryDtos)
                {
                    categoryDto.ProductCount = await _categoryRepository.GetProductCountAsync(categoryDto.Id, true);
                }

                return categoryDtos.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
            }, cacheEmptyCollections: false) ?? new List<CategoryDto>();
        }

        public async Task<List<CategoryTreeDto>> GetCategoryTreeAsync()
        {
            var cacheKey = CacheKeys.CategoryTree;
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var rootCategories = await _categoryRepository.GetCategoryTreeAsync();
                return await BuildCategoryTreeAsync(rootCategories);
            }, cacheEmptyCollections: false) ?? new List<CategoryTreeDto>();
        }


        public async Task<List<CategoryDto>> GetRootCategoriesAsync()
        {
            var cacheKey = CacheKeys.RootCategories;
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var categories = await _categoryRepository.GetRootCategoriesAsync();
                var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

                foreach (var categoryDto in categoryDtos)
                {
                    categoryDto.ProductCount = await _categoryRepository.GetProductCountAsync(categoryDto.Id, true);
                }

                return categoryDtos;
            }, cacheEmptyCollections: false) ?? new List<CategoryDto>();
        }

        public async Task<List<CategoryDto>> GetSubCategoriesAsync(int parentCategoryId)
        {
            var cacheKey = CacheKeys.SubCategories(parentCategoryId);
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var categories = await _categoryRepository.GetSubCategoriesAsync(parentCategoryId);
                var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

                foreach (var categoryDto in categoryDtos)
                {
                    categoryDto.ProductCount = await _categoryRepository.GetProductCountAsync(categoryDto.Id, true);
                }

                return categoryDtos;
            }, cacheEmptyCollections: false) ?? new List<CategoryDto>();
        }

        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto createCategoryDto)
        {
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

            var category = _mapper.Map<Category>(createCategoryDto);
            await _categoryRepository.AddAsync(category);
            await _categoryRepository.SaveChangesAsync();

            // Add images
            if (createCategoryDto.Images.Any())
            {
                await AddCategoryImagesAsync(category.Id, createCategoryDto.Images);
            }

            await InvalidateCategoryCache();

            _logger.LogInformation("Created category: {CategoryName} (ID: {CategoryId})", category.Name, category.Id);

            // Return the complete category with all relations
            var createdCategory = await _categoryRepository.GetWithSubCategoriesAsync(category.Id);
            return _mapper.Map<CategoryDto>(createdCategory!);
        }

        public async Task<CategoryDto> UpdateCategoryAsync(int categoryId, UpdateCategoryDto updateCategoryDto)
        {
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

            _mapper.Map(updateCategoryDto, category);
            _categoryRepository.Update(category);

            // Update images
            await UpdateCategoryImagesAsync(categoryId, updateCategoryDto.Images);

            await _categoryRepository.SaveChangesAsync();
            await InvalidateCategoryCache();

            _logger.LogInformation("Updated category: {CategoryName} (ID: {CategoryId})", category.Name, category.Id);

            // Return the complete updated category
            var updatedCategory = await _categoryRepository.GetWithSubCategoriesAsync(category.Id);
            return _mapper.Map<CategoryDto>(updatedCategory!);
        }

        public async Task<bool> DeleteCategoryAsync(int categoryId)
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category == null) return false;

            if (!await _categoryRepository.CanDeleteAsync(categoryId))
                throw new InvalidOperationException("Cannot delete category that has products or subcategories");

            await _categoryRepository.SoftDeleteAsync(category);
            await InvalidateCategoryCache();

            _logger.LogInformation("Deleted category: {CategoryName} (ID: {CategoryId})", category.Name, category.Id);
            return true;
        }

        public async Task<List<CategoryDto>> SearchCategoriesAsync(CategorySearchDto searchDto)
        {
            var categories = await _categoryRepository.SearchCategoriesAsync(
                searchDto.SearchTerm ?? string.Empty,
                searchDto.Page,
                searchDto.PageSize);

            var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

            foreach (var categoryDto in categoryDtos)
            {
                categoryDto.ProductCount = await _categoryRepository.GetProductCountAsync(categoryDto.Id, true);
            }

            return categoryDtos;
        }

        public async Task<int> GetSearchCountAsync(CategorySearchDto searchDto)
        {
            var categories = await _categoryRepository.SearchCategoriesAsync(
                searchDto.SearchTerm ?? string.Empty,
                1,
                int.MaxValue);

            return categories.Count();
        }

        public async Task<bool> ValidateSlugAsync(string slug, int? excludeCategoryId = null)
        {
            return !await _categoryRepository.SlugExistsAsync(slug, excludeCategoryId);
        }

        public async Task<bool> CanDeleteAsync(int categoryId)
        {
            return await _categoryRepository.CanDeleteAsync(categoryId);
        }

        public async Task<CategoryDto> MoveCategoryAsync(int categoryId, int? newParentCategoryId)
        {
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

            category.ParentCategoryId = newParentCategoryId;
            _categoryRepository.Update(category);
            await _categoryRepository.SaveChangesAsync();

            await InvalidateCategoryCache();

            return _mapper.Map<CategoryDto>(category);
        }

        public async Task<List<CategoryDto>> ReorderCategoriesAsync(List<(int CategoryId, int SortOrder)> categoryOrders)
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

            await InvalidateCategoryCache();

            return _mapper.Map<List<CategoryDto>>(categories);
        }

        // Image management methods
        public async Task<CategoryImageDto> AddCategoryImageAsync(int categoryId, CreateCategoryImageDto createImageDto)
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category == null)
                throw new ArgumentException($"Category with ID {categoryId} not found");

            await ValidateImageAsync(createImageDto.FileId);

            var categoryImage = _mapper.Map<CategoryImage>(createImageDto);
            categoryImage.CategoryId = categoryId;

            // If this is set as featured, remove featured flag from other images
            if (createImageDto.IsFeatured)
            {
                await RemoveFeaturedFlagFromOtherImagesAsync(categoryId);
            }

            await _categoryImageRepository.AddAsync(categoryImage);
            await _categoryImageRepository.SaveChangesAsync();

            await InvalidateCategoryCache();

            _logger.LogInformation("Added image to category {CategoryId}: FileId {FileId}", categoryId, createImageDto.FileId);
            return _mapper.Map<CategoryImageDto>(categoryImage);
        }

        public async Task<CategoryImageDto> UpdateCategoryImageAsync(int imageId, UpdateCategoryImageDto updateImageDto)
        {
            var categoryImage = await _categoryImageRepository.GetByIdAsync(imageId);
            if (categoryImage == null)
                throw new ArgumentException($"Category image with ID {imageId} not found");

            await ValidateImageAsync(updateImageDto.FileId);

            var oldIsFeatured = categoryImage.IsFeatured;
            _mapper.Map(updateImageDto, categoryImage);

            // If this image is being set as featured, remove featured flag from other images
            if (updateImageDto.IsFeatured && !oldIsFeatured)
            {
                await RemoveFeaturedFlagFromOtherImagesAsync(categoryImage.CategoryId, imageId);
            }

            _categoryImageRepository.Update(categoryImage);
            await _categoryImageRepository.SaveChangesAsync();

            await InvalidateCategoryCache();

            _logger.LogInformation("Updated category image {ImageId}", imageId);
            return _mapper.Map<CategoryImageDto>(categoryImage);
        }

        public async Task<bool> DeleteCategoryImageAsync(int imageId)
        {
            var categoryImage = await _categoryImageRepository.GetByIdAsync(imageId);
            if (categoryImage == null) return false;

            var categoryId = categoryImage.CategoryId;
            await _categoryImageRepository.SoftDeleteAsync(categoryImage);

            await InvalidateCategoryCache();

            _logger.LogInformation("Deleted category image {ImageId} from category {CategoryId}", imageId, categoryId);
            return true;
        }

        public async Task<List<CategoryImageDto>> ReorderCategoryImagesAsync(int categoryId, List<(int ImageId, int Position)> imageOrders)
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

            await InvalidateCategoryCache();

            return _mapper.Map<List<CategoryImageDto>>(images.OrderBy(i => i.Position));
        }

        // Private helper methods
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
                    Children = await BuildCategoryTreeAsync(category.SubCategories)
                };

                treeDtos.Add(treeDto);
            }

            return treeDtos.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToList();
        }

        private async Task<bool> WouldCreateCircularReferenceAsync(int categoryId, int potentialParentId)
        {
            var currentCategory = await _categoryRepository.GetByIdAsync(potentialParentId);

            while (currentCategory?.ParentCategoryId.HasValue == true)
            {
                if (currentCategory.ParentCategoryId.Value == categoryId)
                    return true;

                currentCategory = await _categoryRepository.GetByIdAsync(currentCategory.ParentCategoryId.Value);
            }

            return false;
        }

        private async Task ValidateImagesAsync(List<int> fileIds)
        {
            foreach (var fileId in fileIds)
            {
                await ValidateImageAsync(fileId);
            }
        }

        private async Task ValidateImageAsync(int fileId)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
                throw new ArgumentException($"File with ID {fileId} not found");

            if (file.FileType != Domain.Enums.FileType.Image)
                throw new ArgumentException($"File with ID {fileId} is not an image");
        }

        private async Task AddCategoryImagesAsync(int categoryId, List<CreateCategoryImageDto> images)
        {
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

        private async Task UpdateCategoryImagesAsync(int categoryId, List<UpdateCategoryImageDto> images)
        {
            // Remove existing images
            var existingImages = await _categoryImageRepository.FindAsync(i => i.CategoryId == categoryId);
            await _categoryImageRepository.SoftDeleteRangeAsync(existingImages);

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

        private async Task RemoveFeaturedFlagFromOtherImagesAsync(int categoryId, int? excludeImageId = null)
        {
            var otherImages = await _categoryImageRepository.FindAsync(i =>
                i.CategoryId == categoryId &&
                i.IsFeatured &&
                (excludeImageId == null || i.Id != excludeImageId));

            foreach (var image in otherImages)
            {
                image.IsFeatured = false;
            }

            _categoryImageRepository.UpdateRange(otherImages);
        }

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

        private async Task InvalidateCategoryCache()
        {
            await _cacheService.RemoveByPatternAsync(CacheKeys.CategoriesPattern);
        }
    }
}