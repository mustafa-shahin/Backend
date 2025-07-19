using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class AudioFileRepository : Repository<AudioFileEntity>, IAudioFileRepository
    {
        public AudioFileRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<AudioFileEntity>> GetAudiosByFolderIdAsync(int? folderId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FolderId == folderId)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<AudioFileEntity>> GetAudiosByDurationAsync(TimeSpan? minDuration = null, TimeSpan? maxDuration = null)
        {
            var query = _dbSet.AsNoTracking()
                             .Where(f => !f.IsDeleted && f.Duration != null);

            if (minDuration.HasValue)
                query = query.Where(f => f.Duration >= minDuration);
            if (maxDuration.HasValue)
                query = query.Where(f => f.Duration <= maxDuration);

            return await query.OrderByDescending(f => f.Duration).ToListAsync();
        }
    }
}