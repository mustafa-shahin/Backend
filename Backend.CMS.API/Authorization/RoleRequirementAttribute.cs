using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Backend.CMS.API.Authorization
{
    public class RoleRequirementAttribute : AuthorizeAttribute
    {
        public RoleRequirementAttribute(params UserRole[] roles)
        {
            Roles = string.Join(",", roles.Select(r => r.ToString()));
        }
    }

    public class DevOnlyAttribute : RoleRequirementAttribute
    {
        public DevOnlyAttribute() : base(UserRole.Dev) { }
    }

    public class AdminOrDevAttribute : RoleRequirementAttribute
    {
        public AdminOrDevAttribute() : base(UserRole.Admin, UserRole.Dev) { }
    }
}