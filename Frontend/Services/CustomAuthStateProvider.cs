using Backend.CMS.Domain.Enums;
using Frontend.Interface;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Frontend.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IAuthService _authService;
        private ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

        public CustomAuthStateProvider(IAuthService authService)
        {
            _authService = authService;
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                if (!await _authService.IsAuthenticatedAsync())
                {
                    return new AuthenticationState(_anonymous);
                }

                var user = await _authService.GetCurrentUserAsync();
                if (user == null)
                {
                    return new AuthenticationState(_anonymous);
                }

                // Check if user has required role (Admin or Dev)
                if (user.Role != UserRole.Admin && user.Role != UserRole.Dev)
                {
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
                return new AuthenticationState(claimsPrincipal);
            }
            catch
            {
                return new AuthenticationState(_anonymous);
            }
        }

        private void OnAuthenticationStateChanged()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public void NotifyUserAuthentication()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public void NotifyUserLogout()
        {
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
        }
    }
}

