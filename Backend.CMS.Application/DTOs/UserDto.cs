using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Domain.Enums;
using System.Text.Json.Serialization;


namespace Backend.CMS.Application.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public int? PictureFileId { get; set; }
        public string? PictureUrl { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserRole Role { get; set; }
        public string RoleName => Role.ToString();
        public List<AddressDto> Addresses { get; set; } = [];
        public List<ContactDetailsDto> ContactDetails { get; set; } = [];
    }

    public class CreateUserDto
    {
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int? PictureFileId { get; set; }
        public UserRole Role { get; set; } = UserRole.Customer;
        public List<CreateAddressDto> Addresses { get; set; } = [];
        public List<CreateContactDetailsDto> ContactDetails { get; set; } = [];
        public BaseFileEntity? Picture { get; set; }
    }

    public class UpdateUserDto
    {
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int? PictureFileId { get; set; }
        public UserRole Role { get; set; }
        public List<UpdateAddressDto> Addresses { get; set; } = [];
        public List<UpdateContactDetailsDto> ContactDetails { get; set; } = [];
    }

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }

    public class LoginResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = null!;
    }

    public class RefreshTokenDto
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
    /// <summary>
    /// Advanced user search criteria DTO
    /// </summary>
    public class UserSearchDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public UserRole? Role { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsLocked { get; set; }
        public bool? EmailVerified { get; set; }
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
        public string SortBy { get; set; } = "CreatedAt";
        public string SortDirection { get; set; } = "Desc";
    }
    public class UserCacheDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public int? PictureFileId { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? PasswordChangedAt { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public List<string> RecoveryCodes { get; set; } = new();
        public string? EmailVerificationToken { get; set; }
        public string? ExternalId { get; set; }
        public bool IsExternalUser { get; set; }
        public DateTime? LastExternalSync { get; set; }

        [JsonIgnore]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [JsonIgnore]
        public string? PictureUrl => PictureFileId.HasValue ? $"/api/files/{PictureFileId}/download" : null;

        [JsonIgnore]
        public bool IsAdmin => Role == UserRole.Admin;

        [JsonIgnore]
        public bool IsCustomer => Role == UserRole.Customer;

        [JsonIgnore]
        public bool IsDev => Role == UserRole.Dev;
    }


    /// <summary>
    /// Cache-optimized Session DTO without user navigation property
    /// </summary>
    public class UserSessionCacheDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Wrapper for caching user with related data separately
    /// </summary>
    public class UserWithMetadataCacheDto
    {
        public UserCacheDto User { get; set; } = null!;
        public List<string> Permissions { get; set; } = new();
        public DateTime LastCached { get; set; } = DateTime.UtcNow;
        public string CacheVersion { get; set; } = "1.0";
    }
}