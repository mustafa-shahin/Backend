using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class ArchiveFileRepository : Repository<ArchiveFileEntity>, IArchiveFileRepository
    {
        public ArchiveFileRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<ArchiveFileEntity>> GetArchivesByFolderIdAsync(int? folderId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FolderId == folderId)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<ArchiveFileEntity>> GetArchivesByFileCountAsync(int? minFiles = null, int? maxFiles = null)
        {
            var query = _dbSet.AsNoTracking()
                             .Where(f => !f.IsDeleted && f.FileCount != null);

            if (minFiles.HasValue)
                query = query.Where(f => f.FileCount >= minFiles);
            if (maxFiles.HasValue)
                query = query.Where(f => f.FileCount <= maxFiles);

            return await query.OrderByDescending(f => f.FileCount).ToListAsync();
        }

        public async Task<IEnumerable<ArchiveFileEntity>> GetArchivesBySizeAsync(long? minUncompressedSize = null, long? maxUncompressedSize = null)
        {
            var query = _dbSet.AsNoTracking()
                             .Where(f => !f.IsDeleted && f.UncompressedSize != null);

            if (minUncompressedSize.HasValue)
                query = query.Where(f => f.UncompressedSize >= minUncompressedSize);
            if (maxUncompressedSize.HasValue)
                query = query.Where(f => f.UncompressedSize <= maxUncompressedSize);

            return await query.OrderByDescending(f => f.UncompressedSize).ToListAsync();
        }
    }
}