using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Versioning.Entities;
using System.Text.RegularExpressions;

namespace Backend.CMS.Versioning.Services
{
    public class VersioningService : IVersioningService
    {
        private readonly MasterDbContext _context;
        private readonly ILogger<VersioningService> _logger;
        private readonly IDeploymentExecutor _deploymentExecutor;

        public VersioningService(
            MasterDbContext context,
            ILogger<VersioningService> logger,
            IDeploymentExecutor deploymentExecutor)
        {
            _context = context;
            _logger = logger;
            _deploymentExecutor = deploymentExecutor;
        }

        public async Task<DeploymentVersion?> GetCurrentVersionAsync(string tenantId)
        {
            var tenant = await _context.Tenants
                .Include(t => t.Deployments)
                .ThenInclude(d => d.DeploymentVersion)
                .FirstOrDefaultAsync(t => t.Identifier == tenantId);

            if (tenant == null) return null;

            var currentDeployment = tenant.Deployments
                .Where(d => d.Status == DeploymentStatus.Completed)
                .OrderByDescending(d => d.DeployedAt)
                .FirstOrDefault();

            return currentDeployment?.DeploymentVersion;
        }

        public async Task<DeploymentVersion?> GetLatestVersionAsync()
        {
            return await _context.Set<DeploymentVersion>()
                .Where(v => v.IsActive)
                .OrderByDescending(v => v.ReleasedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<DeploymentVersion>> GetAvailableUpdatesAsync(string tenantId)
        {
            var currentVersion = await GetCurrentVersionAsync(tenantId);
            if (currentVersion == null) return Enumerable.Empty<DeploymentVersion>();

            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Identifier == tenantId);

            if (tenant == null || !tenant.AutoUpdate)
                return Enumerable.Empty<DeploymentVersion>();

            return await _context.Set<DeploymentVersion>()
                .Where(v => v.IsActive && v.ReleasedAt > currentVersion.ReleasedAt)
                .OrderBy(v => v.ReleasedAt)
                .ToListAsync();
        }

        public async Task<TenantDeployment> ScheduleDeploymentAsync(
            string tenantId,
            string targetVersion,
            DeploymentStrategy strategy,
            DateTime? scheduledFor = null)
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Identifier == tenantId);

            if (tenant == null)
                throw new InvalidOperationException($"Tenant {tenantId} not found");

            var targetVersionEntity = await _context.Set<DeploymentVersion>()
                .FirstOrDefaultAsync(v => v.Version == targetVersion);

            if (targetVersionEntity == null)
                throw new InvalidOperationException($"Version {targetVersion} not found");

            var currentVersion = await GetCurrentVersionAsync(tenantId);

            var deployment = new TenantDeployment
            {
                TenantId = tenant.Id,
                DeploymentVersionId = targetVersionEntity.Id,
                CurrentVersion = currentVersion?.Version ?? "0.0.0",
                TargetVersion = targetVersion,
                Status = strategy == DeploymentStrategy.Scheduled
                    ? DeploymentStatus.Scheduled
                    : DeploymentStatus.Pending,
                Strategy = strategy,
                ScheduledFor = scheduledFor,
                AutoUpdate = tenant.AutoUpdate
            };

            _context.Set<TenantDeployment>().Add(deployment);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Scheduled deployment for tenant {TenantId} from version {CurrentVersion} to {TargetVersion}",
                tenantId, deployment.CurrentVersion, targetVersion);

