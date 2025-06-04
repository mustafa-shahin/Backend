namespace Backend.CMS.API.Middleware
{
    public class CustomerTenantMiddleware : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            // Extract tenant from subdomain
            var host = context.Request.Host.Host;
            var subdomain = host.Split('.').FirstOrDefault();

            if (!string.IsNullOrEmpty(subdomain) && subdomain != "www" && subdomain != "localhost")
            {
                context.Items["TenantId"] = subdomain;
            }

            await next(context);
        }
    }
}
