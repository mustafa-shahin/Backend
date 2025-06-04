using Backend.CMS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Infrastructure.Repositories
{
    public interface IPageRepository : IGenericRepository<Page>
    {
        Task<Page?> GetBySlugAsync(string slug, bool includeComponents = false);
        Task<IEnumerable<Page>> GetPublishedPagesAsync();
        Task<IEnumerable<Page>> GetChildPagesAsync(Guid parentId);
        Task<bool> SlugExistsAsync(string slug, Guid? excludePageId = null);
    }

    // src/Backend.CMS.Infrastructure/Repositories/CachedPageRepository.cs
    public class CachedPageRepository : IPageRepository
    {
        private readonly IPageRepository _inner;
        private readonly ICacheService _cache;

        public async Task<Page?> GetBySlugAsync(string slug, bool includeComponents = false)
        {
            var cacheKey = $"page:slug:{slug}:components:{includeComponents}";
            return await _cache.GetOrSetAsync(cacheKey,
                () => _inner.GetBySlugAsync(slug, includeComponents),
                TimeSpan.FromMinutes(15));
        }
    }
}
