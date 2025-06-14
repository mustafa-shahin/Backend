using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Application.Interfaces
{
    public interface ICategoryService
    {
        Task<CategoryDto> GetCategoryByIdAsync(int categoryId);
        Task<CategoryDto?> GetCategoryBySlugAsync(string slug);
        Task<List<CategoryDto>> GetCategoriesAsync();
        Task<List<CategoryTreeDto>> GetCategoryTreeAsync();
        Task<List<CategoryDto>> GetRootCategoriesAsync();
        Task<List<CategoryDto>> GetSubCategoriesAsync(int parentCategoryId);
        Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto createCategoryDto);
        Task<CategoryDto> UpdateCategoryAsync(int categoryId, UpdateCategoryDto updateCategoryDto);
        Task<bool> DeleteCategoryAsync(int categoryId);
        Task<List<CategoryDto>> SearchCategoriesAsync(CategorySearchDto searchDto);
        Task<int> GetSearchCountAsync(CategorySearchDto searchDto);
        Task<bool> ValidateSlugAsync(string slug, int? excludeCategoryId = null);
        Task<bool> CanDeleteAsync(int categoryId);
        Task<CategoryDto> MoveCategoryAsync(int categoryId, int? newParentCategoryId);
        Task<List<CategoryDto>> ReorderCategoriesAsync(List<(int CategoryId, int SortOrder)> categoryOrders);
        Task<CategoryImageDto> AddCategoryImageAsync(int categoryId, CreateCategoryImageDto createImageDto);
        Task<CategoryImageDto> UpdateCategoryImageAsync(int imageId, UpdateCategoryImageDto updateImageDto);
        Task<bool> DeleteCategoryImageAsync(int imageId);
        Task<List<CategoryImageDto>> ReorderCategoryImagesAsync(int categoryId, List<(int ImageId, int Position)> imageOrders);
    }
}