using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Domain.Entities
{
    public class User : BaseEntity
    {
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(500)] // Sufficiently long for hashed passwords
        [DataType(DataType.Password)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; } = UserRole.Customer;

        public bool IsActive { get; set; } = true;

        public bool IsLocked { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public int FailedLoginAttempts { get; set; }

        public DateTime? LockoutEnd { get; set; }


        [Column(TypeName = "jsonb")] // For PostgreSQL, use nvarchar(max) for SQL Server if storing as JSON string
        public List<string> RecoveryCodes { get; set; } = [];

        public int? PictureFileId { get; set; }

        [ForeignKey("PictureFileId")]
        public FileEntity? Picture { get; set; }

        public DateTime? EmailVerifiedAt { get; set; }

        [StringLength(255)]
        public string? EmailVerificationToken { get; set; }

        public DateTime? PasswordChangedAt { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(50)]
        public string? Gender { get; set; }

        public ICollection<UserSession> Sessions { get; set; } = [];
        public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = [];
        public ICollection<Address> Addresses { get; set; } = [];
        public ICollection<ContactDetails> ContactDetails { get; set; } = [];

        [NotMapped] // Indicates that this property should not be mapped to the database
        public bool IsAdmin => Role == UserRole.Admin;

        [NotMapped]
        public bool IsCustomer => Role == UserRole.Customer;

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [NotMapped]
        public string RoleDisplayName => Role.ToString();

        public ICollection<UserPermission> UserPermissions { get; set; } = [];

        public List<UserExternalLogin> ExternalLogins { get; set; } = [];

        [StringLength(255)]
        public string? ExternalId { get; set; }

        public bool IsExternalUser { get; set; }

        public DateTime? LastExternalSync { get; set; }

        [NotMapped]
        public string? PictureUrl => Picture != null ? $"/api/files/{PictureFileId}/download" : null;
    }

    public class UserExternalLogin : BaseEntity
    {
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Provider { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string ExternalUserId { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(255)]
        public string? Email { get; set; }

        [StringLength(255)]
        public string? Name { get; set; }

        [Column(TypeName = "jsonb")] // For PostgreSQL, use nvarchar(max) for SQL Server if storing as JSON string
        public Dictionary<string, object> Claims { get; set; } = [];

        public string? AccessToken { get; set; }

        public string? RefreshToken { get; set; }

        public DateTime? TokenExpiry { get; set; }
    }
}