using Backend.CMS.Application.DTOs;

namespace Frontend.Interfaces
{
    public interface IUsersService
    {
        Task<PagedResult<UserDto>> GetUsersAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<UserDto?> GetUserByIdAsync(int id);
        Task<UserDto?> CreateUserAsync(CreateUserDto createUserDto);
        Task<UserDto?> UpdateUserAsync(int id, UpdateUserDto updateUserDto);
        Task<bool> DeleteUserAsync(int id);
        Task<bool> ActivateUserAsync(int id);
        Task<bool> DeactivateUserAsync(int id);
        Task<bool> LockUserAsync(int id);
        Task<bool> UnlockUserAsync(int id);
        Task<UserDto?> UpdateUserAvatarAsync(int id, int? avatarFileId);
        Task<UserDto?> RemoveUserAvatarAsync(int id);
        Task<bool> ChangePasswordAsync(int id, ChangePasswordDto changePasswordDto);
        Task<bool> ResetPasswordAsync(int id);
        Task<bool> SendEmailVerificationAsync(int id);
    }
}
