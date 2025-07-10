using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Caching;
using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.Caching.Services;
using Backend.CMS.Infrastructure.Interfaces;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Backend.CMS.Infrastructure.Services
{
    // Wrapper class for caching boolean values
    public class TokenValidationResult
    {
        public bool IsValid { get; set; }
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AuthService : IAuthService, IDisposable
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEmailService _emailService;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly IUserCacheService _userCacheService;
        private readonly ILogger<AuthService> _logger;
        private readonly CacheOptions _cacheOptions;

        // Security settings
        private readonly int _maxFailedAttempts;
        private readonly TimeSpan _lockoutDuration;
        private readonly TimeSpan _passwordResetTokenExpiry;
        private readonly int _maxConcurrentSessions;
        private readonly bool _enableBruteForceProtection;

        // Rate limiting for login attempts per IP
        private readonly ConcurrentDictionary<string, (int Attempts, DateTime LastAttempt)> _ipAttempts;
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        public AuthService(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor,
            IEmailService emailService,
            ICacheService cacheService,
            ICacheInvalidationService cacheInvalidationService,
            ICacheKeyService cacheKeyService,
            IUserCacheService userCacheService,
            IOptions<CacheOptions> cacheOptions,
            ILogger<AuthService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
            _cacheKeyService = cacheKeyService ?? throw new ArgumentNullException(nameof(cacheKeyService));
            _userCacheService = userCacheService ?? throw new ArgumentNullException(nameof(userCacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheOptions = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));

            // Load security settings
            _maxFailedAttempts = _configuration.GetValue("Security:MaxFailedLoginAttempts", 5);
            _lockoutDuration = TimeSpan.FromMinutes(_configuration.GetValue("Security:LockoutDurationMinutes", 30));
            _passwordResetTokenExpiry = TimeSpan.FromHours(_configuration.GetValue("Security:PasswordResetTokenExpiryHours", 24));
            _maxConcurrentSessions = _configuration.GetValue("Security:MaxConcurrentSessions", 5);
            _enableBruteForceProtection = _configuration.GetValue("Security:EnableBruteForceProtection", true);

            _ipAttempts = new ConcurrentDictionary<string, (int, DateTime)>();

            // Cleanup expired IP attempts every 10 minutes
            _cleanupTimer = new Timer(CleanupExpiredAttempts, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        }

        public async Task<LoginResponseDto> LoginAsync(LoginDto loginDto)
        {
            if (loginDto == null)
                throw new ArgumentNullException(nameof(loginDto));

            if (string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
                throw new ArgumentException("Email and password are required");

            var clientIp = GetClientIpAddress();
            var normalizedEmail = loginDto.Email.Trim().ToLowerInvariant();

            try
            {
                // Check for IP-based rate limiting
                if (_enableBruteForceProtection && IsIpBlocked(clientIp))
                {
                    _logger.LogWarning("Login attempt blocked due to too many failed attempts from IP {ClientIP}", clientIp);
                    throw new UnauthorizedAccessException("Too many failed login attempts. Please try again later.");
                }

                // Always get user from database for authentication to ensure we have password hash
                var user = await _userCacheService.GetUserForAuthenticationAsync(normalizedEmail);

                if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
                {
                    await HandleFailedLoginAttemptAsync(user, clientIp);

                    // Add delay to prevent timing attacks
                    await Task.Delay(new Random().Next(100, 500));

                    throw new UnauthorizedAccessException("Invalid email or password");
                }

                // Validate user account status
                await ValidateUserAccountStatusAsync(user);

                // Check concurrent session limit
                await EnforceConcurrentSessionLimitAsync(user.Id);

                // Reset failed attempts and update last login
                await UpdateSuccessfulLoginAsync(user, clientIp);

                // Generate tokens
                var accessToken = GenerateAccessToken(user);
                var refreshToken = GenerateRefreshToken();

                // Save session
                var session = await CreateUserSessionAsync(user, refreshToken, clientIp);

                var userDto = _mapper.Map<UserDto>(user);

                var response = new LoginResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenExpiryMinutes()),
                    User = userDto,
                };

                // Cache user data and session for faster subsequent access
                await CacheUserLoginDataAsync(user, session);

                _logger.LogInformation("User {UserId} ({Email}) logged in successfully from IP {ClientIP}",
                    user.Id, user.Email, clientIp);

                return response;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Failed login attempt for email {Email} from IP {ClientIP}", normalizedEmail, clientIp);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email {Email} from IP {ClientIP}", normalizedEmail, clientIp);
                throw;
            }
        }

        public async Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenDto refreshTokenDto)
        {
            if (refreshTokenDto == null)
                throw new ArgumentNullException(nameof(refreshTokenDto));

            if (string.IsNullOrWhiteSpace(refreshTokenDto.RefreshToken))
                throw new ArgumentException("Refresh token is required");

            var clientIp = GetClientIpAddress();

            try
            {
                // Try to get session from cache first
                var sessionCacheKey = CacheKeys.SessionByToken(refreshTokenDto.RefreshToken);
                var cachedSession = await _cacheService.GetAsync<UserSessionCacheDto>(sessionCacheKey);

                UserSession? session = null;
                if (cachedSession != null && !cachedSession.IsRevoked && cachedSession.ExpiresAt > DateTime.UtcNow)
                {
                    session = _mapper.Map<UserSession>(cachedSession);
                }
                else
                {
                    // Cache miss or expired - load from database
                    session = await _unitOfWork.GetRepository<UserSession>().FirstOrDefaultAsync(s =>
                        s.RefreshToken == refreshTokenDto.RefreshToken &&
                        !s.IsRevoked &&
                        s.ExpiresAt > DateTime.UtcNow);

                    if (session != null)
                    {
                        // Cache the valid session
                        var sessionDto = _mapper.Map<UserSessionCacheDto>(session);
                        await _cacheService.SetAsync(sessionCacheKey, sessionDto, _cacheOptions.DefaultExpiration);
                    }
                }

                if (session == null)
                {
                    _logger.LogWarning("Refresh token not found or expired from IP {ClientIP}", clientIp);
                    throw new UnauthorizedAccessException("Invalid or expired refresh token");
                }

                // Get user from database for refresh token operations (need to verify user status)
                var user = await _userCacheService.GetUserByIdForAuthenticationAsync(session.UserId);

                if (user == null || !user.IsActive)
                {
                    // Revoke invalid session
                    session.IsRevoked = true;
                    session.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.GetRepository<UserSession>().Update(session);
                    await _unitOfWork.GetRepository<UserSession>().SaveChangesAsync();

                    // Remove from cache
                    await _cacheService.RemoveAsync(sessionCacheKey);
                    await _userCacheService.InvalidateUserCacheAsync(session.UserId);

                    _logger.LogWarning("Refresh token used for invalid/inactive user {UserId} from IP {ClientIP}",
                        session.UserId, clientIp);
                    throw new UnauthorizedAccessException("User not found or inactive");
                }

                // Check if user is locked
                if (user.IsLocked)
                {
                    await CheckAndUnlockUserAsync(user);
                    if (user.IsLocked)
                    {
                        throw new UnauthorizedAccessException("Account is locked");
                    }
                }

                // Generate new tokens
                var accessToken = GenerateAccessToken(user);
                var newRefreshToken = GenerateRefreshToken();

                // Update session
                session.RefreshToken = newRefreshToken;
                session.ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpiryDays());
                session.UpdatedAt = DateTime.UtcNow;
                session.UpdatedByUserId = user.Id;
                session.IpAddress = clientIp; // Update IP in case it changed

                _unitOfWork.GetRepository<UserSession>().Update(session);
                await _unitOfWork.GetRepository<UserSession>().SaveChangesAsync();

                // Update cache with new session data
                await _cacheService.RemoveAsync(sessionCacheKey); // Remove old token cache
                var newSessionCacheKey = CacheKeys.SessionByToken(newRefreshToken);
                var newSessionDto = _mapper.Map<UserSessionCacheDto>(session);
                await _cacheService.SetAsync(newSessionCacheKey, newSessionDto, _cacheOptions.DefaultExpiration);

                var userDto = _mapper.Map<UserDto>(user);

                var response = new LoginResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenExpiryMinutes()),
                    User = userDto
                };

                _logger.LogDebug("Tokens refreshed for user {UserId} from IP {ClientIP}", user.Id, clientIp);

                return response;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token from IP {ClientIP}", clientIp);
                throw;
            }
        }


        public async Task<bool> LogoutAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return false;

            try
            {
                var session = await _unitOfWork.GetRepository<UserSession>().FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);
                if (session != null)
                {
                    session.IsRevoked = true;
                    session.UpdatedAt = DateTime.UtcNow;
                    session.UpdatedByUserId = session.UserId;

                    _unitOfWork.GetRepository<UserSession>().Update(session);
                    await _unitOfWork.GetRepository<UserSession>().SaveChangesAsync();

                    // Remove session from cache
                    var sessionCacheKey = CacheKeys.SessionByToken(refreshToken);
                    await _cacheService.RemoveAsync(sessionCacheKey);

                    // Invalidate user sessions cache
                    await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.UserSessions(session.UserId));

                    _logger.LogInformation("User {UserId} logged out successfully", session.UserId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return false;
            }
        }

        public async Task<bool> RevokeAllSessionsAsync(int userId)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            try
            {
                var sessions = await _unitOfWork.GetRepository<UserSession>().FindAsync(s => s.UserId == userId && !s.IsRevoked);
                foreach (var session in sessions)
                {
                    session.IsRevoked = true;
                    session.UpdatedAt = DateTime.UtcNow;
                    session.UpdatedByUserId = userId;
                    _unitOfWork.GetRepository<UserSession>().Update(session);

                    // Remove individual session from cache
                    var sessionCacheKey = CacheKeys.SessionByToken(session.RefreshToken);
                    await _cacheService.RemoveAsync(sessionCacheKey);
                }
                await _unitOfWork.GetRepository<UserSession>().SaveChangesAsync();

                // Invalidate all user-related session caches
                await _cacheInvalidationService.InvalidateByPatternAsync(CacheKeys.UserSessions(userId));

                _logger.LogInformation("All sessions revoked for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all sessions for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            try
            {
                // Create cache key for token validation
                var tokenHash = ComputeHash(token);
                var cacheKey = _cacheKeyService.GetCustomKey("token_validation", tokenHash);

                // Check cache first
                var cachedResult = await _cacheService.GetAsync<TokenValidationResult>(cacheKey);
                if (cachedResult != null)
                {
                    // Check if cached result is still fresh (not older than 1 minute for security)
                    if (DateTime.UtcNow - cachedResult.ValidatedAt < TimeSpan.FromMinutes(1))
                    {
                        return cachedResult.IsValid;
                    }
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(GetJwtSecretKey());

                var validationResult = await tokenHandler.ValidateTokenAsync(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = GetJwtIssuer(),
                    ValidateAudience = true,
                    ValidAudience = GetJwtAudience(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                });

                var isValid = validationResult.IsValid;

                // Cache the result with short expiration for security
                var resultWrapper = new TokenValidationResult { IsValid = isValid, ValidatedAt = DateTime.UtcNow };
                await _cacheService.SetAsync(cacheKey, resultWrapper, _cacheOptions.ShortExpiration);

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Token validation failed");
                return false;
            }
        }

        public async Task<UserDto> GetCurrentUserAsync()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    throw new UnauthorizedAccessException("No authenticated user found");

                // For current user operations, we can use cache since we don't need password hash
                var cachedUser = await _userCacheService.GetUserByIdAsync(userId.Value);

                User? user = null;
                if (cachedUser != null)
                {
                    user = _mapper.Map<User>(cachedUser);
                }
                else
                {
                    user = await _unitOfWork.Users.GetByIdAsync(userId.Value);
                    if (user != null)
                    {
                        await _userCacheService.SetUserAsync(user);
                    }
                }

                if (user == null)
                {
                    throw new UnauthorizedAccessException("User not found");
                }

                // Check if user is still active and not locked
                if (!user.IsActive)
                    throw new UnauthorizedAccessException("Account is deactivated");

                if (user.IsLocked)
                {
                    // For lock check, we need the full user data from database
                    var fullUser = await _userCacheService.GetUserByIdForAuthenticationAsync(userId.Value);
                    if (fullUser != null)
                    {
                        await CheckAndUnlockUserAsync(fullUser);
                        if (fullUser.IsLocked)
                            throw new UnauthorizedAccessException("Account is locked");

                        // Update cache with unlocked status
                        await _userCacheService.SetUserAsync(fullUser);
                        user = fullUser;
                    }
                }

                return _mapper.Map<UserDto>(user);
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get current user");
                throw new UnauthorizedAccessException($"Failed to get current user: {ex.Message}");
            }
        }
        public async Task<bool> ForgotPasswordAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var clientIp = GetClientIpAddress();

            try
            {
                // For password reset, always get from database to ensure we have complete user data
                var user = await _userCacheService.GetUserForAuthenticationAsync(normalizedEmail);

                if (user == null)
                {
                    _logger.LogInformation("Password reset requested for non-existent email {Email} from IP {ClientIP}",
                        normalizedEmail, clientIp);
                    return true; // Don't reveal if email exists
                }

                // Check if user account is active
                if (!user.IsActive)
                {
                    _logger.LogWarning("Password reset requested for inactive user {UserId} from IP {ClientIP}",
                        user.Id, clientIp);
                    return true; // Don't reveal account status
                }

                // Invalidate any existing password reset tokens for this user
                await InvalidateExistingPasswordResetTokensAsync(user.Id);

                // Generate new reset token
                var resetToken = GenerateSecureToken();
                var passwordResetToken = new PasswordResetToken
                {
                    UserId = user.Id,
                    Token = resetToken,
                    ExpiresAt = DateTime.UtcNow.Add(_passwordResetTokenExpiry),
                    IsUsed = false,
                    IpAddress = clientIp,
                    UserAgent = GetUserAgent(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = user.Id,
                    UpdatedByUserId = user.Id
                };

                await _unitOfWork.GetRepository<PasswordResetToken>().AddAsync(passwordResetToken);
                await _unitOfWork.GetRepository<PasswordResetToken>().SaveChangesAsync();

                // Cache the reset token for faster lookup
                var tokenCacheKey = _cacheKeyService.GetCustomKey("password_reset_token", resetToken);
                await _cacheService.SetAsync(tokenCacheKey, passwordResetToken, _passwordResetTokenExpiry);

                // Send password reset email
                var resetUrl = _configuration["AppSettings:FrontendUrl"] + "/reset-password";
                await _emailService.SendPasswordResetEmailAsync(user.Email, resetToken, resetUrl);

                _logger.LogInformation("Password reset token generated for user {UserId} from IP {ClientIP}",
                    user.Id, clientIp);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing forgot password request for email {Email} from IP {ClientIP}",
                    normalizedEmail, clientIp);
                throw;
            }
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            var clientIp = GetClientIpAddress();

            try
            {
                // Try cache first
                var tokenCacheKey = _cacheKeyService.GetCustomKey("password_reset_token", token);
                var passwordResetToken = await _cacheService.GetAsync<PasswordResetToken>(tokenCacheKey);

                if (passwordResetToken == null)
                {
                    passwordResetToken = await _unitOfWork.GetRepository<PasswordResetToken>().FirstOrDefaultAsync(t =>
                        t.Token == token &&
                        !t.IsUsed &&
                        t.ExpiresAt > DateTime.UtcNow);

                    if (passwordResetToken != null)
                    {
                        await _cacheService.SetAsync(tokenCacheKey, passwordResetToken, TimeSpan.FromMinutes(5));
                    }
                }

                if (passwordResetToken == null || passwordResetToken.IsUsed || passwordResetToken.ExpiresAt <= DateTime.UtcNow)
                {
                    _logger.LogWarning("Invalid or expired password reset token used from IP {ClientIP}", clientIp);
                    return false;
                }

                var user = await _unitOfWork.Users.GetByIdAsync(passwordResetToken.UserId);
                if (user == null)
                {
                    _logger.LogWarning("Password reset attempted for non-existent user {UserId} from IP {ClientIP}",
                        passwordResetToken.UserId, clientIp);
                    return false;
                }

                // Validate password strength
                ValidatePasswordStrength(newPassword);

                // Update password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 12); // Use higher work factor
                user.PasswordChangedAt = DateTime.UtcNow;
                user.FailedLoginAttempts = 0;
                user.IsLocked = false;
                user.LockoutEnd = null;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedByUserId = user.Id;

                // Mark token as used
                passwordResetToken.IsUsed = true;
                passwordResetToken.UsedAt = DateTime.UtcNow;
                passwordResetToken.UpdatedAt = DateTime.UtcNow;
                passwordResetToken.UpdatedByUserId = user.Id;

                _unitOfWork.Users.Update(user);

                _unitOfWork.GetRepository<PasswordResetToken>().Update(passwordResetToken);
                await _unitOfWork.SaveChangesAsync();

                // Update user in cache
                await _userCacheService.SetUserAsync(user);

                // Remove used token from cache
                await _cacheService.RemoveAsync(tokenCacheKey);

                // Revoke all existing sessions for security
                await RevokeAllSessionsAsync(user.Id);

                // Send confirmation email
                await _emailService.SendPasswordChangedEmailAsync(user.Email, user.FirstName);

                _logger.LogInformation("Password reset completed for user {UserId} from IP {ClientIP}",
                    user.Id, clientIp);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password from IP {ClientIP}", clientIp);
                throw;
            }
        }

        public async Task<List<string>> GenerateRecoveryCodesAsync(int userId)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                    throw new KeyNotFoundException("User not found");

                var recoveryCodes = new List<string>();
                for (int i = 0; i < 10; i++)
                {
                    recoveryCodes.Add(GenerateRecoveryCode());
                }

                user.RecoveryCodes = recoveryCodes;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedByUserId = userId;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.Users.SaveChangesAsync();

                // Update user in cache
                await _userCacheService.SetUserAsync(user);

                _logger.LogInformation("Recovery codes generated for user {UserId}", userId);

                return recoveryCodes;
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error generating recovery codes for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UseRecoveryCodeAsync(int userId, string code)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            if (string.IsNullOrWhiteSpace(code))
                return false;

            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null || !user.RecoveryCodes.Contains(code))
                {
                    _logger.LogWarning("Invalid recovery code used for user {UserId}", userId);
                    return false;
                }

                user.RecoveryCodes.Remove(code);
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedByUserId = userId;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.Users.SaveChangesAsync();

                // Update user in cache
                await _userCacheService.SetUserAsync(user);

                _logger.LogInformation("Recovery code used for user {UserId}", userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error using recovery code for user {UserId}", userId);
                throw;
            }
        }

        public async Task<LoginResponseDto> CreateTokenForUserAsync(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            try
            {
                await ValidateUserAccountStatusAsync(user);
                await UpdateSuccessfulLoginAsync(user, GetClientIpAddress());

                var accessToken = GenerateAccessToken(user);
                var refreshToken = GenerateRefreshToken();

                var session = await CreateUserSessionAsync(user, refreshToken, GetClientIpAddress());
                await CacheUserLoginDataAsync(user, session);

                var userDto = _mapper.Map<UserDto>(user);

                return new LoginResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenExpiryMinutes()),
                    User = userDto,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating token for user {UserId}", user.Id);
                throw;
            }
        }

        #region Private Helper Methods

        private async Task CacheUserLoginDataAsync(User user, UserSession session)
        {
            try
            {
                // Use the new UserCacheService instead of direct caching
                await _userCacheService.SetUserAsync(user);

                // Cache session separately (sessions don't have circular references)
                var sessionCacheKey = CacheKeys.SessionByToken(session.RefreshToken);
                var sessionDto = _mapper.Map<UserSessionCacheDto>(session);
                await _cacheService.SetAsync(sessionCacheKey, sessionDto, _cacheOptions.DefaultExpiration);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache user login data for user {UserId}", user.Id);
            }
        }

        private async Task HandleFailedLoginAttemptAsync(User? user, string clientIp)
        {
            // Record IP-based attempt
            if (_enableBruteForceProtection)
            {
                var attempts = _ipAttempts.AddOrUpdate(clientIp,
                    (1, DateTime.UtcNow),
                    (key, value) => (value.Attempts + 1, DateTime.UtcNow));
            }

            // Update user-specific failed attempts
            if (user != null)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= _maxFailedAttempts)
                {
                    user.IsLocked = true;
                    user.LockoutEnd = DateTime.UtcNow.Add(_lockoutDuration);
                    await _emailService.SendAccountLockedEmailAsync(user.Email, user.FirstName);

                    _logger.LogWarning("User {UserId} account locked due to {FailedAttempts} failed login attempts",
                        user.Id, user.FailedLoginAttempts);
                }
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedByUserId = user.Id;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.Users.SaveChangesAsync();

                // Update user in cache
                await _userCacheService.SetUserAsync(user);
            }
        }

        private async Task ValidateUserAccountStatusAsync(User user)
        {
            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Account is deactivated");
            }

            if (user.IsLocked)
            {
                await CheckAndUnlockUserAsync(user);
                if (user.IsLocked)
                {
                    var remainingTime = user.LockoutEnd.HasValue ? user.LockoutEnd.Value - DateTime.UtcNow : TimeSpan.Zero;
                    if (remainingTime > TimeSpan.Zero)
                    {
                        throw new UnauthorizedAccessException($"Account is locked for {remainingTime.Minutes} more minutes");
                    }
                }
            }
        }

        private async Task CheckAndUnlockUserAsync(User user)
        {
            if (user.LockoutEnd.HasValue && user.LockoutEnd <= DateTime.UtcNow)
            {
                // Unlock if lockout period has expired
                user.IsLocked = false;
                user.LockoutEnd = null;
                user.FailedLoginAttempts = 0;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedByUserId = user.Id;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.Users.SaveChangesAsync();

                // Update user in cache
                await _userCacheService.SetUserAsync(user);

                _logger.LogInformation("User {UserId} automatically unlocked after lockout period expired", user.Id);
            }
        }

        private async Task UpdateSuccessfulLoginAsync(User user, string clientIp)
        {
            user.FailedLoginAttempts = 0;
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = user.Id;

            _unitOfWork.Users.Update(user);
            await _unitOfWork.Users.SaveChangesAsync();

            // Update user in cache
            await _userCacheService.SetUserAsync(user);

            // Clear IP-based attempts on successful login
            if (_enableBruteForceProtection)
            {
                _ipAttempts.TryRemove(clientIp, out _);
            }
        }

        private async Task<UserSession> CreateUserSessionAsync(User user, string refreshToken, string clientIp)
        {
            var session = new UserSession
            {
                UserId = user.Id,
                RefreshToken = refreshToken,
                IpAddress = clientIp,
                UserAgent = GetUserAgent(),
                ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpiryDays()),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = user.Id,
                UpdatedByUserId = user.Id
            };

            await _unitOfWork.GetRepository<UserSession>().AddAsync(session);
            await _unitOfWork.GetRepository<UserSession>().SaveChangesAsync();

            return session;
        }

        private async Task EnforceConcurrentSessionLimitAsync(int userId)
        {
            var activeSessions = await _unitOfWork.GetRepository<UserSession>().FindAsync(s =>
                s.UserId == userId &&
                !s.IsRevoked &&
                s.ExpiresAt > DateTime.UtcNow);

            var sessionsList = activeSessions.ToList();

            if (sessionsList.Count >= _maxConcurrentSessions)
            {
                // Revoke oldest sessions
                var sessionsToRevoke = sessionsList
                    .OrderBy(s => s.CreatedAt)
                    .Take(sessionsList.Count - _maxConcurrentSessions + 1);

                foreach (var session in sessionsToRevoke)
                {
                    session.IsRevoked = true;
                    session.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.GetRepository<UserSession>().Update(session);

                    // Remove from cache
                    var sessionCacheKey = CacheKeys.SessionByToken(session.RefreshToken);
                    await _cacheService.RemoveAsync(sessionCacheKey);
                }

                await _unitOfWork.GetRepository<UserSession>().SaveChangesAsync();

                _logger.LogInformation("Revoked {Count} oldest sessions for user {UserId} due to concurrent session limit",
                    sessionsToRevoke.Count(), userId);
            }
        }

        private async Task InvalidateExistingPasswordResetTokensAsync(int userId)
        {
            var existingTokens = await _unitOfWork.GetRepository<PasswordResetToken>().FindAsync(t => t.UserId == userId && !t.IsUsed);
            foreach (var existingToken in existingTokens)
            {
                existingToken.IsUsed = true;
                existingToken.UsedAt = DateTime.UtcNow;
                existingToken.UpdatedAt = DateTime.UtcNow;
                existingToken.UpdatedByUserId = userId;
                _unitOfWork.GetRepository<PasswordResetToken>().Update(existingToken);

                // Remove from cache
                var tokenCacheKey = _cacheKeyService.GetCustomKey("password_reset_token", existingToken.Token);
                await _cacheService.RemoveAsync(tokenCacheKey);
            }

            if (existingTokens.Any())
            {
                await _unitOfWork.GetRepository<PasswordResetToken>().SaveChangesAsync();
            }
        }

        private bool IsIpBlocked(string clientIp)
        {
            if (!_ipAttempts.TryGetValue(clientIp, out var attempts))
                return false;

            // Block if more than 10 attempts in last 15 minutes
            return attempts.Attempts > 10 && DateTime.UtcNow - attempts.LastAttempt < TimeSpan.FromMinutes(15);
        }

        private void CleanupExpiredAttempts(object? state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-15);
                var expiredKeys = _ipAttempts
                    .Where(kv => kv.Value.LastAttempt < cutoffTime)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _ipAttempts.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired IP attempt records", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during IP attempts cleanup");
            }
        }

        private string GenerateAccessToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(GetJwtSecretKey());

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64),
                new("userId", user.Id.ToString()),
                new("firstName", user.FirstName ?? string.Empty),
                new("lastName", user.LastName ?? string.Empty),
                new(ClaimTypes.Role, user.Role.ToString()),
                new("role", user.Role.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(GetAccessTokenExpiryMinutes()),
                Issuer = GetJwtIssuer(),
                Audience = GetJwtAudience(),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private string GenerateSecureToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private string GenerateRecoveryCode()
        {
            var random = new Random();
            return $"{random.Next(1000, 9999)}-{random.Next(1000, 9999)}";
        }

        private int? GetCurrentUserId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var userIdClaim = httpContext.User.FindFirst("sub") ??
                             httpContext.User.FindFirst("userId") ??
                             httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            return userId;
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
            return _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? "Unknown";
        }

        private static void ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                throw new ArgumentException("Password must be at least 8 characters long");

            var hasUppercase = password.Any(char.IsUpper);
            var hasLowercase = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            var hasSpecialChar = password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c));

            if (!(hasUppercase && hasLowercase && hasDigit && hasSpecialChar))
                throw new ArgumentException("Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character");
        }

        private static string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hash);
        }

        private string GetJwtSecretKey() => _configuration["JwtSettings:SecretKey"]!;
        private string GetJwtIssuer() => _configuration["JwtSettings:Issuer"]!;
        private string GetJwtAudience() => _configuration["JwtSettings:Audience"]!;
        private int GetAccessTokenExpiryMinutes() => int.Parse(_configuration["JwtSettings:ExpiryInMinutes"]!);
        private int GetRefreshTokenExpiryDays() => int.Parse(_configuration["JwtSettings:RefreshTokenExpiryInDays"]!);

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
                _ipAttempts?.Clear();
                _disposed = true;
            }
        }

        #endregion
    }
}