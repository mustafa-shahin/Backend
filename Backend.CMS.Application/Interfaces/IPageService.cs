using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Application.Interfaces
{
    public interface IPageService
    {
        Task<PageDto> GetPageByIdAsync(int pageId);
        Task<PageDto> GetPageBySlugAsync(string slug);
        Task<List<PageListDto>> GetPagesAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<List<PageDto>> GetPageHierarchyAsync();
        Task<PageDto> CreatePageAsync(CreatePageDto createPageDto);
        Task<PageDto> UpdatePageAsync(int pageId, UpdatePageDto updatePageDto);
        Task<bool> DeletePageAsync(int pageId);
        Task<PageDto> PublishPageAsync(int pageId);
        Task<PageDto> UnpublishPageAsync(int pageId);
        Task<PageDto> DuplicatePageAsync(int pageId, string newName);
        Task<List<PageDto>> GetPublishedPagesAsync();
        Task<List<PageDto>> GetChildPagesAsync(int parentPageId);
        Task<bool> ValidateSlugAsync(string slug, int? excludePageId = null);
        Task<PageVersion> CreatePageVersionAsync(int pageId, string? changeNotes = null);
        Task<List<PageVersionDto>> GetPageVersionsAsync(int pageId);
        Task<PageDto> RestorePageVersionAsync(int pageId, int versionId);
    }
}