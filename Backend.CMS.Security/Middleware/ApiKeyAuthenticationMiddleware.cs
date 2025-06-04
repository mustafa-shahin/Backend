using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Backend.CMS.Security.Services;
using System.Security.Claims;

namespace Backend.CMS.Security.Middleware
{
    public class ApiKeyAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

        public ApiKeyAuthenticationMiddleware(RequestDelegate next, ILogger<ApiKeyAuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITokenService tokenService)
        {
            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();

            if (!string.IsNullOrEmpty(apiKey))
            {
                if (tokenService is TokenService enhancedTokenService)
                {
                    if (enhancedTokenService.ValidateApiKey(apiKey, out var principal))
                    {
                        context.User = principal!;
                        _logger.LogDebug("API key authentication successful for key ending in {KeySuffix}",
                            apiKey[^4..]);
                    }
                    else
                    {
                        _logger.LogWarning("Invalid API key attempted from {IpAddress}",
                            context.Connection.RemoteIpAddress);

                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Invalid API key");
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}