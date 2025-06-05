using Backend.CMS.Interfaces.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Backend.CMS.Security.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TokenService> _logger;
        private readonly JwtSecurityTokenHandler _tokenHandler;

        public TokenService(IConfiguration configuration, ILogger<TokenService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _tokenHandler = new JwtSecurityTokenHandler();
        }

        public string GenerateAccessToken(IEnumerable<Claim> claims)
        {
            try
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var expireMinutes = _configuration.GetValue<int>("Jwt:ExpireMinutes", 60);

                var now = DateTime.UtcNow;
                var expires = now.AddMinutes(expireMinutes);

                var claimsList = claims.ToList();

                // Add standard claims
                claimsList.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
                claimsList.Add(new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));

                var token = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    claims: claimsList,
                    notBefore: now,
                    expires: expires,
                    signingCredentials: creds
                );

                var tokenString = _tokenHandler.WriteToken(token);

                _logger.LogInformation("Access token generated for user {UserId}",
                    claimsList.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);

                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate access token");
                throw;
            }
        }

        public string GenerateRefreshToken()
        {
            try
            {
                var randomNumber = new byte[64];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate refresh token");
                throw;
            }
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            try
            {
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!)),
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = false, // Don't validate expiration for refresh scenarios
                    ClockSkew = TimeSpan.Zero
                };

                var principal = _tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.LogWarning("Invalid token algorithm or format");
                    return null;
                }

                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get principal from expired token");
                return null;
            }
        }

        public bool ValidateToken(string token)
        {
            try
            {
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidAudience = _configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!)),
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                _tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken validatedToken);
                return true;
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogDebug("Token has expired");
                return false;
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                _logger.LogWarning("Token has invalid signature");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return false;
            }
        }

        public TokenValidationResult ValidateTokenDetailed(string token)
        {
            var result = new TokenValidationResult();

            try
            {
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidAudience = _configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!)),
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                var principal = _tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken validatedToken);

                result.IsValid = true;
                result.Principal = principal;
                result.Token = validatedToken as JwtSecurityToken;
            }
            catch (SecurityTokenExpiredException ex)
            {
                result.IsValid = false;
                result.ErrorType = TokenErrorType.Expired;
                result.ErrorMessage = "Token has expired";
                _logger.LogDebug(ex, "Token validation failed: expired");
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                result.IsValid = false;
                result.ErrorType = TokenErrorType.InvalidSignature;
                result.ErrorMessage = "Invalid token signature";
                _logger.LogWarning(ex, "Token validation failed: invalid signature");
            }
            catch (SecurityTokenInvalidIssuerException ex)
            {
                result.IsValid = false;
                result.ErrorType = TokenErrorType.InvalidIssuer;
                result.ErrorMessage = "Invalid token issuer";
                _logger.LogWarning(ex, "Token validation failed: invalid issuer");
            }
            catch (SecurityTokenInvalidAudienceException ex)
            {
                result.IsValid = false;
                result.ErrorType = TokenErrorType.InvalidAudience;
                result.ErrorMessage = "Invalid token audience";
                _logger.LogWarning(ex, "Token validation failed: invalid audience");
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorType = TokenErrorType.Other;
                result.ErrorMessage = "Token validation failed";
                _logger.LogError(ex, "Unexpected error during token validation");
            }

            return result;
        }

        public string GenerateApiKey(string userId, string? scope = null)
        {
            try
            {
                var payload = new
                {
                    sub = userId,
                    scope = scope ?? "api",
                    type = "api_key",
                    iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    jti = Guid.NewGuid().ToString()
                };

                var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
                var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
                var payloadBase64 = Convert.ToBase64String(payloadBytes);

                // Generate HMAC signature
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
                using var hmac = new HMACSHA256(key);
                var signatureBytes = hmac.ComputeHash(payloadBytes);
                var signatureBase64 = Convert.ToBase64String(signatureBytes);

                return $"{payloadBase64}.{signatureBase64}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate API key for user {UserId}", userId);
                throw;
            }
        }

        public bool ValidateApiKey(string apiKey, out ClaimsPrincipal? principal)
        {
            principal = null;

            try
            {
                var parts = apiKey.Split('.');
                if (parts.Length != 2)
                    return false;

                var payloadBytes = Convert.FromBase64String(parts[0]);
                var expectedSignatureBytes = Convert.FromBase64String(parts[1]);

                // Verify signature
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
                using var hmac = new HMACSHA256(key);
                var actualSignatureBytes = hmac.ComputeHash(payloadBytes);

                if (!actualSignatureBytes.SequenceEqual(expectedSignatureBytes))
                    return false;

                // Parse payload
                var payloadJson = Encoding.UTF8.GetString(payloadBytes);
                var payload = System.Text.Json.JsonSerializer.Deserialize<ApiKeyPayload>(payloadJson);

                if (payload == null)
                    return false;

                // Create claims principal
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, payload.Sub),
                    new("scope", payload.Scope),
                    new("type", payload.Type),
                    new(JwtRegisteredClaimNames.Jti, payload.Jti)
                };

                principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API key validation failed");
                return false;
            }
        }
    }

    public class TokenValidationResult
    {
        public bool IsValid { get; set; }
        public ClaimsPrincipal? Principal { get; set; }
        public JwtSecurityToken? Token { get; set; }
        public TokenErrorType? ErrorType { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public enum TokenErrorType
    {
        Expired,
        InvalidSignature,
        InvalidIssuer,
        InvalidAudience,
        Other
    }

    public class ApiKeyPayload
    {
        public string Sub { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long Iat { get; set; }
        public string Jti { get; set; } = string.Empty;
    }
}