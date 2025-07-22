using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IAudioRepository : IRepository<Audio>
    {
        Task<IEnumerable<Audio>> GetByFolderIdAsync(int folderId);
        Task<Audio?> GetByNameAsync(string name);
        Task<IEnumerable<Audio>> GetByContentTypeAsync(string contentType);
        Task<int> GetCountAsync();
        Task<int> GetCountByFolderIdAsync(int folderId);
        Task<IEnumerable<Audio>> GetPagedAsync(int pageNumber, int pageSize);
        Task<IEnumerable<Audio>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
        Task<IEnumerable<Audio>> GetByEntityAsync(string entityType, int entityId);
    }
}