using System.Text.Json.Serialization;

namespace Backend.CMS.Application.DTOs
{
    public class DesignerPageDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("metaTitle")]
        public string? MetaTitle { get; set; }

        [JsonPropertyName("metaDescription")]
        public string? MetaDescription { get; set; }

        [JsonPropertyName("metaKeywords")]
        public string? MetaKeywords { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("template")]
        public string? Template { get; set; }

        [JsonPropertyName("priority")]
        public int? Priority { get; set; }

        [JsonPropertyName("parentPageId")]
        public int? ParentPageId { get; set; }

        [JsonPropertyName("requiresLogin")]
        public bool RequiresLogin { get; set; }

        [JsonPropertyName("adminOnly")]
        public bool AdminOnly { get; set; }

        [JsonPropertyName("content")]
        public Dictionary<string, object> Content { get; set; } = new();

        [JsonPropertyName("layout")]
        public Dictionary<string, object> Layout { get; set; } = new();

        [JsonPropertyName("settings")]
        public Dictionary<string, object> Settings { get; set; } = new();

        [JsonPropertyName("styles")]
        public Dictionary<string, object> Styles { get; set; } = new();

        [JsonPropertyName("currentVersion")]
        public int CurrentVersion { get; set; }

        [JsonPropertyName("hasUnsavedChanges")]
        public bool HasUnsavedChanges { get; set; }

        [JsonPropertyName("publishedAt")]
        public DateTime? PublishedAt { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }
    }

    public class SaveDesignerPageDto
    {
        [JsonPropertyName("pageId")]
        public int PageId { get; set; }

        [JsonPropertyName("content")]
        public Dictionary<string, object> Content { get; set; } = new();

        [JsonPropertyName("layout")]
        public Dictionary<string, object> Layout { get; set; } = new();

        [JsonPropertyName("settings")]
        public Dictionary<string, object> Settings { get; set; } = new();

        [JsonPropertyName("styles")]
        public Dictionary<string, object> Styles { get; set; } = new();

        [JsonPropertyName("changeDescription")]
        public string? ChangeDescription { get; set; }

        [JsonPropertyName("createVersion")]
        public bool CreateVersion { get; set; } = true;

        [JsonPropertyName("autoSave")]
        public bool AutoSave { get; set; } = false;
    }

    public class DesignerPreviewDto
    {
        [JsonPropertyName("pageId")]
        public int PageId { get; set; }

        [JsonPropertyName("previewUrl")]
        public string PreviewUrl { get; set; } = string.Empty;

        [JsonPropertyName("previewToken")]
        public string PreviewToken { get; set; } = string.Empty;

        [JsonPropertyName("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        [JsonPropertyName("settings")]
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    public class PublishPageDto
    {
        [JsonPropertyName("pageId")]
        public int PageId { get; set; }

        [JsonPropertyName("publishMessage")]
        public string? PublishMessage { get; set; }

        [JsonPropertyName("createVersion")]
        public bool CreateVersion { get; set; } = true;

        [JsonPropertyName("scheduledAt")]
        public DateTime? ScheduledAt { get; set; }
    }

    public class GeneratePreviewDto
    {
        [JsonPropertyName("settings")]
        public Dictionary<string, object>? Settings { get; set; }
    }

    public class CreateVersionDto
    {
        [JsonPropertyName("changeNotes")]
        public string? ChangeNotes { get; set; }
    }

    public class DesignerStateDto
    {
        [JsonPropertyName("pageId")]
        public int PageId { get; set; }

        [JsonPropertyName("selectedComponent")]
        public string? SelectedComponent { get; set; }

        [JsonPropertyName("selectedBlock")]
        public string? SelectedBlock { get; set; }

        [JsonPropertyName("zoomLevel")]
        public double ZoomLevel { get; set; } = 1.0;

        [JsonPropertyName("panelState")]
        public Dictionary<string, bool> PanelState { get; set; } = new();

        [JsonPropertyName("viewMode")]
        public string ViewMode { get; set; } = "desktop";

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("customState")]
        public Dictionary<string, object> CustomState { get; set; } = new();
    }
}