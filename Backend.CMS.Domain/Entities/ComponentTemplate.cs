using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;
using System;
using System.Collections.Generic;

namespace Backend.CMS.Domain.Entities
{
    public class ComponentTemplate : BaseEntity
    {
        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        public ComponentType Type { get; set; }

        [StringLength(255)]
        public string? Icon { get; set; }

        [StringLength(255)]
        public string? Category { get; set; }

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> DefaultProperties { get; set; } = new();

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> DefaultStyles { get; set; } = new();

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> Schema { get; set; } = new();

        [StringLength(int.MaxValue)] // Or a more reasonable max length for HTML
        public string? PreviewHtml { get; set; }

        [StringLength(500)]
        public string? PreviewImage { get; set; }

        public bool IsSystemTemplate { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; }

        [StringLength(500)]
        public string? Tags { get; set; }

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> ConfigSchema { get; set; } = new();
    }
}