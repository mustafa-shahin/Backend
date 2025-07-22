using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class AudioRepository : Repository<Audio>, IAudioRepository
    {
        public AudioRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Audio>> GetByFolderIdAsync(int folderId)
        {
            return await _context.Audios
                .Where(a => a.FolderId == folderId)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<Audio?> GetByNameAsync(string name)
        {
            return await _context.Audios
                .FirstOrDefaultAsync(a => a.Name == name);
        }

        public async Task<IEnumerable<Audio>> GetByContentTypeAsync(string contentType)
        {
            return await _context.Audios
                .Where(a => a.ContentType == contentType)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<int> GetCountAsync()
        {
            return await _context.Audios.AsNoTracking().Where(x => !x.IsDeleted).CountAsync();
        }

        public async Task<int> GetCountByFolderIdAsync(int folderId)
        {
            return await _context.Audios.AsNoTracking().Where(x => !x.IsDeleted && x.FolderId == folderId).CountAsync();
        }

        public async Task<IEnumerable<Audio>> GetPagedAsync(int pageNumber, int pageSize)
        {
            return await _context.Audios.AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Audio>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            return await _context.Audios.AsNoTracking()
                .Where(x => !x.IsDeleted && x.FolderId == folderId)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Audio>> GetByEntityAsync(string entityType, int entityId)
        {
            return await _context.Audios.AsNoTracking()
                .Where(x => !x.IsDeleted && x.EntityType == entityType && x.EntityId == entityId)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }
    }
}