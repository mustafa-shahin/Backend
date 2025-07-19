using Backend.CMS.Domain.Entities.Files;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IOtherFileRepository : IRepository<OtherFileEntity>
    {
        Task<IEnumerable<OtherFileEntity>> GetOtherFilesByFolderIdAsync(int? folderId);
    }
}