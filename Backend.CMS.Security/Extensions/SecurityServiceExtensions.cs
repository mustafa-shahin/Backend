using Backend.CMS.Interfaces.Interfaces;
using Backend.CMS.Security.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.CMS.Security.Extensions
{
    public static class SecurityServiceExtensions
    {
        public static IServiceCollection AddSecurityServices(this IServiceCollection services)
        {
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IPasswordService, PasswordService>();

            return services;
        }
    }
}