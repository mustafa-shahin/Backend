
namespace Backend.CMS.Application.Common.Interfaces
{
    public interface ITenantService
    {
        string? GetCurrentTenantId();
        Task<string> GetConnectionStringAsync(string tenantId);
    }
}
