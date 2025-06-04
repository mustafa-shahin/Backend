using System.Security.Claims;

namespace Backend.CMS.API.Telemetry
{
    public class CustomTelemetryInitializer : ITelemetryInitializer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public void Initialize(ITelemetry telemetry)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context != null)
            {
                telemetry.Context.Properties["TenantId"] = context.Items["TenantId"]?.ToString();
                telemetry.Context.Properties["UserId"] = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }
        }
    }
}
