using System.ComponentModel.DataAnnotations;


namespace Backend.CMS.Application.DTOs
{
    public class LocationDto
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LocationCode { get; set; }
        public string LocationType { get; set; } = "Branch";
        public bool IsMainLocation { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object> LocationSettings { get; set; } = [];
        public Dictionary<string, object> AdditionalInfo { get; set; } = [];
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<LocationOpeningHourDto> OpeningHours { get; set; } = [];
        public List<AddressDto> Addresses { get; set; } = []    ;
        public List<ContactDetailsDto> ContactDetails { get; set; } = [];
    }

    public class CreateLocationDto
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? LocationCode { get; set; }

        [Required]
        [MaxLength(50)]
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
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? LocationCode { get; set; }

        [Required]
        [MaxLength(50)]
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
        public int LocationId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly OpenTime { get; set; }
        public TimeOnly CloseTime { get; set; }
        public bool IsClosed { get; set; }
        public bool IsOpen24Hours { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    public class CreateLocationOpeningHourDto
    {
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

    public class UpdateLocationOpeningHourDto
    {
        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeOnly OpenTime { get; set; }

        [Required]
        public TimeOnly CloseTime { get; set; }

        public bool IsClosed { get; set; }
        public bool IsOpen24Hours { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
    public class LocationSearchDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public string? LocationType { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsMainLocation { get; set; }
        public int? CompanyId { get; set; }
        public string SortBy { get; set; } = "Name";
        public string SortDirection { get; set; } = "Asc";
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
    }

    public class BulkUpdateLocationsDto
    {
        [Required]
        public List<int> LocationIds { get; set; } = new();

        [Required]
        public UpdateLocationDto UpdateDto { get; set; } = null!;
    }

    public class BulkDeleteLocationsDto
    {
        [Required]
        public List<int> LocationIds { get; set; } = new();
    }
}