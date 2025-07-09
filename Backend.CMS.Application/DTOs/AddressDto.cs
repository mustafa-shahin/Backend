using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Application.DTOs
{
    public class AddressDto
    {
        public int Id { get; set; }
        public string Street { get; set; } = string.Empty;
        public string? HouseNr { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string? Region { get; set; }
        public string? District { get; set; }
        public bool IsDefault { get; set; }
        public string? AddressType { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    /// <summary>
    /// Address search DTO for pagination and filtering
    /// </summary>
    public class AddressSearchDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public string? Country { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }
        public string? AddressType { get; set; }
        public bool? IsDefault { get; set; }
        public string? EntityType { get; set; } // "user", "company", "location"
        public int? EntityId { get; set; }
        public string SortBy { get; set; } = "CreatedAt";
        public string SortDirection { get; set; } = "Desc";
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
    }

    public class CreateAddressDto
    {
        public string Street { get; set; } = string.Empty;
        public string? HouseNr { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string? Region { get; set; }
        public string? District { get; set; }
        public bool IsDefault { get; set; } = false;
        public string? AddressType { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateAddressDto
    {
        public string Street { get; set; } = string.Empty;
        public string? HouseNr { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string? Region { get; set; }
        public string? District { get; set; }
        public bool IsDefault { get; set; }
        public string? AddressType { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Bulk operations for addresses
    /// </summary>
    public class BulkUpdateAddressesDto
    {
        [Required]
        public List<int> AddressIds { get; set; } = new();

        [Required]
        public UpdateAddressDto UpdateDto { get; set; } = null!;
    }

    public class BulkDeleteAddressesDto
    {
        [Required]
        public List<int> AddressIds { get; set; } = new();
    }
}