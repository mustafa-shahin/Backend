using Backend.CMS.Versioning.Entities;

namespace Backend.CMS.Versioning.Services
{
    public interface IDeploymentExecutor
    {
        Task ExecuteAsync(TenantDeployment deployment);
        Task RollbackAsync(TenantDeployment deployment);
        Task ValidateAsync(TenantDeployment deployment);
        Task<HealthCheckResult> CheckHealthAsync(string tenantId);
    }

    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public Dictionary<string, ComponentHealth> Components { get; set; } = new();
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    public class ComponentHealth
    {
        public string Name { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}