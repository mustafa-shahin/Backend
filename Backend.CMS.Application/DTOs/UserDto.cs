using Backend.CMS.Domain.Enums;

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
        public int? AvatarFileId { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Timezone { get; set; }
        public string? Language { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserRole Role { get; set; }
        public string RoleName => Role.ToString();
        public Dictionary<string, object> Preferences { get; set; } = [];
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
        public int? AvatarFileId { get; set; }
        public string? Timezone { get; set; }
        public string? Language { get; set; }
        public UserRole Role { get; set; } = UserRole.Customer;
        public Dictionary<string, object> Preferences { get; set; } = [];
        public List<CreateAddressDto> Addresses { get; set; } = [];
        public List<CreateContactDetailsDto> ContactDetails { get; set; } = [];
    }

    public class UpdateUserDto
    {
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int? AvatarFileId { get; set; }
        public string? Timezone { get; set; }
        public string? Language { get; set; }
        public UserRole Role { get; set; }
        public Dictionary<string, object> Preferences { get; set; } = [];
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
        public string? TwoFactorCode { get; set; }
    }

    public class LoginResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = null!;
        public bool RequiresTwoFactor { get; set; }
    }

    public class RefreshTokenDto
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class UserListDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AvatarUrl { get; set; }
        public UserRole Role { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public List<AddressDto> Addresses { get; set; } = [];
        public List<ContactDetailsDto> ContactDetails { get; set; } = [];
    }
}