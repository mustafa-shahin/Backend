using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Application.DTOs
{
    public class PermissionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsSystemPermission { get; set; }
        public int SortOrder { get; set; }
    }

    public class RolePermissionDto
    {
        public UserRole Role { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public List<PermissionDto> Permissions { get; set; } = [];
    }

    public class UserPermissionDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<PermissionAssignmentDto> Permissions { get; set; } = [];
    }

    public class PermissionAssignmentDto
    {
        public int PermissionId { get; set; }
        public string PermissionName { get; set; } = string.Empty;
        public string PermissionDisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsGranted { get; set; }
        public string Source { get; set; } = string.Empty; // "Role" or "Direct"
        public string? Reason { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class AssignPermissionDto
    {
        public int UserId { get; set; }
        public int PermissionId { get; set; }
        public bool IsGranted { get; set; } = true;
        public string? Reason { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class UpdateRolePermissionsDto
    {
        public UserRole Role { get; set; }
        public List<int> PermissionIds { get; set; } = [];
    }

    public class PermissionCategoryDto
    {
        public string Category { get; set; } = string.Empty;
        public List<PermissionDto> Permissions { get; set; } = [];
    }
    public class CreatePermissionDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public class UpdatePermissionDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }
}