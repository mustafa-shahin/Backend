using Backend.CMS.Domain.Enums;


namespace Backend.CMS.Application.DTOs
{
    public class PageDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public PageStatus Status { get; set; }
        public string? Template { get; set; }
        public int? Priority { get; set; }
        public int? ParentPageId { get; set; }

        // Add access control
        public bool RequiresLogin { get; set; }
        public bool AdminOnly { get; set; }

        public DateTime? PublishedOn { get; set; }
        public string? PublishedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<PageComponentDto> Components { get; set; } = new();
        public List<PageDto> ChildPages { get; set; } = new();
    }

    public class CreatePageDto
    {
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public PageStatus Status { get; set; } = PageStatus.Draft;
        public string? Template { get; set; }
        public int? Priority { get; set; }
        public int? ParentPageId { get; set; }

        // Add access control
        public bool RequiresLogin { get; set; } = false;
        public bool AdminOnly { get; set; } = false;
    }

    public class UpdatePageDto
    {
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public PageStatus Status { get; set; }
        public string? Template { get; set; }
        public int? Priority { get; set; }
        public int? ParentPageId { get; set; }

        // Add access control
        public bool RequiresLogin { get; set; }
        public bool AdminOnly { get; set; }
    }

    public class PageComponentDto
    {
        public int Id { get; set; }
        public ComponentType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
        public Dictionary<string, object> Styles { get; set; } = new();
        public Dictionary<string, object> Content { get; set; } = new();
        public int Order { get; set; }
        public int? ParentComponentId { get; set; }
        public List<PageComponentDto> ChildComponents { get; set; } = new();
        public bool IsVisible { get; set; } = true;
        public string? CssClasses { get; set; }
        public string? CustomCss { get; set; }
        public Dictionary<string, object> ResponsiveSettings { get; set; } = new();
        public Dictionary<string, object> AnimationSettings { get; set; } = new();
        public Dictionary<string, object> InteractionSettings { get; set; } = new();
    }

    public class SavePageStructureDto
    {
        public int PageId { get; set; }
        public List<PageComponentDto> Components { get; set; } = new();
    }

    public class PageListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public PageStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? PublishedOn { get; set; }
        public bool HasChildren { get; set; }
    }
    public class PageSnapshotDto
    {
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? MetaKeywords { get; set; }
        public PageStatus Status { get; set; }
        public string? Template { get; set; }
        public int? Priority { get; set; }
        public int? ParentPageId { get; set; }
        public bool RequiresLogin { get; set; }
        public bool AdminOnly { get; set; }
        public List<ComponentSnapshotDto> Components { get; set; } = new();
    }
    public class ComponentSnapshotDto
    {
        public ComponentType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
        public Dictionary<string, object> Styles { get; set; } = new();
        public Dictionary<string, object> Content { get; set; } = new();
        public int Order { get; set; }
        public int? ParentComponentId { get; set; }
        public bool IsVisible { get; set; } = true;
        public string? CssClasses { get; set; }
        public string? CustomCss { get; set; }
        public Dictionary<string, object> ResponsiveSettings { get; set; } = new();
        public Dictionary<string, object> AnimationSettings { get; set; } = new();
        public Dictionary<string, object> InteractionSettings { get; set; } = new();
    }

    public class DuplicatePageDto
    {
        public string NewName { get; set; } = string.Empty;
    }

    public class CreatePageVersionDto
    {
        public string? ChangeNotes { get; set; }
    }
}