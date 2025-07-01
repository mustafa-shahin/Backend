using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface ISearchService
    {
        Task<SearchResponseDto> SearchAsync(SearchRequestDto searchRequest);
        Task<List<SearchResultDto>> SuggestAsync(string query, int maxSuggestions = 5);
        Task<IndexingStatusDto> GetIndexingStatusAsync();
        Task<bool> TriggerFullIndexAsync();
        Task<bool> TriggerIncrementalIndexAsync();
        Task<bool> IndexEntityAsync(string entityType, int entityId);
        Task<bool> RemoveFromIndexAsync(string entityType, int entityId);
    }
}