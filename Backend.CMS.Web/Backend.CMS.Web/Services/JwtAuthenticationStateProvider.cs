using Backend.CMS.Application.DTOs;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace Backend.CMS.Web.Services
{
    public class JwtAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ILocalStorageService _localStorage;
        private readonly HttpClient _httpClient;
        private readonly ILogger<JwtAuthenticationStateProvider> _logger;
        private readonly JwtSecurityTokenHandler _jwtHandler;

        private const string TOKEN_KEY = "authToken";
        private const string USER_KEY = "currentUser";

        public JwtAuthenticationStateProvider(
            ILocalStorageService localStorage,
            IHttpClientFactory httpClientFactory,
            ILogger<JwtAuthenticationStateProvider> logger)
        {
            _localStorage = localStorage;
            _httpClient = httpClientFactory.CreateClient("API");
            _logger = logger;
            _jwtHandler = new JwtSecurityTokenHandler();
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                var token = await _localStorage.GetItemAsync<string>(TOKEN_KEY);

                if (string.IsNullOrEmpty(token))
                {
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }

                // Validate token format
                if (!_jwtHandler.CanReadToken(token))
                {
                    await ClearAuthenticationDataAsync();
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }

                var jwtToken = _jwtHandler.ReadJwtToken(token);

                // Check if token is expired
                if (jwtToken.ValidTo < DateTime.UtcNow)
                {
                    await ClearAuthenticationDataAsync();
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }

                var claims = GetClaimsFromToken(jwtToken);
                var identity = new ClaimsIdentity(claims, "jwt");
                var user = new ClaimsPrincipal(identity);

                // Set authorization header for API calls
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                return new AuthenticationState(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting authentication state");
                await ClearAuthenticationDataAsync();
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }

        public async Task<bool> LoginAsync(string email, string password, bool rememberMe)
        {
            try
            {
                var loginRequest = new
                {
                    Email = email,
                    Password = password,
                    RememberMe = rememberMe
                };

                var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponseDto>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (loginResponse != null)
                    {
                        await _localStorage.SetItemAsync(TOKEN_KEY, loginResponse.AccessToken);
                        await _localStorage.SetItemAsync(USER_KEY, loginResponse.User);

                        // Set authorization header
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

                        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                // Call logout endpoint
                await _httpClient.PostAsync("/api/auth/logout", null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calling logout endpoint");
            }
            finally
            {
                await ClearAuthenticationDataAsync();
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            }
        }

        public async Task<UserDto?> GetCurrentUserAsync()
        {
            try
            {
                return await _localStorage.GetItemAsync<UserDto>(USER_KEY);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return null;
            }
        }

        public async Task<bool> IsInRoleAsync(params string[] roles)
        {
            var authState = await GetAuthenticationStateAsync();
            return roles.Any(role => authState.User.IsInRole(role));
        }

        private async Task ClearAuthenticationDataAsync()
        {
            await _localStorage.RemoveItemAsync(TOKEN_KEY);
            await _localStorage.RemoveItemAsync(USER_KEY);
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        private static List<Claim> GetClaimsFromToken(JwtSecurityToken token)
        {
            var claims = new List<Claim>();

            foreach (var claim in token.Claims)
            {
                claims.Add(new Claim(claim.Type, claim.Value));
            }

            // Ensure we have the required claims for authorization
            if (claims.All(c => c.Type != ClaimTypes.Role))
            {
                var roleClaim = claims.FirstOrDefault(c => c.Type == "role");
                if (roleClaim != null)
                {
                    claims.Add(new Claim(ClaimTypes.Role, roleClaim.Value));
                }
            }

            if (claims.All(c => c.Type != ClaimTypes.NameIdentifier))
            {
                var subClaim = claims.FirstOrDefault(c => c.Type == "sub");
                if (subClaim != null)
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, subClaim.Value));
                }
            }

            if (claims.All(c => c.Type != ClaimTypes.Email))
            {
                var emailClaim = claims.FirstOrDefault(c => c.Type == "email");
                if (emailClaim != null)
                {
                    claims.Add(new Claim(ClaimTypes.Email, emailClaim.Value));
                }
            }

            return claims;
        }
    }
}