using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;

namespace Backend.CMS.Domain.Entities
{
    public class ContactDetails : BaseEntity
    {
        [Phone]
        [StringLength(50)]
        public string? PrimaryPhone { get; set; }

        [Phone]
        [StringLength(50)]
        public string? SecondaryPhone { get; set; }

        [Phone]
        [StringLength(50)]
        public string? Mobile { get; set; }

        [Phone]
        [StringLength(50)]
        public string? Fax { get; set; }

        [EmailAddress]
        [StringLength(255)]
        public string? Email { get; set; }

        [EmailAddress]
        [StringLength(255)]
        public string? SecondaryEmail { get; set; }

        [Url]
        [StringLength(500)]
        public string? Website { get; set; }

        [Url]
        [StringLength(500)]
        public string? LinkedInProfile { get; set; }

        [Url]
        [StringLength(500)]
        public string? TwitterProfile { get; set; }

        [Url]
        [StringLength(500)]
        public string? FacebookProfile { get; set; }

        [Url]
        [StringLength(500)]
        public string? InstagramProfile { get; set; }

        [Phone]
        [StringLength(50)]
        public string? WhatsAppNumber { get; set; }

        [StringLength(100)]
        public string? TelegramHandle { get; set; }

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> AdditionalContacts { get; set; } = new();

        public bool IsDefault { get; set; } = false;

        [StringLength(50)]
        public string? ContactType { get; set; }
    }
}