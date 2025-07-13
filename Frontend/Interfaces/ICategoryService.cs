using Backend.CMS.Application.DTOs;

namespace Frontend.Interfaces
{
    public interface ICategoryService
    {
        Task<PagedResult<CategoryDto>> GetCategoriesAsync(CategorySearchDto? searchDto = null);
        Task<List<CategoryTreeDto>> GetCategoryTreeAsync();
        Task<PagedResult<CategoryDto>> GetRootCategoriesAsync(int page = 1, int pageSize = 10);
        Task<CategoryDto?> GetCategoryByIdAsync(int id);
        Task<CategoryDto?> GetCategoryBySlugAsync(string slug);
        Task<PagedResult<CategoryDto>> GetSubCategoriesAsync(int id, int page = 1, int pageSize = 10);
        Task<PagedResult<CategoryDto>> SearchCategoriesAsync(CategorySearchDto searchDto);
        Task<CategoryDto?> CreateCategoryAsync(CreateCategoryDto createCategoryDto);
        Task<CategoryDto?> UpdateCategoryAsync(int id, UpdateCategoryDto updateCategoryDto);
        Task<bool> DeleteCategoryAsync(int id);
        Task<CategoryDto?> MoveCategoryAsync(int id, int? newParentCategoryId);
        Task<List<CategoryDto>> ReorderCategoriesAsync(List<(int Id, int SortOrder)> categoryOrders);
        Task<bool> ValidateSlugAsync(string slug, int? excludeCategoryId = null);
        Task<bool> CanDeleteAsync(int id);
        Task<CategoryImageDto?> AddCategoryImageAsync(int id, CreateCategoryImageDto createImageDto);
        Task<CategoryImageDto?> UpdateCategoryImageAsync(int imageId, UpdateCategoryImageDto updateImageDto);
        Task<bool> DeleteCategoryImageAsync(int imageId);
        Task<List<CategoryImageDto>> ReorderCategoryImagesAsync(int id, List<(int Id, int Position)> imageOrders);
    }
}