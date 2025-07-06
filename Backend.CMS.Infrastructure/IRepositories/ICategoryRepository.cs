using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface ICategoryRepository : IRepository<Category>
    {
        Task<Category?> GetBySlugAsync(string slug);
        Task<IEnumerable<Category>> GetCategoryTreeAsync();
        Task<IEnumerable<Category>> GetRootCategoriesAsync();
        Task<IEnumerable<Category>> GetSubCategoriesAsync(int parentCategoryId);
        Task<Category?> GetWithSubCategoriesAsync(int categoryId);
        Task<Category?> GetWithProductsAsync(int categoryId);
        Task<bool> SlugExistsAsync(string slug, int? excludeCategoryId = null);
        Task<IEnumerable<Category>> SearchCategoriesAsync(string searchTerm, int page, int pageSize);
        Task<int> GetProductCountAsync(int categoryId, bool includeSubCategories = false);
        Task<bool> HasSubCategoriesAsync(int categoryId);
        Task<bool> CanDeleteAsync(int categoryId);

        // Paginated methods with proper return types
        Task<(IEnumerable<Category> categories, int totalCount)> GetCategoriesPagedAsync(CategorySearchDto searchDto);
        Task<(IEnumerable<Category> categories, int totalCount)> GetRootCategoriesPagedAsync(int pageNumber, int pageSize);
        Task<(IEnumerable<Category> categories, int totalCount)> GetSubCategoriesPagedAsync(int parentCategoryId, int pageNumber, int pageSize);
        Task<(IEnumerable<Category> categories, int totalCount)> SearchCategoriesPagedAsync(CategorySearchDto searchDto);
    }
}