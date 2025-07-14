using Backend.CMS.Application.DTOs;

namespace Frontend.Interfaces
{
    public interface IUsersService
    {
        #region Paginated User Operations

        /// <summary>
        /// Get paginated list of users with optional search
        /// </summary>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Page size (1-100)</param>
        /// <param name="search">Optional search term</param>
        /// <returns>Paginated result with metadata</returns>
        Task<PaginatedResult<UserDto>> GetUsersAsync(int pageNumber = 1, int pageSize = 10, string? search = null);

        /// <summary>
        /// Advanced user search with filtering
        /// </summary>
        /// <param name="searchDto">Advanced search criteria</param>
        /// <returns>Paginated search results</returns>
        Task<PaginatedResult<UserDto>> SearchUsersAsync(UserSearchDto searchDto);

        /// <summary>
        /// Get users using API version 2.0 with fallback to v1.0
        /// </summary>
        /// <param name="pageNumber">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="search">Search term</param>
        /// <returns>Paginated users</returns>
        Task<PaginatedResult<UserDto>> GetUsersV2Async(int pageNumber = 1, int pageSize = 10, string? search = null);

        #endregion

        #region Individual User Operations

        /// <summary>
        /// Get user by ID
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>User information or null if not found</returns>
        Task<UserDto?> GetUserByIdAsync(int id);

        /// <summary>
        /// Create a new user
        /// </summary>
        /// <param name="createUserDto">User creation data</param>
        /// <returns>Created user information</returns>
        Task<UserDto?> CreateUserAsync(CreateUserDto createUserDto);

        /// <summary>
        /// Update an existing user
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="updateUserDto">User update data</param>
        /// <returns>Updated user information</returns>
        Task<UserDto?> UpdateUserAsync(int id, UpdateUserDto updateUserDto);

        /// <summary>
        /// Delete a user
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> DeleteUserAsync(int id);

        #endregion

        #region User Status Management

        /// <summary>
        /// Activate a user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> ActivateUserAsync(int id);

        /// <summary>
        /// Deactivate a user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> DeactivateUserAsync(int id);

        /// <summary>
        /// Lock a user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> LockUserAsync(int id);

        /// <summary>
        /// Unlock a user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> UnlockUserAsync(int id);

        #endregion

        #region Avatar Management

        /// <summary>
        /// Update user avatar
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="avatarFileId">Avatar file ID</param>
        /// <returns>Updated user information</returns>
        Task<UserDto?> UpdateUserAvatarAsync(int id, int? avatarFileId);

        /// <summary>
        /// Remove user avatar
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Updated user information</returns>
        Task<UserDto?> RemoveUserAvatarAsync(int id);

        #endregion

        #region Password Management

        /// <summary>
        /// Change user password
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="changePasswordDto">Password change data</param>
        /// <returns>Success status</returns>
        Task<bool> ChangePasswordAsync(int id, ChangePasswordDto changePasswordDto);

        /// <summary>
        /// Reset user password (admin action)
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> ResetPasswordAsync(int id);

        #endregion

        #region Email Verification

        /// <summary>
        /// Send email verification
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        Task<bool> SendEmailVerificationAsync(int id);

        #endregion
    }
}