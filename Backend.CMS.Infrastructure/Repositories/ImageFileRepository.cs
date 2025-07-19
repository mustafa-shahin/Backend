using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class ImageFileRepository : Repository<ImageFileEntity>, IImageFileRepository
    {
        public ImageFileRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<ImageFileEntity>> GetImagesByFolderIdAsync(int? folderId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FolderId == folderId)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<ImageFileEntity>> GetImagesByDimensionsAsync(int? minWidth = null, int? maxWidth = null, int? minHeight = null, int? maxHeight = null)
        {
            var query = _dbSet.AsNoTracking()
                             .Where(f => !f.IsDeleted);

            if (minWidth.HasValue)
                query = query.Where(f => f.Width >= minWidth);
            if (maxWidth.HasValue)
                query = query.Where(f => f.Width <= maxWidth);
            if (minHeight.HasValue)
                query = query.Where(f => f.Height >= minHeight);
            if (maxHeight.HasValue)
                query = query.Where(f => f.Height <= maxHeight);

            return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
        }

        public async Task<IEnumerable<ImageFileEntity>> GetImagesWithThumbnailsAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.ThumbnailContent != null)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<ImageFileEntity>> GetImagesWithoutThumbnailsAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.ThumbnailContent == null)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }
    }
}