using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Backend.CMS.Audit.Services;
using System.Security.Claims;
using System.Text;

namespace Backend.CMS.Security.Middleware
{
    public class SecurityAuditMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityAuditMiddleware> _logger;

        public SecurityAuditMiddleware(RequestDelegate next, ILogger<SecurityAuditMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAuditService auditService)
        {
            var startTime = DateTime.UtcNow;
            var originalResponseBody = context.Response.Body;

            try
            {
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                await _next(context);

                var duration = DateTime.UtcNow - startTime;
                await LogRequestAsync(context, auditService, duration, true);

                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalResponseBody);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                await LogRequestAsync(context, auditService, duration, false, ex);
                throw;
            }
            finally
            {
                context.Response.Body = originalResponseBody;
            }
        }

        private async Task LogRequestAsync(HttpContext context, IAuditService auditService, TimeSpan duration, bool success, Exception? exception = null)
        {
            var request = context.Request;
            var response = context.Response;
            var user = context.User;

            // Only log significant requests
            if (!ShouldLogRequest(request))
                return;

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var requestDetails = new
            {
                Method = request.Method,
                Path = request.Path.Value,
                QueryString = request.QueryString.Value,
                StatusCode = response.StatusCode,
                Duration = duration.TotalMilliseconds,
                Success = success,
                UserAgent = request.Headers["User-Agent"].ToString(),
                IpAddress = GetClientIpAddress(context),
                TenantId = context.Items["TenantId"]?.ToString(),
                ErrorMessage = exception?.Message
            };

            if (IsSensitiveOperation(request))
            {
                await auditService.LogSecurityEventAsync(
                    GetSecurityEventType(request),
                    $"Sensitive operation: {request.Method} {request.Path}",
                    userId);
            }
            else if (IsFailedRequest(response.StatusCode))
            {
                await auditService.LogSecurityEventAsync(
                    "failed_request",
                    $"Failed request: {response.StatusCode} for {request.Method} {request.Path}",
                    userId);
            }

            _logger.LogInformation("Request: {Method} {Path} - {StatusCode} in {Duration}ms by user {UserId}",
                request.Method, request.Path, response.StatusCode, duration.TotalMilliseconds, userId);
        }

        private bool ShouldLogRequest(HttpRequest request)
        {
            var path = request.Path.Value?.ToLowerInvariant() ?? "";

            // Skip health checks, metrics, static files
            if (path.StartsWith("/health") ||
                path.StartsWith("/metrics") ||
                path.StartsWith("/swagger") ||
                path.StartsWith("/favicon") ||
                path.Contains("/static/"))
                return false;

            // Log API calls and auth requests
            return path.StartsWith("/api/") || path.StartsWith("/auth/");
        }

        private bool IsSensitiveOperation(HttpRequest request)
        {
            var path = request.Path.Value?.ToLowerInvariant() ?? "";
            var method = request.Method.ToUpperInvariant();

            return path.StartsWith("/api/auth/") ||
                   path.Contains("/password") ||
                   path.Contains("/permissions") ||
                   path.Contains("/roles") ||
                   (method == "DELETE" && path.StartsWith("/api/")) ||
                   (method == "POST" && path.Contains("/admin/"));
        }

        private string GetSecurityEventType(HttpRequest request)
        {
            var path = request.Path.Value?.ToLowerInvariant() ?? "";
            var method = request.Method.ToUpperInvariant();

            if (path.StartsWith("/api/auth/login"))
                return "login_attempt";
            if (path.StartsWith("/api/auth/"))
                return "auth_operation";
            if (path.Contains("/password"))
                return "password_operation";
            if (path.Contains("/permissions") || path.Contains("/roles"))
                return "permission_operation";
            if (method == "DELETE")
                return "delete_operation";
            if (path.Contains("/admin/"))
                return "admin_operation";

            return "sensitive_operation";
        }

        private bool IsFailedRequest(int statusCode)
        {
            return statusCode >= 400;
        }

        private string GetClientIpAddress(HttpContext context)
        {
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}