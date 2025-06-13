namespace Backend.CMS.Application.DTOs
{
    public class LocationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LocationCode { get; set; }
        public string LocationType { get; set; } = "Branch";
        public bool IsMainLocation { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object> LocationSettings { get; set; } = [];
        public Dictionary<string, object> AdditionalInfo { get; set; } = [];
        public List<LocationOpeningHourDto> OpeningHours { get; set; } = [];
        public List<AddressDto> Addresses { get; set; } = [];
        public List<ContactDetailsDto> ContactDetails { get; set; } = [];
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateLocationDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LocationCode { get; set; }
        public string LocationType { get; set; } = "Branch";
        public bool IsMainLocation { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public Dictionary<string, object> LocationSettings { get; set; } = [];
        public Dictionary<string, object> AdditionalInfo { get; set; } = [];
        public List<CreateLocationOpeningHourDto> OpeningHours { get; set; } = [];
        public List<CreateAddressDto> Addresses { get; set; } = [];
        public List<CreateContactDetailsDto> ContactDetails { get; set; } = [];
    }

    public class UpdateLocationDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LocationCode { get; set; }
        public string LocationType { get; set; } = "Branch";
        public bool IsMainLocation { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object> LocationSettings { get; set; } = [];
        public Dictionary<string, object> AdditionalInfo { get; set; } = [];
        public List<UpdateLocationOpeningHourDto> OpeningHours { get; set; } = [];
        public List<UpdateAddressDto> Addresses { get; set; } = [];
        public List<UpdateContactDetailsDto> ContactDetails { get; set; } = [];
    }

    public class LocationOpeningHourDto
    {
        public int Id { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly OpenTime { get; set; }
        public TimeOnly CloseTime { get; set; }
        public bool IsClosed { get; set; }
        public bool IsOpen24Hours { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateLocationOpeningHourDto
    {
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly OpenTime { get; set; }
        public TimeOnly CloseTime { get; set; }
        public bool IsClosed { get; set; } = false;
        public bool IsOpen24Hours { get; set; } = false;
        public string? Notes { get; set; }
    }

    public class UpdateLocationOpeningHourDto
    {
        public int? Id { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly OpenTime { get; set; }
        public TimeOnly CloseTime { get; set; }
        public bool IsClosed { get; set; }
        public bool IsOpen24Hours { get; set; }
        public string? Notes { get; set; }
    }
}