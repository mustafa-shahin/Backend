using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;


namespace Backend.CMS.Security.Policies
{

    public class TenantAuthorizationHandler : AuthorizationHandler<TenantRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            TenantRequirement requirement)
        {
            var userTenantId = context.User.FindFirst("TenantId")?.Value;
            var currentTenantId = _httpContextAccessor.HttpContext?.Items["TenantId"]?.ToString();

            if (string.IsNullOrEmpty(userTenantId) || string.IsNullOrEmpty(currentTenantId))
            {
                context.Fail();
                return Task.CompletedTask;
            }

            if (userTenantId == currentTenantId || userTenantId == requirement.RequiredTenant)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }

            return Task.CompletedTask;
        }
    }


    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        // This would typically check against a permission service
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var permissions = context.User.FindAll("permission");
            var requiredPermission = $"{requirement.Resource}:{requirement.Action}";

            if (permissions.Any(p => p.Value == requiredPermission) ||
                context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }

            return Task.CompletedTask;
        }
    }
}