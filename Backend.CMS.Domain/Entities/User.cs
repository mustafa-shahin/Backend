using Backend.CMS.Domain.Common;
using System;
using System.Collections.Generic;

namespace Backend.CMS.Domain.Entities
{
    public class User : BaseEntity, ITenantEntity
    {
        public string TenantId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsLocked { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public string? TwoFactorSecret { get; set; }
        public List<string> RecoveryCodes { get; set; } = new();

        // Profile enhancements
        public string? Avatar { get; set; }
        public string? Timezone { get; set; }
        public string? Language { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }
        public string? EmailVerificationToken { get; set; }
        public DateTime? PasswordChangedAt { get; set; }

        // Preferences
        public Dictionary<string, object> Preferences { get; set; } = new();

        // Navigation properties
        public ICollection<UserSession> Sessions { get; set; } = [];
    }
}
