using Microsoft.Extensions.Caching.Memory;

namespace Backend.CMS.API.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;

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
                await context.Response.WriteAsync("Rate limit exceeded");
                return;
            }

            _cache.Set(key, requestCount + 1, TimeSpan.FromMinutes(1));
            await _next(context);
        }
    }
}
