using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User?> GetWithAddressesAndContactsAsync(int userId)
        {
            return await _dbSet
                .Include(u => u.Addresses.Where(a => !a.IsDeleted))
                .Include(u => u.ContactDetails.Where(c => !c.IsDeleted))
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<User?> GetWithRolesAsync(int userId)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<User?> GetWithRolesAndPermissionsAsync(int userId)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<IEnumerable<User>> SearchUsersAsync(string searchTerm, int page, int pageSize)
        {
            return await _dbSet
                .Include(u => u.Addresses.Where(a => !a.IsDeleted))
                .Include(u => u.ContactDetails.Where(c => !c.IsDeleted))
                .Where(u => u.FirstName.Contains(searchTerm) ||
                           u.LastName.Contains(searchTerm) ||
                           u.Email.Contains(searchTerm) ||
                           u.Username.Contains(searchTerm))
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        // New method to get paginated users with related entities
        public async Task<IEnumerable<User>> GetPagedWithRelatedAsync(int page, int pageSize)
        {
            return await _dbSet
                .Include(u => u.Addresses.Where(a => !a.IsDeleted))
                .Include(u => u.ContactDetails.Where(c => !c.IsDeleted))
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<bool> EmailExistsAsync(string email, int? excludeUserId = null)
        {
            var query = _dbSet.Where(u => u.Email == email);

            if (excludeUserId.HasValue)
                query = query.Where(u => u.Id != excludeUserId.Value);

            return await query.AnyAsync();
        }

        public async Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null)
        {
            var query = _dbSet.Where(u => u.Username == username);

            if (excludeUserId.HasValue)
                query = query.Where(u => u.Id != excludeUserId.Value);

            return await query.AnyAsync();
        }

        public async Task<User?> GetByEmailVerificationTokenAsync(string token)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
        }

        public async Task<int> CountSearchAsync(string search)
        {
            return await _dbSet
                .Where(u => u.Email.Contains(search) || u.Username.Contains(search) ||
                           u.FirstName.Contains(search) || u.LastName.Contains(search))
                .CountAsync();
        }
    }
}