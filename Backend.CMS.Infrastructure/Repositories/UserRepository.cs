using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(ApplicationDbContext context, ILogger<UserRepository> logger) 
            : base(context, logger)
        {
        }

        public UserRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    throw new ArgumentException("Email cannot be null or empty", nameof(email));

                var user = await _dbSet
                    .Include(u => u.Picture)
                    .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

                _logger.LogDebug("Retrieved user by email {Email}: {Found}", email, user != null ? "Found" : "Not found");
                
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by email {Email}", email);
                throw;
            }
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username))
                    throw new ArgumentException("Username cannot be null or empty", nameof(username));

                var user = await _dbSet
                    .Include(u => u.Picture)
                    .FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted);

                _logger.LogDebug("Retrieved user by username {Username}: {Found}", username, user != null ? "Found" : "Not found");
                
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by username {Username}", username);
                throw;
            }
        }

        public async Task<User?> GetWithAddressesAndContactsAsync(int userId)
        {
            try
            {
                var user = await _dbSet
                    .Include(u => u.Picture)
                    .Include(u => u.Addresses.Where(a => !a.IsDeleted))
                    .Include(u => u.ContactDetails.Where(c => !c.IsDeleted))
                    .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

                _logger.LogDebug("Retrieved user with addresses and contacts {UserId}: {Found}", userId, user != null ? "Found" : "Not found");
                
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with addresses and contacts {UserId}", userId);
                throw;
            }
        }

        public async Task<User?> GetWithRolesAsync(int userId)
        {
            try
            {
                var user = await _dbSet
                    .Include(u => u.Picture)
                    .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

                _logger.LogDebug("Retrieved user with roles {UserId}: {Found}", userId, user != null ? "Found" : "Not found");
                
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with roles {UserId}", userId);
                throw;
            }
        }

        public async Task<User?> GetWithRolesAndPermissionsAsync(int userId)
        {
            try
            {
                var user = await _dbSet
                    .Include(u => u.Picture)
                    .Include(u => u.UserPermissions.Where(up => !up.IsDeleted))
                        .ThenInclude(up => up.Permission)
                    .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

                _logger.LogDebug("Retrieved user with roles and permissions {UserId}: {Found}", userId, user != null ? "Found" : "Not found");
                
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with roles and permissions {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<User>> SearchUsersAsync(string searchTerm, int page, int pageSize)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));

                ValidatePagination(page, pageSize);

                var users = await _dbSet
                    .Include(u => u.Picture)
                    .Include(u => u.Addresses.Where(a => !a.IsDeleted))
                    .Include(u => u.ContactDetails.Where(c => !c.IsDeleted))
                    .Where(u => !u.IsDeleted && (
                        u.FirstName.Contains(searchTerm) ||
                        u.LastName.Contains(searchTerm) ||
                        u.Email.Contains(searchTerm) ||
                        u.Username.Contains(searchTerm)))
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogDebug("Found {Count} users matching search term '{SearchTerm}'", users.Count, searchTerm);
                
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with term '{SearchTerm}'", searchTerm);
                throw;
            }
        }

        public async Task<IEnumerable<User>> GetPagedWithRelatedAsync(int page, int pageSize)
        {
            try
            {
                ValidatePagination(page, pageSize);

                var users = await _dbSet
                    .Include(u => u.Picture)
                    .Include(u => u.Addresses.Where(a => !a.IsDeleted))
                    .Include(u => u.ContactDetails.Where(c => !c.IsDeleted))
                    .Where(u => !u.IsDeleted)
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} users with related data, page {Page}", users.Count, page);
                
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged users with related data, page {Page}", page);
                throw;
            }
        }

        public async Task<bool> EmailExistsAsync(string email, int? excludeUserId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    throw new ArgumentException("Email cannot be null or empty", nameof(email));

                var query = _dbSet.Where(u => !u.IsDeleted && u.Email == email);

                if (excludeUserId.HasValue)
                    query = query.Where(u => u.Id != excludeUserId.Value);

                var exists = await query.AnyAsync();
                
                _logger.LogDebug("Email {Email} exists: {Exists} (excluding user {ExcludeUserId})", email, exists, excludeUserId);
                
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if email exists {Email}", email);
                throw;
            }
        }

        public async Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username))
                    throw new ArgumentException("Username cannot be null or empty", nameof(username));

                var query = _dbSet.Where(u => !u.IsDeleted && u.Username == username);

                if (excludeUserId.HasValue)
                    query = query.Where(u => u.Id != excludeUserId.Value);

                var exists = await query.AnyAsync();
                
                _logger.LogDebug("Username {Username} exists: {Exists} (excluding user {ExcludeUserId})", username, exists, excludeUserId);
                
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if username exists {Username}", username);
                throw;
            }
        }

        public async Task<User?> GetByEmailVerificationTokenAsync(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    throw new ArgumentException("Token cannot be null or empty", nameof(token));

                var user = await _dbSet
                    .Include(u => u.Picture)
                    .FirstOrDefaultAsync(u => !u.IsDeleted && u.EmailVerificationToken == token);

                _logger.LogDebug("Retrieved user by email verification token: {Found}", user != null ? "Found" : "Not found");
                
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by email verification token");
                throw;
            }
        }

        public async Task<int> CountSearchAsync(string search)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(search))
                    throw new ArgumentException("Search term cannot be null or empty", nameof(search));

                var count = await _dbSet
                    .Where(u => !u.IsDeleted && (
                        u.Email.Contains(search) || 
                        u.Username.Contains(search) ||
                        u.FirstName.Contains(search) || 
                        u.LastName.Contains(search)))
                    .CountAsync();

                _logger.LogDebug("Count of users matching search '{Search}': {Count}", search, count);
                
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting users matching search '{Search}'", search);
                throw;
            }
        }

        public new async Task<User?> GetByIdAsync(int userId)
        {
            try
            {
                var user = await _dbSet
                    .Include(u => u.Picture)
                    .Include(u => u.Addresses.Where(a => !a.IsDeleted))
                    .Include(u => u.ContactDetails.Where(c => !c.IsDeleted))
                    .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

                _logger.LogDebug("Retrieved user by ID {UserId}: {Found}", userId, user != null ? "Found" : "Not found");
                
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by ID {UserId}", userId);
                throw;
            }
        }

        public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                    throw new ArgumentException("Refresh token cannot be null or empty", nameof(refreshToken));

                var user = await _dbSet
                    .Include(u => u.Picture)
                    .Include(u => u.Sessions.Where(s => !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow))
                    .FirstOrDefaultAsync(u => !u.IsDeleted && 
                        u.Sessions.Any(s => s.RefreshToken == refreshToken && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow));

                _logger.LogDebug("Retrieved user by refresh token: {Found}", user != null ? "Found" : "Not found");
                
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by refresh token");
                throw;
            }
        }

        public async Task<IEnumerable<User>> GetActiveUsersAsync()
        {
            try
            {
                var users = await _dbSet
                    .Include(u => u.Picture)
                    .Where(u => !u.IsDeleted && u.IsActive)
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} active users", users.Count);
                
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active users");
                throw;
            }
        }

        public async Task<IEnumerable<User>> GetUsersByRoleAsync(string role)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(role))
                    throw new ArgumentException("Role cannot be null or empty", nameof(role));

                var users = await _dbSet
                    .Include(u => u.Picture)
                    .Where(u => !u.IsDeleted && u.Role.ToString() == role)
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} users with role {Role}", users.Count, role);
                
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users by role {Role}", role);
                throw;
            }
        }

        public async Task<int> GetActiveUserCountAsync()
        {
            try
            {
                var count = await _dbSet
                    .Where(u => !u.IsDeleted && u.IsActive)
                    .CountAsync();

                _logger.LogDebug("Active user count: {Count}", count);
                
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active user count");
                throw;
            }
        }

        public async Task<bool> UpdateLastLoginAsync(int userId, DateTime lastLogin, string? ipAddress = null)
        {
            try
            {
                var user = await _dbSet.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found for last login update", userId);
                    return false;
                }

                user.LastLoginAt = lastLogin;
                user.UpdatedAt = DateTime.UtcNow;

                Update(user);
                await SaveChangesAsync();

                _logger.LogDebug("Updated last login for user {UserId}", userId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last login for user {UserId}", userId);
                throw;
            }
        }

        private static void ValidatePagination(int page, int pageSize)
        {
            if (page < 1)
                throw new ArgumentException("Page number must be greater than 0", nameof(page));
            
            if (pageSize < 1)
                throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));
            
            if (pageSize > 1000)
                throw new ArgumentException("Page size cannot exceed 1000", nameof(pageSize));
        }
    }
}