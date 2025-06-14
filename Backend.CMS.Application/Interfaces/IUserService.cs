using Backend.CMS.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Backend.CMS.Application.Interfaces.Services
{
    public interface IUserService
    {
        Task<UserDto> GetUserByIdAsync(int userId);
        Task<UserDto> GetUserByEmailAsync(string email);
        Task<UserDto> GetUserByUsernameAsync(string username);
        Task<(List<UserDto> users, int totalCount)> GetUsersAsync(int page, int pageSize, string? search = null);
        Task<UserDto> CreateUserAsync(CreateUserDto createUserDto);
        Task<UserDto> UpdateUserAsync(int userId, UpdateUserDto updateUserDto);
        Task<bool> DeleteUserAsync(int userId);
        Task<bool> ActivateUserAsync(int userId);
        Task<bool> DeactivateUserAsync(int userId);
        Task<bool> LockUserAsync(int userId);
        Task<bool> UnlockUserAsync(int userId);
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto);
        Task<bool> ResetPasswordAsync(string email);

        Task<bool> ValidateUserCredentialsAsync(string email, string password);
        Task<UserDto> UpdateUserPreferencesAsync(int userId, Dictionary<string, object> preferences);
        Task<bool> VerifyEmailAsync(string token);
        Task<bool> SendEmailVerificationAsync(int userId);
        Task<UserDto> UpdateUserAvatarAsync(int userId, int? avatarFileId);
        Task<UserDto> RemoveUserAvatarAsync(int userId);
    }
}