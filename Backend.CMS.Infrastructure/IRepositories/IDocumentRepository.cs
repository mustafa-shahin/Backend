using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IDocumentRepository : IRepository<Document>
    {
        Task<IEnumerable<Document>> GetByFolderIdAsync(int folderId);
        Task<Document?> GetByNameAsync(string name);
        Task<IEnumerable<Document>> GetByContentTypeAsync(string contentType);
        Task<int> GetCountAsync();
        Task<int> GetCountByFolderIdAsync(int folderId);
        Task<IEnumerable<Document>> GetPagedAsync(int pageNumber, int pageSize);
        Task<IEnumerable<Document>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
    }
}