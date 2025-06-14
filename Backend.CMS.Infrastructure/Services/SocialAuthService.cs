using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class SocialAuthService : ISocialAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRepository<UserExternalLogin> _externalLoginRepository;
        private readonly IRepository<FileEntity> _fileRepository;
        private readonly IAuthService _authService;
        private readonly IUserService _userService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SocialAuthService> _logger;
        private readonly IMapper _mapper;
        private readonly HttpClient _httpClient;

        public SocialAuthService(
            IUserRepository userRepository,
            IRepository<UserExternalLogin> externalLoginRepository,
            IRepository<FileEntity> fileRepository,
            IAuthService authService,
            IUserService userService,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<SocialAuthService> logger,
            IMapper mapper,
            HttpClient httpClient)
        {
            _userRepository = userRepository;
            _externalLoginRepository = externalLoginRepository;
            _fileRepository = fileRepository;
            _authService = authService;
            _userService = userService;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _logger = logger;
            _mapper = mapper;
            _httpClient = httpClient;
        }

        public async Task<LoginResponseDto> SocialLoginAsync(SocialLoginDto socialLoginDto)
        {
            try
            {
                var socialUserInfo = await GetSocialUserInfoAsync(socialLoginDto.Provider, socialLoginDto.AccessToken);

                if (string.IsNullOrEmpty(socialUserInfo.Email))
                {
                    throw new ArgumentException("Email is required from social provider");
                }

                // Check if user exists by social ID
                var existingUser = await FindUserBySocialIdAsync(socialLoginDto.Provider, socialUserInfo.Id);

                if (existingUser != null)
                {
                    // Update social login info
                    await UpdateExternalLoginAsync(existingUser.Id, socialLoginDto.Provider, socialLoginDto.AccessToken, socialUserInfo);

                    // Generate JWT tokens
                    var user = await _userRepository.GetByIdAsync(existingUser.Id);
                    return await GenerateTokenResponseAsync(user!);
                }

                // Check if user exists by email
                var userByEmail = await _userRepository.GetByEmailAsync(socialUserInfo.Email);

                if (userByEmail != null)
                {
                    // Link social account to existing user
                    await CreateExternalLoginAsync(userByEmail.Id, socialLoginDto.Provider, socialUserInfo, socialLoginDto.AccessToken);
                    return await GenerateTokenResponseAsync(userByEmail);
                }

                // Create new user
                var newUser = await CreateUserFromSocialInfoAsync(socialUserInfo, socialLoginDto.Provider, socialLoginDto.AccessToken);
                return await GenerateTokenResponseAsync(newUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during social login with provider {Provider}", socialLoginDto.Provider);
                throw;
            }
        }

        public async Task<string> GetSocialLoginUrlAsync(string provider, string returnUrl)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                throw new InvalidOperationException("HTTP context not available");

            var properties = new AuthenticationProperties
            {
                RedirectUri = returnUrl,
                Items = { ["scheme"] = provider }
            };

            var challengeResult = new Microsoft.AspNetCore.Mvc.ChallengeResult(provider, properties);

            // For API usage, return the URL instead of performing redirect
            return $"/api/auth/{provider.ToLower()}/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        public async Task<LoginResponseDto> HandleSocialCallbackAsync(string provider, SocialAuthCallbackDto callbackDto)
        {
            if (!string.IsNullOrEmpty(callbackDto.Error))
            {
                throw new UnauthorizedAccessException($"Social authentication error: {callbackDto.ErrorDescription ?? callbackDto.Error}");
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                throw new InvalidOperationException("HTTP context not available");

            // Get the access token from the authentication result
            var authResult = await httpContext.AuthenticateAsync(provider);
            if (!authResult.Succeeded)
            {
                throw new UnauthorizedAccessException("Social authentication failed");
            }

            var accessToken = authResult.Properties?.GetTokenValue("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new UnauthorizedAccessException("Access token not received from social provider");
            }

            var socialLoginDto = new SocialLoginDto
            {
                Provider = provider,
                AccessToken = accessToken
            };

            return await SocialLoginAsync(socialLoginDto);
        }

        public async Task<SocialUserInfoDto> GetSocialUserInfoAsync(string provider, string accessToken)
        {
            return provider.ToLower() switch
            {
                "google" => await GetGoogleUserInfoAsync(accessToken),
                "facebook" => await GetFacebookUserInfoAsync(accessToken),
                _ => throw new NotSupportedException($"Provider {provider} is not supported")
            };
        }

        public async Task<bool> LinkSocialAccountAsync(int userId, LinkSocialAccountDto linkAccountDto)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return false;

                var socialUserInfo = await GetSocialUserInfoAsync(linkAccountDto.Provider, linkAccountDto.AccessToken);

                // Check if this social account is already linked to another user
                var existingLink = await _externalLoginRepository.FirstOrDefaultAsync(
                    el => el.Provider == linkAccountDto.Provider && el.ExternalUserId == socialUserInfo.Id);

                if (existingLink != null && existingLink.UserId != userId)
                {
                    throw new InvalidOperationException("This social account is already linked to another user");
                }

                if (existingLink == null)
                {
                    await CreateExternalLoginAsync(userId, linkAccountDto.Provider, socialUserInfo, linkAccountDto.AccessToken);
                }
                else
                {
                    await UpdateExternalLoginAsync(userId, linkAccountDto.Provider, linkAccountDto.AccessToken, socialUserInfo);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking social account for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UnlinkSocialAccountAsync(int userId, string provider)
        {
            try
            {
                var externalLogin = await _externalLoginRepository.FirstOrDefaultAsync(
                    el => el.UserId == userId && el.Provider == provider);

                if (externalLogin == null)
                    return false;

                await _externalLoginRepository.SoftDeleteAsync(externalLogin);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlinking social account for user {UserId}", userId);
                return false;
            }
        }

        public async Task<List<UserExternalLogin>> GetUserSocialAccountsAsync(int userId)
        {
            var externalLogins = await _externalLoginRepository.FindAsync(el => el.UserId == userId);
            return externalLogins.ToList();
        }

        public async Task<UserDto?> FindUserBySocialIdAsync(string provider, string externalUserId)
        {
            var externalLogin = await _externalLoginRepository.FirstOrDefaultAsync(
                el => el.Provider == provider && el.ExternalUserId == externalUserId);

            if (externalLogin == null)
                return null;

            var user = await _userRepository.GetByIdAsync(externalLogin.UserId);
            return user != null ? _mapper.Map<UserDto>(user) : null;
        }

        public async Task<bool> RefreshSocialTokenAsync(int userId, string provider)
        {
            // Implementation depends on provider - some support refresh tokens
            // This is a placeholder for future implementation
            await Task.CompletedTask;
            return false;
        }

        #region Private Helper Methods

        private async Task<SocialUserInfoDto> GetGoogleUserInfoAsync(string accessToken)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var userInfo = JsonSerializer.Deserialize<JsonElement>(json);

                return new SocialUserInfoDto
                {
                    Id = userInfo.GetProperty("id").GetString() ?? string.Empty,
                    Email = userInfo.GetProperty("email").GetString() ?? string.Empty,
                    Name = userInfo.GetProperty("name").GetString() ?? string.Empty,
                    FirstName = userInfo.TryGetProperty("given_name", out var givenName) ? givenName.GetString() : null,
                    LastName = userInfo.TryGetProperty("family_name", out var familyName) ? familyName.GetString() : null,
                    Picture = null, // Handle picture file creation separately
                    EmailVerified = userInfo.TryGetProperty("verified_email", out var verified) && verified.GetBoolean(),
                    Provider = "Google"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Google user info");
                throw new UnauthorizedAccessException("Failed to get user information from Google");
            }
        }

        private async Task<SocialUserInfoDto> GetFacebookUserInfoAsync(string accessToken)
        {
            try
            {
                var url = $"https://graph.facebook.com/me?fields=id,name,email,first_name,last_name,picture.type(large)&access_token={accessToken}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var userInfo = JsonSerializer.Deserialize<JsonElement>(json);

                return new SocialUserInfoDto
                {
                    Id = userInfo.GetProperty("id").GetString() ?? string.Empty,
                    Email = userInfo.TryGetProperty("email", out var email) ? email.GetString() ?? string.Empty : string.Empty,
                    Name = userInfo.GetProperty("name").GetString() ?? string.Empty,
                    FirstName = userInfo.TryGetProperty("first_name", out var firstName) ? firstName.GetString() : null,
                    LastName = userInfo.TryGetProperty("last_name", out var lastName) ? lastName.GetString() : null,
                    Picture = null, // Handle picture file creation separately
                    EmailVerified = true, // Facebook emails are generally verified
                    Provider = "Facebook"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Facebook user info");
                throw new UnauthorizedAccessException("Failed to get user information from Facebook");
            }
        }

        private async Task<User> CreateUserFromSocialInfoAsync(SocialUserInfoDto socialUserInfo, string provider, string accessToken)
        {
            var createUserDto = new CreateUserDto
            {
                Email = socialUserInfo.Email,
                Username = GenerateUniqueUsername(socialUserInfo.Email, socialUserInfo.Name),
                Password = GenerateRandomPassword(), // Generate random password for social users
                FirstName = socialUserInfo.FirstName ?? ExtractFirstName(socialUserInfo.Name),
                LastName = socialUserInfo.LastName ?? ExtractLastName(socialUserInfo.Name),
                IsActive = true,
                Role = UserRole.Customer
                // Note: Avatar will be handled separately if needed
            };

            var userDto = await _userService.CreateUserAsync(createUserDto);
            var user = await _userRepository.GetByIdAsync(userDto.Id);

            if (user != null)
            {
                // Mark as external user and email verified
                user.IsExternalUser = true;
                user.EmailVerifiedAt = DateTime.UtcNow;
                // Note: Avatar file handling would be done separately if needed
                user.UpdatedAt = DateTime.UtcNow;
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                // Create external login record
                await CreateExternalLoginAsync(user.Id, provider, socialUserInfo, accessToken);
            }

            return user!;
        }

        private async Task CreateExternalLoginAsync(int userId, string provider, SocialUserInfoDto socialUserInfo, string accessToken)
        {
            var externalLogin = new UserExternalLogin
            {
                UserId = userId,
                Provider = provider,
                ExternalUserId = socialUserInfo.Id,
                Email = socialUserInfo.Email,
                Name = socialUserInfo.Name,
                AccessToken = accessToken,
                TokenExpiry = DateTime.UtcNow.AddHours(1), // Default 1 hour expiry
                Claims = new Dictionary<string, object>
                {
                    ["email"] = socialUserInfo.Email,
                    ["name"] = socialUserInfo.Name,
                    ["picture"] = socialUserInfo.Picture?.Id.ToString() ?? string.Empty, // Store file ID as string
                    ["email_verified"] = socialUserInfo.EmailVerified
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _externalLoginRepository.AddAsync(externalLogin);
            await _externalLoginRepository.SaveChangesAsync();
        }

        private async Task UpdateExternalLoginAsync(int userId, string provider, string accessToken, SocialUserInfoDto socialUserInfo)
        {
            var externalLogin = await _externalLoginRepository.FirstOrDefaultAsync(
                el => el.UserId == userId && el.Provider == provider);

            if (externalLogin != null)
            {
                externalLogin.AccessToken = accessToken;
                externalLogin.TokenExpiry = DateTime.UtcNow.AddHours(1);
                externalLogin.Email = socialUserInfo.Email;
                externalLogin.Name = socialUserInfo.Name;
                externalLogin.Claims = new Dictionary<string, object>
                {
                    ["email"] = socialUserInfo.Email,
                    ["name"] = socialUserInfo.Name,
                    ["picture"] = socialUserInfo.Picture?.Id.ToString() ?? string.Empty, // Store file ID as string
                    ["email_verified"] = socialUserInfo.EmailVerified
                };
                externalLogin.UpdatedAt = DateTime.UtcNow;

                _externalLoginRepository.Update(externalLogin);
                await _externalLoginRepository.SaveChangesAsync();
            }
        }

        private async Task<LoginResponseDto> GenerateTokenResponseAsync(User user)
        {
            var loginDto = new LoginDto
            {
                Email = user.Email,
                Password = string.Empty // Not used for social login
            };

            // Use a different method that doesn't require password verification
            return await _authService.CreateTokenForUserAsync(user);
        }

        private string GenerateUniqueUsername(string email, string name)
        {
            var username = email.Split('@')[0];
            if (string.IsNullOrEmpty(username))
            {
                username = name.Replace(" ", "").ToLower();
            }

            // Add random suffix to ensure uniqueness
            var random = new Random();
            return $"{username}_{random.Next(1000, 9999)}";
        }

        private string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 16)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string ExtractFirstName(string fullName)
        {
            return fullName.Split(' ').FirstOrDefault() ?? string.Empty;
        }

        private string ExtractLastName(string fullName)
        {
            var parts = fullName.Split(' ');
            return parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;
        }

        #endregion
    }
}