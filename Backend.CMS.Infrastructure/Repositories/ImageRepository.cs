using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class ImageRepository : Repository<Image>, IImageRepository
    {
        public ImageRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Image>> GetByFolderIdAsync(int folderId)
        {
            return await _context.Images
                .Where(i => i.FolderId == folderId)
                .OrderBy(i => i.Name)
                .ToListAsync();
        }

        public async Task<Image?> GetByNameAsync(string name)
        {
            return await _context.Images
                .FirstOrDefaultAsync(i => i.Name == name);
        }

        public async Task<IEnumerable<Image>> GetByContentTypeAsync(string contentType)
        {
            return await _context.Images
                .Where(i => i.ContentType == contentType)
                .OrderBy(i => i.Name)
                .ToListAsync();
        }

        public async Task<int> GetCountAsync()
        {
            return await _context.Images.AsNoTracking().Where(x => !x.IsDeleted).CountAsync();
        }

        public async Task<int> GetCountByFolderIdAsync(int folderId)
        {
            return await _context.Images.AsNoTracking().Where(x => !x.IsDeleted && x.FolderId == folderId).CountAsync();
        }

        public async Task<IEnumerable<Image>> GetPagedAsync(int pageNumber, int pageSize)
        {
            return await _context.Images.AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Image>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            return await _context.Images.AsNoTracking()
                .Where(x => !x.IsDeleted && x.FolderId == folderId)
                .OrderBy(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Image>> GetByEntityAsync(string entityType, int entityId)
        {
            return await _context.Images.AsNoTracking()
                .Where(x => !x.IsDeleted && x.EntityType == entityType && x.EntityId == entityId)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }
    }
}