using Backend.CMS.Audit.Entities;
using Backend.CMS.Audit.Services;

public interface IAuditService
{
    public interface IAuditService
    {
        Task LogAsync(string entityName, string entityId, string action, object? oldValues = null, object? newValues = null);
        Task LogUserActionAsync(string userId, string action, string details);
        Task LogSecurityEventAsync(string eventType, string details, string? userId = null);
        Task LogLoginAttemptAsync(string email, bool success, string? failureReason = null);
        Task<IEnumerable<AuditLog>> GetAuditTrailAsync(string entityName, string entityId, int limit = 50);
        Task<IEnumerable<Backend.CMS.Audit.Entities.LoginAttempt>> GetRecentLoginAttemptsAsync(string email, TimeSpan timeWindow);
        Task<SecurityMetrics> GetSecurityMetricsAsync(DateTime from, DateTime to);
    }
}