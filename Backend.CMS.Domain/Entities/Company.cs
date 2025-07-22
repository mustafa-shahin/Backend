using Backend.CMS.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.CMS.Domain.Entities
{
    public class Company : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        
        public int? LogoId { get; set; }
        [ForeignKey("LogoId")]
        public Image? Logo { get; set; }
        
        public int? FaviconId { get; set; }
        [ForeignKey("FaviconId")]
        public Image? Favicon { get; set; }
        
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