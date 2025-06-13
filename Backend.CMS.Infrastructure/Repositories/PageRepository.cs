using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class PageRepository : Repository<Page>, IPageRepository
    {
        public PageRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Page?> GetBySlugAsync(string slug)
        {
            return await _dbSet
                .Include(p => p.Components.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.ChildComponents.Where(cc => !cc.IsDeleted))
                .FirstOrDefaultAsync(p => p.Slug == slug);
        }

        public async Task<IEnumerable<Page>> GetPublishedPagesAsync()
        {
            return await _dbSet
                .Where(p => p.Status == PageStatus.Published)
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Page>> GetPageHierarchyAsync()
        {
            return await _dbSet
                .Include(p => p.ChildPages.Where(cp => !cp.IsDeleted))
                .Where(p => p.ParentPageId == null)
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Page>> GetChildPagesAsync(int parentPageId)
        {
            return await _dbSet
                .Where(p => p.ParentPageId == parentPageId)
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<Page?> GetWithComponentsAsync(int pageId)
        {
            return await _dbSet
                .Include(p => p.Components.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.ChildComponents.Where(cc => !cc.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == pageId);
        }

        public async Task<bool> SlugExistsAsync(string slug, int? excludePageId = null)
        {
            var query = _dbSet.Where(p => p.Slug == slug);

            if (excludePageId.HasValue)
                query = query.Where(p => p.Id != excludePageId.Value);

            return await query.AnyAsync();
        }

        public async Task<IEnumerable<Page>> SearchPagesAsync(string searchTerm, int page, int pageSize)
        {
            return await _dbSet
                .Where(p => p.Name.Contains(searchTerm) ||
                           p.Title.Contains(searchTerm) ||
                           p.Description!.Contains(searchTerm))
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<Page?> GetWithVersionsAsync(int pageId)
        {
            return await _context.Pages
                .Include(p => p.Components.Where(c => !c.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == pageId);
        }
    }
}
