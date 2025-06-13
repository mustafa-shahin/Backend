using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.IRepositories;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Backend.CMS.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRepository<UserSession> _sessionRepository;
        private readonly IRepository<PasswordResetToken> _passwordResetRepository;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEmailService _emailService;

        public AuthService(
            IUserRepository userRepository,
            IRepository<UserSession> sessionRepository,
            IRepository<PasswordResetToken> passwordResetRepository,
            IConfiguration configuration,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor,
            IEmailService emailService)
        {
            _userRepository = userRepository;
            _sessionRepository = sessionRepository;
            _passwordResetRepository = passwordResetRepository;
            _configuration = configuration;
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor;
            _emailService = emailService;
        }

        public async Task<LoginResponseDto> LoginAsync(LoginDto loginDto)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
                throw new ArgumentException("Email and password are required");

            var user = await _userRepository.GetByEmailAsync(loginDto.Email.Trim().ToLowerInvariant());

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                // Increment failed attempts for existing user
                if (user != null)
                {
                    await HandleFailedLoginAttemptAsync(user);
                }

                throw new UnauthorizedAccessException("Invalid email or password");
            }

            // Check account status before proceeding
            await ValidateUserAccountStatusAsync(user);

            // Check for 2FA
            if (user.TwoFactorEnabled && string.IsNullOrEmpty(loginDto.TwoFactorCode))
            {
                return new LoginResponseDto
                {
                    RequiresTwoFactor = true
                };
            }

            if (user.TwoFactorEnabled && !string.IsNullOrEmpty(loginDto.TwoFactorCode))
            {
                var isValidCode = await Verify2FACodeAsync(user.Id, loginDto.TwoFactorCode);
                if (!isValidCode)
                {
                    // Check if it is a recovery code
                    var isRecoveryCode = await UseRecoveryCodeAsync(user.Id, loginDto.TwoFactorCode);
                    if (!isRecoveryCode)
                    {
                        throw new UnauthorizedAccessException("Invalid two-factor authentication code");
                    }
                }
            }

            // Reset failed login attempts and update last login
            await UpdateSuccessfulLoginAsync(user);

            // Generate tokens
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            // Save session
            var session = new UserSession
            {
                UserId = user.Id,
                RefreshToken = refreshToken,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpiryDays()),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = user.Id,
                UpdatedByUserId = user.Id
            };

            await _sessionRepository.AddAsync(session);
            await _sessionRepository.SaveChangesAsync();

            var userDto = _mapper.Map<UserDto>(user);

            return new LoginResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenExpiryMinutes()),
                User = userDto,
                RequiresTwoFactor = false
            };
        }

        public async Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenDto refreshTokenDto)
        {
            //Input validation
            if (string.IsNullOrWhiteSpace(refreshTokenDto.RefreshToken))
                throw new ArgumentException("Refresh token is required");

            var session = await _sessionRepository.FirstOrDefaultAsync(s =>
                s.RefreshToken == refreshTokenDto.RefreshToken &&
                !s.IsRevoked &&
                s.ExpiresAt > DateTime.UtcNow);

            if (session == null)
                throw new UnauthorizedAccessException("Invalid or expired refresh token");

            var user = await _userRepository.GetByIdAsync(session.UserId);
            if (user == null || !user.IsActive)
            {
                // Revoke invalid session
                session.IsRevoked = true;
                _sessionRepository.Update(session);
                await _sessionRepository.SaveChangesAsync();

                throw new UnauthorizedAccessException("User not found or inactive");
            }

            //  Check if user is locked
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

            _sessionRepository.Update(session);
            await _sessionRepository.SaveChangesAsync();

            var userDto = _mapper.Map<UserDto>(user);

            return new LoginResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenExpiryMinutes()),
                User = userDto
            };
        }

        public async Task<bool> LogoutAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return false;

            var session = await _sessionRepository.FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);
            if (session != null)
            {
                session.IsRevoked = true;
                session.UpdatedAt = DateTime.UtcNow;
                session.UpdatedByUserId = session.UserId;

                _sessionRepository.Update(session);
                await _sessionRepository.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<bool> RevokeAllSessionsAsync(int userId)
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
            return true;
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            try
            {
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

                return validationResult.IsValid;
            }
            catch
            {
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
            catch (Exception ex)
            {
                // Log the specific error for debugging
                throw new UnauthorizedAccessException($"Failed to get current user: {ex.Message}");
            }
        }

        public async Task<bool> ForgotPasswordAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var user = await _userRepository.GetByEmailAsync(email.Trim().ToLowerInvariant());
            if (user == null)
            {
                return true; // Don't reveal if email exists
            }

            // Check if user account is active
            if (!user.IsActive)
                return true; // Don't reveal account status

            // Invalidate any existing password reset tokens for this user
            var existingTokens = await _passwordResetRepository.FindAsync(t => t.UserId == user.Id && !t.IsUsed);
            foreach (var existingToken in existingTokens)
            {
                existingToken.IsUsed = true;
                existingToken.UsedAt = DateTime.UtcNow;
                existingToken.UpdatedAt = DateTime.UtcNow;
                existingToken.UpdatedByUserId = user.Id;
                _passwordResetRepository.Update(existingToken);
            }

            // Generate new reset token
            var resetToken = GenerateSecureToken();
            var passwordResetToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = resetToken,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                IsUsed = false,
                IpAddress = GetClientIpAddress(),
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

            return true;
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            var passwordResetToken = await _passwordResetRepository.FirstOrDefaultAsync(t =>
                t.Token == token &&
                !t.IsUsed &&
                t.ExpiresAt > DateTime.UtcNow);

            if (passwordResetToken == null)
            {
                return false;
            }

            var user = await _userRepository.GetByIdAsync(passwordResetToken.UserId);
            if (user == null)
            {
                return false;
            }

            // Validate password strength
            if (!IsValidPassword(newPassword))
                throw new ArgumentException("Password does not meet security requirements");

            // Update password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
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

            // Revoke all existing sessions
            await RevokeAllSessionsAsync(user.Id);

            // Send confirmation email
            await _emailService.SendPasswordChangedEmailAsync(user.Email, user.FirstName);

            return true;
        }

        public async Task<bool> Enable2FAAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            user.TwoFactorEnabled = true;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = userId;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            // Send confirmation email
            await _emailService.Send2FAEnabledEmailAsync(user.Email, user.FirstName);

            return true;
        }

        public async Task<bool> Disable2FAAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            user.TwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            user.RecoveryCodes.Clear();
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = userId;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
            return true;
        }

        public async Task<string> Generate2FASecretAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new ArgumentException("User not found");

            var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
            user.TwoFactorSecret = secret;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = userId;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            return secret;
        }

        public async Task<bool> Verify2FACodeAsync(int userId, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.TwoFactorSecret))
                return false;

            try
            {
                var secretBytes = Base32Encoding.ToBytes(user.TwoFactorSecret);
                var totp = new Totp(secretBytes);

                // Verify current window and one window before/after for clock drift
                var currentTime = DateTime.UtcNow;

                for (int i = -1; i <= 1; i++)
                {
                    var timeToCheck = currentTime.AddSeconds(i * 30);
                    var expectedCode = totp.ComputeTotp(timeToCheck);
                    if (expectedCode == code)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> GenerateRecoveryCodesAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) throw new ArgumentException("User not found");

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

            return recoveryCodes;
        }

        public async Task<bool> UseRecoveryCodeAsync(int userId, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || !user.RecoveryCodes.Contains(code))
                return false;

            user.RecoveryCodes.Remove(code);
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = userId;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            return true;
        }

        //Private helper methods
        private async Task HandleFailedLoginAttemptAsync(User user)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.IsLocked = true;
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(30);
                await _emailService.SendAccountLockedEmailAsync(user.Email, user.FirstName);
            }
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = user.Id;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
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
            }
        }

        private async Task UpdateSuccessfulLoginAsync(User user)
        {
            user.FailedLoginAttempts = 0;
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = user.Id;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
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
                new("firstName", user.FirstName),
                new("lastName", user.LastName),
                
                //  Consistent role claims
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

            // Try different claim types
            var userIdClaim = httpContext.User.FindFirst("sub") ??
                             httpContext.User.FindFirst("userId") ??
                             httpContext.User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
            {
                return null;
            }

            if (!int.TryParse(userIdClaim.Value, out var userId))
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

            // Check for forwarded IP (load balancer, proxy)
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
        public async Task<LoginResponseDto> CreateTokenForUserAsync(User user)
        {
            // Similar to LoginAsync but without password verification
            await ValidateUserAccountStatusAsync(user);
            await UpdateSuccessfulLoginAsync(user);

            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            var session = new UserSession
            {
                UserId = user.Id,
                RefreshToken = refreshToken,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpiryDays()),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = user.Id,
                UpdatedByUserId = user.Id
            };

            await _sessionRepository.AddAsync(session);
            await _sessionRepository.SaveChangesAsync();

            var userDto = _mapper.Map<UserDto>(user);

            return new LoginResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenExpiryMinutes()),
                User = userDto,
                RequiresTwoFactor = false
            };
        }
        private static bool IsValidPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;

            // Check for at least one uppercase, lowercase, digit, and special character
            var hasUppercase = password.Any(char.IsUpper);
            var hasLowercase = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            var hasSpecialChar = password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c));

            return hasUppercase && hasLowercase && hasDigit && hasSpecialChar;
        }

        private string GetJwtSecretKey() => _configuration["JwtSettings:SecretKey"]!;
        private string GetJwtIssuer() => _configuration["JwtSettings:Issuer"]!;
        private string GetJwtAudience() => _configuration["JwtSettings:Audience"]!;
        private int GetAccessTokenExpiryMinutes() => int.Parse(_configuration["JwtSettings:ExpiryInMinutes"]!);
        private int GetRefreshTokenExpiryDays() => int.Parse(_configuration["JwtSettings:RefreshTokenExpiryInDays"]!);

    }
}