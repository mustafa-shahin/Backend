using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IDesignerService
    {
        Task<DesignerPageDto> GetDesignerPageAsync(int pageId);
        Task<DesignerPageDto> SaveDesignerPageAsync(SaveDesignerPageDto saveDto);
        Task<bool> DeleteDesignerPageAsync(int pageId);
        Task<DesignerPreviewDto> GeneratePreviewAsync(int pageId, Dictionary<string, object>? settings = null);
        Task<string> GetPreviewContentAsync(string previewToken);
        Task<DesignerPageDto> PublishPageAsync(PublishPageDto publishDto);
        Task<DesignerPageDto> UnpublishPageAsync(int pageId);
        Task<PageVersion> CreateVersionAsync(int pageId, string? changeNotes = null);
        Task<List<PageVersionDto>> GetPageVersionsAsync(int pageId);
        Task<DesignerPageDto> RestoreVersionAsync(int pageId, int versionId);
        Task<DesignerStateDto> GetDesignerStateAsync(int pageId);
        Task<DesignerStateDto> SaveDesignerStateAsync(DesignerStateDto stateDto);
        Task ClearDesignerStateAsync(int pageId);
    }
}