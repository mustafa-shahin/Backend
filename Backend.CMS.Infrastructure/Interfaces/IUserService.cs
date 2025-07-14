using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Services;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IUserService
    {
        #region User Retrieval Operations

        /// <summary>
        /// Get user by ID with caching support
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>User information</returns>
        Task<UserDto> GetUserByIdAsync(int userId);

        /// <summary>
        /// Get user by email with caching support
        /// </summary>
        /// <param name="email">User email</param>
        /// <returns>User information</returns>
        Task<UserDto> GetUserByEmailAsync(string email);

        /// <summary>
        /// Get user by username with caching support
        /// </summary>
        /// <param name="username">Username</param>
        /// <returns>User information</returns>
        Task<UserDto> GetUserByUsernameAsync(string username);

        #endregion

        #region Pagination and Search Operations

        /// <summary>
        /// Get paginated list of users with optional search
        /// ALL pagination logic is handled in the service layer
        /// </summary>
        /// <param name="pageNumber">Page number (1-based, auto-corrected if invalid)</param>
        /// <param name="pageSize">Page size (auto-corrected to valid range 1-100)</param>
        /// <param name="search">Optional search term</param>
        /// <returns>Paginated result with all metadata calculated in service</returns>
        Task<PaginatedResult<UserDto>> GetUsersPagedAsync(int pageNumber = 1, int pageSize = 10, string? search = null);

        /// <summary>
        /// Advanced user search with filtering options
        /// </summary>
        /// <param name="searchDto">Advanced search criteria with pagination</param>
        /// <returns>Paginated search results</returns>
        Task<PaginatedResult<UserDto>> SearchUsersPagedAsync(UserSearchDto searchDto);

        #endregion

        #region User Management Operations

        /// <summary>
        /// Create a new user with validation
        /// </summary>
        /// <param name="createUserDto">User creation data</param>
        /// <returns>Created user information</returns>
        Task<UserDto> CreateUserAsync(CreateUserDto createUserDto);

        /// <summary>
        /// Update an existing user with validation
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="updateUserDto">User update data</param>
        /// <returns>Updated user information</returns>
        Task<UserDto> UpdateUserAsync(int userId, UpdateUserDto updateUserDto);

        /// <summary>
        /// Delete a user (soft delete)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> DeleteUserAsync(int userId);

        #endregion

        #region User Status Management

        /// <summary>
        /// Activate a user account
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> ActivateUserAsync(int userId);

        /// <summary>
        /// Deactivate a user account
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> DeactivateUserAsync(int userId);

        /// <summary>
        /// Lock a user account
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> LockUserAsync(int userId);

        /// <summary>
        /// Unlock a user account
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> UnlockUserAsync(int userId);

        #endregion

        #region Authentication and Security

        /// <summary>
        /// Change user password with validation
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="changePasswordDto">Password change data</param>
        /// <returns>Success status</returns>
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto);

        /// <summary>
        /// Reset user password (admin action)
        /// </summary>
        /// <param name="email">User email</param>
        /// <returns>Success status</returns>
        Task<bool> ResetPasswordAsync(string email);

        /// <summary>
        /// Validate user credentials for authentication
        /// </summary>
        /// <param name="email">User email</param>
        /// <param name="password">User password</param>
        /// <returns>Validation result</returns>
        Task<bool> ValidateUserCredentialsAsync(string email, string password);

        #endregion

        #region User Preferences and Profile

        /// <summary>
        /// Update user preferences
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="preferences">User preferences dictionary</param>
        /// <returns>Updated user information</returns>
        Task<UserDto> UpdateUserPreferencesAsync(int userId, Dictionary<string, object> preferences);

        /// <summary>
        /// Update user avatar
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="pictureFileId">File ID of the new avatar image</param>
        /// <returns>Updated user information</returns>
        Task<UserDto> UpdateUserAvatarAsync(int userId, int? pictureFileId);

        /// <summary>
        /// Remove user avatar
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Updated user information</returns>
        Task<UserDto> RemoveUserAvatarAsync(int userId);

        #endregion

        #region Email Verification

        /// <summary>
        /// Verify user email using verification token
        /// </summary>
        /// <param name="token">Email verification token</param>
        /// <returns>Verification result</returns>
        Task<bool> VerifyEmailAsync(string token);

        /// <summary>
        /// Send email verification to user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> SendEmailVerificationAsync(int userId);

        #endregion
    }
}