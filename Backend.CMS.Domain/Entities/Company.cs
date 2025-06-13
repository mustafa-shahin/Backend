using Backend.CMS.Domain.Common;

namespace Backend.CMS.Domain.Entities
{
    public class Company : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Logo { get; set; }
        public string? Favicon { get; set; }
        public Dictionary<string, object> BrandingSettings { get; set; } = [];
        public Dictionary<string, object> BusinessSettings { get; set; } = [];
        public bool IsActive { get; set; } = true;
        public string? Timezone { get; set; }
        public string? Currency { get; set; }
        public string? Language { get; set; }

        // Navigation properties
        public ICollection<Location> Locations { get; set; } = [];
        public ICollection<Address> Addresses { get; set; } = [];
        public ICollection<ContactDetails> ContactDetails { get; set; } = [];
    }
}