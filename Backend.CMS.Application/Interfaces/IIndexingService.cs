using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Application.Interfaces
{
    public interface IIndexingService
    {
        Task<bool> IndexPagesAsync(IEnumerable<int>? pageIds = null);
        Task<bool> IndexFilesAsync(IEnumerable<int>? fileIds = null);
        Task<bool> IndexUsersAsync(IEnumerable<int>? userIds = null);
        Task<bool> IndexComponentTemplatesAsync(IEnumerable<int>? templateIds = null);
        Task<bool> RemoveFromIndexAsync(string entityType, int entityId);
        Task<bool> FullReindexAsync();
        Task<bool> IncrementalIndexAsync(DateTime? since = null);
        Task<IndexingJob> CreateIndexingJobAsync(string jobType, Dictionary<string, object>? metadata = null);
        Task<bool> UpdateIndexingJobAsync(int jobId, string status, int? processedEntities = null, int? failedEntities = null, string? errorMessage = null, int? totalEntities = null);
        Task<bool> CompleteIndexingJobAsync(int jobId, bool success = true, string? errorMessage = null);
    }
}