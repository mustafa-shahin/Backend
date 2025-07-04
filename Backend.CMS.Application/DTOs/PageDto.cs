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
        public bool RequiresLogin { get; set; }
        public bool AdminOnly { get; set; }
        public DateTime? PublishedOn { get; set; }
        public string? PublishedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

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
        public bool RequiresLogin { get; set; } = false;
        public bool AdminOnly { get; set; } = false;
        public Dictionary<string, object> Content { get; set; } = new();
        public Dictionary<string, object> Layout { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public Dictionary<string, object> Styles { get; set; } = new();
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
        public bool RequiresLogin { get; set; }
        public bool AdminOnly { get; set; }
        public Dictionary<string, object> Content { get; set; } = new();
        public Dictionary<string, object> Layout { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public Dictionary<string, object> Styles { get; set; } = new();
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
        public int VersionCount { get; set; }
        public int CurrentVersion { get; set; }
    }

    public class DuplicatePageDto
    {
        public string NewName { get; set; } = string.Empty;
        public bool DuplicateContent { get; set; } = true;
    }

    public class CreatePageVersionDto
    {
        public string? ChangeNotes { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }


    // Add PageVersionDto here since it's referenced by both services
    public class PageVersionDto
    {
        public int Id { get; set; }
        public int VersionNumber { get; set; }
        public string? ChangeNotes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByUserName { get; set; }
        public int? CreatedByUserId { get; set; }
        public bool IsPublished { get; set; }
        public DateTime? PublishedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}