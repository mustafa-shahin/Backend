using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class CachedUserRepository : IUserRepository
    {
        private readonly IUserRepository _repository;
        private readonly ICachedRepositoryService _cache;
        private readonly ILogger<CachedUserRepository> _logger;
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(15);

        public CachedUserRepository(
            IUserRepository repository,
            ICachedRepositoryService cache,
            ILogger<CachedUserRepository> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var key = CacheKeyBuilder.BuildEntityKey<User>(id);
            var user = await _cache.GetOrSetAsync(key, 
                async () => await _repository.GetByIdAsync(id, cancellationToken), 
                _defaultExpiration);

            if (user != null)
            {
                _cache.SetTags(key, "User", $"User:{id}");
            }

            return user;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            var key = CacheKeyBuilder.BuildEntityKey<User>($"Email:{email}");
            var user = await _cache.GetOrSetAsync(key, 
                async () => await _repository.GetByEmailAsync(email), 
                _defaultExpiration);

            if (user != null)
            {
                _cache.SetTags(key, "User", $"User:{user.Id}", "UserEmail");
            }

            return user;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            var key = CacheKeyBuilder.BuildEntityKey<User>($"Username:{username}");
            var user = await _cache.GetOrSetAsync(key, 
                async () => await _repository.GetByUsernameAsync(username), 
                _defaultExpiration);

            if (user != null)
            {
                _cache.SetTags(key, "User", $"User:{user.Id}", "UserUsername");
            }

            return user;
        }

        public async Task<IEnumerable<User>> GetActiveUsersAsync()
        {
            var key = CacheKeyBuilder.BuildListKey<User>("ActiveUsers");
            var users = await _cache.GetOrSetListAsync(key, 
                async () => await _repository.GetActiveUsersAsync(), 
                TimeSpan.FromMinutes(10));

            _cache.SetTags(key, "User", "ActiveUsers");
            return users;
        }

        public async Task<IEnumerable<User>> SearchUsersAsync(string searchTerm, int page, int pageSize)
        {
            var key = CacheKeyBuilder.BuildSearchKey<User>(searchTerm, page, pageSize);
            var users = await _cache.GetOrSetListAsync(key, 
                async () => await _repository.SearchUsersAsync(searchTerm, page, pageSize), 
                TimeSpan.FromMinutes(5));

            _cache.SetTags(key, "User", "UserSearch");
            return users;
        }

        public async Task<int> CountSearchAsync(string search)
        {
            var key = CacheKeyBuilder.BuildCountKey<User>($"Search:{search}");
            var count = await _cache.GetOrSetIntAsync(key, 
                async () => await _repository.CountSearchAsync(search), 
                TimeSpan.FromMinutes(5));

            _cache.SetTags(key, "User", "UserCount");
            return count ?? 0;
        }

        public async Task<int> GetActiveUserCountAsync()
        {
            var key = CacheKeyBuilder.BuildCountKey<User>("Active");
            var count = await _cache.GetOrSetIntAsync(key, 
                async () => await _repository.GetActiveUserCountAsync(), 
                TimeSpan.FromMinutes(10));

            _cache.SetTags(key, "User", "UserCount");
            return count ?? 0;
        }

        public async Task<bool> EmailExistsAsync(string email, int? excludeUserId = null)
        {
            // Don't cache existence checks for data integrity
            return await _repository.EmailExistsAsync(email, excludeUserId);
        }

        public async Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null)
        {
            // Don't cache existence checks for data integrity
            return await _repository.UsernameExistsAsync(username, excludeUserId);
        }

        // Write operations - invalidate cache
        public async Task AddAsync(User entity, CancellationToken cancellationToken = default)
        {
            await _repository.AddAsync(entity, cancellationToken);
            InvalidateUserCache(entity.Id);
        }

        public async Task AddRangeAsync(IEnumerable<User> entities, CancellationToken cancellationToken = default)
        {
            await _repository.AddRangeAsync(entities, cancellationToken);
            InvalidateAllUserCaches();
        }

        public void Update(User entity)
        {
            _repository.Update(entity);
            InvalidateUserCache(entity.Id);
        }

        public void UpdateRange(IEnumerable<User> entities)
        {
            _repository.UpdateRange(entities);
            InvalidateAllUserCaches();
        }

        public async Task<bool> SoftDeleteAsync(int id, int? deletedByUserId = null, CancellationToken cancellationToken = default)
        {
            var result = await _repository.SoftDeleteAsync(id, deletedByUserId, cancellationToken);
            if (result)
            {
                InvalidateUserCache(id);
            }
            return result;
        }

        public async Task<bool> SoftDeleteAsync(User entity, int? deletedByUserId = null, CancellationToken cancellationToken = default)
        {
            var result = await _repository.SoftDeleteAsync(entity, deletedByUserId, cancellationToken);
            if (result)
            {
                InvalidateUserCache(entity.Id);
            }
            return result;
        }

        public async Task<bool> UpdateLastLoginAsync(int userId, DateTime lastLogin, string? ipAddress = null)
        {
            var result = await _repository.UpdateLastLoginAsync(userId, lastLogin, ipAddress);
            if (result)
            {
                InvalidateUserCache(userId);
            }
            return result;
        }

        private void InvalidateUserCache(int userId)
        {
            _cache.RemoveByTag($"User:{userId}");
            _cache.RemoveByTag("UserSearch");
            _cache.RemoveByTag("UserCount");
            _cache.RemoveByTag("ActiveUsers");
            _logger.LogDebug("Invalidated cache for user {UserId}", userId);
        }

        private void InvalidateAllUserCaches()
        {
            _cache.RemoveByTag("User");
            _logger.LogDebug("Invalidated all user caches");
        }

        // Delegate all other methods to the underlying repository
        public async Task<User?> GetByIdIncludeDeletedAsync(int id, CancellationToken cancellationToken = default)
            => await _repository.GetByIdIncludeDeletedAsync(id, cancellationToken);

        public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _repository.GetAllAsync(cancellationToken);

        public async Task<IEnumerable<User>> GetAllIncludeDeletedAsync(CancellationToken cancellationToken = default)
            => await _repository.GetAllIncludeDeletedAsync(cancellationToken);

        public async Task<IEnumerable<User>> FindAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
            => await _repository.FindAsync(predicate, cancellationToken);

        public async Task<IEnumerable<User>> FindIncludeDeletedAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
            => await _repository.FindIncludeDeletedAsync(predicate, cancellationToken);

        public async Task<User?> FirstOrDefaultAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
            => await _repository.FirstOrDefaultAsync(predicate, cancellationToken);

        public async Task<User?> FirstOrDefaultIncludeDeletedAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
            => await _repository.FirstOrDefaultIncludeDeletedAsync(predicate, cancellationToken);

        public async Task<bool> AnyAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
            => await _repository.AnyAsync(predicate, cancellationToken);

        public async Task<bool> AnyIncludeDeletedAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
            => await _repository.AnyIncludeDeletedAsync(predicate, cancellationToken);

        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
            => await _repository.CountAsync(cancellationToken);

        public async Task<int> CountAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
            => await _repository.CountAsync(predicate, cancellationToken);

        public async Task<int> CountIncludeDeletedAsync(CancellationToken cancellationToken = default)
            => await _repository.CountIncludeDeletedAsync(cancellationToken);

        public async Task<int> CountIncludeDeletedAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
            => await _repository.CountIncludeDeletedAsync(predicate, cancellationToken);

        public async Task<IEnumerable<User>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => await _repository.GetPagedAsync(page, pageSize, cancellationToken);

        public async Task<IEnumerable<User>> GetPagedIncludeDeletedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => await _repository.GetPagedIncludeDeletedAsync(page, pageSize, cancellationToken);

        public void Remove(User entity)
        {
            _repository.Remove(entity);
            InvalidateUserCache(entity.Id);
        }

        public void RemoveRange(IEnumerable<User> entities)
        {
            _repository.RemoveRange(entities);
            InvalidateAllUserCaches();
        }

        public async Task<bool> SoftDeleteRangeAsync(IEnumerable<User> entities, int? deletedByUserId = null, CancellationToken cancellationToken = default)
        {
            var result = await _repository.SoftDeleteRangeAsync(entities, deletedByUserId, cancellationToken);
            if (result)
            {
                InvalidateAllUserCaches();
            }
            return result;
        }

        public async Task<bool> RestoreAsync(int id, int? restoredByUserId = null, CancellationToken cancellationToken = default)
        {
            var result = await _repository.RestoreAsync(id, restoredByUserId, cancellationToken);
            if (result)
            {
                InvalidateUserCache(id);
            }
            return result;
        }

        public async Task<bool> RestoreAsync(User entity, int? restoredByUserId = null, CancellationToken cancellationToken = default)
        {
            var result = await _repository.RestoreAsync(entity, restoredByUserId, cancellationToken);
            if (result)
            {
                InvalidateUserCache(entity.Id);
            }
            return result;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => await _repository.SaveChangesAsync(cancellationToken);

        public IQueryable<User> GetQueryable()
            => _repository.GetQueryable();

        public async Task<Backend.CMS.Application.DTOs.PaginatedResult<User>> GetPagedResultAsync(int page, int pageSize, Expression<Func<User, bool>>? predicate = null, Func<IQueryable<User>, IOrderedQueryable<User>>? orderBy = null, CancellationToken cancellationToken = default)
            => await _repository.GetPagedResultAsync(page, pageSize, predicate, orderBy, cancellationToken);

        public async Task<IEnumerable<User>> FindWithIncludesAsync(Expression<Func<User, bool>>? predicate = null, params Expression<Func<User, object>>[] includes)
            => await _repository.FindWithIncludesAsync(predicate, includes);

        public async Task<IEnumerable<User>> BulkInsertAsync(IEnumerable<User> entities, CancellationToken cancellationToken = default)
        {
            var result = await _repository.BulkInsertAsync(entities, cancellationToken);
            InvalidateAllUserCaches();
            return result;
        }

        public async Task<int> BulkUpdateAsync(Expression<Func<User, bool>> predicate, Expression<Func<User, User>> updateExpression, CancellationToken cancellationToken = default)
        {
            var result = await _repository.BulkUpdateAsync(predicate, updateExpression, cancellationToken);
            InvalidateAllUserCaches();
            return result;
        }

        public async Task<int> BulkDeleteAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var result = await _repository.BulkDeleteAsync(predicate, cancellationToken);
            InvalidateAllUserCaches();
            return result;
        }

        public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
            => await _repository.ExistsAsync(id, cancellationToken);

        public async Task<bool> ExistsAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
            => await _repository.ExistsAsync(predicate, cancellationToken);

        public void Attach(User entity)
            => _repository.Attach(entity);

        public void Detach(User entity)
            => _repository.Detach(entity);

        public void SetEntityState(User entity, Microsoft.EntityFrameworkCore.EntityState state)
            => _repository.SetEntityState(entity, state);

        public async Task<User?> GetWithAddressesAndContactsAsync(int userId)
            => await _repository.GetWithAddressesAndContactsAsync(userId);

        public async Task<User?> GetWithRolesAsync(int userId)
            => await _repository.GetWithRolesAsync(userId);

        public async Task<User?> GetWithRolesAndPermissionsAsync(int userId)
            => await _repository.GetWithRolesAndPermissionsAsync(userId);

        public async Task<IEnumerable<User>> GetPagedWithRelatedAsync(int page, int pageSize)
            => await _repository.GetPagedWithRelatedAsync(page, pageSize);

        public async Task<User?> GetByEmailVerificationTokenAsync(string token)
            => await _repository.GetByEmailVerificationTokenAsync(token);

        public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
            => await _repository.GetByRefreshTokenAsync(refreshToken);

        public async Task<IEnumerable<User>> GetUsersByRoleAsync(string role)
            => await _repository.GetUsersByRoleAsync(role);
    }
}