using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;

namespace Backend.CMS.Domain.Entities
{
    public class Address : BaseEntity
    {
        [Required]
        [StringLength(255)]
        public string Street { get; set; } = string.Empty;
        [Required]
        [StringLength(255)]
        public string? HouseNr { get; set; }

        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string State { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Country { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string PostalCode { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Region { get; set; }

        [StringLength(100)]
        public string? District { get; set; }

        public bool IsDefault { get; set; } = false;

        [StringLength(50)]
        public string? AddressType { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}