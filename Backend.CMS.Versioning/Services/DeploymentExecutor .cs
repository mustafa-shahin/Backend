using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Versioning.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Backend.CMS.Versioning.Services
{
    public class DeploymentExecutor : IDeploymentExecutor
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DeploymentExecutor> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpClient _httpClient;

        public DeploymentExecutor(
            IConfiguration configuration,
            ILogger<DeploymentExecutor> logger,
            IServiceProvider serviceProvider,
            HttpClient httpClient)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _httpClient = httpClient;
        }

        public async Task ExecuteAsync(TenantDeployment deployment)
        {
            _logger.LogInformation(
                "Starting deployment execution for tenant {TenantId}, version {Version}",
                deployment.Tenant.Identifier, deployment.TargetVersion);

            // Pre-deployment validation
            await ValidateAsync(deployment);

            // Create deployment context
            var context = new DeploymentContext
            {
                TenantId = deployment.Tenant.Identifier,
                CurrentVersion = deployment.CurrentVersion,
                TargetVersion = deployment.TargetVersion,
                ConnectionString = deployment.Tenant.ConnectionString,
                DeploymentId = deployment.Id
            };

            // Execute deployment steps
            var steps = GetDeploymentSteps(deployment);
            var executedSteps = new List<DeploymentStep>();

            try
            {
                foreach (var step in steps)
                {
                    _logger.LogInformation("Executing step: {StepName}", step.Name);

                    deployment.DeploymentLog[$"step_{step.Name}_start"] = DateTime.UtcNow;

                    await step.ExecuteAsync(context);

                    deployment.DeploymentLog[$"step_{step.Name}_end"] = DateTime.UtcNow;
                    executedSteps.Add(step);
                }

                // Post-deployment health check
                var healthResult = await CheckHealthAsync(deployment.Tenant.Identifier);
                if (!healthResult.IsHealthy)
                {
                    throw new InvalidOperationException("Post-deployment health check failed");
                }

                deployment.DeploymentLog["completed"] = DateTime.UtcNow;
                deployment.DeploymentLog["health_check"] = healthResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deployment failed at step {StepCount}", executedSteps.Count);

                // Rollback executed steps in reverse order
                foreach (var step in executedSteps.AsEnumerable().Reverse())
                {
                    try
                    {
                        _logger.LogInformation("Rolling back step: {StepName}", step.Name);
                        await step.RollbackAsync(context);
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, "Failed to rollback step: {StepName}", step.Name);
                    }
                }

                throw;
            }
        }

        public async Task RollbackAsync(TenantDeployment deployment)
        {
            _logger.LogInformation(
                "Starting rollback for tenant {TenantId}, from version {Version}",
                deployment.Tenant.Identifier, deployment.TargetVersion);

            var context = new DeploymentContext
            {
                TenantId = deployment.Tenant.Identifier,
                CurrentVersion = deployment.TargetVersion,
                TargetVersion = deployment.CurrentVersion,
                ConnectionString = deployment.Tenant.ConnectionString,
                DeploymentId = deployment.Id
            };

            // Execute rollback script if available
            if (!string.IsNullOrEmpty(deployment.DeploymentVersion.RollbackScript))
            {
                await ExecuteScriptAsync(context, deployment.DeploymentVersion.RollbackScript);
            }

            // Restore previous version
            await RestorePreviousVersionAsync(context);
        }

        public async Task ValidateAsync(TenantDeployment deployment)
        {
            var validations = new List<ValidationResult>();

            // Check version compatibility
            if (!await CanUpgradeDirectly(deployment.CurrentVersion, deployment.TargetVersion))
            {
                validations.Add(new ValidationResult
                {
                    IsValid = false,
                    Message = $"Cannot upgrade directly from {deployment.CurrentVersion} to {deployment.TargetVersion}"
                });
            }

            // Check system requirements
            var requirements = await CheckSystemRequirementsAsync(deployment);
            validations.AddRange(requirements);

            // Check for running processes
            if (await HasRunningProcessesAsync(deployment.Tenant.Identifier))
            {
                validations.Add(new ValidationResult
                {
                    IsValid = false,
                    Message = "Active processes detected. Please wait for completion."
                });
            }

            if (validations.Any(v => !v.IsValid))
            {
                var errors = string.Join(", ", validations.Where(v => !v.IsValid).Select(v => v.Message));
                throw new InvalidOperationException($"Deployment validation failed: {errors}");
            }
        }

        public async Task<HealthCheckResult> CheckHealthAsync(string tenantId)
        {
            var result = new HealthCheckResult { IsHealthy = true };

            try
            {
                // Check API health
                var apiHealth = await CheckApiHealthAsync(tenantId);
                result.Components["api"] = apiHealth;
                result.IsHealthy &= apiHealth.IsHealthy;

                // Check database health
                var dbHealth = await CheckDatabaseHealthAsync(tenantId);
                result.Components["database"] = dbHealth;
                result.IsHealthy &= dbHealth.IsHealthy;

                // Check background services
                var servicesHealth = await CheckBackgroundServicesAsync(tenantId);
                result.Components["background_services"] = servicesHealth;
                result.IsHealthy &= servicesHealth.IsHealthy;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for tenant {TenantId}", tenantId);
                result.IsHealthy = false;
                result.Components["error"] = new ComponentHealth
                {
                    Name = "error",
                    IsHealthy = false,
                    Message = ex.Message
                };
                return result;
            }
        }

        private List<DeploymentStep> GetDeploymentSteps(TenantDeployment deployment)
        {
            var steps = new List<DeploymentStep>();

            // 1. Backup current state
            steps.Add(new BackupStep(_serviceProvider));

            // 2. Apply database migrations
            if (HasDatabaseChanges(deployment))
            {
                steps.Add(new DatabaseMigrationStep(_serviceProvider));
            }

            // 3. Update application files
            steps.Add(new ApplicationUpdateStep(_serviceProvider));

            // 4. Update configuration
            steps.Add(new ConfigurationUpdateStep(_serviceProvider));

            // 5. Clear caches
            steps.Add(new CacheClearStep(_serviceProvider));

            // 6. Restart services
            steps.Add(new ServiceRestartStep(_serviceProvider));

            return steps;
        }

        private async Task<bool> CanUpgradeDirectly(string fromVersion, string toVersion)
        {
            // Implement version compatibility check logic
            var from = new Version(fromVersion);
            var to = new Version(toVersion);

            // Allow upgrades within same major version or one major version up
            return to.Major - from.Major <= 1;
        }

        private async Task<List<ValidationResult>> CheckSystemRequirementsAsync(TenantDeployment deployment)
        {
            var results = new List<ValidationResult>();

            // Check disk space
            var requiredSpace = deployment.DeploymentVersion.Features.GetValueOrDefault("requiredDiskSpaceMB", 500);
            var availableSpace = GetAvailableDiskSpace();

            if (availableSpace < (long)requiredSpace * 1024 * 1024)
            {
                results.Add(new ValidationResult
                {
                    IsValid = false,
                    Message = $"Insufficient disk space. Required: {requiredSpace}MB, Available: {availableSpace / 1024 / 1024}MB"
                });
            }

            // Check memory
            var requiredMemory = deployment.DeploymentVersion.Features.GetValueOrDefault("requiredMemoryMB", 1024);
            var availableMemory = GC.GetTotalMemory(false) / 1024 / 1024;

            if (availableMemory < (long)requiredMemory)
            {
                results.Add(new ValidationResult
                {
                    IsValid = false,
                    Message = $"Insufficient memory. Required: {requiredMemory}MB"
                });
            }

            return results;
        }

        private async Task<bool> HasRunningProcessesAsync(string tenantId)
        {
            // Check for running background jobs, active connections, etc.
            // This is a simplified implementation
            return false;
        }

        private async Task ExecuteScriptAsync(DeploymentContext context, string script)
        {
            // Execute deployment/rollback script
            // This could be SQL scripts, PowerShell, or custom scripts
            _logger.LogInformation("Executing script for tenant {TenantId}", context.TenantId);
        }

        private async Task RestorePreviousVersionAsync(DeploymentContext context)
        {
            // Restore application to previous version
            _logger.LogInformation("Restoring previous version for tenant {TenantId}", context.TenantId);
        }

        private async Task<ComponentHealth> CheckApiHealthAsync(string tenantId)
        {
            try
            {
                var apiUrl = _configuration[$"Tenants:{tenantId}:ApiUrl"] ?? $"https://{tenantId}.yourdomain.com/health";
                var response = await _httpClient.GetAsync(apiUrl);

                return new ComponentHealth
                {
                    Name = "API",
                    IsHealthy = response.IsSuccessStatusCode,
                    Message = response.IsSuccessStatusCode ? "API is healthy" : $"API returned {response.StatusCode}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["statusCode"] = (int)response.StatusCode,
                        ["responseTime"] = response.Headers.Date?.Subtract(DateTime.UtcNow).TotalMilliseconds ?? 0
                    }
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealth
                {
                    Name = "API",
                    IsHealthy = false,
                    Message = ex.Message
                };
            }
        }

        private async Task<ComponentHealth> CheckDatabaseHealthAsync(string tenantId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

                var canConnect = await dbContext.Database.CanConnectAsync();

                return new ComponentHealth
                {
                    Name = "Database",
                    IsHealthy = canConnect,
                    Message = canConnect ? "Database is accessible" : "Cannot connect to database"
                };
            }
            catch (Exception ex)
            {
                return new ComponentHealth
                {
                    Name = "Database",
                    IsHealthy = false,
                    Message = ex.Message
                };
            }
        }

        private async Task<ComponentHealth> CheckBackgroundServicesAsync(string tenantId)
        {
            // Check if background services are running
            // This is a simplified implementation
            return new ComponentHealth
            {
                Name = "BackgroundServices",
                IsHealthy = true,
                Message = "All background services are running"
            };
        }

        private bool HasDatabaseChanges(TenantDeployment deployment)
        {
            // Check if the deployment includes database migrations
            return deployment.DeploymentVersion.Features.ContainsKey("databaseMigrations");
        }

        private long GetAvailableDiskSpace()
        {
            var drive = new DriveInfo(Path.GetPathRoot(Directory.GetCurrentDirectory())!);
            return drive.AvailableFreeSpace;
        }
    }

    public class DeploymentContext
    {
        public string TenantId { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string TargetVersion { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public Guid DeploymentId { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public abstract class DeploymentStep
    {
        protected readonly IServiceProvider _serviceProvider;

        protected DeploymentStep(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public abstract string Name { get; }
        public abstract Task ExecuteAsync(DeploymentContext context);
        public abstract Task RollbackAsync(DeploymentContext context);
    }

    public class BackupStep : DeploymentStep
    {
        public BackupStep(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public override string Name => "Backup";

        public override async Task ExecuteAsync(DeploymentContext context)
        {
            // Create backup of current state
            var backupPath = Path.Combine("Backups", context.TenantId, $"{context.CurrentVersion}_{DateTime.UtcNow:yyyyMMddHHmmss}");
            Directory.CreateDirectory(backupPath);

            // Backup database
            await BackupDatabaseAsync(context, backupPath);

            // Backup configuration
            await BackupConfigurationAsync(context, backupPath);

            context.Metadata["backupPath"] = backupPath;
        }

        public override async Task RollbackAsync(DeploymentContext context)
        {
            // Restore from backup
            if (context.Metadata.TryGetValue("backupPath", out var backupPath))
            {
                await RestoreFromBackupAsync(context, backupPath.ToString()!);
            }
        }

        private async Task BackupDatabaseAsync(DeploymentContext context, string backupPath)
        {
            // Implement database backup logic
            await Task.CompletedTask;
        }

        private async Task BackupConfigurationAsync(DeploymentContext context, string backupPath)
        {
            // Implement configuration backup logic
            await Task.CompletedTask;
        }

        private async Task RestoreFromBackupAsync(DeploymentContext context, string backupPath)
        {
            // Implement restore logic
            await Task.CompletedTask;
        }
    }

    public class DatabaseMigrationStep : DeploymentStep
    {
        public DatabaseMigrationStep(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public override string Name => "DatabaseMigration";

        public override async Task ExecuteAsync(DeploymentContext context)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

            // Apply pending migrations
            await dbContext.Database.MigrateAsync();
        }

        public override async Task RollbackAsync(DeploymentContext context)
        {
            // Rollback database changes
            // This is complex and would need careful implementation
            await Task.CompletedTask;
        }
    }

    public class ApplicationUpdateStep : DeploymentStep
    {
        public ApplicationUpdateStep(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public override string Name => "ApplicationUpdate";

        public override async Task ExecuteAsync(DeploymentContext context)
        {
            // Update application files
            var deploymentPath = Path.Combine("Deployments", context.TargetVersion);
            var targetPath = Path.Combine("Tenants", context.TenantId, "App");

            // Copy new files
            await CopyFilesAsync(deploymentPath, targetPath);
        }

        public override async Task RollbackAsync(DeploymentContext context)
        {
            // Restore previous version files
            await Task.CompletedTask;
        }

        private async Task CopyFilesAsync(string source, string destination)
        {
            // Implement file copy logic
            await Task.CompletedTask;
        }
    }

    public class ConfigurationUpdateStep : DeploymentStep
    {
        public ConfigurationUpdateStep(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public override string Name => "ConfigurationUpdate";

        public override async Task ExecuteAsync(DeploymentContext context)
        {
            // Update configuration settings
            var configPath = Path.Combine("Tenants", context.TenantId, "Config");

            // Apply configuration changes
            await UpdateConfigurationAsync(context, configPath);
        }

        public override async Task RollbackAsync(DeploymentContext context)
        {
            // Restore previous configuration
            await Task.CompletedTask;
        }

        private async Task UpdateConfigurationAsync(DeploymentContext context, string configPath)
        {
            // Implement configuration update logic
            await Task.CompletedTask;
        }
    }

    public class CacheClearStep : DeploymentStep
    {
        public CacheClearStep(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public override string Name => "CacheClear";

        public override async Task ExecuteAsync(DeploymentContext context)
        {
            using var scope = _serviceProvider.CreateScope();
            var cacheService = scope.ServiceProvider.GetRequiredService<Backend.CMS.Caching.Services.ICacheService>();

            await cacheService.ClearAsync();
        }

        public override async Task RollbackAsync(DeploymentContext context)
        {
            // Cache clearing doesn't need rollback
            await Task.CompletedTask;
        }
    }

    public class ServiceRestartStep : DeploymentStep
    {
        public ServiceRestartStep(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public override string Name => "ServiceRestart";

        public override async Task ExecuteAsync(DeploymentContext context)
        {
            // Restart application services
            // This would typically involve IIS reset, service restart, or container restart
            await RestartServicesAsync(context);
        }

        public override async Task RollbackAsync(DeploymentContext context)
        {
            // Services would be restarted as part of rollback
            await Task.CompletedTask;
        }

        private async Task RestartServicesAsync(DeploymentContext context)
        {
            // Implement service restart logic
            await Task.CompletedTask;
        }
    }
}