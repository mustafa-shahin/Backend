using Backend.CMS.Domain.Entities.Files;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IVideoFileRepository : IRepository<VideoFileEntity>
    {
        Task<IEnumerable<VideoFileEntity>> GetVideosByFolderIdAsync(int? folderId);
        Task<IEnumerable<VideoFileEntity>> GetVideosByDimensionsAsync(int? minWidth = null, int? maxWidth = null, int? minHeight = null, int? maxHeight = null);
        Task<IEnumerable<VideoFileEntity>> GetVideosByDurationAsync(TimeSpan? minDuration = null, TimeSpan? maxDuration = null);
        Task<IEnumerable<VideoFileEntity>> GetVideosWithThumbnailsAsync();
        Task<IEnumerable<VideoFileEntity>> GetVideosWithoutThumbnailsAsync();
    }
}