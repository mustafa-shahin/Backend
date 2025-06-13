using Backend.CMS.Application.DTOs.Designer;

namespace Backend.CMS.Application.Interfaces
{
    public interface IDesignerService
    {
        #region Page Designer Operations

        /// <summary>
        /// Get page data optimized for the designer interface with grid layout
        /// </summary>
        Task<DesignerPageDto> GetDesignerPageAsync(int pageId);

        /// <summary>
        /// Save complete page structure with components and their grid positions
        /// </summary>
        Task<DesignerPageDto> SaveDesignerPageAsync(SaveDesignerPageDto saveDto);

        /// <summary>
        /// Delete a page and all its components
        /// </summary>
        Task<bool> DeleteDesignerPageAsync(int pageId);

        #endregion

        #region Component Management

        /// <summary>
        /// Create a new component directly (no templates needed)
        /// </summary>
        Task<DesignerComponentDto> CreateComponentAsync(CreateComponentDto createDto);

        /// <summary>
        /// Update component properties, styles, content, and grid position
        /// </summary>
        Task<DesignerComponentDto> UpdateComponentAsync(UpdateComponentDto updateDto);

        /// <summary>
        /// Duplicate an existing component with new grid position
        /// </summary>
        Task<DesignerComponentDto> DuplicateComponentAsync(DuplicateComponentDto duplicateDto);

        /// <summary>
        /// Delete a component and all its children
        /// </summary>
        Task<bool> DeleteComponentAsync(int componentId, string componentKey);

        /// <summary>
        /// Move component to new grid position or parent
        /// </summary>
        Task<DesignerComponentDto> MoveComponentAsync(MoveComponentDto moveDto);

        /// <summary>
        /// Reorder multiple components on the grid
        /// </summary>
        Task<List<DesignerComponentDto>> ReorderComponentsAsync(int pageId, List<ComponentOrderDto> componentOrders);

        #endregion

        #region Component Library

        /// <summary>
        /// Get available component types organized by category
        /// </summary>
        Task<ComponentLibraryDto> GetComponentLibraryAsync();

        #endregion

        #region Preview and Publishing

        /// <summary>
        /// Generate a temporary preview URL for the page
        /// </summary>
        Task<DesignerPreviewDto> GeneratePreviewAsync(int pageId, Dictionary<string, object>? settings = null);

        /// <summary>
        /// Get preview content by token (for the preview app)
        /// </summary>
        Task<string> GetPreviewContentAsync(string previewToken);

        /// <summary>
        /// Publish page to make it live
        /// </summary>
        Task<DesignerPageDto> PublishPageAsync(PublishPageDto publishDto);

        /// <summary>
        /// Unpublish page to take it offline
        /// </summary>
        Task<DesignerPageDto> UnpublishPageAsync(int pageId);

        #endregion

        #region Page Versioning

        /// <summary>
        /// Create a version snapshot of the current page state
        /// </summary>
        Task<DesignerPageDto> CreateVersionAsync(int pageId, string? changeNotes = null);

        /// <summary>
        /// Get all versions for a page
        /// </summary>
        Task<List<PageVersionDto>> GetPageVersionsAsync(int pageId);

        /// <summary>
        /// Restore page from a specific version
        /// </summary>
        Task<DesignerPageDto> RestoreVersionAsync(int pageId, int versionId);

        #endregion

        #region Designer State Management

        /// <summary>
        /// Get current designer UI state for the user (selected components, zoom level, etc.)
        /// </summary>
        Task<DesignerStateDto> GetDesignerStateAsync(int pageId);

        /// <summary>
        /// Save designer UI state for the user
        /// </summary>
        Task<DesignerStateDto> SaveDesignerStateAsync(DesignerStateDto stateDto);

        /// <summary>
        /// Clear designer state for the user
        /// </summary>
        Task ClearDesignerStateAsync(int pageId);

        #endregion
    }
}