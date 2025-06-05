namespace Backend.CMS.Interfaces.Interfaces
{
    public interface ITenantService
    {
        string? GetCurrentTenantId();
        Task<string> GetConnectionStringAsync(string tenantId);
    }
}
