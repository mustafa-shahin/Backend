using System;
using System.Collections.Generic;
using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Domain.Entities
{
    public class Page : BaseEntity, ITenantEntity
    {
        public string TenantId { get; set; } = string.Empty;
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
        public Page? ParentPage { get; set; }
        public ICollection<Page> ChildPages { get; set; } = new List<Page>();
        public ICollection<PageComponent> Components { get; set; } = new List<PageComponent>();
        public ICollection<PagePermission> Permissions { get; set; } = new List<PagePermission>();
        public DateTime? PublishedOn { get; set; }
        public string? PublishedBy { get; set; }
    }
}
