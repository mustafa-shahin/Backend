using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IArchiveRepository : IRepository<Archive>
    {
        Task<IEnumerable<Archive>> GetByFolderIdAsync(int folderId);
        Task<Archive?> GetByNameAsync(string name);
        Task<IEnumerable<Archive>> GetByContentTypeAsync(string contentType);
        Task<int> GetCountAsync();
        Task<int> GetCountByFolderIdAsync(int folderId);
        Task<IEnumerable<Archive>> GetPagedAsync(int pageNumber, int pageSize);
        Task<IEnumerable<Archive>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
    }
}