using Backend.CMS.Domain.Enums;
using Frontend.Interface;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Frontend.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IAuthService _authService;
        private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());
        private bool _isInitialized = false;

        public CustomAuthStateProvider(IAuthService authService)
        {
            _authService = authService;
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // Prevent multiple rapid calls during initialization
                if (!_isInitialized)
                {
                    await Task.Delay(50); // Small delay to prevent race conditions
                    _isInitialized = true;
                }

                Console.WriteLine("Getting authentication state...");

                if (!await _authService.IsAuthenticatedAsync())
                {
                    Console.WriteLine("User is not authenticated");
                    return new AuthenticationState(_anonymous);
                }

                var user = await _authService.GetCurrentUserAsync();
                if (user == null)
                {
                    Console.WriteLine("User data not found");
                    return new AuthenticationState(_anonymous);
                }

                Console.WriteLine($"User authenticated: {user.Username}, Role: {user.Role}");

                // Check if user has required role (Admin or Dev)
                if (user.Role != UserRole.Admin && user.Role != UserRole.Dev)
                {
                    Console.WriteLine($"User role {user.Role} is not authorized");
                    return new AuthenticationState(_anonymous);
                }

                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.GivenName, user.FirstName),
                    new Claim(ClaimTypes.Surname, user.LastName),
                    new Claim(ClaimTypes.Role, user.Role.ToString()),
                    new Claim("userId", user.Id.ToString()),
                    new Claim("firstName", user.FirstName),
                    new Claim("lastName", user.LastName),
                    new Claim("role", user.Role.ToString())
                }, "Bearer");

                var claimsPrincipal = new ClaimsPrincipal(identity);
                Console.WriteLine("Authentication state created successfully");
                return new AuthenticationState(claimsPrincipal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting authentication state: {ex.Message}");
                return new AuthenticationState(_anonymous);
            }
        }

        private void OnAuthenticationStateChanged()
        {
            try
            {
                Console.WriteLine("Authentication state changed event triggered");
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error notifying authentication state change: {ex.Message}");
            }
        }

        public void NotifyUserAuthentication()
        {
            try
            {
                Console.WriteLine("Notifying user authentication");
                _isInitialized = false; // Reset to ensure fresh state check
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error notifying user authentication: {ex.Message}");
            }
        }

        public void NotifyUserLogout()
        {
            try
            {
                Console.WriteLine("Notifying user logout");
                _isInitialized = false; // Reset state
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error notifying user logout: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _authService.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }
    }
}