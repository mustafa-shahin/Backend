using Backend.CMS.Application.Common;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Caching;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Backend.CMS.Infrastructure.Services
{
    public class UserSessionService : IUserSessionService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserSessionService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICacheService _cacheService;
        private readonly TimeSpan _sessionTimeout;
        private readonly TimeSpan _cacheTimeout;

        // Memory cache for performance (with fallback to distributed cache)
        private UserSessionContext? _memoryCachedSession;
        private DateTime _lastCacheCheck = DateTime.MinValue;

        public UserSessionService(
            IHttpContextAccessor httpContextAccessor,
            IUserRepository userRepository,
            IConfiguration configuration,
            ILogger<UserSessionService> logger,
            IServiceProvider serviceProvider,
            ICacheService cacheService)
        {
            _httpContextAccessor = httpContextAccessor;
            _userRepository = userRepository;
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _cacheService = cacheService;

            var timeoutMinutes = 30;
            var timeoutConfig = _configuration["SessionSettings:TimeoutMinutes"];
            if (!string.IsNullOrEmpty(timeoutConfig) && int.TryParse(timeoutConfig, out var parsedTimeout))
            {
                timeoutMinutes = parsedTimeout;
            }

            _sessionTimeout = TimeSpan.FromMinutes(timeoutMinutes);
            _cacheTimeout = TimeSpan.FromMinutes(5); // Cache user data for 5 minutes
        }

        public UserSessionContext? GetCurrentSession()
        {
            try
            {
                // Check if we have a valid memory cached session
                if (_memoryCachedSession != null &&
                    !_memoryCachedSession.IsSessionExpired(_sessionTimeout) &&
                    DateTime.UtcNow - _lastCacheCheck < _cacheTimeout)
                {
                    _memoryCachedSession.UpdateLastActivity();
                    return _memoryCachedSession;
                }

                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext?.User?.Identity?.IsAuthenticated != true)
                {
                    // Create anonymous session
                    return CreateAnonymousSession();
                }

                // Try to get session from distributed cache first
                var sessionId = GetSessionId();
                var cachedSession = GetSessionFromCacheSync(sessionId);
                if (cachedSession != null && !cachedSession.IsSessionExpired(_sessionTimeout))
                {
                    _memoryCachedSession = cachedSession;
                    _lastCacheCheck = DateTime.UtcNow;
                    cachedSession.UpdateLastActivity();
                    return cachedSession;
                }

                // Load user synchronously (this is the key fix)
                var userIdClaim = httpContext.User.FindFirst("sub") ??
                                 httpContext.User.FindFirst("userId") ??
                                 httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    // Load user synchronously using a separate method
                    var user = GetUserSynchronously(userId);
                    if (user != null && user.IsActive && !user.IsLocked)
                    {
                        var session = CreateSessionFromUser(user);

                        // Cache the session
                        _memoryCachedSession = session;
                        _lastCacheCheck = DateTime.UtcNow;
                        _ = Task.Run(() => CacheSessionAsync(session)); // Fire and forget

                        return session;
                    }
                }

                _logger.LogWarning("Failed to create session - user not found or invalid");
                return CreateAnonymousSession();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current session");
                return CreateAnonymousSession();
            }
        }

        public async Task<UserSessionContext?> GetCurrentSessionAsync()
        {
            try
            {
                // Check memory cache first
                if (_memoryCachedSession != null &&
                    !_memoryCachedSession.IsSessionExpired(_sessionTimeout) &&
                    DateTime.UtcNow - _lastCacheCheck < _cacheTimeout)
                {
                    _memoryCachedSession.UpdateLastActivity();
                    return _memoryCachedSession;
                }

                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext?.User?.Identity?.IsAuthenticated != true)
                {
                    return CreateAnonymousSession();
                }

                // Try to get from distributed cache
                var sessionId = GetSessionId();
                var cachedSession = await GetSessionFromCacheAsync(sessionId);
                if (cachedSession != null && !cachedSession.IsSessionExpired(_sessionTimeout))
                {
                    _memoryCachedSession = cachedSession;
                    _lastCacheCheck = DateTime.UtcNow;
                    cachedSession.UpdateLastActivity();
                    return cachedSession;
                }

                // Load user from database
                var userIdClaim = httpContext.User.FindFirst("sub") ??
                                 httpContext.User.FindFirst("userId") ??
                                 httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    var user = await _userRepository.GetWithAddressesAndContactsAsync(userId);
                    if (user != null && user.IsActive && !user.IsLocked)
                    {
                        var session = await CreateSessionFromUserAsync(user);

                        // Cache the session
                        _memoryCachedSession = session;
                        _lastCacheCheck = DateTime.UtcNow;
                        await CacheSessionAsync(session);

                        return session;
                    }
                    else
                    {
                        _logger.LogWarning("User {UserId} not found, deactivated, or locked - clearing session", userId);
                        await ClearSessionAsync();
                        return null;
                    }
                }

                _logger.LogWarning("No valid user ID found in claims");
                return CreateAnonymousSession();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current session async");
                return CreateAnonymousSession();
            }
        }

        public async Task SetCurrentSessionAsync(User user, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                var fullUser = await _userRepository.GetWithAddressesAndContactsAsync(user.Id);
                if (fullUser == null)
                {
                    throw new ArgumentException("User not found");
                }

                var session = await CreateSessionFromUserAsync(fullUser, ipAddress, userAgent);

                // Cache both in memory and distributed cache
                _memoryCachedSession = session;
                _lastCacheCheck = DateTime.UtcNow;
                await CacheSessionAsync(session);

                _logger.LogInformation("User session created for user {UserId} ({Email})", fullUser.Id, fullUser.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting current session for user {UserId}", user.Id);
                throw;
            }
        }

        public async Task UpdateSessionAsync()
        {
            try
            {
                var session = await GetCurrentSessionAsync();
                if (session?.CurrentUser != null)
                {
                    var user = await _userRepository.GetWithAddressesAndContactsAsync(session.CurrentUser.Id);
                    if (user != null)
                    {
                        session.CurrentUser = user;
                        session.UpdateLastActivity();
                        await CacheSessionAsync(session);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session");
            }
        }

        public async Task ClearSessionAsync()
        {
            try
            {
                var sessionId = GetSessionId();

                // Clear memory cache
                _memoryCachedSession = null;
                _lastCacheCheck = DateTime.MinValue;

                // Clear distributed cache
                if (!string.IsNullOrEmpty(sessionId))
                {
                    await _cacheService.RemoveAsync(CacheKeys.SessionById(sessionId));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing session");
            }
        }

        #region Quick Access Properties

        public int? GetCurrentUserId()
        {
            return GetCurrentSession()?.CurrentUser?.Id;
        }

        public string? GetCurrentUserEmail()
        {
            return GetCurrentSession()?.CurrentUser?.Email;
        }

        public string? GetCurrentUserFullName()
        {
            return GetCurrentSession()?.CurrentUser?.FullName;
        }

        public UserRole? GetCurrentUserRole()
        {
            return GetCurrentSession()?.CurrentUser?.Role;
        }

        public bool IsAuthenticated()
        {
            return GetCurrentSession()?.CurrentUser != null;
        }

        public bool IsInRole(params UserRole[] roles)
        {
            var session = GetCurrentSession();
            return session?.CurrentUser != null && roles.Contains(session.CurrentUser.Role);
        }

        #endregion

        #region Permission Checks

        public bool HasPermission(string permission)
        {
            var session = GetCurrentSession();
            return session?.HasPermission(permission) ?? false;
        }

        public bool CanManageUsers()
        {
            return IsInRole(UserRole.Admin, UserRole.Dev);
        }

        public bool CanCreateUserWithRole(UserRole targetRole)
        {
            var currentRole = GetCurrentUserRole();
            if (currentRole == null) return false;

            return currentRole switch
            {
                UserRole.Dev => true,
                UserRole.Admin => targetRole != UserRole.Dev,
                _ => false
            };
        }

        public bool CanTriggerDeploymentJobs()
        {
            return IsInRole(UserRole.Dev);
        }

        public bool CanTriggerTemplateSyncJobs()
        {
            return IsInRole(UserRole.Dev);
        }

        public T GetUserPreference<T>(string key, T defaultValue = default!)
        {
            var session = GetCurrentSession();

            if (session == null)
            {
                return defaultValue;
            }

            var preference = session.GetPreference(key, defaultValue);

            if (preference is null && defaultValue is not null)
            {
                return defaultValue;
            }

            return preference ?? defaultValue;
        }

        public async Task SetUserPreferenceAsync<T>(string key, T value)
        {
            var session = await GetCurrentSessionAsync();
            if (session?.CurrentUser != null)
            {
                session.SetPreference(key, value);

                session.CurrentUser.Preferences = session.Preferences;
                session.CurrentUser.UpdatedAt = DateTime.UtcNow;
                session.CurrentUser.UpdatedByUserId = session.CurrentUser.Id;

                _userRepository.Update(session.CurrentUser);
                await _userRepository.SaveChangesAsync();

                // Update cache
                await CacheSessionAsync(session);
            }
        }

        #endregion

        #region Audit Information

        public string GetAuditUserName()
        {
            return GetCurrentSession()?.CurrentUser?.FullName ?? "System";
        }

        public string GetAuditUserId()
        {
            return GetCurrentUserId()?.ToString() ?? "Unknown";
        }

        #endregion

        #region Session Metadata

        public bool IsSessionValid()
        {
            var session = GetCurrentSession();
            return session != null && !session.IsSessionExpired(_sessionTimeout);
        }

        public void UpdateLastActivity()
        {
            GetCurrentSession()?.UpdateLastActivity();
        }

        public TimeSpan GetSessionDuration()
        {
            var session = GetCurrentSession();
            return session != null ? DateTime.UtcNow - session.SessionStartTime : TimeSpan.Zero;
        }

        #endregion

        #region Private Helper Methods

        private UserSessionContext CreateAnonymousSession()
        {
            return new UserSessionContext
            {
                CurrentUser = null,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                SessionId = GetSessionId(),
                RequestId = _httpContextAccessor.HttpContext?.TraceIdentifier,
                CorrelationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString(),
                SessionStartTime = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                Permissions = new List<string>(),
                Claims = new Dictionary<string, object>()
            };
        }

        private User? GetUserSynchronously(int userId)
        {
            try
            {
                // Use a separate DbContext to avoid threading issues
                using var scope = _serviceProvider.CreateScope();
                var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                // This will be a synchronous database call
                return userRepo.GetWithAddressesAndContactsAsync(userId).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user {UserId} synchronously", userId);
                return null;
            }
        }

        private UserSessionContext CreateSessionFromUser(User user, string? ipAddress = null, string? userAgent = null)
        {
            var session = UserSessionContext.CreateFromUser(
                user,
                ipAddress ?? GetClientIpAddress(),
                userAgent ?? GetUserAgent());

            session.RequestId = _httpContextAccessor.HttpContext?.TraceIdentifier;
            session.CorrelationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
            session.SessionId = GetSessionId();

            // Load basic permissions synchronously
            session.Permissions = GetBasicPermissionsForRole(user.Role);

            return session;
        }

        private async Task<UserSessionContext> CreateSessionFromUserAsync(User user, string? ipAddress = null, string? userAgent = null)
        {
            var session = UserSessionContext.CreateFromUser(
                user,
                ipAddress ?? GetClientIpAddress(),
                userAgent ?? GetUserAgent());

            session.RequestId = _httpContextAccessor.HttpContext?.TraceIdentifier;
            session.CorrelationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
            session.SessionId = GetSessionId();

            // Load permissions asynchronously
            try
            {
                var permissionResolver = _serviceProvider.GetService<IPermissionResolver>();
                if (permissionResolver != null)
                {
                    var rolePermissions = await permissionResolver.GetRolePermissionsAsync(user.Role);
                    var userPermissions = await permissionResolver.GetUserPermissionsAsync(user.Id);
                    session.Permissions = rolePermissions.Union(userPermissions).Distinct().ToList();
                }
                else
                {
                    session.Permissions = GetBasicPermissionsForRole(user.Role);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load permissions for user {UserId}, using basic permissions", user.Id);
                session.Permissions = GetBasicPermissionsForRole(user.Role);
            }

            return session;
        }

        private async Task CacheSessionAsync(UserSessionContext session)
        {
            try
            {
                if (!string.IsNullOrEmpty(session.SessionId))
                {
                    var cacheKey = CacheKeys.SessionById(session.SessionId);
                    await _cacheService.SetAsync(cacheKey, session, _sessionTimeout);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching session {SessionId}", session.SessionId);
            }
        }

        private UserSessionContext? GetSessionFromCacheSync(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId)) return null;

                var cacheKey = CacheKeys.SessionById(sessionId);
                return _cacheService.GetAsync<UserSessionContext>(cacheKey).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session from cache synchronously");
                return null;
            }
        }

        private async Task<UserSessionContext?> GetSessionFromCacheAsync(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId)) return null;

                var cacheKey = CacheKeys.SessionById(sessionId);
                return await _cacheService.GetAsync<UserSessionContext>(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session from cache");
                return null;
            }
        }

        private string GetSessionId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Session != null)
            {
                return httpContext.Session.Id;
            }

            // Fallback to JWT jti claim
            var jtiClaim = httpContext?.User?.FindFirst("jti");
            if (jtiClaim != null)
            {
                return jtiClaim.Value;
            }

            // Generate new session ID
            return Guid.NewGuid().ToString();
        }

        private string GetClientIpAddress()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return "Unknown";

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                ipAddress = forwardedFor.Split(',').FirstOrDefault()?.Trim();
            }

            return ipAddress ?? "Unknown";
        }

        private string GetUserAgent()
        {
            return _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        }

        private List<string> GetBasicPermissionsForRole(UserRole role)
        {
            return role switch
            {
                UserRole.Dev => new List<string> { "system.admin", "permissions.manage", "jobs.manage", "users.manage", "pages.manage", "components.manage", "stores.manage", "company.manage" },
                UserRole.Admin => new List<string> { "users.manage", "pages.manage", "components.manage", "stores.manage", "company.manage" },
                UserRole.Customer => new List<string> { "pages.view", "profile.manage" },
                _ => new List<string>()
            };
        }

        #endregion
    }
}