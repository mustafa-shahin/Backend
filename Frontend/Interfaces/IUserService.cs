using Backend.CMS.Application.DTOs;

namespace Frontend.Interface
{
    public interface IUserService
    {
        Task<UserDto?> GetCurrentUserAsync();
        Task<UserDto?> UpdateUserAsync(int id, UpdateUserDto updateUserDto);
        Task<bool> ChangePasswordAsync(ChangePasswordDto changePasswordDto);
    }
}
