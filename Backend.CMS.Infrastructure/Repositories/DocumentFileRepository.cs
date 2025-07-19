using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class DocumentFileRepository : Repository<DocumentFileEntity>, IDocumentFileRepository
    {
        public DocumentFileRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<DocumentFileEntity>> GetDocumentsByFolderIdAsync(int? folderId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FolderId == folderId)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<DocumentFileEntity>> GetDocumentsByPageCountAsync(int? minPages = null, int? maxPages = null)
        {
            var query = _dbSet.AsNoTracking()
                             .Where(f => !f.IsDeleted && f.PageCount != null);

            if (minPages.HasValue)
                query = query.Where(f => f.PageCount >= minPages);
            if (maxPages.HasValue)
                query = query.Where(f => f.PageCount <= maxPages);

            return await query.OrderByDescending(f => f.PageCount).ToListAsync();
        }

        public async Task<IEnumerable<DocumentFileEntity>> GetDocumentsWithThumbnailsAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.ThumbnailContent != null)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<DocumentFileEntity>> GetDocumentsWithoutThumbnailsAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.ThumbnailContent == null)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }
    }
}