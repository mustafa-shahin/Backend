using Backend.CMS.API.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Net.Http;

namespace Backend.CMS.API.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;

        public RateLimitingMiddleware(RequestDelegate next, IMemoryCache cache, IConfiguration configuration)
        {
            _next = next;
            _cache = cache;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var key = GetClientKey(context);
            var limit = _configuration.GetValue<int>("RateLimit:RequestsPerMinute", 100);

            var requestCount = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return 0;
            });

            if (requestCount >= limit)
            {
                context.Response.StatusCode = 429;
                context.Response.ContentType = "application/json";

                var errorResponse = new ApiErrorResponse
                {
                    Message = "Rate limit exceeded",
                    Details = $"Maximum {limit} requests per minute allowed",
                    StatusCode = 429
                };

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
                return;
            }

            _cache.Set(key, requestCount + 1, TimeSpan.FromMinutes(1));
            await _next(context);
        }

        private string GetClientKey(HttpContext context)
        {
            var clientId = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            return $"{clientId}:{userAgent.GetHashCode()}";
        }
    }
}