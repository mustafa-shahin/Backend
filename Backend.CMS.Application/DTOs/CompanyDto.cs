

namespace Backend.CMS.Application.DTOs
{
    public class CompanyDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Logo { get; set; }
        public string? Favicon { get; set; }
        public Dictionary<string, object> BrandingSettings { get; set; } = [];
        public Dictionary<string, object> BusinessSettings { get; set; } = [];
        public bool IsActive { get; set; }
        public string? Timezone { get; set; }
        public string? Currency { get; set; }
        public string? Language { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<LocationDto> Locations { get; set; } = [];
        public List<AddressDto> Addresses { get; set; } = [];
        public List<ContactDetailsDto> ContactDetails { get; set; } = [];
    }

    public class UpdateCompanyDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Logo { get; set; }
        public string? Favicon { get; set; }
        public Dictionary<string, object> BrandingSettings { get; set; } = [];
        public Dictionary<string, object> BusinessSettings { get; set; } = [];
        public string? Timezone { get; set; }
        public string? Currency { get; set; }
        public string? Language { get; set; }
        public List<UpdateAddressDto> Addresses { get; set; } = [];
        public List<UpdateContactDetailsDto> ContactDetails { get; set; } = [];
    }
}