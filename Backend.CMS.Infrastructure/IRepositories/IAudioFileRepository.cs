using Backend.CMS.Domain.Entities.Files;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IAudioFileRepository : IRepository<AudioFileEntity>
    {
        Task<IEnumerable<AudioFileEntity>> GetAudiosByFolderIdAsync(int? folderId);
        Task<IEnumerable<AudioFileEntity>> GetAudiosByDurationAsync(TimeSpan? minDuration = null, TimeSpan? maxDuration = null);
    }
}