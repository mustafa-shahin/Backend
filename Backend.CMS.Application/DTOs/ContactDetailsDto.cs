using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Application.DTOs
{
    public class ContactDetailsDto
    {
        public int Id { get; set; }
        public string? PrimaryPhone { get; set; }
        public string? SecondaryPhone { get; set; }
        public string? Mobile { get; set; }
        public string? Fax { get; set; }
        public string? Email { get; set; }
        public string? SecondaryEmail { get; set; }
        public string? Website { get; set; }
        public string? LinkedInProfile { get; set; }
        public string? TwitterProfile { get; set; }
        public string? FacebookProfile { get; set; }
        public string? InstagramProfile { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? TelegramHandle { get; set; }
        public Dictionary<string, object> AdditionalContacts { get; set; } = new();
        public bool IsDefault { get; set; }
        public string? ContactType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateContactDetailsDto
    {
        public string? PrimaryPhone { get; set; }
        public string? SecondaryPhone { get; set; }
        public string? Mobile { get; set; }
        public string? Fax { get; set; }
        public string? Email { get; set; }
        public string? SecondaryEmail { get; set; }
        public string? Website { get; set; }
        public string? LinkedInProfile { get; set; }
        public string? TwitterProfile { get; set; }
        public string? FacebookProfile { get; set; }
        public string? InstagramProfile { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? TelegramHandle { get; set; }
        public Dictionary<string, object> AdditionalContacts { get; set; } = [];
        public bool IsDefault { get; set; } = false;
        public string? ContactType { get; set; }
    }

    public class UpdateContactDetailsDto
    {
        public string? PrimaryPhone { get; set; }
        public string? SecondaryPhone { get; set; }
        public string? Mobile { get; set; }
        public string? Fax { get; set; }
        public string? Email { get; set; }
        public string? SecondaryEmail { get; set; }
        public string? Website { get; set; }
        public string? LinkedInProfile { get; set; }
        public string? TwitterProfile { get; set; }
        public string? FacebookProfile { get; set; }
        public string? InstagramProfile { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? TelegramHandle { get; set; }
        public Dictionary<string, object> AdditionalContacts { get; set; } = new();
        public bool IsDefault { get; set; }
        public string? ContactType { get; set; }
    }
    /// <summary>
    /// Contact details search DTO for pagination and filtering
    /// </summary>
    public class ContactDetailsSearchDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public string? ContactType { get; set; }
        public bool? IsDefault { get; set; }
        public string? EntityType { get; set; } // "user", "company", "location"
        public int? EntityId { get; set; }
        public string SortBy { get; set; } = "CreatedAt";
        public string SortDirection { get; set; } = "Desc";
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
    }
    /// <summary>
    /// Bulk operations for contact details
    /// </summary>
    public class BulkUpdateContactDetailsDto
    {
        [Required]
        public List<int> ContactDetailsIds { get; set; } = new();

        [Required]
        public UpdateContactDetailsDto UpdateDto { get; set; } = null!;
    }

    public class BulkDeleteContactDetailsDto
    {
        [Required]
        public List<int> ContactDetailsIds { get; set; } = new();
    }
}
