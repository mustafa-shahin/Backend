using Backend.CMS.Domain.Common;

namespace Backend.CMS.Domain.Entities
{
    public class PageVersion : BaseEntity
    {
        public Guid PageId { get; set; }
        public Page Page { get; set; } = null!;
        public int VersionNumber { get; set; }
        public string Data { get; set; } = string.Empty; // JSON snapshot
        public string? ChangeNotes { get; set; }
        public User CreatedByUser { get; set; } = null!;
    }
}