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
}