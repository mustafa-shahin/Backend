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

}
