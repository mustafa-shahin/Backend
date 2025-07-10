using Asp.Versioning;
using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// User management controller providing user operations with pagination
    /// </summary>
    [ApiController]
    [Route("api/{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]

    [AdminOrDev]
    [EnableRateLimiting("ApiPolicy")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of users with optional filtering and search
        /// </summary>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <param name="search">Optional search term for filtering users</param>
        /// <returns>Paginated list of users with metadata</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            try
            {
                _logger.LogDebug("Getting paginated users: page {PageNumber}, size {PageSize}, search '{Search}'",
                    pageNumber, pageSize, search);

                // Service handles all pagination logic including validation and normalization
                var result = await _userService.GetUsersPagedAsync(pageNumber, pageSize, search);

                _logger.LogInformation("Retrieved {UserCount} users on page {Page} of {TotalPages} (total: {TotalCount})",
                    result.Data.Count, result.PageNumber, result.TotalPages, result.TotalCount);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting users");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated users");
                return StatusCode(500, new { Message = "An error occurred while retrieving users" });
            }
        }

        /// <summary>
        /// Advanced user search with filtering options
        /// </summary>
        /// <param name="searchDto">Advanced search criteria with pagination</param>
        /// <returns>Paginated search results</returns>
        [HttpPost("search")]
        [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<UserDto>>> SearchUsers([FromBody] UserSearchDto searchDto)
        {
            try
            {
                if (searchDto == null)
                {
                    return BadRequest(new { Message = "Search criteria is required" });
                }

                _logger.LogDebug("Advanced user search: page {PageNumber}, size {PageSize}, term '{SearchTerm}', role {Role}",
                    searchDto.PageNumber, searchDto.PageSize, searchDto.SearchTerm, searchDto.Role);

                // Service handles all search and pagination logic
                var result = await _userService.SearchUsersPagedAsync(searchDto);

                _logger.LogInformation("Advanced search completed: {UserCount} users on page {Page} of {TotalPages} (total: {TotalCount})",
                    result.Data.Count, result.PageNumber, result.TotalPages, result.TotalCount);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid search criteria for advanced user search");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing advanced user search");
                return StatusCode(500, new { Message = "An error occurred while searching users" });
            }
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>User information with related data</returns>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<UserDto>> GetUser([FromRoute] int id)
        {
            try
            {
                _logger.LogDebug("Getting user by ID: {UserId}", id);

                var user = await _userService.GetUserByIdAsync(id);

                _logger.LogDebug("Successfully retrieved user {UserId}", id);
                return Ok(user);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid user ID {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("User {UserId} not found", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the user" });
            }
        }

        /// <summary>
        /// Create a new user with validation
        /// </summary>
        /// <param name="createUserDto">User creation data</param>
        /// <returns>Created user information</returns>
        [HttpPost]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto createUserDto)
        {
            try
            {
                if (createUserDto == null)
                {
                    return BadRequest(new { Message = "User data is required" });
                }

                _logger.LogDebug("Creating user with email {Email}", createUserDto.Email);

                var user = await _userService.CreateUserAsync(createUserDto);

                _logger.LogInformation("User {UserId} created successfully with email {Email}", user.Id, user.Email);

                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid user creation data");
                return BadRequest(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
            {
                _logger.LogWarning(ex, "User creation failed due to conflict");
                return Conflict(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized user creation attempt");
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new { Message = "An error occurred while creating the user" });
            }
        }

        /// <summary>
        /// Update an existing user with validation
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="updateUserDto">User update data</param>
        /// <returns>Updated user information</returns>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<UserDto>> UpdateUser([FromRoute] int id, [FromBody] UpdateUserDto updateUserDto)
        {
            try
            {
                if (updateUserDto == null)
                {
                    return BadRequest(new { Message = "Update data is required" });
                }

                _logger.LogDebug("Updating user {UserId}", id);

                var user = await _userService.UpdateUserAsync(id, updateUserDto);

                _logger.LogInformation("User {UserId} updated successfully", id);
                return Ok(user);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid user update data for user {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("User {UserId} not found for update", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
            {
                _logger.LogWarning(ex, "User update failed due to conflict for user {UserId}", id);
                return Conflict(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized user update attempt for user {UserId}", id);
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the user" });
            }
        }

        /// <summary>
        /// Delete a user (soft delete)
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Deletion confirmation</returns>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteUser([FromRoute] int id)
        {
            try
            {
                _logger.LogDebug("Deleting user {UserId}", id);

                var success = await _userService.DeleteUserAsync(id);
                if (!success)
                {
                    _logger.LogWarning("User {UserId} not found for deletion", id);
                    return NotFound(new { Message = "User not found" });
                }

                _logger.LogInformation("User {UserId} deleted successfully", id);
                return Ok(new { Message = "User deleted successfully", UserId = id });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid user ID for deletion: {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized user deletion attempt for user {UserId}", id);
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the user" });
            }
        }

        /// <summary>
        /// Activate a user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Activation confirmation</returns>
        [HttpPost("{id:int}/activate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ActivateUser([FromRoute] int id)
        {
            try
            {
                _logger.LogDebug("Activating user {UserId}", id);

                var success = await _userService.ActivateUserAsync(id);
                if (!success)
                {
                    _logger.LogWarning("User {UserId} not found for activation", id);
                    return NotFound(new { Message = "User not found" });
                }

                _logger.LogInformation("User {UserId} activated successfully", id);
                return Ok(new { Message = "User activated successfully", UserId = id });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid user ID for activation: {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized user activation attempt for user {UserId}", id);
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while activating the user" });
            }
        }

        /// <summary>
        /// Deactivate a user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Deactivation confirmation</returns>
        [HttpPost("{id:int}/deactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeactivateUser([FromRoute] int id)
        {
            try
            {
                _logger.LogDebug("Deactivating user {UserId}", id);

                var success = await _userService.DeactivateUserAsync(id);
                if (!success)
                {
                    _logger.LogWarning("User {UserId} not found for deactivation", id);
                    return NotFound(new { Message = "User not found" });
                }

                _logger.LogInformation("User {UserId} deactivated successfully", id);
                return Ok(new { Message = "User deactivated successfully", UserId = id });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid user ID for deactivation: {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized user deactivation attempt for user {UserId}", id);
                return Forbid(ex.Message);
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
        /// <param name="id">User ID</param>
        /// <returns>Lock confirmation</returns>
        [HttpPost("{id:int}/lock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> LockUser([FromRoute] int id)
        {
            try
            {
                _logger.LogDebug("Locking user {UserId}", id);

                var success = await _userService.LockUserAsync(id);
                if (!success)
                {
                    _logger.LogWarning("User {UserId} not found for locking", id);
                    return NotFound(new { Message = "User not found" });
                }

                _logger.LogInformation("User {UserId} locked successfully", id);
                return Ok(new { Message = "User locked successfully", UserId = id });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid user ID for locking: {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized user lock attempt for user {UserId}", id);
                return Forbid(ex.Message);
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
        /// <param name="id">User ID</param>
        /// <returns>Unlock confirmation</returns>
        [HttpPost("{id:int}/unlock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> UnlockUser([FromRoute] int id)
        {
            try
            {
                _logger.LogDebug("Unlocking user {UserId}", id);

                var success = await _userService.UnlockUserAsync(id);
                if (!success)
                {
                    _logger.LogWarning("User {UserId} not found for unlocking", id);
                    return NotFound(new { Message = "User not found" });
                }

                _logger.LogInformation("User {UserId} unlocked successfully", id);
                return Ok(new { Message = "User unlocked successfully", UserId = id });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid user ID for unlocking: {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized user unlock attempt for user {UserId}", id);
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while unlocking the user" });
            }
        }

        /// <summary>
        /// Change user password with validation
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="changePasswordDto">Password change data</param>
        /// <returns>Password change confirmation</returns>
        [HttpPost("{id:int}/change-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ChangePassword([FromRoute] int id, [FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                if (changePasswordDto == null)
                {
                    return BadRequest(new { Message = "Password change data is required" });
                }

                _logger.LogDebug("Changing password for user {UserId}", id);

                var success = await _userService.ChangePasswordAsync(id, changePasswordDto);
                if (!success)
                {
                    _logger.LogWarning("Password change failed for user {UserId} - invalid current password", id);
                    return BadRequest(new { Message = "Current password is incorrect" });
                }

                _logger.LogInformation("Password changed successfully for user {UserId}", id);
                return Ok(new { Message = "Password changed successfully", UserId = id });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid password change data for user {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized password change attempt for user {UserId}", id);
                return Forbid(ex.Message);
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
        /// <param name="id">User ID</param>
        /// <returns>Password reset confirmation</returns>
        [HttpPost("{id:int}/reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ResetPassword([FromRoute] int id)
        {
            try
            {
                _logger.LogDebug("Resetting password for user {UserId}", id);

                // Note: This implementation needs the user's email
                // In a real implementation, you'd get the user first to get their email
                var user = await _userService.GetUserByIdAsync(id);
                var success = await _userService.ResetPasswordAsync(user.Email);

                if (!success)
                {
                    _logger.LogWarning("Password reset failed for user {UserId}", id);
                    return NotFound(new { Message = "User not found or password reset failed" });
                }

                _logger.LogInformation("Password reset initiated for user {UserId}", id);
                return Ok(new { Message = "Password reset initiated", UserId = id });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("User {UserId} not found for password reset", id);
                return NotFound(new { Message = ex.Message });
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
        /// <param name="id">User ID</param>
        /// <param name="preferences">User preferences</param>
        /// <returns>Updated user information</returns>
        [HttpPut("{id:int}/preferences")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<UserDto>> UpdateUserPreferences([FromRoute] int id, [FromBody] Dictionary<string, object> preferences)
        {
            try
            {
                if (preferences == null)
                {
                    return BadRequest(new { Message = "Preferences data is required" });
                }

                _logger.LogDebug("Updating preferences for user {UserId}", id);

                var user = await _userService.UpdateUserPreferencesAsync(id, preferences);

                _logger.LogInformation("Preferences updated successfully for user {UserId}", id);
                return Ok(user);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid preferences data for user {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("User {UserId} not found for preferences update", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized preferences update attempt for user {UserId}", id);
                return Forbid(ex.Message);
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
        /// <param name="id">User ID</param>
        /// <returns>Email verification confirmation</returns>
        [HttpPost("{id:int}/send-verification")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> SendEmailVerification([FromRoute] int id)
        {
            try
            {
                _logger.LogDebug("Sending email verification for user {UserId}", id);

                var success = await _userService.SendEmailVerificationAsync(id);
                if (!success)
                {
                    _logger.LogWarning("Email verification send failed for user {UserId}", id);
                    return BadRequest(new { Message = "Failed to send verification email" });
                }

                _logger.LogInformation("Email verification sent successfully for user {UserId}", id);
                return Ok(new { Message = "Verification email sent successfully", UserId = id });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid user ID for email verification: {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email verification for user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while sending verification email" });
            }
        }

        /// <summary>
        /// Update user avatar
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="updateAvatarDto">Avatar update data</param>
        /// <returns>Updated user information</returns>
        [HttpPut("{id:int}/avatar")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<UserDto>> UpdateUserAvatar([FromRoute] int id, [FromBody] UpdateUserAvatarDto updateAvatarDto)
        {
            try
            {
                if (updateAvatarDto == null)
                {
                    return BadRequest(new { Message = "Avatar data is required" });
                }

                _logger.LogDebug("Updating avatar for user {UserId} with file {FileId}", id, updateAvatarDto.AvatarFileId);

                var user = await _userService.UpdateUserAvatarAsync(id, updateAvatarDto.AvatarFileId);

                _logger.LogInformation("Avatar updated successfully for user {UserId}", id);
                return Ok(user);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid avatar data for user {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("User {UserId} not found for avatar update", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized avatar update attempt for user {UserId}", id);
                return Forbid(ex.Message);
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
        /// <param name="id">User ID</param>
        /// <returns>Updated user information</returns>
        [HttpDelete("{id:int}/avatar")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<UserDto>> RemoveUserAvatar([FromRoute] int id)
        {
            try
            {
                _logger.LogDebug("Removing avatar for user {UserId}", id);

                var user = await _userService.RemoveUserAvatarAsync(id);

                _logger.LogInformation("Avatar removed successfully for user {UserId}", id);
                return Ok(user);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid user ID for avatar removal: {UserId}", id);
                return BadRequest(new { Message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("User {UserId} not found for avatar removal", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized avatar removal attempt for user {UserId}", id);
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing avatar for user {UserId}", id);
                return StatusCode(500, new { Message = "An error occurred while removing the avatar" });
            }
        }
    }

    /// <summary>
    /// DTO for updating user avatar
    /// </summary>
    public class UpdateUserAvatarDto
    {
        /// <summary>
        /// File ID of the new avatar image, or null to remove avatar
        /// </summary>
        public int? AvatarFileId { get; set; }
    }
}