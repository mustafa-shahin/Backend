using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IVideoRepository : IRepository<Video>
    {
        Task<IEnumerable<Video>> GetByFolderIdAsync(int folderId);
        Task<Video?> GetByNameAsync(string name);
        Task<IEnumerable<Video>> GetByContentTypeAsync(string contentType);
        Task<int> GetCountAsync();
        Task<int> GetCountByFolderIdAsync(int folderId);
        Task<IEnumerable<Video>> GetPagedAsync(int pageNumber, int pageSize);
        Task<IEnumerable<Video>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
        Task<IEnumerable<Video>> GetByEntityAsync(string entityType, int entityId);
    }
}