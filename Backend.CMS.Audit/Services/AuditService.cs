using Backend.CMS.Audit.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Backend.CMS.Audit.Services
{
    public class AuditService : IAuditService
    {
        private readonly ILogger<AuditService> _logger;

        public AuditService(ILogger<AuditService> logger)
        {
            _logger = logger;
        }

        public async Task LogAsync(string entityName, string entityId, string action, object? oldValues = null, object? newValues = null)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    EntityName = entityName,
                    EntityId = entityId,
                    Action = action,
                    OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                    NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                    CreatedOn = DateTime.UtcNow
                };

                // In a real implementation, you would save this to a database
                _logger.LogInformation("Audit: {Action} on {EntityName} {EntityId}", action, entityName, entityId);

                await Task.CompletedTask; // Placeholder for async database operation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit entry for {EntityName} {EntityId}", entityName, entityId);
            }
        }

        public async Task LogUserActionAsync(string userId, string action, string details)
        {
            try
            {
                _logger.LogInformation("User Action: User {UserId} performed {Action}: {Details}", userId, action, details);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log user action for user {UserId}", userId);
            }
        }

        public async Task LogSecurityEventAsync(string eventType, string details, string? userId = null)
        {
            try
            {
                _logger.LogWarning("Security Event: {EventType} - {Details} for user {UserId}", eventType, details, userId);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log security event {EventType}", eventType);
            }
        }
    }
}