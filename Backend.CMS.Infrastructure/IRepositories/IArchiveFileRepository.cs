using Backend.CMS.Domain.Entities.Files;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IArchiveFileRepository : IRepository<ArchiveFileEntity>
    {
        Task<IEnumerable<ArchiveFileEntity>> GetArchivesByFolderIdAsync(int? folderId);
        Task<IEnumerable<ArchiveFileEntity>> GetArchivesByFileCountAsync(int? minFiles = null, int? maxFiles = null);
        Task<IEnumerable<ArchiveFileEntity>> GetArchivesBySizeAsync(long? minUncompressedSize = null, long? maxUncompressedSize = null);
    }
}