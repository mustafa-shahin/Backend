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
        private readonly ICacheService _cacheService;
        private readonly IMapper _mapper;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(
            ICategoryRepository categoryRepository,
            ICacheService cacheService,
            IMapper mapper,
            ILogger<CategoryService> logger)
        {
            _categoryRepository = categoryRepository;
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
            });
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
            });
        }

        public async Task<List<CategoryDto>> GetCategoriesAsync()
        {
            var cacheKey = CacheKeys.AllCategories;
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var categories = await _categoryRepository.GetAllAsync();
                var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

                foreach (var categoryDto in categoryDtos)
                {
                    categoryDto.ProductCount = await _categoryRepository.GetProductCountAsync(categoryDto.Id, true);
                }

                return categoryDtos.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
            });
        }

        public async Task<List<CategoryTreeDto>> GetCategoryTreeAsync()
        {
            var cacheKey = CacheKeys.CategoryTree;
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var rootCategories = await _categoryRepository.GetCategoryTreeAsync();
                return await BuildCategoryTreeAsync(rootCategories);
            });
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
            });
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
            });
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

            var category = _mapper.Map<Category>(createCategoryDto);
            await _categoryRepository.AddAsync(category);
            await _categoryRepository.SaveChangesAsync();

            await InvalidateCategoryCache();

            _logger.LogInformation("Created category: {CategoryName} (ID: {CategoryId})", category.Name, category.Id);
            return _mapper.Map<CategoryDto>(category);
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

            _mapper.Map(updateCategoryDto, category);
            _categoryRepository.Update(category);
            await _categoryRepository.SaveChangesAsync();

            await InvalidateCategoryCache();

            _logger.LogInformation("Updated category: {CategoryName} (ID: {CategoryId})", category.Name, category.Id);
            return _mapper.Map<CategoryDto>(category);
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

        private async Task InvalidateCategoryCache()
        {
            await _cacheService.RemoveByPatternAsync(CacheKeys.CategoriesPattern);
        }
    }
}