using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IOtherFileRepository : IRepository<OtherFile>
    {
        Task<IEnumerable<OtherFile>> GetByFolderIdAsync(int folderId);
        Task<OtherFile?> GetByNameAsync(string name);
        Task<IEnumerable<OtherFile>> GetByContentTypeAsync(string contentType);
        Task<int> GetCountAsync();
        Task<int> GetCountByFolderIdAsync(int folderId);
        Task<IEnumerable<OtherFile>> GetPagedAsync(int pageNumber, int pageSize);
        Task<IEnumerable<OtherFile>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
    }
}