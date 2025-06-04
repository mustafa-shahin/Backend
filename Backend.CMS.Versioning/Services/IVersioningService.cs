using Backend.CMS.Versioning.Entities;

namespace Backend.CMS.Versioning.Services
{
    public interface IVersioningService
    {
        Task<DeploymentVersion?> GetCurrentVersionAsync(string tenantId);
        Task<DeploymentVersion?> GetLatestVersionAsync();
        Task<IEnumerable<DeploymentVersion>> GetAvailableUpdatesAsync(string tenantId);
        Task<TenantDeployment> ScheduleDeploymentAsync(string tenantId, string targetVersion, DeploymentStrategy strategy, DateTime? scheduledFor = null);
        Task<TenantDeployment> ExecuteDeploymentAsync(Guid deploymentId);
        Task<TenantDeployment> RollbackDeploymentAsync(Guid deploymentId);
        Task<bool> CanUpgradeAsync(string tenantId, string targetVersion);
        Task<IEnumerable<TenantDeployment>> GetDeploymentHistoryAsync(string tenantId);
        Task SetAutoUpdateAsync(string tenantId, bool enabled);
        Task<Dictionary<string, object>> GetVersionFeaturesAsync(string version);
    }
}