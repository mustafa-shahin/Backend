using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;
using System;

namespace Backend.CMS.Domain.Entities
{
    public class UserSession : BaseEntity
    {
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [Required]
        [StringLength(500)] 
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
    }
}