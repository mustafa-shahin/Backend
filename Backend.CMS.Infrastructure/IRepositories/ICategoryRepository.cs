﻿using Backend.CMS.Domain.Entities;

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
    }
}