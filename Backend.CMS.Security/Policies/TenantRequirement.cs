using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Security.Policies
{
    public class TenantRequirement : IAuthorizationRequirement
    {
        public string RequiredTenant { get; }

        public TenantRequirement(string requiredTenant)
        {
            RequiredTenant = requiredTenant;
        }
    }
}
