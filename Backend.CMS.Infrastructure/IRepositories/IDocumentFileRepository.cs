using Backend.CMS.Domain.Entities.Files;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IDocumentFileRepository : IRepository<DocumentFileEntity>
    {
        Task<IEnumerable<DocumentFileEntity>> GetDocumentsByFolderIdAsync(int? folderId);
        Task<IEnumerable<DocumentFileEntity>> GetDocumentsByPageCountAsync(int? minPages = null, int? maxPages = null);
        Task<IEnumerable<DocumentFileEntity>> GetDocumentsWithThumbnailsAsync();
        Task<IEnumerable<DocumentFileEntity>> GetDocumentsWithoutThumbnailsAsync();
    }
}