            return deployment;
        }

        public async Task<TenantDeployment> ExecuteDeploymentAsync(Guid deploymentId)
        {
            var deployment = await _context.Set<TenantDeployment>()
                .Include(d => d.Tenant)
                .Include(d => d.DeploymentVersion)
                .FirstOrDefaultAsync(d => d.Id == deploymentId);

            if (deployment == null)
                throw new InvalidOperationException($"Deployment {deploymentId} not found");

            if (deployment.Status != DeploymentStatus.Pending &&
                deployment.Status != DeploymentStatus.Scheduled)
            {
                throw new InvalidOperationException(
                    $"Cannot execute deployment in status {deployment.Status}");
            }

            try
            {
                deployment.Status = DeploymentStatus.InProgress;
                await _context.SaveChangesAsync();

                // Execute deployment using the deployment executor
                await _deploymentExecutor.ExecuteAsync(deployment);

                deployment.Status = DeploymentStatus.Completed;
                deployment.DeployedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Successfully deployed version {Version} to tenant {TenantId}",
                    deployment.TargetVersion, deployment.Tenant.Identifier);

                return deployment;
            }
            catch (Exception ex)
            {
                deployment.Status = DeploymentStatus.Failed;
                deployment.DeploymentLog["error"] = ex.Message;
                deployment.DeploymentLog["stackTrace"] = ex.StackTrace ?? "";

                await _context.SaveChangesAsync();

                _logger.LogError(ex,
                    "Failed to deploy version {Version} to tenant {TenantId}",
                    deployment.TargetVersion, deployment.Tenant.Identifier);

                throw;
            }
        }

        public async Task<TenantDeployment> RollbackDeploymentAsync(Guid deploymentId)
        {
            var deployment = await _context.Set<TenantDeployment>()
                .Include(d => d.Tenant)
                .Include(d => d.DeploymentVersion)
                .FirstOrDefaultAsync(d => d.Id == deploymentId);

            if (deployment == null)
                throw new InvalidOperationException($"Deployment {deploymentId} not found");

            if (deployment.Status != DeploymentStatus.Completed &&
                deployment.Status != DeploymentStatus.Failed)
            {
                throw new InvalidOperationException(
                    $"Cannot rollback deployment in status {deployment.Status}");
            }

            try
            {
                // Execute rollback using the deployment executor
                await _deploymentExecutor.RollbackAsync(deployment);

                deployment.Status = DeploymentStatus.RolledBack;
                deployment.DeploymentLog["rolledBackAt"] = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Successfully rolled back deployment {DeploymentId} for tenant {TenantId}",
                    deploymentId, deployment.Tenant.Identifier);

                return deployment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to rollback deployment {DeploymentId} for tenant {TenantId}",
                    deploymentId, deployment.Tenant.Identifier);

                throw;
            }
        }

        public async Task<bool> CanUpgradeAsync(string tenantId, string targetVersion)
        {
            var currentVersion = await GetCurrentVersionAsync(tenantId);
            if (currentVersion == null) return true;

            var targetVersionEntity = await _context.Set<DeploymentVersion>()
                .FirstOrDefaultAsync(v => v.Version == targetVersion);

            if (targetVersionEntity == null) return false;

            // Check if minimum required version is met
            if (!string.IsNullOrEmpty(targetVersionEntity.MinimumRequiredVersion))
            {
                return CompareVersions(currentVersion.Version, targetVersionEntity.MinimumRequiredVersion) >= 0;
            }

            return true;
        }

        public async Task<IEnumerable<TenantDeployment>> GetDeploymentHistoryAsync(string tenantId)
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Identifier == tenantId);

            if (tenant == null) return Enumerable.Empty<TenantDeployment>();

            return await _context.Set<TenantDeployment>()
                .Include(d => d.DeploymentVersion)
                .Where(d => d.TenantId == tenant.Id)
                .OrderByDescending(d => d.CreatedOn)
                .ToListAsync();
        }

        public async Task SetAutoUpdateAsync(string tenantId, bool enabled)
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Identifier == tenantId);

            if (tenant == null)
                throw new InvalidOperationException($"Tenant {tenantId} not found");

            tenant.AutoUpdate = enabled;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Auto-update {Status} for tenant {TenantId}",
                enabled ? "enabled" : "disabled", tenantId);
        }

        public async Task<Dictionary<string, object>> GetVersionFeaturesAsync(string version)
        {
            var versionEntity = await _context.Set<DeploymentVersion>()
                .FirstOrDefaultAsync(v => v.Version == version);

            return versionEntity?.Features ?? new Dictionary<string, object>();
        }

        private int CompareVersions(string version1, string version2)
        {
            var v1 = new Version(version1);
            var v2 = new Version(version2);
            return v1.CompareTo(v2);
        }
    }
}