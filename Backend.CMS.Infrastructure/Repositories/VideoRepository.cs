using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class VideoRepository : Repository<Video>, IVideoRepository
    {
        public VideoRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Video>> GetByFolderIdAsync(int folderId)
        {
            return await _context.Videos
                .Where(v => v.FolderId == folderId)
                .OrderBy(v => v.Name)
                .ToListAsync();
        }

        public async Task<Video?> GetByNameAsync(string name)
        {
            return await _context.Videos
                .FirstOrDefaultAsync(v => v.Name == name);
        }

        public async Task<IEnumerable<Video>> GetByContentTypeAsync(string contentType)
        {
            return await _context.Videos
                .Where(v => v.ContentType == contentType)
                .OrderBy(v => v.Name)
                .ToListAsync();
        }

        public async Task<int> GetCountAsync()
        {
            return await _context.Videos.AsNoTracking().Where(x => !x.IsDeleted).CountAsync();
        }

        public async Task<int> GetCountByFolderIdAsync(int folderId)
        {
            return await _context.Videos.AsNoTracking().Where(x => !x.IsDeleted && x.FolderId == folderId).CountAsync();
        }

        public async Task<IEnumerable<Video>> GetPagedAsync(int pageNumber, int pageSize)
        {
            return await _context.Videos.AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Video>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            return await _context.Videos.AsNoTracking()
                .Where(x => !x.IsDeleted && x.FolderId == folderId)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Video>> GetByEntityAsync(string entityType, int entityId)
        {
            return await _context.Videos.AsNoTracking()
                .Where(x => !x.IsDeleted && x.EntityType == entityType && x.EntityId == entityId)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }
    }
}