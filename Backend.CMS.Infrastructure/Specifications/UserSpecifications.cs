using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Application.DTOs;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Specifications
{
    /// <summary>
    /// User-specific specifications
    /// </summary>
    public static class UserSpecifications
    {
        /// <summary>
        /// Get user by email specification
        /// </summary>
        public class GetByEmailSpec : BaseSpecification<User>
        {
            public GetByEmailSpec(string email) : base(u => u.Email == email)
            {
                AddInclude(u => u.Picture);
                AddInclude(u => u.Addresses.Where(a => !a.IsDeleted));
                AddInclude(u => u.ContactDetails.Where(c => !c.IsDeleted));
                SetCache($"User:Email:{email}", 300);
                AddCacheTag("User");
            }
        }

        /// <summary>
        /// Get user by username specification
        /// </summary>
        public class GetByUsernameSpec : BaseSpecification<User>
        {
            public GetByUsernameSpec(string username) : base(u => u.Username == username)
            {
                AddInclude(u => u.Picture);
                SetCache($"User:Username:{username}", 300);
                AddCacheTag("User");
            }
        }

        /// <summary>
        /// Get user with full details specification
        /// </summary>
        public class GetWithDetailsSpec : BaseSpecification<User>
        {
            public GetWithDetailsSpec(int userId) : base(u => u.Id == userId)
            {
                AddInclude(u => u.Picture);
                AddInclude(u => u.Addresses.Where(a => !a.IsDeleted));
                AddInclude(u => u.ContactDetails.Where(c => !c.IsDeleted));
                AddInclude(u => u.UserPermissions.Where(up => !up.IsDeleted));
                AddInclude("UserPermissions.Permission");
                SetCache($"User:Details:{userId}", 300);
                AddCacheTag("User");
                AddCacheTag($"User:Id:{userId}");
            }
        }

        /// <summary>
        /// Search users specification
        /// </summary>
        public class SearchUsersSpec : BaseSpecification<User>
        {
            public SearchUsersSpec(UserSearchDto searchDto)
            {
                // Build criteria based on search parameters
                if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
                {
                    var searchTerm = searchDto.SearchTerm.ToLowerInvariant();
                    AddCriteria(u => u.FirstName.ToLower().Contains(searchTerm) ||
                                     u.LastName.ToLower().Contains(searchTerm) ||
                                     u.Email.ToLower().Contains(searchTerm) ||
                                     u.Username.ToLower().Contains(searchTerm));
                }

                if (searchDto.Role.HasValue)
                {
                    AddCriteria(u => u.Role == searchDto.Role.Value);
                }

                if (searchDto.IsActive.HasValue)
                {
                    AddCriteria(u => u.IsActive == searchDto.IsActive.Value);
                }

                if (searchDto.IsLocked.HasValue)
                {
                    AddCriteria(u => u.IsLocked == searchDto.IsLocked.Value);
                }

                if (searchDto.EmailVerified.HasValue)
                {
                    if (searchDto.EmailVerified.Value)
                    {
                        AddCriteria(u => u.EmailVerifiedAt.HasValue);
                    }
                    else
                    {
                        AddCriteria(u => !u.EmailVerifiedAt.HasValue);
                    }
                }

                if (searchDto.CreatedAfter.HasValue)
                {
                    AddCriteria(u => u.CreatedAt >= searchDto.CreatedAfter.Value);
                }

                if (searchDto.CreatedBefore.HasValue)
                {
                    AddCriteria(u => u.CreatedAt <= searchDto.CreatedBefore.Value);
                }

                // Apply sorting
                switch (searchDto.SortBy?.ToLowerInvariant())
                {
                    case "email":
                        if (searchDto.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
                            AddOrderByDescending(u => u.Email);
                        else
                            AddOrderBy(u => u.Email);
                        break;
                    case "username":
                        if (searchDto.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
                            AddOrderByDescending(u => u.Username);
                        else
                            AddOrderBy(u => u.Username);
                        break;
                    case "createdat":
                        if (searchDto.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
                            AddOrderByDescending(u => u.CreatedAt);
                        else
                            AddOrderBy(u => u.CreatedAt);
                        break;
                    default:
                        AddOrderBy(u => u.FirstName);
                        AddThenBy(u => u.LastName);
                        break;
                }

                // Apply pagination
                if (searchDto.PageSize > 0)
                {
                    ApplyPaging((searchDto.PageNumber - 1) * searchDto.PageSize, searchDto.PageSize);
                }

                // Include related data
                AddInclude(u => u.Picture);
                AddInclude(u => u.Addresses.Where(a => !a.IsDeleted));
                AddInclude(u => u.ContactDetails.Where(c => !c.IsDeleted));

                // Cache configuration
                var cacheKey = $"User:Search:{searchDto.SearchTerm}:{searchDto.Role}:{searchDto.IsActive}:{searchDto.PageNumber}:{searchDto.PageSize}";
                SetCache(cacheKey, 60); // Cache for 1 minute for search results
                AddCacheTag("User");
            }
        }

        /// <summary>
        /// Get users by role specification
        /// </summary>
        public class GetByRoleSpec : BaseSpecification<User>
        {
            public GetByRoleSpec(UserRole role) : base(u => u.Role == role)
            {
                AddOrderBy(u => u.FirstName);
                AddThenBy(u => u.LastName);
                SetCache($"User:Role:{role}", 300);
                AddCacheTag("User");
            }
        }

        /// <summary>
        /// Get active users specification
        /// </summary>
        public class GetActiveUsersSpec : BaseSpecification<User>
        {
            public GetActiveUsersSpec() : base(u => u.IsActive && !u.IsLocked)
            {
                AddOrderBy(u => u.FirstName);
                AddThenBy(u => u.LastName);
                SetCache("User:Active", 300);
                AddCacheTag("User");
            }
        }

        /// <summary>
        /// User summary projection specification
        /// </summary>
        public class UserSummaryProjectionSpec : BaseProjectionSpecification<User, UserDto>
        {
            public UserSummaryProjectionSpec() : base(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                Username = u.Username,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                IsLocked = u.IsLocked,
                LastLoginAt = u.LastLoginAt,
                EmailVerifiedAt = u.EmailVerifiedAt,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                Role = u.Role,
                PictureFileId = u.PictureFileId,
                PictureUrl = u.Picture != null ? $"/api/files/{u.PictureFileId}/download" : null
            })
            {
                SetCache("User:Summary", 180);
                AddCacheTag("User");
            }
        }

        /// <summary>
        /// Check if email exists specification
        /// </summary>
        public class EmailExistsSpec : BaseSpecification<User>
        {
            public EmailExistsSpec(string email, int? excludeUserId = null)
            {
                AddCriteria(u => u.Email == email);

                if (excludeUserId.HasValue)
                {
                    AddCriteria(u => u.Id != excludeUserId.Value);
                }

                SetCache($"User:EmailExists:{email}:{excludeUserId}", 60);
                AddCacheTag("User");
            }
        }

        /// <summary>
        /// Check if username exists specification
        /// </summary>
        public class UsernameExistsSpec : BaseSpecification<User>
        {
            public UsernameExistsSpec(string username, int? excludeUserId = null)
            {
                AddCriteria(u => u.Username == username);

                if (excludeUserId.HasValue)
                {
                    AddCriteria(u => u.Id != excludeUserId.Value);
                }

                SetCache($"User:UsernameExists:{username}:{excludeUserId}", 60);
                AddCacheTag("User");
            }
        }

        /// <summary>
        /// Get user by email verification token specification
        /// </summary>
        public class GetByEmailVerificationTokenSpec : BaseSpecification<User>
        {
            public GetByEmailVerificationTokenSpec(string token) : base(u => u.EmailVerificationToken == token)
            {
                // Don't cache sensitive operations
                DisableTracking();
            }
        }

        /// <summary>
        /// Get recently created users specification
        /// </summary>
        public class GetRecentUsersSpec : BaseSpecification<User>
        {
            public GetRecentUsersSpec(int count = 10) : base(u => u.CreatedAt >= DateTime.UtcNow.AddDays(-30))
            {
                AddOrderByDescending(u => u.CreatedAt);
                ApplyPaging(0, count);
                AddInclude(u => u.Picture);
                SetCache($"User:Recent:{count}", 300);
                AddCacheTag("User");
            }
        }

        /// <summary>
        /// Get locked users specification
        /// </summary>
        public class GetLockedUsersSpec : BaseSpecification<User>
        {
            public GetLockedUsersSpec() : base(u => u.IsLocked)
            {
                AddOrderByDescending(u => u.LockoutEnd);
                AddInclude(u => u.Picture);
                SetCache("User:Locked", 60);
                AddCacheTag("User");
            }
        }

        /// <summary>
        /// Specification for fetching users by a list of IDs.
        /// </summary>
        public class UsersByIdsSpec : BaseSpecification<User>
        {
            public UsersByIdsSpec(IEnumerable<int> userIds) : base(u => userIds.Contains(u.Id))
            {
                // No specific includes or ordering unless needed for this bulk operation
            }
        }

        /// <summary>
        /// Specification for all users (used for total count).
        /// </summary>
        public class AllUsersSpec : BaseSpecification<User>
        {
            public AllUsersSpec() : base()
            {
                // No criteria, orders, or includes by default, just a concrete spec for counting all.
            }
        }

        /// <summary>
        /// Search users with projection specification. Combines search criteria and user DTO projection.
        /// </summary>
        public class SearchUserSummaryProjectionSpec : BaseProjectionSpecification<User, UserDto>
        {
            public SearchUserSummaryProjectionSpec(UserSearchDto searchDto) : base(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                Username = u.Username,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                IsLocked = u.IsLocked,
                LastLoginAt = u.LastLoginAt,
                EmailVerifiedAt = u.EmailVerifiedAt,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                Role = u.Role,
                PictureFileId = u.PictureFileId,
                PictureUrl = u.Picture != null ? $"/api/files/{u.PictureFileId}/download" : null
            })
            {
                // Apply search criteria
                if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
                {
                    var searchTerm = searchDto.SearchTerm.ToLowerInvariant();
                    AddCriteria(u => u.FirstName.ToLower().Contains(searchTerm) ||
                                     u.LastName.ToLower().Contains(searchTerm) ||
                                     u.Email.ToLower().Contains(searchTerm) ||
                                     u.Username.ToLower().Contains(searchTerm));
                }

                if (searchDto.Role.HasValue)
                {
                    AddCriteria(u => u.Role == searchDto.Role.Value);
                }

                if (searchDto.IsActive.HasValue)
                {
                    AddCriteria(u => u.IsActive == searchDto.IsActive.Value);
                }

                if (searchDto.IsLocked.HasValue)
                {
                    AddCriteria(u => u.IsLocked == searchDto.IsLocked.Value);
                }

                if (searchDto.EmailVerified.HasValue)
                {
                    if (searchDto.EmailVerified.Value)
                    {
                        AddCriteria(u => u.EmailVerifiedAt.HasValue);
                    }
                    else
                    {
                        AddCriteria(u => !u.EmailVerifiedAt.HasValue);
                    }
                }

                if (searchDto.CreatedAfter.HasValue)
                {
                    AddCriteria(u => u.CreatedAt >= searchDto.CreatedAfter.Value);
                }

                if (searchDto.CreatedBefore.HasValue)
                {
                    AddCriteria(u => u.CreatedAt <= searchDto.CreatedBefore.Value);
                }

                // Apply sorting
                switch (searchDto.SortBy?.ToLowerInvariant())
                {
                    case "email":
                        if (searchDto.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
                            AddOrderByDescending(u => u.Email);
                        else
                            AddOrderBy(u => u.Email);
                        break;
                    case "username":
                        if (searchDto.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
                            AddOrderByDescending(u => u.Username);
                        else
                            AddOrderBy(u => u.Username);
                        break;
                    case "createdat":
                        if (searchDto.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
                            AddOrderByDescending(u => u.CreatedAt);
                        else
                            AddOrderBy(u => u.CreatedAt);
                        break;
                    default:
                        AddOrderBy(u => u.FirstName);
                        AddThenBy(u => u.LastName);
                        break;
                }

                // Apply pagination
                if (searchDto.PageSize > 0)
                {
                    ApplyPaging((searchDto.PageNumber - 1) * searchDto.PageSize, searchDto.PageSize);
                }
                AddInclude(u => u.Picture); // To ensure Picture is available for PictureUrl projection

                // Cache configuration
                var cacheKey = $"User:SearchProjection:{searchDto.SearchTerm}:{searchDto.Role}:{searchDto.IsActive}:{searchDto.PageNumber}:{searchDto.PageSize}";
                SetCache(cacheKey, 60);
                AddCacheTag("User");
            }
        }
    }
}