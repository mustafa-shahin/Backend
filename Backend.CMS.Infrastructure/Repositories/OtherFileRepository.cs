using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class OtherFileRepository : Repository<OtherFile>, IOtherFileRepository
    {
        public OtherFileRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<OtherFile>> GetByFolderIdAsync(int folderId)
        {
            return await _context.OtherFiles
                .Where(o => o.FolderId == folderId)
                .OrderBy(o => o.Name)
                .ToListAsync();
        }

        public async Task<OtherFile?> GetByNameAsync(string name)
        {
            return await _context.OtherFiles
                .FirstOrDefaultAsync(o => o.Name == name);
        }

        public async Task<IEnumerable<OtherFile>> GetByContentTypeAsync(string contentType)
        {
            return await _context.OtherFiles
                .Where(o => o.ContentType == contentType)
                .OrderBy(o => o.Name)
                .ToListAsync();
        }

        public async Task<int> GetCountAsync()
        {
            return await _context.OtherFiles.AsNoTracking().Where(x => !x.IsDeleted).CountAsync();
        }

        public async Task<int> GetCountByFolderIdAsync(int folderId)
        {
            return await _context.OtherFiles.AsNoTracking().Where(x => !x.IsDeleted && x.FolderId == folderId).CountAsync();
        }

        public async Task<IEnumerable<OtherFile>> GetPagedAsync(int pageNumber, int pageSize)
        {
            return await _context.OtherFiles.AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<OtherFile>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            return await _context.OtherFiles.AsNoTracking()
                .Where(x => !x.IsDeleted && x.FolderId == folderId)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}