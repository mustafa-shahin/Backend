using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;
using System.Collections.Generic;

namespace Backend.CMS.Domain.Entities
{
    public class PageComponent : BaseEntity
    {
        public int PageId { get; set; }

        [ForeignKey("PageId")]
        public Page Page { get; set; } = null!;

        [Required]
        public ComponentType Type { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string ComponentKey { get; set; } = string.Empty;

        public int GridColumn { get; set; } = 1;

        public int GridColumnSpan { get; set; } = 12;

        public int GridRow { get; set; } = 1;

        public int GridRowSpan { get; set; } = 1;

        public int Order { get; set; }

        public int? ParentComponentId { get; set; }

        [ForeignKey("ParentComponentId")]
        public PageComponent? ParentComponent { get; set; }

        public ICollection<PageComponent> ChildComponents { get; set; } = new List<PageComponent>();

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> Properties { get; set; } = new();

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> Styles { get; set; } = new();

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> Content { get; set; } = new();

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> Settings { get; set; } = new();

        public bool IsVisible { get; set; } = true;

        public bool IsLocked { get; set; } = false;

        [StringLength(500)]
        public string? CssClasses { get; set; }

        [StringLength(int.MaxValue)] // For potentially large custom CSS
        public string? CustomCss { get; set; }

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> ResponsiveSettings { get; set; } = new();

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> AnimationSettings { get; set; } = new();

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> InteractionSettings { get; set; } = new();
    }
}