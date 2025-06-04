using Backend.CMS.Domain.Common;

namespace Backend.CMS.Versioning.Entities
{
    public class DeploymentVersion : BaseEntity
    {
        public string Version { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public DateTime ReleasedAt { get; set; }
        public bool IsMandatory { get; set; }
        public string? MinimumRequiredVersion { get; set; }
        public Dictionary<string, object> Features { get; set; } = [];
        public Dictionary<string, object> BreakingChanges { get; set; } = [];
        public string? MigrationScript { get; set; }
        public string? RollbackScript { get; set; }
        public bool IsActive { get; set; } = true;
        public ICollection<TenantDeployment> TenantDeployments { get; set; } = [];
    }
}
