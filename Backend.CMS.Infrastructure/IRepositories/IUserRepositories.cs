using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Repositories;

namespace Backend.CMS.Infrastructure.IRepositories
{
    /// <summary>
    /// User repository interface
    /// </summary>
    public interface IUserRepository : IRepository<User>
    {
        #region Basic User Operations

        /// <summary>
        /// Get user by email
        /// </summary>
        Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get user by username
        /// </summary>
        Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get user with full details
        /// </summary>
        Task<User?> GetWithDetailsAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Search users
        /// </summary>
        Task<PagedResult<User>> SearchUsersAsync(UserSearchDto searchDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Search users with projection
        /// </summary>
        Task<PagedResult<UserDto>> SearchUsersProjectedAsync(UserSearchDto searchDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get users by role
        /// </summary>
        Task<IReadOnlyList<User>> GetByRoleAsync(UserRole role, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get active users
        /// </summary>
        Task<IReadOnlyList<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Validation Operations

        /// <summary>
        /// Check if email exists
        /// </summary>
        Task<bool> EmailExistsAsync(string email, int? excludeUserId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if username exists
        /// </summary>
        Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get user by email verification token
        /// </summary>
        Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default);

        #endregion

        #region Query Operations

        /// <summary>
        /// Get recent users
        /// </summary>
        Task<IReadOnlyList<User>> GetRecentUsersAsync(int count = 10, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get locked users
        /// </summary>
        Task<IReadOnlyList<User>> GetLockedUsersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get user count by role
        /// </summary>
        Task<int> GetUserCountByRoleAsync(UserRole role, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get user count breakdown by role
        /// </summary>
        Task<Dictionary<UserRole, int>> GetUserCountByRoleAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Advanced User Operations

        /// <summary>
        /// Update user last login time
        /// </summary>
        Task UpdateLastLoginAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lock user account
        /// </summary>
        Task LockUserAsync(int userId, DateTime? lockoutEnd = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unlock user account
        /// </summary>
        Task UnlockUserAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verify user email
        /// </summary>
        Task VerifyEmailAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Increment failed login attempts
        /// </summary>
        Task IncrementFailedLoginAttemptsAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reset failed login attempts
        /// </summary>
        Task ResetFailedLoginAttemptsAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update user password
        /// </summary>
        Task UpdatePasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Bulk activate users
        /// </summary>
        Task<int> BulkActivateUsersAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk deactivate users
        /// </summary>
        Task<int> BulkDeactivateUsersAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default);

        #endregion

        #region Statistics

        /// <summary>
        /// Get user statistics
        /// </summary>
        Task<UserStatisticsDto> GetUserStatisticsAsync(CancellationToken cancellationToken = default);

        #endregion
    }
}