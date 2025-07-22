using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class DocumentRepository : Repository<Document>, IDocumentRepository
    {
        public DocumentRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Document>> GetByFolderIdAsync(int folderId)
        {
            return await _context.Documents
                .Where(d => d.FolderId == folderId)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<Document?> GetByNameAsync(string name)
        {
            return await _context.Documents
                .FirstOrDefaultAsync(d => d.Name == name);
        }

        public async Task<IEnumerable<Document>> GetByContentTypeAsync(string contentType)
        {
            return await _context.Documents
                .Where(d => d.ContentType == contentType)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<int> GetCountAsync()
        {
            return await _context.Documents.AsNoTracking().Where(x => !x.IsDeleted).CountAsync();
        }

        public async Task<int> GetCountByFolderIdAsync(int folderId)
        {
            return await _context.Documents.AsNoTracking().Where(x => !x.IsDeleted && x.FolderId == folderId).CountAsync();
        }

        public async Task<IEnumerable<Document>> GetPagedAsync(int pageNumber, int pageSize)
        {
            return await _context.Documents.AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Document>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            return await _context.Documents.AsNoTracking()
                .Where(x => !x.IsDeleted && x.FolderId == folderId)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}