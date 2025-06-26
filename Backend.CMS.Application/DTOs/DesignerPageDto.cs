using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Application.DTOs
{
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

        /// <summary>
        /// Page content containing all components
        /// </summary>
        public Dictionary<string, object> Content { get; set; } = new();

        /// <summary>
        /// Designer layout configuration
        /// </summary>
        public Dictionary<string, object> Layout { get; set; } = new();

        /// <summary>
        /// Page-level settings
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        /// <summary>
        /// Custom CSS styles
        /// </summary>
        public Dictionary<string, object> Styles { get; set; } = new();

        public bool HasUnsavedChanges { get; set; }
        public int CurrentVersion { get; set; }
    }

    public class SaveDesignerPageDto
    {
        public int PageId { get; set; }
        public Dictionary<string, object> Content { get; set; } = new();
        public Dictionary<string, object> Layout { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public Dictionary<string, object> Styles { get; set; } = new();
        public string? ChangeDescription { get; set; }
        public bool CreateVersion { get; set; } = true;
        public bool AutoSave { get; set; } = false;
    }

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

    public class PublishPageDto
    {
        public int PageId { get; set; }
        public string? PublishMessage { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public bool CreateVersion { get; set; } = true;
    }

    public class DesignerStateDto
    {
        public int PageId { get; set; }
        public string? SelectedComponentKey { get; set; }
        public List<string> ExpandedComponents { get; set; } = new();
        public string ActiveBreakpoint { get; set; } = "lg";
        public string ViewMode { get; set; } = "desktop";
        public float ZoomLevel { get; set; } = 1.0f;
        public bool ShowGrid { get; set; } = true;
        public bool ShowRulers { get; set; } = false;
        public bool SnapToGrid { get; set; } = true;
        public Dictionary<string, object> Preferences { get; set; } = new();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }

    public class CreateVersionDto
    {
        public string? ChangeNotes { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}