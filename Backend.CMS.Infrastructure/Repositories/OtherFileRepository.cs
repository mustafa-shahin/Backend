using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class OtherFileRepository : Repository<OtherFileEntity>, IOtherFileRepository
    {
        public OtherFileRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<OtherFileEntity>> GetOtherFilesByFolderIdAsync(int? folderId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FolderId == folderId)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }
    }
}