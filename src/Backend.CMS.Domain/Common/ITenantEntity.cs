using System;

namespace Backend.CMS.Domain.Common
{
    public interface ITenantEntity
    {
        string TenantId { get; set; }
    }
}
