using Backend.CMS.Application.DTOs;
using System.Threading.Tasks;

namespace Frontend.Interfaces
{
    public interface ICategoryService
    {
        Task<PagedResult<CategoryDto>> GetCategoriesAsync(int pageNumber = 1, int pageSize = 10, string? searchTerm = null);
        Task<CategoryDto?> GetCategoryByIdAsync(int id);
        Task<CategoryDto?> CreateCategoryAsync(CreateCategoryDto createCategoryDto);
        Task<CategoryDto?> UpdateCategoryAsync(int id, UpdateCategoryDto updateCategoryDto);
        Task<bool> DeleteCategoryAsync(int id);
        Task<bool> ValidateSlugAsync(string slug, int? excludeId = null);
    }
}