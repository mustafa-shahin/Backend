using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Cache;
using Backend.CMS.Infrastructure.Caching;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Specifications;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Repositories
{
    /// <summary>
    /// User repository implementation
    /// </summary>
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            IRepositoryCache cache,
            ILogger<Repository<User>> logger,
            IMapper mapper)
            : base(context, userSessionService, cache, logger, mapper)
        {
        }

        #region IUserRepository Implementation

        public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.GetByEmailSpec(email);
            return await GetAsync(spec, cancellationToken);
        }

        public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.GetByUsernameSpec(username);
            return await GetAsync(spec, cancellationToken);
        }

        public async Task<User?> GetWithDetailsAsync(int userId, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.GetWithDetailsSpec(userId);
            return await GetAsync(spec, cancellationToken);
        }

        public async Task<PagedResult<User>> SearchUsersAsync(UserSearchDto searchDto, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.SearchUsersSpec(searchDto);
            return await GetPagedAsync(spec, searchDto.PageNumber, searchDto.PageSize, cancellationToken);
        }

        public async Task<PagedResult<UserDto>> SearchUsersProjectedAsync(UserSearchDto searchDto, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.SearchUsersSpec(searchDto);
            var projectionSpec = new UserSpecifications.UserSummaryProjectionSpec();


            var searchProjectionSpec = new UserSpecifications.SearchUserSummaryProjectionSpec(searchDto);
            return await GetPagedProjectedAsync(searchProjectionSpec, searchDto.PageNumber, searchDto.PageSize, cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetByRoleAsync(UserRole role, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.GetByRoleSpec(role);
            return await ListAsync(spec, cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.GetActiveUsersSpec();
            return await ListAsync(spec, cancellationToken);
        }

        public async Task<bool> EmailExistsAsync(string email, int? excludeUserId = null, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.EmailExistsSpec(email, excludeUserId);
            return await AnyAsync(spec, cancellationToken);
        }

        public async Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.UsernameExistsSpec(username, excludeUserId);
            return await AnyAsync(spec, cancellationToken);
        }

        public async Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.GetByEmailVerificationTokenSpec(token);
            return await GetAsync(spec, cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetRecentUsersAsync(int count = 10, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.GetRecentUsersSpec(count);
            return await ListAsync(spec, cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetLockedUsersAsync(CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.GetLockedUsersSpec();
            return await ListAsync(spec, cancellationToken);
        }

        public async Task<int> GetUserCountByRoleAsync(UserRole role, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.GetByRoleSpec(role);
            return await CountAsync(spec, cancellationToken);
        }

        public async Task<Dictionary<UserRole, int>> GetUserCountByRoleAsync(CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<UserRole, int>();

            foreach (UserRole role in Enum.GetValues<UserRole>())
            {
                var count = await GetUserCountByRoleAsync(role, cancellationToken);
                result[role] = count;
            }

            return result;
        }

        #endregion

        #region Advanced User Operations

        /// <summary>
        /// Update user last login time
        /// </summary>
        public async Task UpdateLastLoginAsync(int userId, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdTrackedAsync(userId, cancellationToken);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await UpdateAsync(user, cancellationToken);
            }
        }

        /// <summary>
        /// Lock user account
        /// </summary>
        public async Task LockUserAsync(int userId, DateTime? lockoutEnd = null, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdTrackedAsync(userId, cancellationToken);
            if (user != null)
            {
                user.IsLocked = true;
                user.LockoutEnd = lockoutEnd ?? DateTime.UtcNow.AddDays(30);
                await UpdateAsync(user, cancellationToken);
            }
        }

        /// <summary>
        /// Unlock user account
        /// </summary>
        public async Task UnlockUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdTrackedAsync(userId, cancellationToken);
            if (user != null)
            {
                user.IsLocked = false;
                user.LockoutEnd = null;
                user.FailedLoginAttempts = 0;
                await UpdateAsync(user, cancellationToken);
            }
        }

        /// <summary>
        /// Verify user email
        /// </summary>
        public async Task VerifyEmailAsync(int userId, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdTrackedAsync(userId, cancellationToken);
            if (user != null)
            {
                user.EmailVerifiedAt = DateTime.UtcNow;
                user.EmailVerificationToken = null;
                await UpdateAsync(user, cancellationToken);
            }
        }

        /// <summary>
        /// Increment failed login attempts
        /// </summary>
        public async Task IncrementFailedLoginAttemptsAsync(int userId, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdTrackedAsync(userId, cancellationToken);
            if (user != null)
            {
                user.FailedLoginAttempts++;

                // Auto-lock after 5 failed attempts
                if (user.FailedLoginAttempts >= 5)
                {
                    user.IsLocked = true;
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(30);
                }

                await UpdateAsync(user, cancellationToken);
            }
        }

        /// <summary>
        /// Reset failed login attempts
        /// </summary>
        public async Task ResetFailedLoginAttemptsAsync(int userId, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdTrackedAsync(userId, cancellationToken);
            if (user != null)
            {
                user.FailedLoginAttempts = 0;
                await UpdateAsync(user, cancellationToken);
            }
        }

        /// <summary>
        /// Update user password
        /// </summary>
        public async Task UpdatePasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdTrackedAsync(userId, cancellationToken);
            if (user != null)
            {
                user.PasswordHash = passwordHash;
                user.PasswordChangedAt = DateTime.UtcNow;
                await UpdateAsync(user, cancellationToken);
            }
        }

        /// <summary>
        /// Bulk activate users
        /// </summary>
        public async Task<int> BulkActivateUsersAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.UsersByIdsSpec(userIds); 
            return await BulkUpdateAsync(spec, u => new User { IsActive = true }, cancellationToken);
        }

        /// <summary>
        /// Bulk deactivate users
        /// </summary>
        public async Task<int> BulkDeactivateUsersAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default)
        {
            var spec = new UserSpecifications.UsersByIdsSpec(userIds); 
            return await BulkUpdateAsync(spec, u => new User { IsActive = false }, cancellationToken);
        }

        /// <summary>
        /// Get user statistics
        /// </summary>
        public async Task<UserStatisticsDto> GetUserStatisticsAsync(CancellationToken cancellationToken = default)
        {

            var totalUsers = await CountAsync(new UserSpecifications.AllUsersSpec(), cancellationToken);
            var activeUsers = await CountAsync(new UserSpecifications.GetActiveUsersSpec(), cancellationToken);
            var lockedUsers = await CountAsync(new UserSpecifications.GetLockedUsersSpec(), cancellationToken);
            var roleBreakdown = await GetUserCountByRoleAsync(cancellationToken);

            return new UserStatisticsDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                LockedUsers = lockedUsers,
                UsersByRole = roleBreakdown,
                LastUpdated = DateTime.UtcNow
            };
        }

        #endregion
    }

    /// <summary>
    /// User statistics DTO
    /// </summary>
    public class UserStatisticsDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int LockedUsers { get; set; }
        public Dictionary<UserRole, int> UsersByRole { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }
}