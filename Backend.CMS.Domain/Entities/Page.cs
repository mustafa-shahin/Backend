using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;
using System;
using System.Collections.Generic;

namespace Backend.CMS.Domain.Entities
{
    public class Page : BaseEntity
    {
        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(255)]
        public string? MetaTitle { get; set; }

        [StringLength(500)]
        public string? MetaDescription { get; set; }

        [StringLength(500)]
        public string? MetaKeywords { get; set; }

        [Required]
        public PageStatus Status { get; set; }

        [StringLength(255)]
        public string? Template { get; set; }

        public int? Priority { get; set; }

        public int? ParentPageId { get; set; }

        public bool RequiresLogin { get; set; } = false;

        public bool AdminOnly { get; set; } = false;

        [ForeignKey("ParentPageId")]
        public Page? ParentPage { get; set; }

        public ICollection<Page> ChildPages { get; set; } = new List<Page>();

        public ICollection<PageComponent> Components { get; set; } = new List<PageComponent>();

        public DateTime? PublishedOn { get; set; }

        [StringLength(255)]
        public string? PublishedBy { get; set; }
    }
}