using Backend.CMS.Domain.Entities.Files;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IImageFileRepository : IRepository<ImageFileEntity>
    {
        Task<IEnumerable<ImageFileEntity>> GetImagesByFolderIdAsync(int? folderId);
        Task<IEnumerable<ImageFileEntity>> GetImagesByDimensionsAsync(int? minWidth = null, int? maxWidth = null, int? minHeight = null, int? maxHeight = null);
        Task<IEnumerable<ImageFileEntity>> GetImagesWithThumbnailsAsync();
        Task<IEnumerable<ImageFileEntity>> GetImagesWithoutThumbnailsAsync();
    }
}