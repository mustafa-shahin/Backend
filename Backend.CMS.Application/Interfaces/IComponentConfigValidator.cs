using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Application.Interfaces
{
    public interface IComponentConfigValidator
    {
        Task<ComponentConfigValidationResult> ValidateAsync(ComponentType type, Dictionary<string, object> config);
        Task<Dictionary<string, object>> GetDefaultConfigAsync(ComponentType type);
        Task<Dictionary<string, object>> GetConfigSchemaAsync(ComponentType type);
        Task<Dictionary<string, object>> SanitizeConfigAsync(ComponentType type, Dictionary<string, object> config);
    }

    public class ComponentConfigValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> SanitizedConfig { get; set; } = new();
    }
}