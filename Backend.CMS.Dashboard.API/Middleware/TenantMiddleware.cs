using Microsoft.EntityFrameworkCore;
using Backend.CMS.Infrastructure.Data;

namespace Backend.CMS.Dashboard.API.Middleware
{
    public class TenantMiddleware : IMiddleware
    {
        private readonly IServiceProvider _serviceProvider;

        public TenantMiddleware(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            // Try to get tenant from header
            var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();

            // If not in header, try to get from subdomain
            if (string.IsNullOrEmpty(tenantId))
            {
                var host = context.Request.Host.Host;
                var subdomain = host.Split('.').FirstOrDefault();

                if (!string.IsNullOrEmpty(subdomain) && subdomain != "www")
                {
                    tenantId = subdomain;
                }
            }

            // If still not found, try to get from user claims
            if (string.IsNullOrEmpty(tenantId) && context.User.Identity?.IsAuthenticated == true)
            {
                tenantId = context.User.FindFirst("TenantId")?.Value;
            }

            if (!string.IsNullOrEmpty(tenantId))
            {
                try
                {
                    // Create a scope to get the master context
                    using var scope = _serviceProvider.CreateScope();
                    var masterContext = scope.ServiceProvider.GetService<MasterDbContext>();

                    if (masterContext != null)
                    {
                        // Validate tenant exists
                        var tenantExists = await masterContext.Tenants
                            .AnyAsync(t => t.Identifier == tenantId && t.IsActive);

                        if (tenantExists)
                        {
                            context.Items["TenantId"] = tenantId;
                        }
                    }
                }
                catch
                {
                    // If we can't connect to master DB (e.g., during migrations), continue without tenant validation
                }
            }

            await next(context);
        }
    }
}