namespace Backend.CMS.Audit.Services
{
    public interface IAuditService
    {
        Task LogAsync(string entityName, string entityId, string action, object? oldValues = null, object? newValues = null);
        Task LogUserActionAsync(string userId, string action, string details);
        Task LogSecurityEventAsync(string eventType, string details, string? userId = null);
    }
}