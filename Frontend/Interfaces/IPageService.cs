using Backend.CMS.Application.DTOs;

namespace Frontend.Interface
{
    public interface IPageService
    {
        Task<PaginatedResult<PageListDto>> GetPagesAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<PageDto?> GetPageByIdAsync(int id);
        Task<PageDto?> CreatePageAsync(CreatePageDto createPageDto);
        Task<PageDto?> UpdatePageAsync(int id, UpdatePageDto updatePageDto);
        Task<bool> DeletePageAsync(int id);
        Task<PageDto?> PublishPageAsync(int id);
        Task<PageDto?> UnpublishPageAsync(int id);
        Task<PageDto?> DuplicatePageAsync(int id, string newName);
        Task<List<PageVersionDto>> GetPageVersionsAsync(int pageId);
        Task<bool> ValidateSlugAsync(string slug, int? excludePageId = null);
    }
}

