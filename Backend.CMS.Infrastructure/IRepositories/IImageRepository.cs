using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IImageRepository : IRepository<Image>
    {
        Task<IEnumerable<Image>> GetByFolderIdAsync(int folderId);
        Task<Image?> GetByNameAsync(string name);
        Task<IEnumerable<Image>> GetByContentTypeAsync(string contentType);
        Task<int> GetCountAsync();
        Task<int> GetCountByFolderIdAsync(int folderId);
        Task<IEnumerable<Image>> GetPagedAsync(int pageNumber, int pageSize);
        Task<IEnumerable<Image>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
        Task<IEnumerable<Image>> GetByEntityAsync(string entityType, int entityId);
    }
}