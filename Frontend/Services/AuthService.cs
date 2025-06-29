using Backend.CMS.Application.DTOs;
using Blazored.LocalStorage;
using Frontend.Interface;
using System.Net.Http.Json;
using System.Text.Json;

namespace Frontend.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly JsonSerializerOptions _jsonOptions;

        public event Action? AuthenticationStateChanged;

        public AuthService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<LoginResponseDto?> LoginAsync(LoginDto loginDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginDto);

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponseDto>(_jsonOptions);

                    if (loginResponse != null)
                    {
                        // Check if user has admin or dev role
                        if (loginResponse.User.Role != Backend.CMS.Domain.Enums.UserRole.Admin &&
                            loginResponse.User.Role != Backend.CMS.Domain.Enums.UserRole.Dev)
                        {
                            throw new UnauthorizedAccessException("Access denied. Admin or Developer role required.");
                        }

                        await _localStorage.SetItemAsync("access_token", loginResponse.AccessToken);
                        await _localStorage.SetItemAsync("refresh_token", loginResponse.RefreshToken);
                        await _localStorage.SetItemAsync("user", loginResponse.User);
                        await _localStorage.SetItemAsync("token_expiry", loginResponse.ExpiresAt);

                        // Add Authorization header
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

                        AuthenticationStateChanged?.Invoke();
                        return loginResponse;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Login failed: {response.StatusCode} - {errorContent}");
                }
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Login error: {ex.Message}", ex);
            }

            return null;
        }

        public async Task LogoutAsync()
        {
            try
            {
                var refreshToken = await _localStorage.GetItemAsync<string>("refresh_token");

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    await _httpClient.PostAsJsonAsync("/api/auth/logout", new { RefreshToken = refreshToken });
                }
            }
            catch
            {
                // Continue with logout even if API call fails
            }
            finally
            {
                await _localStorage.RemoveItemAsync("access_token");
                await _localStorage.RemoveItemAsync("refresh_token");
                await _localStorage.RemoveItemAsync("user");
                await _localStorage.RemoveItemAsync("token_expiry");

                _httpClient.DefaultRequestHeaders.Authorization = null;
                AuthenticationStateChanged?.Invoke();
            }
        }

        public async Task<bool> RefreshTokenAsync()
        {
            try
            {
                var refreshToken = await _localStorage.GetItemAsync<string>("refresh_token");

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return false;
                }

                var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh",
                    new RefreshTokenDto { RefreshToken = refreshToken });

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponseDto>(_jsonOptions);

                    if (loginResponse != null)
                    {
                        await _localStorage.SetItemAsync("access_token", loginResponse.AccessToken);
                        await _localStorage.SetItemAsync("refresh_token", loginResponse.RefreshToken);
                        await _localStorage.SetItemAsync("user", loginResponse.User);
                        await _localStorage.SetItemAsync("token_expiry", loginResponse.ExpiresAt);

                        _httpClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

                        AuthenticationStateChanged?.Invoke();
                        return true;
                    }
                }
            }
            catch
            {
                // If refresh fails, logout
                await LogoutAsync();
            }

            return false;
        }

        public async Task<UserDto?> GetCurrentUserAsync()
        {
            try
            {
                return await _localStorage.GetItemAsync<UserDto>("user");
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                var token = await _localStorage.GetItemAsync<string>("access_token");
                var expiry = await _localStorage.GetItemAsync<DateTime?>("token_expiry");

                if (string.IsNullOrEmpty(token) || !expiry.HasValue)
                {
                    return false;
                }

                // Check if token is expired
                if (expiry.Value <= DateTime.UtcNow.AddMinutes(5)) // Refresh 5 minutes before expiry
                {
                    return await RefreshTokenAsync();
                }

                // Set authorization header if not already set
                if (_httpClient.DefaultRequestHeaders.Authorization == null)
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> GetTokenAsync()
        {
            try
            {
                return await _localStorage.GetItemAsync<string>("access_token");
            }
            catch
            {
                return null;
            }
        }
    }
}

