using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;
        private readonly ISocialAuthService _socialAuthService;
        public AuthController(IAuthService authService, IUserService userService, ILogger<AuthController> logger, ISocialAuthService socialAuthService)
        {
            _authService = authService;
            _userService = userService;
            _logger = logger;
            _socialAuthService = socialAuthService;
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]  
        public async Task<ActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                var createUserDto = new CreateUserDto
                {
                    Email = registerDto.Email,
                    Username = registerDto.Username,
                    Password = registerDto.Password,
                    FirstName = registerDto.FirstName,
                    LastName = registerDto.LastName,
                    IsActive = true, 
                };

                var user = await _userService.CreateUserAsync(createUserDto);
                _logger.LogInformation("User registered successfully: {Email}", registerDto.Email);

                return Ok(new
                {
                    Success = true,
                    Message = "User registered successfully",
                    UserId = user.Id
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("User registration failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration for {Email}", registerDto.Email);
                return StatusCode(500, new { Message = "An error occurred during registration" });
            }
        }

        /// <summary>
        /// Authenticate user and return JWT tokens
        /// </summary>
        [EnableRateLimiting("AuthPolicy")]
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                var result = await _authService.LoginAsync(loginDto);

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Login failed for {Email}: {Message}", loginDto.Email, ex.Message);
                return Unauthorized(new { Message = "Invalid credentials" });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Login validation failed for {Email}: {Message}", loginDto.Email, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", loginDto.Email);
                return StatusCode(500, new { Message = "An error occurred during login" });
            }
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<LoginResponseDto>> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(refreshTokenDto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Token refresh failed: {Message}", ex.Message);
                return Unauthorized(new { Message = "Invalid refresh token" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, new { Message = "An error occurred during token refresh" });
            }
        }

        /// <summary>
        /// Logout user and revoke refresh token
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult> Logout([FromBody] RefreshTokenDto refreshTokenDto)
        {
            try
            {
                await _authService.LogoutAsync(refreshTokenDto.RefreshToken);
                return Ok(new { Message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { Message = "An error occurred during logout" });
            }
        }

        /// <summary>
        /// Get current authenticated user information
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            try
            {
                var user = await _authService.GetCurrentUserAsync();
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, new { Message = "An error occurred while retrieving user information" });
            }
        }

        /// <summary>
        /// Initiate password reset process
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            try
            {
                await _authService.ForgotPasswordAsync(forgotPasswordDto.Email);
                return Ok(new { Message = "If the email exists, a password reset link has been sent" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forgot password for {Email}", forgotPasswordDto.Email);
                return StatusCode(500, new { Message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Reset password using reset token
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            try
            {
                var success = await _authService.ResetPasswordAsync(resetPasswordDto.Token, resetPasswordDto.NewPassword);

                if (!success)
                {
                    return BadRequest(new { Message = "Invalid or expired reset token" });
                }

                return Ok(new { Message = "Password reset successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset");
                return StatusCode(500, new { Message = "An error occurred while resetting the password" });
            }
        }


        /// <summary>
        /// Revoke all user sessions
        /// </summary>
        [HttpPost("revoke-all-sessions")]
        [Authorize]
        public async Task<ActionResult> RevokeAllSessions()
        {
            try
            {
                var userId = GetCurrentUserId();
                await _authService.RevokeAllSessionsAsync(userId);

                return Ok(new { Message = "All sessions revoked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all sessions");
                return StatusCode(500, new { Message = "An error occurred while revoking sessions" });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("userId");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid user token");
            }

            return userId;
        }

        [HttpGet("{provider}/login")]
        [AllowAnonymous]
        public async Task<ActionResult> SocialLogin(string provider, [FromQuery] string returnUrl = "/")
        {
            try
            {
                var url = await _socialAuthService.GetSocialLoginUrlAsync(provider, returnUrl);
                return Challenge(new AuthenticationProperties
                {
                    RedirectUri = Url.Action(nameof(SocialCallback), new { provider, returnUrl })
                }, provider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating social login for provider {Provider}", provider);
                return BadRequest(new { Message = $"Social login not configured for {provider}" });
            }
        }

        [HttpGet("{provider}/callback")]
        [AllowAnonymous]
        public async Task<ActionResult> SocialCallback(string provider, [FromQuery] string returnUrl = "/")
        {
            try
            {
                var callbackDto = new SocialAuthCallbackDto
                {
                    Code = Request.Query["code"],
                    State = Request.Query["state"],
                    Error = Request.Query["error"],
                    ErrorDescription = Request.Query["error_description"]
                };

                var result = await _socialAuthService.HandleSocialCallbackAsync(provider, callbackDto);

                // For web applications might want to redirect with tokens
                // For API-only return the token response
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling social callback for provider {Provider}", provider);
                return BadRequest(new { Message = "Social authentication failed" });
            }
        }

        [HttpPost("social/login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponseDto>> SocialLoginDirect([FromBody] SocialLoginDto socialLoginDto)
        {
            try
            {
                var result = await _socialAuthService.SocialLoginAsync(socialLoginDto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Social login failed: {Message}", ex.Message);
                return Unauthorized(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Social login validation failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during social login");
                return StatusCode(500, new { Message = "An error occurred during social login" });
            }
        }

        [HttpPost("social/link")]
        [Authorize]
        public async Task<ActionResult> LinkSocialAccount([FromBody] LinkSocialAccountDto linkAccountDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _socialAuthService.LinkSocialAccountAsync(userId, linkAccountDto);

                if (!success)
                    return BadRequest(new { Message = "Failed to link social account" });

                return Ok(new { Message = "Social account linked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking social account");
                return StatusCode(500, new { Message = "An error occurred while linking social account" });
            }
        }

        [HttpDelete("social/unlink")]
        [Authorize]
        public async Task<ActionResult> UnlinkSocialAccount([FromBody] UnlinkSocialAccountDto unlinkAccountDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _socialAuthService.UnlinkSocialAccountAsync(userId, unlinkAccountDto.Provider);

                if (!success)
                    return BadRequest(new { Message = "Failed to unlink social account" });

                return Ok(new { Message = "Social account unlinked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlinking social account");
                return StatusCode(500, new { Message = "An error occurred while unlinking social account" });
            }
        }

        [HttpGet("social/accounts")]
        [Authorize]
        public async Task<ActionResult<List<UserExternalLogin>>> GetLinkedSocialAccounts()
        {
            try
            {
                var userId = GetCurrentUserId();
                var accounts = await _socialAuthService.GetUserSocialAccountsAsync(userId);
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting linked social accounts");
                return StatusCode(500, new { Message = "An error occurred while retrieving social accounts" });
            }
        }

    }

    //public class ForgotPasswordDto
    //{
    //    public string Email { get; set; } = string.Empty;
    //}

    //public class ResetPasswordDto
    //{
    //    public string Token { get; set; } = string.Empty;
    //    public string NewPassword { get; set; } = string.Empty;
    //}

    //public class Enable2FAResponseDto
    //{
    //    public string Secret { get; set; } = string.Empty;
    //    public string QrCodeUrl { get; set; } = string.Empty;
    //}

    //public class Verify2FADto
    //{
    //    public string Code { get; set; } = string.Empty;
    //}
    //public class RegisterDto
    //{
    //    public string Email { get; set; } = string.Empty;
    //    public string Username { get; set; } = string.Empty;
    //    public string Password { get; set; } = string.Empty;
    //    public string FirstName { get; set; } = string.Empty;
    //    public string LastName { get; set; } = string.Empty;
    //}
}