using Backend.CMS.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.CMS.Domain.Entities
{
    public class Location : BaseEntity
    {
        [Required]
        public int CompanyId { get; set; }

        [ForeignKey("CompanyId")]
        public Company Company { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? LocationCode { get; set; } // Unique identifier for the location

        [Required]
        [MaxLength(50)]
        public string LocationType { get; set; } = "Branch"; // "Branch", "Headquarters", etc.

        public bool IsMainLocation { get; set; } = false;

        public bool IsActive { get; set; } = true;

        [NotMapped]
        public Dictionary<string, object> LocationSettings { get; set; } = [];

        [NotMapped]
        public Dictionary<string, object> AdditionalInfo { get; set; } = [];

        // Navigation properties
        public ICollection<LocationOpeningHour> OpeningHours { get; set; } = [];

        public ICollection<Address> Addresses { get; set; } = [];

        public ICollection<ContactDetails> ContactDetails { get; set; } = [];
    }

    public class LocationOpeningHour : BaseEntity
    {
        [Required]
        public int LocationId { get; set; }

        [ForeignKey("LocationId")]
        public Location Location { get; set; } = null!;

        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeOnly OpenTime { get; set; }

        [Required]
        public TimeOnly CloseTime { get; set; }

        public bool IsClosed { get; set; } = false;

        public bool IsOpen24Hours { get; set; } = false;

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }

}