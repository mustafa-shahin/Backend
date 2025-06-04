using Backend.CMS.Caching.Services;
using Backend.CMS.Domain.Entities;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class CachedPageRepository : IPageRepository
    {
        private readonly IPageRepository _inner;
        private readonly ICacheService _cache;

        public CachedPageRepository(IPageRepository inner, ICacheService cache)
        {
            _inner = inner;
            _cache = cache;
        }

        public async Task<Page?> GetBySlugAsync(string slug, bool includeComponents = false)
        {
            var cacheKey = $"page:slug:{slug}:components:{includeComponents}";
            return await _cache.GetOrSetAsync(cacheKey,
                () => _inner.GetBySlugAsync(slug, includeComponents),
                TimeSpan.FromMinutes(15));
        }

        public async Task<IEnumerable<Page>> GetPublishedPagesAsync()
        {
            var cacheKey = "pages:published";
            return await _cache.GetOrSetAsync(cacheKey,
                () => _inner.GetPublishedPagesAsync(),
                TimeSpan.FromMinutes(10));
        }

        public async Task<IEnumerable<Page>> GetChildPagesAsync(Guid parentId)
        {
            var cacheKey = $"pages:children:{parentId}";
            return await _cache.GetOrSetAsync(cacheKey,
                () => _inner.GetChildPagesAsync(parentId),
                TimeSpan.FromMinutes(10));
        }

        public async Task<bool> SlugExistsAsync(string slug, Guid? excludePageId = null)
        {
            // Don't cache slug existence checks as they're used for validation
            return await _inner.SlugExistsAsync(slug, excludePageId);
        }

        // Delegate all other IGenericRepository methods
        public Task<Page?> GetByIdAsync(Guid id) => _inner.GetByIdAsync(id);
        public Task<IEnumerable<Page>> GetAllAsync() => _inner.GetAllAsync();
        public Task<IEnumerable<Page>> FindAsync(Expression<Func<Page, bool>> predicate) => _inner.FindAsync(predicate);
        public Task<Page?> FirstOrDefaultAsync(Expression<Func<Page, bool>> predicate) => _inner.FirstOrDefaultAsync(predicate);
        public Task AddAsync(Page entity) => _inner.AddAsync(entity);
        public Task AddRangeAsync(IEnumerable<Page> entities) => _inner.AddRangeAsync(entities);
        public void Update(Page entity) => _inner.Update(entity);
        public void Remove(Page entity) => _inner.Remove(entity);
        public void RemoveRange(IEnumerable<Page> entities) => _inner.RemoveRange(entities);
        public Task<int> CountAsync(Expression<Func<Page, bool>>? predicate = null) => _inner.CountAsync(predicate);
        public Task<bool> AnyAsync(Expression<Func<Page, bool>> predicate) => _inner.AnyAsync(predicate);
    }
}