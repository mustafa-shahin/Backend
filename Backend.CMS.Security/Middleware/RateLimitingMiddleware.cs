using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Backend.CMS.Security.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        public RateLimitingMiddleware(
            RequestDelegate next,
            IMemoryCache cache,
            IConfiguration configuration,
            ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientKey = GetClientKey(context);
            var endpoint = GetEndpoint(context);
            var isExempt = IsExemptFromRateLimit(context);

            if (isExempt)
            {
                await _next(context);
                return;
            }

            var limit = GetRateLimit(endpoint);
            var windowSize = GetWindowSize(endpoint);

            if (!await CheckRateLimitAsync(clientKey, endpoint, limit, windowSize))
            {
                await HandleRateLimitExceeded(context, clientKey, endpoint, limit);
                return;
            }

            await _next(context);
        }

        private async Task<bool> CheckRateLimitAsync(string clientKey, string endpoint, int limit, TimeSpan windowSize)
        {
            var key = $"rate_limit:{clientKey}:{endpoint}";
            var bucketKey = $"bucket:{key}";
            var countKey = $"count:{key}";

            var currentWindow = DateTimeOffset.UtcNow.Ticks / windowSize.Ticks;
            var storedWindow = _cache.Get<long?>($"window:{key}");

            if (storedWindow != currentWindow)
            {
                // New window, reset counter
                _cache.Set($"window:{key}", currentWindow, windowSize);
                _cache.Set(countKey, 1, windowSize);
                return true;
            }

            var currentCount = _cache.Get<int>(countKey);
            if (currentCount >= limit)
            {
                return false;
            }

            _cache.Set(countKey, currentCount + 1, windowSize);
            return true;
        }

        private async Task HandleRateLimitExceeded(HttpContext context, string clientKey, string endpoint, int limit)
        {
            var retryAfter = GetRetryAfter(endpoint);

            context.Response.StatusCode = 429;
            context.Response.ContentType = "application/json";
            context.Response.Headers.Add("Retry-After", retryAfter.ToString());
            context.Response.Headers.Add("X-RateLimit-Limit", limit.ToString());
            context.Response.Headers.Add("X-RateLimit-Remaining", "0");
            context.Response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.Add(retryAfter).ToUnixTimeSeconds().ToString());

            var errorResponse = new
            {
                Error = "Rate limit exceeded",
                Message = $"Maximum {limit} requests per minute allowed for this endpoint",
                StatusCode = 429,
                Timestamp = DateTime.UtcNow,
                RetryAfter = retryAfter.TotalSeconds
            };

            _logger.LogWarning("Rate limit exceeded for client {ClientKey} on endpoint {Endpoint}", clientKey, endpoint);

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
        }

        private string GetClientKey(HttpContext context)
        {
            // Try to get authenticated user first
            var userId = context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            // Fall back to IP + User Agent
            var ip = GetClientIpAddress(context);
            var userAgent = context.Request.Headers["User-Agent"].ToString();

            // Create a hash to avoid storing potentially long user agent strings
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{ip}:{userAgent}"));
            var hashString = Convert.ToBase64String(hash)[..16]; // Take first 16 chars

            return $"ip:{ip}:ua:{hashString}";
        }

        private string GetClientIpAddress(HttpContext context)
        {
            // Check for forwarded IP first (for load balancers/proxies)
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

        private string GetEndpoint(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "/";
            var method = context.Request.Method.ToUpperInvariant();

            // Group similar endpoints
            if (path.StartsWith("/api/auth/"))
                return "auth";
            if (path.StartsWith("/api/pages/"))
                return "pages";
            if (path.StartsWith("/api/"))
                return "api";

            return $"{method}:{path}";
        }

        private int GetRateLimit(string endpoint)
        {
            var endpointLimits = _configuration.GetSection("RateLimit:EndpointLimits").Get<Dictionary<string, int>>();

            if (endpointLimits?.TryGetValue(endpoint, out var specificLimit) == true)
            {
                return specificLimit;
            }

            return endpoint switch
            {
                "auth" => _configuration.GetValue<int>("RateLimit:AuthRequestsPerMinute", 10),
                "pages" => _configuration.GetValue<int>("RateLimit:PagesRequestsPerMinute", 50),
                "api" => _configuration.GetValue<int>("RateLimit:ApiRequestsPerMinute", 100),
                _ => _configuration.GetValue<int>("RateLimit:RequestsPerMinute", 100)
            };
        }

        private TimeSpan GetWindowSize(string endpoint)
        {
            return endpoint switch
            {
                "auth" => TimeSpan.FromMinutes(1),
                _ => TimeSpan.FromMinutes(1)
            };
        }

        private TimeSpan GetRetryAfter(string endpoint)
        {
            return endpoint switch
            {
                "auth" => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromMinutes(1)
            };
        }

        private bool IsExemptFromRateLimit(HttpContext context)
        {
            var ip = GetClientIpAddress(context);
            var exemptedIPs = _configuration.GetSection("RateLimit:ExemptedIPs").Get<string[]>() ?? Array.Empty<string>();

            return exemptedIPs.Contains(ip) ||
                   context.User.IsInRole("Admin") ||
                   context.Request.Headers.ContainsKey("X-API-Key"); // API keys get different limits
        }
    }
}
