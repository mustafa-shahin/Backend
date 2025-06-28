using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace Backend.CMS.Application.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponseDto> LoginAsync(LoginDto loginDto);
        Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenDto refreshTokenDto);
        Task<bool> LogoutAsync(string refreshToken);
        Task<bool> RevokeAllSessionsAsync(int userId);
        Task<bool> ValidateTokenAsync(string token);
        Task<UserDto> GetCurrentUserAsync();
        Task<bool> ForgotPasswordAsync(string email);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
        Task<List<string>> GenerateRecoveryCodesAsync(int userId);
        Task<bool> UseRecoveryCodeAsync(int userId, string code);
        Task<LoginResponseDto> CreateTokenForUserAsync(User user);
    }
}