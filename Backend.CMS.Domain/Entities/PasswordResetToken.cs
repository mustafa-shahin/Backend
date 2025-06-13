using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;
using System;

namespace Backend.CMS.Domain.Entities
{
    public class PasswordResetToken : BaseEntity
    {
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [Required]
        [StringLength(255)] // Token length can vary
        public string Token { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; }

        public DateTime? UsedAt { get; set; }

        [StringLength(45)] 
        public string? IpAddress { get; set; }

        [StringLength(1000)]
        public string? UserAgent { get; set; }
    }
}