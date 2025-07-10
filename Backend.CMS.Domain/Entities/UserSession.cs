using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Backend.CMS.Domain.Common;

namespace Backend.CMS.Domain.Entities
{
    public class UserSession : BaseEntity
    {
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        [JsonIgnore]
        public User User { get; set; } = null!;

        [Required]
        [StringLength(500)]
        [JsonIgnore] 
        public string RefreshToken { get; set; } = string.Empty;

        [Required]
        [StringLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string UserAgent { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; }

        [StringLength(100)]
        public string? ApplicationContext { get; set; }

        [StringLength(255)]
        public string? DeviceFingerprint { get; set; }
    }
}