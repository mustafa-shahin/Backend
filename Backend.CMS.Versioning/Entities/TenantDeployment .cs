using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Versioning.Entities
{
    public class TenantDeployment : BaseEntity
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public Guid DeploymentVersionId { get; set; }
        public DeploymentVersion DeploymentVersion { get; set; } = null!;
        public string CurrentVersion { get; set; } = string.Empty;
        public string TargetVersion { get; set; } = string.Empty;
        public DeploymentStatus Status { get; set; }
        public DateTime? DeployedAt { get; set; }
        public string? DeployedBy { get; set; }
        public Dictionary<string, object> DeploymentLog { get; set; } = new();
        public bool AutoUpdate { get; set; }
        public DeploymentStrategy Strategy { get; set; }
        public DateTime? ScheduledFor { get; set; }
        public int? RolloutPercentage { get; set; }
    }

    public enum DeploymentStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        RolledBack,
        Scheduled,
        PartiallyDeployed
    }

    public enum DeploymentStrategy
    {
        Immediate,
        Scheduled,
        Manual,
        Canary,
        BlueGreen,
        Rolling
    }
}