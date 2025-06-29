using Backend.CMS.Application.DTOs;

namespace Frontend.Interface;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginDto loginDto);
    Task LogoutAsync();
    Task<bool> RefreshTokenAsync();
    Task<UserDto?> GetCurrentUserAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetTokenAsync();
    event Action? AuthenticationStateChanged;
}