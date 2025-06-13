using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.API.Middleware
{
    public class SessionManagementMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SessionManagementMiddleware> _logger;

        public SessionManagementMiddleware(RequestDelegate next, ILogger<SessionManagementMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IUserSessionService sessionService)
        {
            try
            {
                // Ensure session is started
                await context.Session.LoadAsync();

                // Initialize session if user is authenticated
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    // This will load the session and cache it properly
                    var session = await sessionService.GetCurrentSessionAsync();
                    if (session == null)
                    {
                        _logger.LogWarning("Failed to initialize session for authenticated user");
                    }
                    else
                    {
                        // Update last activity
                        session.UpdateLastActivity();
                    }
                }

                await _next(context);

                // Update session after request processing
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    try
                    {
                        sessionService.UpdateLastActivity();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update session activity");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in session management middleware");

                // Continue processing even if session management fails
                await _next(context);
            }
        }
    }

    // Extension method to register the middleware
    public static class SessionManagementMiddlewareExtensions
    {
        public static IApplicationBuilder UseSessionManagement(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SessionManagementMiddleware>();
        }
    }
}