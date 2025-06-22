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
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Backend.CMS.Infrastructure.Services
{
    public class UserSessionService : IUserSessionService, IDisposable
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserSessionService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICacheService _cacheService;
        private readonly TimeSpan _sessionTimeout;
        private readonly TimeSpan _memoryCacheTimeout;
        private readonly int _maxConcurrentSessions;

        // Thread-safe memory cache with expiration
        private readonly ConcurrentDictionary<string, (UserSessionContext Session, DateTime ExpiresAt)> _memorySessionCache;
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        public UserSessionService(
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<UserSessionService> logger,
            IServiceProvider serviceProvider,
            ICacheService cacheService)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));

            var timeoutMinutes = _configuration.GetValue("SessionSettings:TimeoutMinutes", 30);
            _sessionTimeout = TimeSpan.FromMinutes(timeoutMinutes);
            _memoryCacheTimeout = TimeSpan.FromMinutes(5);
            _maxConcurrentSessions = _configuration.GetValue("SessionSettings:MaxConcurrentSessions", 1000);

            _memorySessionCache = new ConcurrentDictionary<string, (UserSessionContext, DateTime)>();

            // Cleanup expired sessions every 5 minutes
            _cleanupTimer = new Timer(CleanupExpiredMemorySessions, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public UserSessionContext? GetCurrentSession()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext?.User?.Identity?.IsAuthenticated != true)
                {
                    return CreateAnonymousSession();
                }

                var sessionId = GetSessionId();
                if (string.IsNullOrEmpty(sessionId))
                {
                    return CreateAnonymousSession();
                }

                // Check memory cache first
                if (_memorySessionCache.TryGetValue(sessionId, out var cachedEntry))
                {
                    if (DateTime.UtcNow <= cachedEntry.ExpiresAt && !cachedEntry.Session.IsSessionExpired(_sessionTimeout))
                    {
                        cachedEntry.Session.UpdateLastActivity();
                        return cachedEntry.Session;
                    }
                    else
                    {
                        // Remove expired entry
                        _memorySessionCache.TryRemove(sessionId, out _);
                    }
                }

                // For synchronous call, return basic session with cached user data
                var userIdClaim = httpContext.User.FindFirst("sub") ??
                                 httpContext.User.FindFirst("userId") ??
                                 httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    var session = CreateBasicSessionFromClaims(httpContext.User, sessionId);

                    // Cache the session
                    CacheSessionInMemory(sessionId, session);

                    return session;
                }

                _logger.LogWarning("Failed to create session - no valid user ID in claims");
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
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext?.User?.Identity?.IsAuthenticated != true)
                {
                    return CreateAnonymousSession();
                }

                var sessionId = GetSessionId();
                if (string.IsNullOrEmpty(sessionId))
                {
                    return CreateAnonymousSession();
                }

                // Check memory cache first
                if (_memorySessionCache.TryGetValue(sessionId, out var cachedEntry))
                {
                    if (DateTime.UtcNow <= cachedEntry.ExpiresAt && !cachedEntry.Session.IsSessionExpired(_sessionTimeout))
                    {
                        cachedEntry.Session.UpdateLastActivity();
                        return cachedEntry.Session;
                    }
                    else
                    {
                        _memorySessionCache.TryRemove(sessionId, out _);
                    }
                }

                // Try to get from distributed cache
                var cachedSession = await GetSessionFromDistributedCacheAsync(sessionId);
                if (cachedSession != null && !cachedSession.IsSessionExpired(_sessionTimeout))
                {
                    CacheSessionInMemory(sessionId, cachedSession);
                    cachedSession.UpdateLastActivity();
                    return cachedSession;
                }

                // Load from database using proper scoping
                var userIdClaim = httpContext.User.FindFirst("sub") ??
                                 httpContext.User.FindFirst("userId") ??
                                 httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    var session = await LoadFullSessionAsync(userId, sessionId);
                    if (session != null)
                    {
                        CacheSessionInMemory(sessionId, session);
                        await CacheSessionInDistributedCacheAsync(sessionId, session);
                        return session;
                    }
                }

                _logger.LogWarning("Failed to create session - user not found or invalid");
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
                if (user == null) throw new ArgumentNullException(nameof(user));

                var sessionId = GetSessionId();
                if (string.IsNullOrEmpty(sessionId))
                {
                    throw new InvalidOperationException("Cannot set session without valid session ID");
                }

                var session = await CreateSessionFromUserAsync(user, ipAddress, userAgent);
                session.SessionId = sessionId;

                CacheSessionInMemory(sessionId, session);
                await CacheSessionInDistributedCacheAsync(sessionId, session);

                _logger.LogInformation("User session set for user {UserId} ({Email})", user.Id, user.Email);
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
                var sessionId = GetSessionId();
                if (string.IsNullOrEmpty(sessionId)) return;

                var session = await GetCurrentSessionAsync();
                if (session?.UserId != null)
                {
                    // Use scoped service to get user data
                    using var scope = _serviceProvider.CreateScope();
                    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                    var user = await userRepository.GetByIdAsync(session.UserId.Value);

                    if (user != null)
                    {
                        await RefreshSessionWithUser(session, user);
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
                if (string.IsNullOrEmpty(sessionId)) return;

                // Remove from memory cache
                _memorySessionCache.TryRemove(sessionId, out _);

                // Remove from distributed cache
                await _cacheService.RemoveAsync(CacheKeys.SessionById(sessionId));

                _logger.LogDebug("Session {SessionId} cleared", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing session");
            }
        }

        #region Quick Access Properties

        public int? GetCurrentUserId()
        {
            return GetCurrentSession()?.UserId;
        }

        public string? GetCurrentUserEmail()
        {
            return GetCurrentSession()?.Email;
        }

        public string? GetCurrentUserFullName()
        {
            return GetCurrentSession()?.FullName;
        }

        public UserRole? GetCurrentUserRole()
        {
            return GetCurrentSession()?.Role;
        }

        public bool IsAuthenticated()
        {
            return GetCurrentSession()?.IsAuthenticated ?? false;
        }

        public bool IsInRole(params UserRole[] roles)
        {
            var session = GetCurrentSession();
            return session?.Role != null && roles.Contains(session.Role.Value);
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

        #endregion

        #region Audit Information

        public string GetAuditUserName()
        {
            return GetCurrentSession()?.FullName ?? "System";
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

        private UserSessionContext CreateBasicSessionFromClaims(ClaimsPrincipal user, string sessionId)
        {
            var userId = int.Parse(user.FindFirst("sub")?.Value ?? user.FindFirst("userId")?.Value ?? "0");
            var email = user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value ?? "";
            var firstName = user.FindFirst("firstName")?.Value ?? "";
            var lastName = user.FindFirst("lastName")?.Value ?? "";
            var roleStr = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value ?? "Customer";

            Enum.TryParse<UserRole>(roleStr, out var role);

            var session = new UserSessionContext
            {
                UserId = userId,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Role = role,
                IsActive = true,
                SessionId = sessionId,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                RequestId = _httpContextAccessor.HttpContext?.TraceIdentifier,
                CorrelationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString(),
                SessionStartTime = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                Permissions = GetBasicPermissionsForRole(role),
                Claims = user.Claims
    .GroupBy(c => c.Type)
    .ToDictionary(g => g.Key, g => (object)g.First().Value)
            };

            return session;
        }

        private async Task<UserSessionContext?> LoadFullSessionAsync(int userId, string sessionId)
        {
            try
            {
                // Use scoped service to ensure proper DbContext handling
                using var scope = _serviceProvider.CreateScope();
                var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                var user = await userRepository.GetByIdAsync(userId);
                if (user == null || !user.IsActive || user.IsLocked)
                {
                    _logger.LogWarning("User {UserId} not found, deactivated, or locked", userId);
                    return null;
                }

                return await CreateSessionFromUserAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading full session for user {UserId}", userId);
                return null;
            }
        }

        private async Task<UserSessionContext> CreateSessionFromUserAsync(User user, string? ipAddress = null, string? userAgent = null)
        {
            var session = UserSessionContext.CreateFromUser(
                user,
                ipAddress ?? GetClientIpAddress(),
                userAgent ?? GetUserAgent());

            session.RequestId = _httpContextAccessor.HttpContext?.TraceIdentifier;
            session.CorrelationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();

            // Load permissions using scoped service
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var permissionResolver = scope.ServiceProvider.GetService<IPermissionResolver>();
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

        private async Task RefreshSessionWithUser(UserSessionContext session, User user)
        {
            session.CurrentUser = user;
            session.SyncFromCurrentUser();
            session.UpdateLastActivity();

            var sessionId = session.SessionId;
            if (!string.IsNullOrEmpty(sessionId))
            {
                CacheSessionInMemory(sessionId, session);
                await CacheSessionInDistributedCacheAsync(sessionId, session);
            }
        }

        private void CacheSessionInMemory(string sessionId, UserSessionContext session)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            try
            {
                // Enforce cache size limit
                if (_memorySessionCache.Count >= _maxConcurrentSessions)
                {
                    CleanupExpiredMemorySessions(null);

                    // If still at limit, remove oldest entries
                    if (_memorySessionCache.Count >= _maxConcurrentSessions)
                    {
                        var oldestEntries = _memorySessionCache
                            .OrderBy(kv => kv.Value.Session.LastActivity)
                            .Take(_maxConcurrentSessions / 10) // Remove 10% of entries
                            .Select(kv => kv.Key)
                            .ToList();

                        foreach (var key in oldestEntries)
                        {
                            _memorySessionCache.TryRemove(key, out _);
                        }
                    }
                }

                var expiresAt = DateTime.UtcNow.Add(_memoryCacheTimeout);
                _memorySessionCache.AddOrUpdate(sessionId, (session, expiresAt), (key, oldValue) => (session, expiresAt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching session in memory");
            }
        }

        private async Task CacheSessionInDistributedCacheAsync(string sessionId, UserSessionContext session)
        {
            try
            {
                if (!string.IsNullOrEmpty(sessionId))
                {
                    var cacheKey = CacheKeys.SessionById(sessionId);
                    await _cacheService.SetAsync(cacheKey, session, _sessionTimeout);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching session in distributed cache");
            }
        }

        private async Task<UserSessionContext?> GetSessionFromDistributedCacheAsync(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId)) return null;

                var cacheKey = CacheKeys.SessionById(sessionId);
                return await _cacheService.GetAsync<UserSessionContext>(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting session from distributed cache");
                return null;
            }
        }

        private void CleanupExpiredMemorySessions(object? state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredKeys = _memorySessionCache
                    .Where(kv => now > kv.Value.ExpiresAt || kv.Value.Session.IsSessionExpired(_sessionTimeout))
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _memorySessionCache.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired memory sessions", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during memory session cleanup");
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

            // Generate new session ID as last resort
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

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _cleanupTimer?.Dispose();
                _memorySessionCache?.Clear();
                _disposed = true;
            }
        }

        #endregion
    }
}