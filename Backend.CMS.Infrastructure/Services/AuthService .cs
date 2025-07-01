using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Caching;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    }

    public class AuthService : IAuthService, IDisposable
    {
        private readonly IUserRepository _userRepository;
        private readonly IRepository<UserSession> _sessionRepository;
        private readonly IRepository<PasswordResetToken> _passwordResetRepository;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEmailService _emailService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<AuthService> _logger;

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

        // Cache settings
        private readonly TimeSpan _tokenValidationCacheExpiry = TimeSpan.FromMinutes(5);

        public AuthService(
            IUserRepository userRepository,
            IRepository<UserSession> sessionRepository,
            IRepository<PasswordResetToken> passwordResetRepository,
            IConfiguration configuration,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor,
            IEmailService emailService,
            ICacheService cacheService,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _passwordResetRepository = passwordResetRepository ?? throw new ArgumentNullException(nameof(passwordResetRepository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

                var user = await _userRepository.GetByEmailAsync(normalizedEmail);

                if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
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
                var session = await _sessionRepository.FirstOrDefaultAsync(s =>
                    s.RefreshToken == refreshTokenDto.RefreshToken &&
                    !s.IsRevoked &&
                    s.ExpiresAt > DateTime.UtcNow);

                if (session == null)
                {
                    _logger.LogWarning("Refresh token not found or expired from IP {ClientIP}", clientIp);
                    throw new UnauthorizedAccessException("Invalid or expired refresh token");
                }

                var user = await _userRepository.GetByIdAsync(session.UserId);
                if (user == null || !user.IsActive)
                {
                    // Revoke invalid session
                    session.IsRevoked = true;
                    session.UpdatedAt = DateTime.UtcNow;
                    _sessionRepository.Update(session);
                    await _sessionRepository.SaveChangesAsync();

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

                _sessionRepository.Update(session);
                await _sessionRepository.SaveChangesAsync();

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
                var session = await _sessionRepository.FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);
                if (session != null)
                {
                    session.IsRevoked = true;
                    session.UpdatedAt = DateTime.UtcNow;
                    session.UpdatedByUserId = session.UserId;

                    _sessionRepository.Update(session);
                    await _sessionRepository.SaveChangesAsync();

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
                var sessions = await _sessionRepository.FindAsync(s => s.UserId == userId && !s.IsRevoked);
                foreach (var session in sessions)
                {
                    session.IsRevoked = true;
                    session.UpdatedAt = DateTime.UtcNow;
                    session.UpdatedByUserId = userId;
                    _sessionRepository.Update(session);
                }
                await _sessionRepository.SaveChangesAsync();

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
                // Check cache first
                var cacheKey = $"token_validation:{ComputeHash(token)}";
                var cachedResult = await _cacheService.GetAsync<TokenValidationResult>(cacheKey);
                if (cachedResult != null)
                {
                    return cachedResult.IsValid;
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

                // Cache the result
                var resultWrapper = new TokenValidationResult { IsValid = isValid };
                await _cacheService.SetAsync(cacheKey, resultWrapper, _tokenValidationCacheExpiry);

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

                var user = await _userRepository.GetByIdAsync(userId.Value);

                if (user == null)
                {
                    throw new UnauthorizedAccessException("User not found");
                }

                // Check if user is still active and not locked
                if (!user.IsActive)
                    throw new UnauthorizedAccessException("Account is deactivated");

                if (user.IsLocked)
                {
                    await CheckAndUnlockUserAsync(user);
                    if (user.IsLocked)
                        throw new UnauthorizedAccessException("Account is locked");
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
                var user = await _userRepository.GetByEmailAsync(normalizedEmail);
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

                await _passwordResetRepository.AddAsync(passwordResetToken);
                await _passwordResetRepository.SaveChangesAsync();

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
                var passwordResetToken = await _passwordResetRepository.FirstOrDefaultAsync(t =>
                    t.Token == token &&
                    !t.IsUsed &&
                    t.ExpiresAt > DateTime.UtcNow);

                if (passwordResetToken == null)
                {
                    _logger.LogWarning("Invalid or expired password reset token used from IP {ClientIP}", clientIp);
                    return false;
                }

                var user = await _userRepository.GetByIdAsync(passwordResetToken.UserId);
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

                _userRepository.Update(user);
                _passwordResetRepository.Update(passwordResetToken);
                await _passwordResetRepository.SaveChangesAsync();

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
                var user = await _userRepository.GetByIdAsync(userId);
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

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

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
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || !user.RecoveryCodes.Contains(code))
                {
                    _logger.LogWarning("Invalid recovery code used for user {UserId}", userId);
                    return false;
                }

                user.RecoveryCodes.Remove(code);
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedByUserId = userId;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

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

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();
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

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                _logger.LogInformation("User {UserId} automatically unlocked after lockout period expired", user.Id);
            }
        }

        private async Task UpdateSuccessfulLoginAsync(User user, string clientIp)
        {
            user.FailedLoginAttempts = 0;
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = user.Id;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

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

            await _sessionRepository.AddAsync(session);
            await _sessionRepository.SaveChangesAsync();

            return session;
        }

        private async Task EnforceConcurrentSessionLimitAsync(int userId)
        {
            var activeSessions = await _sessionRepository.FindAsync(s =>
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
                    _sessionRepository.Update(session);
                }

                await _sessionRepository.SaveChangesAsync();

                _logger.LogInformation("Revoked {Count} oldest sessions for user {UserId} due to concurrent session limit",
                    sessionsToRevoke.Count(), userId);
            }
        }

        private async Task InvalidateExistingPasswordResetTokensAsync(int userId)
        {
            var existingTokens = await _passwordResetRepository.FindAsync(t => t.UserId == userId && !t.IsUsed);
            foreach (var existingToken in existingTokens)
            {
                existingToken.IsUsed = true;
                existingToken.UsedAt = DateTime.UtcNow;
                existingToken.UpdatedAt = DateTime.UtcNow;
                existingToken.UpdatedByUserId = userId;
                _passwordResetRepository.Update(existingToken);
            }

            if (existingTokens.Any())
            {
                await _passwordResetRepository.SaveChangesAsync();
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