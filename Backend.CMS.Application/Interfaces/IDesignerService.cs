using Backend.CMS.Application.DTOs.Designer;

namespace Backend.CMS.Application.Interfaces
{
    public interface IDesignerService
    {
        Task<DesignerPageDto> GetDesignerPageAsync(int pageId);
        Task<DesignerPageDto> SaveDesignerPageAsync(SaveDesignerPageDto saveDto);
        Task<bool> DeleteDesignerPageAsync(int pageId);

        Task<DesignerComponentDto> CreateComponentAsync(CreateComponentDto createDto);
        Task<DesignerComponentDto> UpdateComponentAsync(UpdateComponentDto updateDto);
        Task<DesignerComponentDto> DuplicateComponentAsync(DuplicateComponentDto duplicateDto);
        Task<bool> DeleteComponentAsync(int componentId, string componentKey);
        Task<DesignerComponentDto> MoveComponentAsync(MoveComponentDto moveDto);
        Task<List<DesignerComponentDto>> ReorderComponentsAsync(int pageId, List<ComponentOrderDto> componentOrders);

        Task<ComponentLibraryDto> GetComponentLibraryAsync();

        Task<DesignerPreviewDto> GeneratePreviewAsync(int pageId, Dictionary<string, object>? settings = null);
        Task<string> GetPreviewContentAsync(string previewToken);

        Task<DesignerPageDto> PublishPageAsync(PublishPageDto publishDto);
        Task<DesignerPageDto> UnpublishPageAsync(int pageId);

        Task<DesignerPageDto> CreateVersionAsync(int pageId, string? changeNotes = null);
        Task<List<PageVersionDto>> GetPageVersionsAsync(int pageId);
        Task<DesignerPageDto> RestoreVersionAsync(int pageId, int versionId);

        Task<DesignerStateDto> GetDesignerStateAsync(int pageId);
        Task<DesignerStateDto> SaveDesignerStateAsync(DesignerStateDto stateDto);
        Task ClearDesignerStateAsync(int pageId);
    }
}