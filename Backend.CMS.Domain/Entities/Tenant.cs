using Backend.CMS.Domain.Common;

namespace Backend.CMS.Domain.Entities
{
    public class Tenant : BaseEntity
    {
        public string Identifier { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public string ConnectionString { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime? SubscriptionExpiresAt { get; set; }
        public string? Theme { get; set; }
        public string? Settings { get; set; } // JSON settings
        public bool AutoUpdate { get; set; } = false;
    }
}