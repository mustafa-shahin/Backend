using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AdminOrDev]

    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                return Ok(user);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the user" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<UserListDto>>> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            try
            {
                var (users, totalCount) = await _userService.GetUsersAsync(page, pageSize, search);

                var result = new PagedResult<UserListDto>
                {
                    Items = users,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, new { Message = "An error occurred while retrieving users" });
            }
        }

        /// <summary>
        /// Create a new user
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto createUserDto)
        {
            try
            {
                var user = await _userService.CreateUserAsync(createUserDto);
                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new { Message = "An error occurred while creating the user" });
            }
        }

        /// <summary>
        /// Update an existing user
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] UpdateUserDto updateUserDto)
        {
            try
            {
                var user = await _userService.UpdateUserAsync(id, updateUserDto);
                return Ok(user);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the user" });
            }
        }

        /// <summary>
        /// Delete a user
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            try
            {
                var success = await _userService.DeleteUserAsync(id);
                if (!success)
                    return NotFound(new { Message = "User not found" });

                return Ok(new { Message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the user" });
            }
        }

        /// <summary>
        /// Activate a user
        /// </summary>
        [HttpPost("{id:int}/activate")]
        public async Task<ActionResult> ActivateUser(int id)
        {
            try
            {
                var success = await _userService.ActivateUserAsync(id);
                if (!success)
                {
                    return NotFound(new { Message = "User not found" });
                }
                return Ok(new { Message = "User activated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while activating the user" });
            }
        }

        /// <summary>
        /// Deactivate a user
        /// </summary>
        [HttpPost("{id:int}/deactivate")]
        public async Task<ActionResult> DeactivateUser(int id)
        {
            try
            {
                var success = await _userService.DeactivateUserAsync(id);
                if (!success)
                {
                    return NotFound(new { Message = "User not found" });
                }
                return Ok(new { Message = "User deactivated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while deactivating the user" });
            }
        }

        /// <summary>
        /// Lock a user account
        /// </summary>
        [HttpPost("{id:int}/lock")]
        public async Task<ActionResult> LockUser(int id)
        {
            try
            {
                var success = await _userService.LockUserAsync(id);
                if (!success)
                {
                    return NotFound(new { Message = "User not found" });
                }
                return Ok(new { Message = "User locked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while locking the user" });
            }
        }

        /// <summary>
        /// Unlock a user account
        /// </summary>
        [HttpPost("{id:int}/unlock")]
        public async Task<ActionResult> UnlockUser(int id)
        {
            try
            {
                var success = await _userService.UnlockUserAsync(id);
                if (!success)
                {
                    return NotFound(new { Message = "User not found" });
                }
                return Ok(new { Message = "User unlocked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while unlocking the user" });
            }
        }

        /// <summary>
        /// Change user password
        /// </summary>
        [HttpPost("{id:int}/change-password")]
        public async Task<ActionResult> ChangePassword(int id, [FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                var success = await _userService.ChangePasswordAsync(id, changePasswordDto);
                if (!success)
                {
                    return BadRequest(new { Message = "Current password is incorrect" });
                }
                return Ok(new { Message = "Password changed successfully" });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Password change failed for {UserId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while changing the password" });
            }
        }

        /// <summary>
        /// Reset user password (admin action)
        /// </summary>
        [HttpPost("{id:int}/reset-password")]
        public async Task<ActionResult> ResetPassword(int id)
        {
            try
            {
                // This should generate a temporary password or send reset email
                var success = await _userService.ResetPasswordAsync(string.Empty); // Needs user email
                if (!success)
                {
                    return NotFound(new { Message = "User not found" });
                }
                return Ok(new { Message = "Password reset initiated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while resetting the password" });
            }
        }

        /// <summary>
        /// Update user preferences
        /// </summary>
        [HttpPut("{id:int}/preferences")]
        public async Task<ActionResult<UserDto>> UpdateUserPreferences(int id, [FromBody] Dictionary<string, object> preferences)
        {
            try
            {
                var user = await _userService.UpdateUserPreferencesAsync(id, preferences);
                return Ok(user);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Preferences update failed for {UserId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating preferences for user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating user preferences" });
            }
        }

        /// <summary>
        /// Send email verification
        /// </summary>
        [HttpPost("{id:int}/send-verification")]
        public async Task<ActionResult> SendEmailVerification(int id)
        {
            try
            {
                var success = await _userService.SendEmailVerificationAsync(id);
                if (!success)
                {
                    return BadRequest(new { Message = "Failed to send verification email" });
                }
                return Ok(new { Message = "Verification email sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification email for user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while sending verification email" });
            }
        }
        [HttpPut("{id:int}/avatar")]
        public async Task<ActionResult<UserDto>> UpdateUserAvatar(int id, [FromBody] UpdateUserAvatarDto updateAvatarDto)
        {
            try
            {
                var user = await _userService.UpdateUserAvatarAsync(id, updateAvatarDto.AvatarFileId);
                return Ok(user);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Avatar update failed for {UserId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating avatar for user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the avatar" });
            }
        }
        /// <summary>
        /// Remove user avatar
        /// </summary>
        [HttpDelete("{id:int}/avatar")]
        public async Task<ActionResult<UserDto>> RemoveUserAvatar(int id)
        {
            try
            {
                var user = await _userService.RemoveUserAvatarAsync(id);
                return Ok(user);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Avatar removal failed for {UserId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing avatar for user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while removing the avatar" });
            }
        }
    }

    public class UpdateUserAvatarDto
    {
        public int? AvatarFileId { get; set; }
    }
}