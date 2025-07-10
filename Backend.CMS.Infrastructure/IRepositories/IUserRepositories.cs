using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetWithAddressesAndContactsAsync(int userId);
        Task<User?> GetWithRolesAsync(int userId);
        Task<User?> GetWithRolesAndPermissionsAsync(int userId);
        Task<IEnumerable<User>> SearchUsersAsync(string searchTerm, int page, int pageSize);
        Task<IEnumerable<User>> GetPagedWithRelatedAsync(int page, int pageSize);
        Task<bool> EmailExistsAsync(string email, int? excludeUserId = null);
        Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null);
        Task<User?> GetByEmailVerificationTokenAsync(string token);
        Task<int> CountSearchAsync(string search);
        Task<User?> GetByRefreshTokenAsync(string refreshToken);
        Task<IEnumerable<User>> GetActiveUsersAsync();
        Task<IEnumerable<User>> GetUsersByRoleAsync(string role);
        Task<int> GetActiveUserCountAsync();
        Task<bool> UpdateLastLoginAsync(int userId, DateTime lastLogin, string? ipAddress = null);
    }
}

