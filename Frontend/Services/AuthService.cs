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
                Console.WriteLine("Attempting login...");
                var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginDto);

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponseDto>(_jsonOptions);

                    if (loginResponse != null)
                    {
                        Console.WriteLine($"Login successful. User role: {loginResponse.User.Role}");

                        // Check if user has admin or dev role
                        if (loginResponse.User.Role != Backend.CMS.Domain.Enums.UserRole.Admin &&
                            loginResponse.User.Role != Backend.CMS.Domain.Enums.UserRole.Dev)
                        {
                            Console.WriteLine("Access denied: User does not have required role");
                            throw new UnauthorizedAccessException("Access denied. Admin or Developer role required.");
                        }

                        await _localStorage.SetItemAsync("access_token", loginResponse.AccessToken);
                        await _localStorage.SetItemAsync("refresh_token", loginResponse.RefreshToken);
                        await _localStorage.SetItemAsync("user", loginResponse.User);
                        await _localStorage.SetItemAsync("token_expiry", loginResponse.ExpiresAt);

                        // Add Authorization header
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

                        Console.WriteLine("Login completed successfully");
                        AuthenticationStateChanged?.Invoke();
                        return loginResponse;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Login failed: {response.StatusCode} - {errorContent}");
                    throw new HttpRequestException($"Login failed: {response.StatusCode} - {errorContent}");
                }
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("HTTP request exception during login");
                throw;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Login request timed out");
                throw new HttpRequestException("Login request timed out. Please check your connection.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected login error: {ex.Message}");
                throw new Exception($"Login error: {ex.Message}", ex);
            }

            return null;
        }

        public async Task LogoutAsync()
        {
            try
            {
                Console.WriteLine("Logging out...");
                var refreshToken = await _localStorage.GetItemAsync<string>("refresh_token");

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    try
                    {
                        await _httpClient.PostAsJsonAsync("/api/auth/logout", new { RefreshToken = refreshToken });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Logout API call failed: {ex.Message}");
                        // Continue with logout even if API call fails
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during logout: {ex.Message}");
            }
            finally
            {
                try
                {
                    await _localStorage.RemoveItemAsync("access_token");
                    await _localStorage.RemoveItemAsync("refresh_token");
                    await _localStorage.RemoveItemAsync("user");
                    await _localStorage.RemoveItemAsync("token_expiry");

                    _httpClient.DefaultRequestHeaders.Authorization = null;
                    Console.WriteLine("Logout completed");
                    AuthenticationStateChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error clearing storage during logout: {ex.Message}");
                }
            }
        }

        public async Task<bool> RefreshTokenAsync()
        {
            try
            {
                Console.WriteLine("Attempting token refresh...");
                var refreshToken = await _localStorage.GetItemAsync<string>("refresh_token");

                if (string.IsNullOrEmpty(refreshToken))
                {
                    Console.WriteLine("No refresh token available");
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

                        Console.WriteLine("Token refresh successful");
                        AuthenticationStateChanged?.Invoke();
                        return true;
                    }
                }
                else
                {
                    Console.WriteLine($"Token refresh failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Token refresh error: {ex.Message}");
            }

            // If refresh fails, logout
            await LogoutAsync();
            return false;
        }

        public async Task<UserDto?> GetCurrentUserAsync()
        {
            try
            {
                return await _localStorage.GetItemAsync<UserDto>("user");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting current user: {ex.Message}");
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
                    Console.WriteLine("No token or expiry found");
                    return false;
                }

                // Check if token is expired
                if (expiry.Value <= DateTime.UtcNow.AddMinutes(5)) // Refresh 5 minutes before expiry
                {
                    Console.WriteLine("Token expired or expiring soon, attempting refresh");
                    return await RefreshTokenAsync();
                }

                // Set authorization header if not already set
                if (_httpClient.DefaultRequestHeaders.Authorization == null)
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                Console.WriteLine("User is authenticated");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking authentication: {ex.Message}");
                return false;
            }
        }

        public async Task<string?> GetTokenAsync()
        {
            try
            {
                return await _localStorage.GetItemAsync<string>("access_token");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting token: {ex.Message}");
                return null;
            }
        }
    }
}