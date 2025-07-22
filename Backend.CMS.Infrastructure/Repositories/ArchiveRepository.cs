using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class ArchiveRepository : Repository<Archive>, IArchiveRepository
    {
        public ArchiveRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Archive>> GetByFolderIdAsync(int folderId)
        {
            return await _context.Archives
                .Where(a => a.FolderId == folderId)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<Archive?> GetByNameAsync(string name)
        {
            return await _context.Archives
                .FirstOrDefaultAsync(a => a.Name == name);
        }

        public async Task<IEnumerable<Archive>> GetByContentTypeAsync(string contentType)
        {
            return await _context.Archives
                .Where(a => a.ContentType == contentType)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<int> GetCountAsync()
        {
            return await _context.Archives.AsNoTracking().Where(x => !x.IsDeleted).CountAsync();
        }

        public async Task<int> GetCountByFolderIdAsync(int folderId)
        {
            return await _context.Archives.AsNoTracking().Where(x => !x.IsDeleted && x.FolderId == folderId).CountAsync();
        }

        public async Task<IEnumerable<Archive>> GetPagedAsync(int pageNumber, int pageSize)
        {
            return await _context.Archives.AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Archive>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            return await _context.Archives.AsNoTracking()
                .Where(x => !x.IsDeleted && x.FolderId == folderId)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}