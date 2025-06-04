using Backend.CMS.Audit.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Backend.CMS.Domain.Common;

namespace Backend.CMS.Audit.Services
{
    public class AuditService : IAuditService
    {
        private readonly ILogger<AuditService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly CmsDbContext _context;

        public AuditService(
            ILogger<AuditService> logger,
            IHttpContextAccessor httpContextAccessor,
            CmsDbContext context)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public async Task LogAsync(string entityName, string entityId, string action, object? oldValues = null, object? newValues = null)
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                var tenantId = GetTenantId();
                var userId = GetUserId();
                var ipAddress = GetIpAddress();
                var userAgent = GetUserAgent();

                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EntityName = entityName,
                    EntityId = entityId,
                    Action = action,
                    OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, GetJsonOptions()) : null,
                    NewValues = newValues != null ? JsonSerializer.Serialize(newValues, GetJsonOptions()) : null,
                    UserId = userId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    CreatedOn = DateTime.UtcNow
                };

                await SaveAuditLogAsync(auditLog);

                _logger.LogInformation("Audit: {Action} on {EntityName} {EntityId} by user {UserId} from {IpAddress}",
                    action, entityName, entityId, userId, ipAddress);
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
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = GetTenantId(),
                    EntityName = "User",
                    EntityId = userId,
                    Action = action,
                    NewValues = JsonSerializer.Serialize(new { Details = details }, GetJsonOptions()),
                    UserId = userId,
                    IpAddress = GetIpAddress(),
                    UserAgent = GetUserAgent(),
                    CreatedOn = DateTime.UtcNow
                };

                await SaveAuditLogAsync(auditLog);

                _logger.LogInformation("User Action: User {UserId} performed {Action}: {Details}", userId, action, details);
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
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = GetTenantId(),
                    EntityName = "Security",
                    EntityId = Guid.NewGuid().ToString(),
                    Action = eventType,
                    NewValues = JsonSerializer.Serialize(new
                    {
                        EventType = eventType,
                        Details = details,
                        Severity = GetSecurityEventSeverity(eventType)
                    }, GetJsonOptions()),
                    UserId = userId ?? GetUserId(),
                    IpAddress = GetIpAddress(),
                    UserAgent = GetUserAgent(),
                    CreatedOn = DateTime.UtcNow
                };

                await SaveAuditLogAsync(auditLog);

                _logger.LogWarning("Security Event: {EventType} - {Details} for user {UserId} from {IpAddress}",
                    eventType, details, userId, GetIpAddress());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log security event {EventType}", eventType);
            }
        }

        public async Task LogLoginAttemptAsync(string email, bool success, string? failureReason = null)
        {
            try
            {
                var loginAttempt = new LoginAttempt
                {
                    Id = Guid.NewGuid(),
                    TenantId = GetTenantId(),
                    Email = email,
                    Success = success,
                    FailureReason = failureReason,
                    IpAddress = GetIpAddress(),
                    UserAgent = GetUserAgent(),
                    AttemptedAt = DateTime.UtcNow
                };

                _context.Set<LoginAttempt>().Add(loginAttempt);
                await _context.SaveChangesAsync();

                if (success)
                {
                    _logger.LogInformation("Successful login for {Email} from {IpAddress}", email, GetIpAddress());
                }
                else
                {
                    _logger.LogWarning("Failed login attempt for {Email} from {IpAddress}: {FailureReason}",
                        email, GetIpAddress(), failureReason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log login attempt for {Email}", email);
            }
        }

        public async Task<IEnumerable<AuditLog>> GetAuditTrailAsync(string entityName, string entityId, int limit = 50)
        {
            try
            {
                return await _context.Set<AuditLog>()
                    .Where(a => a.EntityName == entityName && a.EntityId == entityId)
                    .OrderByDescending(a => a.CreatedOn)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get audit trail for {EntityName} {EntityId}", entityName, entityId);
                return [];
            }
        }

        public async Task<IEnumerable<LoginAttempt>> GetRecentLoginAttemptsAsync(string email, TimeSpan timeWindow)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
                return await _context.Set<LoginAttempt>()
                    .Where(l => l.Email == email && l.AttemptedAt >= cutoffTime)
                    .OrderByDescending(l => l.AttemptedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent login attempts for {Email}", email);
                return Enumerable.Empty<LoginAttempt>();
            }
        }

        public async Task<SecurityMetrics> GetSecurityMetricsAsync(DateTime from, DateTime to)
        {
            try
            {
                var securityEvents = await _context.Set<AuditLog>()
                    .Where(a => a.EntityName == "Security" && a.CreatedOn >= from && a.CreatedOn <= to)
                    .ToListAsync();

                var loginAttempts = await _context.Set<LoginAttempt>()
                    .Where(l => l.AttemptedAt >= from && l.AttemptedAt <= to)
                    .ToListAsync();

                return new SecurityMetrics
                {
                    PeriodStart = from,
                    PeriodEnd = to,
                    TotalSecurityEvents = securityEvents.Count,
                    TotalLoginAttempts = loginAttempts.Count,
                    SuccessfulLogins = loginAttempts.Count(l => l.Success),
                    FailedLogins = loginAttempts.Count(l => !l.Success),
                    UniqueUsers = loginAttempts.Select(l => l.Email).Distinct().Count(),
                    TopFailureReasons = loginAttempts
                        .Where(l => !l.Success && !string.IsNullOrEmpty(l.FailureReason))
                        .GroupBy(l => l.FailureReason!)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get security metrics for period {From} to {To}", from, to);
                throw;
            }
        }

        private async Task SaveAuditLogAsync(AuditLog auditLog)
        {
            _context.Set<AuditLog>().Add(auditLog);
            await _context.SaveChangesAsync();
        }

        private string GetTenantId()
        {
            return _httpContextAccessor.HttpContext?.Items["TenantId"]?.ToString() ?? "unknown";
        }

        private string? GetUserId()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        private string GetIpAddress()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return "unknown";

            // Check for forwarded IP first (for load balancers/proxies)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private string GetUserAgent()
        {
            return _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString() ?? "unknown";
        }

        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        private string GetSecurityEventSeverity(string eventType)
        {
            return eventType.ToLower() switch
            {
                "login_failed" => "Medium",
                "account_locked" => "High",
                "password_changed" => "Low",
                "permission_denied" => "Medium",
                "suspicious_activity" => "High",
                "data_export" => "Medium",
                "admin_action" => "Medium",
                _ => "Low"
            };
        }
    }

    // Update the LoginAttempt entity
    public class LoginAttempt : BaseEntity
    {
        public string TenantId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
        public string? SessionId { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public Guid Id { get; internal set; }
    }

    public class SecurityMetrics
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalSecurityEvents { get; set; }
        public int TotalLoginAttempts { get; set; }
        public int SuccessfulLogins { get; set; }
        public int FailedLogins { get; set; }
        public int UniqueUsers { get; set; }
        public Dictionary<string, int> TopFailureReasons { get; set; } = new();
    }

}