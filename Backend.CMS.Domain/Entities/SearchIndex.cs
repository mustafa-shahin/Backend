using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;
using System;
using System.Collections.Generic;

namespace Backend.CMS.Domain.Entities
{
    public class SearchIndex : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } = string.Empty;

        public int EntityId { get; set; }

        [Required]
        [StringLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(int.MaxValue)] // For potentially large content
        public string Content { get; set; } = string.Empty;

        [Column(TypeName = "tsvector")] // PostgreSQL specific type for full-text search, adjust for other databases
        public string SearchVector { get; set; } = string.Empty;

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> Metadata { get; set; } = new();

        public bool IsPublic { get; set; } = true;

        [Required]
        public DateTime LastIndexedAt { get; set; } = DateTime.UtcNow;
    }

    public class IndexingJob : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string JobType { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty;

        [Required]
        public DateTime StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int TotalEntities { get; set; }

        public int ProcessedEntities { get; set; }

        public int FailedEntities { get; set; }

        [StringLength(2000)]
        public string? ErrorMessage { get; set; }

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> JobMetadata { get; set; } = new();
    }
}