using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Application.DTOs.Components
{
    public class ComponentLibraryDto
    {
        public List<ComponentCategoryDto> Categories { get; set; } = [];
    }

    public class ComponentCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public List<ComponentTemplateDto> Templates { get; set; } = [];
    }

    public class ComponentTemplateDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ComponentType Type { get; set; }
        public string? Icon { get; set; }
        public string? Category { get; set; }
        public Dictionary<string, object> DefaultProperties { get; set; } = [];
        public Dictionary<string, object> DefaultStyles { get; set; } = [];
        public Dictionary<string, object> Schema { get; set; } = [];
        public string? PreviewHtml { get; set; }
        public string? PreviewImage { get; set; }
        public bool IsSystemTemplate { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string? Tags { get; set; }
        public Dictionary<string, object> ConfigSchema { get; set; } = [];
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateComponentTemplateDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ComponentType Type { get; set; }
        public string? Icon { get; set; }
        public string? Category { get; set; }
        public Dictionary<string, object> DefaultProperties { get; set; } = [];
        public Dictionary<string, object> DefaultStyles { get; set; } = [];
        public Dictionary<string, object> Schema { get; set; } = [];
        public string? PreviewHtml { get; set; }
        public string? PreviewImage { get; set; }
        public bool IsSystemTemplate { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public string? Tags { get; set; }
        public Dictionary<string, object> ConfigSchema { get; set; } = [];
    }

    public class UpdateComponentTemplateDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ComponentType Type { get; set; }
        public string? Icon { get; set; }
        public string? Category { get; set; }
        public Dictionary<string, object> DefaultProperties { get; set; } = [];
        public Dictionary<string, object> DefaultStyles { get; set; } = [];
        public Dictionary<string, object> Schema { get; set; } = [];
        public string? PreviewHtml { get; set; }
        public string? PreviewImage { get; set; }
        public bool IsSystemTemplate { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string? Tags { get; set; }
        public Dictionary<string, object> ConfigSchema { get; set; } = [];
    }

    public class ComponentTemplateListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public ComponentType Type { get; set; }
        public string? Icon { get; set; }
        public string? Category { get; set; }
        public bool IsSystemTemplate { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}