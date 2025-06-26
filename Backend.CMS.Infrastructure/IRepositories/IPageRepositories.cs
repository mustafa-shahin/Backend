using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IPageRepository : IRepository<Page>
    {
        Task<Page?> GetBySlugAsync(string slug);
        Task<Page?> GetPublishedBySlugAsync(string slug);
        Task<IEnumerable<Page>> GetPublishedPagesAsync();
        Task<IEnumerable<Page>> GetPageHierarchyAsync();
        Task<IEnumerable<Page>> GetChildPagesAsync(int parentPageId);
        Task<Page?> GetWithComponentsAsync(int pageId);
        Task<bool> SlugExistsAsync(string slug, int? excludePageId = null);
        Task<IEnumerable<Page>> SearchPagesAsync(string searchTerm, int page, int pageSize);
        Task<Page?> GetWithVersionsAsync(int pageId);
        Task<IEnumerable<Page>> GetPagesByStatusAsync(PageStatus status);
    }
}