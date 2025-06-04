using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Backend.CMS.Security.Middleware
{
    public class TenantSecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantSecurityMiddleware> _logger;

        public TenantSecurityMiddleware(RequestDelegate next, ILogger<TenantSecurityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Validate tenant access for authenticated requests
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userTenantId = context.User.FindFirst("TenantId")?.Value;
                var requestTenantId = context.Items["TenantId"]?.ToString();

                if (!string.IsNullOrEmpty(userTenantId) &&
                    !string.IsNullOrEmpty(requestTenantId) &&
                    userTenantId != requestTenantId)
                {
                    _logger.LogWarning("Tenant mismatch detected: User tenant {UserTenant} vs Request tenant {RequestTenant} for user {UserId}",
                        userTenantId, requestTenantId, context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Access denied: Tenant mismatch");
                    return;
                }
            }

            await _next(context);
        }
    }
}