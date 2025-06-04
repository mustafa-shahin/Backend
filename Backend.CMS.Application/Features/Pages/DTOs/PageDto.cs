using System;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Application.Features.Pages.DTOs
{
    public class PageDto
    {
        public Guid Id { get; set; }
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
        public Guid? ParentPageId { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public DateTime? PublishedOn { get; set; }
        public List<PageComponentDto> Components { get; set; } = new List<PageComponentDto>();
    }
    public class PageListDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public PageStatus Status { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? PublishedOn { get; set; }
        public string? ParentPageName { get; set; }
    }
    public class PageComponentDto
    {
        public Guid Id { get; set; }
        public string ComponentType { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public int Order { get; set; }
        public string? ContainerName { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> Content { get; set; } = new Dictionary<string, object>();
        public bool IsActive { get; set; }
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
        public Guid? ParentPageId { get; set; }
    }
    public class UpdatePageDto
    {
        public Guid Id { get; set; }
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
        public Guid? ParentPageId { get; set; }
    }
    public class UpdatePageComponentsDto
    {
        public Guid PageId { get; set; }
        public List<PageComponentDto> Components { get; set; } = new List<PageComponentDto>();
    }
}
