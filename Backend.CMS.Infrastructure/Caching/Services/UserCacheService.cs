using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Caching;
using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.Caching.Services;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.CMS.Infrastructure.Services
{
    public interface IUserCacheService
    {
        Task<UserCacheDto?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<UserCacheDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<UserCacheDto?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task SetUserAsync(User user, CancellationToken cancellationToken = default);
        Task SetUserAsync(UserCacheDto userDto, CancellationToken cancellationToken = default);
        Task RemoveUserAsync(int userId, CancellationToken cancellationToken = default);
        Task RemoveUserByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task RemoveUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task InvalidateUserCacheAsync(int userId, CancellationToken cancellationToken = default);
        Task<User?> GetUserForAuthenticationAsync(string email, CancellationToken cancellationToken = default);
        Task<User?> GetUserByIdForAuthenticationAsync(int userId, CancellationToken cancellationToken = default);
    }

    public class UserCacheService : IUserCacheService
    {
        private readonly ICacheService _cacheService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly IMapper _mapper;
        private readonly ILogger<UserCacheService> _logger;
        private readonly TimeSpan _userCacheExpiration;
        private readonly IUnitOfWork _unitOfWork;
        public UserCacheService(
            ICacheService cacheService,
            ICacheKeyService cacheKeyService,
            ICacheInvalidationService cacheInvalidationService,
            IMapper mapper,
            ILogger<UserCacheService> logger,
            IOptions<CacheOptions> cacheOptions,
            IUnitOfWork unitOfWork  )
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _cacheKeyService = cacheKeyService ?? throw new ArgumentNullException(nameof(cacheKeyService));
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var options = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));
            _userCacheExpiration = options.DefaultExpiration;
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<UserCacheDto?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0) return null;

            try
            {
                var key = _cacheKeyService.GetEntityKey<User>(userId);
                return await _cacheService.GetAsync<UserCacheDto>(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId} from cache", userId);
                return null;
            }
        }

        public async Task<UserCacheDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            try
            {
                var normalizedEmail = email.Trim().ToLowerInvariant();
                var key = _cacheKeyService.GetCustomKey("user", "email", normalizedEmail);
                return await _cacheService.GetAsync<UserCacheDto>(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email {Email} from cache", email);
                return null;
            }
        }


        public async Task<UserCacheDto?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;

            try
            {
                var normalizedUsername = username.Trim();
                var key = _cacheKeyService.GetCustomKey("user", "username", normalizedUsername);
                return await _cacheService.GetAsync<UserCacheDto>(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by username {Username} from cache", username);
                return null;
            }
        }
        /// <summary>
        /// Get user for authentication purposes - always fetches from database to get password hash
        /// This method intentionally bypasses cache for password verification security
        /// </summary>
        public async Task<User?> GetUserForAuthenticationAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            try
            {
                var normalizedEmail = email.Trim().ToLowerInvariant();

                // Always fetch from database for authentication to ensure we have the password hash
                var user = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);

                if (user != null)
                {
                    // Cache the user data (without password hash) for future non-auth operations
                    await SetUserAsync(user, cancellationToken);
                    _logger.LogDebug("User {UserId} retrieved from database for authentication and cached", user.Id);
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email {Email} for authentication", email);
                return null;
            }
        }
        /// <summary>
        /// Get user by ID for authentication purposes - always fetches from database to get password hash
        /// This method intentionally bypasses cache for password verification security
        /// </summary>
        public async Task<User?> GetUserByIdForAuthenticationAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0) return null;

            try
            {
                // Always fetch from database for authentication to ensure we have the password hash
                var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

                if (user != null)
                {
                    // Cache the user data (without password hash) for future non-auth operations
                    await SetUserAsync(user, cancellationToken);
                    _logger.LogDebug("User {UserId} retrieved from database for authentication and cached", user.Id);
                }

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId} for authentication", userId);
                return null;
            }
        }

        public async Task SetUserAsync(User user, CancellationToken cancellationToken = default)
        {
            if (user == null) return;

            try
            {
                var userDto = _mapper.Map<UserCacheDto>(user);
                await SetUserAsync(userDto, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting user {UserId} in cache", user.Id);
            }
        }

        public async Task SetUserAsync(UserCacheDto userDto, CancellationToken cancellationToken = default)
        {
            if (userDto == null) return;

            try
            {
                var tasks = new List<Task>
                {
                    // Cache by ID
                    _cacheService.SetAsync(
                        _cacheKeyService.GetEntityKey<User>(userDto.Id),
                        userDto,
                        _userCacheExpiration,
                        cancellationToken),
                    
                    // Cache by email
                    _cacheService.SetAsync(
                        _cacheKeyService.GetCustomKey("user", "email", userDto.Email.ToLowerInvariant()),
                        userDto,
                        _userCacheExpiration,
                        cancellationToken),
                    
                    // Cache by username
                    _cacheService.SetAsync(
                        _cacheKeyService.GetCustomKey("user", "username", userDto.Username),
                        userDto,
                        _userCacheExpiration,
                        cancellationToken)
                };

                await Task.WhenAll(tasks);
                _logger.LogDebug("User {UserId} cached successfully", userDto.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting user {UserId} in cache", userDto.Id);
            }
        }

        public async Task RemoveUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0) return;

            try
            {
                // Get user from cache first to remove all related keys
                var userDto = await GetUserByIdAsync(userId, cancellationToken);
                if (userDto != null)
                {
                    var tasks = new List<Task>
                    {
                        _cacheService.RemoveAsync(_cacheKeyService.GetEntityKey<User>(userId), cancellationToken),
                        _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("user", "email", userDto.Email.ToLowerInvariant()), cancellationToken),
                        _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("user", "username", userDto.Username), cancellationToken)
                    };

                    await Task.WhenAll(tasks);
                }
                else
                {
                    // Just remove by ID if we can't get the user
                    await _cacheService.RemoveAsync(_cacheKeyService.GetEntityKey<User>(userId), cancellationToken);
                }

                _logger.LogDebug("User {UserId} removed from cache", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {UserId} from cache", userId);
            }
        }

        public async Task RemoveUserByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            try
            {
                var normalizedEmail = email.Trim().ToLowerInvariant();
                var key = _cacheKeyService.GetCustomKey("user", "email", normalizedEmail);
                await _cacheService.RemoveAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user by email {Email} from cache", email);
            }
        }


        public async Task RemoveUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            try
            {
                var normalizedUsername = username.Trim();
                var key = _cacheKeyService.GetCustomKey("user", "username", normalizedUsername);
                await _cacheService.RemoveAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user by username {Username} from cache", username);
            }
        }

        public async Task InvalidateUserCacheAsync(int userId, CancellationToken cancellationToken = default)
        {
            try
            {
                await RemoveUserAsync(userId, cancellationToken);
                await _cacheInvalidationService.InvalidateRelatedAsync<User>(userId, cancellationToken);
                _logger.LogDebug("User {UserId} cache invalidated", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating user {UserId} cache", userId);
            }
        }
    }
}
