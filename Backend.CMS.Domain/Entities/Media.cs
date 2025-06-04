using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Domain.Entities
{
    public class Media : BaseEntity, ITenantEntity
    {
        public string TenantId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Path { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public MediaType Type { get; set; }
        public Guid UploadedBy { get; set; }
        public User UploadedByUser { get; set; } = null!;
    }
}
