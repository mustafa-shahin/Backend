using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IPermissionService
    {
        // Permission Management
        Task<List<PermissionDto>> GetAllPermissionsAsync();
        Task<List<PermissionCategoryDto>> GetPermissionsByCategoryAsync();
        Task<PermissionDto> CreatePermissionAsync(CreatePermissionDto createPermissionDto);
        Task<PermissionDto> UpdatePermissionAsync(int permissionId, UpdatePermissionDto updatePermissionDto);
        Task<bool> DeletePermissionAsync(int permissionId);

        // Role Permissions
        Task<List<RolePermissionDto>> GetAllRolePermissionsAsync();
        Task<RolePermissionDto> GetRolePermissionsAsync(UserRole role);
        Task<bool> UpdateRolePermissionsAsync(UpdateRolePermissionsDto updateRolePermissionsDto);
        Task<List<string>> GetUserPermissionsAsync(int userId);

        // User Permissions
        Task<UserPermissionDto> GetUserPermissionsDetailAsync(int userId);
        Task<bool> AssignPermissionToUserAsync(AssignPermissionDto assignPermissionDto);
        Task<bool> RemovePermissionFromUserAsync(int userId, int permissionId);
        Task<bool> BulkAssignPermissionsAsync(int userId, List<AssignPermissionDto> permissions);

        // Permission Checking
        Task<bool> HasPermissionAsync(int userId, string permissionName);
        Task<List<string>> GetEffectivePermissionsAsync(int userId);

        // System
        Task SeedDefaultPermissionsAsync();
    }

}