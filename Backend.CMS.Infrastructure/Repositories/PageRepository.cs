using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class PageRepository : Repository<Page>, IPageRepository
    {
        public PageRepository(ApplicationDbContext context, IUserSessionService userSessionService) : base(context, userSessionService)
        {
        }

        public async Task<Page?> GetBySlugAsync(string slug)
        {
            return await _dbSet
                .Include(p => p.ChildPages.Where(cp => !cp.IsDeleted))
                .Include(p => p.Versions.Where(v => !v.IsDeleted))
                .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDeleted);
        }

        public async Task<IEnumerable<Page>> GetPublishedPagesAsync()
        {
            return await _dbSet
                .Where(p => p.Status == PageStatus.Published && !p.IsDeleted)
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Page>> GetPageHierarchyAsync()
        {
            return await _dbSet
                .Include(p => p.ChildPages.Where(cp => !cp.IsDeleted))
                .Where(p => p.ParentPageId == null && !p.IsDeleted)
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Page>> GetChildPagesAsync(int parentPageId)
        {
            return await _dbSet
                .Where(p => p.ParentPageId == parentPageId && !p.IsDeleted)
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<Page?> GetWithComponentsAsync(int pageId)
        {
            // Since components are stored as JSON in Content field, no need for includes
            return await _dbSet
                .Include(p => p.Versions.Where(v => !v.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == pageId && !p.IsDeleted);
        }

        public async Task<bool> SlugExistsAsync(string slug, int? excludePageId = null)
        {
            var query = _dbSet.Where(p => p.Slug == slug && !p.IsDeleted);

            if (excludePageId.HasValue)
                query = query.Where(p => p.Id != excludePageId.Value);

            return await query.AnyAsync();
        }

        public async Task<IEnumerable<Page>> SearchPagesAsync(string searchTerm, int page, int pageSize)
        {
            var normalizedSearch = searchTerm.ToLowerInvariant();

            return await _dbSet
                .Where(p => !p.IsDeleted && (
                    p.Name.ToLower().Contains(normalizedSearch) ||
                    p.Title.ToLower().Contains(normalizedSearch) ||
                    (p.Description != null && p.Description.ToLower().Contains(normalizedSearch))))
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<Page?> GetWithVersionsAsync(int pageId)
        {
            return await _dbSet
                .Include(p => p.Versions.Where(v => !v.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == pageId && !p.IsDeleted);
        }

        public async Task<Page?> GetPublishedBySlugAsync(string slug)
        {
            return await _dbSet
                .Where(p => p.Slug == slug &&
                           p.Status == PageStatus.Published &&
                           !p.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Page>> GetPagesByStatusAsync(PageStatus status)
        {
            return await _dbSet
                .Where(p => p.Status == status && !p.IsDeleted)
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.Name)
                .ToListAsync();
        }
    }
}