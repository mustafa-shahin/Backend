using Backend.CMS.Application.Common;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IUserSessionService
    {
        // Session Management
        UserSessionContext? GetCurrentSession();
        Task<UserSessionContext?> GetCurrentSessionAsync();
        Task SetCurrentSessionAsync(User user, string? ipAddress = null, string? userAgent = null);
        Task UpdateSessionAsync();
        Task ClearSessionAsync();

        // Quick Access Properties
        int? GetCurrentUserId();
        string? GetCurrentUserEmail();
        string? GetCurrentUserFullName();
        UserRole? GetCurrentUserRole();
        bool IsAuthenticated();
        bool IsInRole(params UserRole[] roles);

        // Permission Checks
        bool HasPermission(string permission);
        bool CanManageUsers();
        bool CanCreateUserWithRole(UserRole targetRole);
        bool CanTriggerDeploymentJobs();
        bool CanTriggerTemplateSyncJobs();

 
        // Audit Information
        string GetAuditUserName();
        string GetAuditUserId();

        // Session Metadata
        bool IsSessionValid();
        void UpdateLastActivity();
        TimeSpan GetSessionDuration();
    }
}