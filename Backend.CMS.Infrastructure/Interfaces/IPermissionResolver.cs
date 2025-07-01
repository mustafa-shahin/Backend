using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IPermissionResolver
    {
        Task<List<string>> GetUserPermissionsAsync(int userId);
        Task<List<string>> GetRolePermissionsAsync(UserRole role);
    }
}