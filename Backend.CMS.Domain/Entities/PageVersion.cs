using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;
using System;

namespace Backend.CMS.Domain.Entities
{
    public class PageVersion : BaseEntity
    {
        public int PageId { get; set; }

        [ForeignKey("PageId")]
        public Page Page { get; set; } = null!;

        [Required]
        public int VersionNumber { get; set; }

        [Required]
        [Column(TypeName = "jsonb")] // Or nvarchar(max) for SQL Server if storing as string
        public string Data { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? ChangeNotes { get; set; }
    }
}