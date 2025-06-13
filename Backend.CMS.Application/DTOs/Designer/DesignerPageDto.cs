using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Application.DTOs.Designer
{
    // Main page DTO for designer
    public class DesignerPageDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public PageStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }

        // Grid layout settings
        public DesignerPageLayoutDto Layout { get; set; } = new();
        public List<DesignerComponentDto> Components { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public Dictionary<string, object> Styles { get; set; } = new();
        public bool HasUnsavedChanges { get; set; }
    }

    // Page layout configuration
    public class DesignerPageLayoutDto
    {
        public string Type { get; set; } = "grid"; // grid, fluid, fixed
        public int MaxWidth { get; set; } = 1200;
        public int Columns { get; set; } = 12; // 12-column grid system
        public string GutterSize { get; set; } = "16px";
        public Dictionary<string, object> Breakpoints { get; set; } = new()
        {
            { "xs", 576 },
            { "sm", 768 },
            { "md", 992 },
            { "lg", 1200 },
            { "xl", 1400 }
        };
    }

    // Component DTO for designer
    public class DesignerComponentDto
    {
        public int Id { get; set; }
        public string ComponentKey { get; set; } = string.Empty;
        public ComponentType Type { get; set; }
        public string Name { get; set; } = string.Empty;

        // Grid positioning
        public int GridColumn { get; set; } = 1;
        public int GridColumnSpan { get; set; } = 12;
        public int GridRow { get; set; } = 1;
        public int GridRowSpan { get; set; } = 1;

        // Hierarchy
        public int Order { get; set; }
        public int? ParentComponentId { get; set; }
        public string? ParentComponentKey { get; set; }

        // Component data
        public Dictionary<string, object> Properties { get; set; } = new();
        public Dictionary<string, object> Styles { get; set; } = new();
        public Dictionary<string, object> Content { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();

        // Display state
        public bool IsVisible { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public bool IsSelected { get; set; } = false;

        // CSS
        public string? CssClasses { get; set; }
        public string? CustomCss { get; set; }

        // Responsive and animation settings
        public Dictionary<string, object> ResponsiveSettings { get; set; } = new();
        public Dictionary<string, object> AnimationSettings { get; set; } = new();
        public Dictionary<string, object> InteractionSettings { get; set; } = new();

        // Child components
        public List<DesignerComponentDto> Children { get; set; } = new();
    }

    // Available component types for the designer
    public class AvailableComponentDto
    {
        public ComponentType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string Category { get; set; } = string.Empty;
        public Dictionary<string, object> DefaultProperties { get; set; } = new();
        public Dictionary<string, object> DefaultStyles { get; set; } = new();
        public Dictionary<string, object> DefaultSettings { get; set; } = new();
        public bool AllowChildren { get; set; } = false;
        public int DefaultColumnSpan { get; set; } = 12;
    }

    // DTOs for component operations
    public class CreateComponentDto
    {
        public int PageId { get; set; }
        public ComponentType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ComponentKey { get; set; } = string.Empty;

        // Grid positioning
        public int GridColumn { get; set; } = 1;
        public int GridColumnSpan { get; set; } = 12;
        public int GridRow { get; set; } = 1;
        public int GridRowSpan { get; set; } = 1;

        public int Order { get; set; }
        public int? ParentComponentId { get; set; }
        public string? ParentComponentKey { get; set; }

        public Dictionary<string, object> Properties { get; set; } = new();
        public Dictionary<string, object> Styles { get; set; } = new();
        public Dictionary<string, object> Content { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    public class UpdateComponentDto
    {
        public int ComponentId { get; set; }
        public string ComponentKey { get; set; } = string.Empty;
        public string? Name { get; set; }

        // Grid positioning
        public int? GridColumn { get; set; }
        public int? GridColumnSpan { get; set; }
        public int? GridRow { get; set; }
        public int? GridRowSpan { get; set; }

        public Dictionary<string, object>? Properties { get; set; }
        public Dictionary<string, object>? Styles { get; set; }
        public Dictionary<string, object>? Content { get; set; }
        public Dictionary<string, object>? Settings { get; set; }
        public Dictionary<string, object>? ResponsiveSettings { get; set; }
        public Dictionary<string, object>? AnimationSettings { get; set; }
        public Dictionary<string, object>? InteractionSettings { get; set; }

        public bool? IsVisible { get; set; }
        public bool? IsLocked { get; set; }
        public string? CssClasses { get; set; }
        public string? CustomCss { get; set; }
    }

    public class MoveComponentDto
    {
        public int ComponentId { get; set; }
        public string ComponentKey { get; set; } = string.Empty;
        public int? NewParentComponentId { get; set; }
        public string? NewParentComponentKey { get; set; }
        public int NewGridColumn { get; set; }
        public int NewGridRow { get; set; }
        public int NewOrder { get; set; }
    }

    public class DuplicateComponentDto
    {
        public int ComponentId { get; set; }
        public string ComponentKey { get; set; } = string.Empty;
        public int? ParentComponentId { get; set; }
        public string? ParentComponentKey { get; set; }
        public int GridColumn { get; set; } = 1;
        public int GridRow { get; set; } = 1;
        public int Order { get; set; }
        public string? NewName { get; set; }
    }

    public class SaveDesignerPageDto
    {
        public int PageId { get; set; }
        public List<DesignerComponentDto> Components { get; set; } = new();
        public DesignerPageLayoutDto Layout { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public Dictionary<string, object> Styles { get; set; } = new();
        public string? ChangeDescription { get; set; }
        public bool CreateVersion { get; set; } = false;
    }

    // Preview DTOs
    public class DesignerPreviewDto
    {
        public int PageId { get; set; }
        public string PreviewUrl { get; set; } = string.Empty;
        public string PreviewToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    public class GeneratePreviewDto
    {
        public Dictionary<string, object>? Settings { get; set; }
    }

    // Publishing DTOs
    public class PublishPageDto
    {
        public int PageId { get; set; }
        public string? PublishMessage { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public bool CreateVersion { get; set; } = true;
    }

    // Designer state management
    public class DesignerStateDto
    {
        public int PageId { get; set; }
        public string? SelectedComponentKey { get; set; }
        public List<string> ExpandedComponents { get; set; } = new();
        public string ActiveBreakpoint { get; set; } = "lg";
        public string ViewMode { get; set; } = "desktop"; // desktop, tablet, mobile
        public float ZoomLevel { get; set; } = 1.0f;
        public bool ShowGrid { get; set; } = true;
        public bool ShowRulers { get; set; } = false;
        public bool SnapToGrid { get; set; } = true;
        public Dictionary<string, object> Preferences { get; set; } = new();
    }

    // Component library for the designer
    public class ComponentLibraryDto
    {
        public List<ComponentCategoryDto> Categories { get; set; } = new();
    }

    public class ComponentCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public List<AvailableComponentDto> Components { get; set; } = new();
    }

    // Version management
    public class PageVersionDto
    {
        public int Id { get; set; }
        public int VersionNumber { get; set; }
        public string? ChangeNotes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByUserName { get; set; }
        public int? CreatedByUserId { get; set; }
    }

    public class CreateVersionDto
    {
        public string? ChangeNotes { get; set; }
    }

    // Component reordering
    public class ReorderComponentsDto
    {
        public List<ComponentOrderDto> ComponentOrders { get; set; } = new();
    }

    public class ComponentOrderDto
    {
        public string ComponentKey { get; set; } = string.Empty;
        public int Order { get; set; }
        public int GridColumn { get; set; }
        public int GridRow { get; set; }
    }
}