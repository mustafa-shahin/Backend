using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class PageRepository : GenericRepository<Page>, IPageRepository
    {
        public PageRepository(CmsDbContext context) : base(context)
        {
        }

        public async Task<Page?> GetBySlugAsync(string slug, bool includeComponents = false)
        {
            var query = _dbSet.Where(p => p.Slug == slug && !p.IsDeleted);

            if (includeComponents)
            {
                query = query.Include(p => p.Components.OrderBy(c => c.Order));
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Page>> GetPublishedPagesAsync()
        {
            return await _dbSet
                .Where(p => p.Status == Domain.Enums.PageStatus.Published && !p.IsDeleted)
                .OrderBy(p => p.Priority)
                .ToListAsync();
        }

        public async Task<IEnumerable<Page>> GetChildPagesAsync(Guid parentId)
        {
            return await _dbSet
                .Where(p => p.ParentPageId == parentId && !p.IsDeleted)
                .OrderBy(p => p.Priority)
                .ToListAsync();
        }

        public async Task<bool> SlugExistsAsync(string slug, Guid? excludePageId = null)
        {
            var query = _dbSet.Where(p => p.Slug == slug && !p.IsDeleted);

            if (excludePageId.HasValue)
            {
                query = query.Where(p => p.Id != excludePageId.Value);
            }

            return await query.AnyAsync();
        }
    }
}