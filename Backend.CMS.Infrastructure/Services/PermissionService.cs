using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PermissionService> _logger;

        private const string ROLE_PERMISSIONS_CACHE_KEY = "role_permissions";
        private const string USER_PERMISSIONS_CACHE_KEY = "user_permissions_{0}";
        private const int CACHE_EXPIRY_MINUTES = 30;

        public PermissionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IUserSessionService userSessionService,
            IMemoryCache cache,
            ILogger<PermissionService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _userSessionService = userSessionService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<PermissionDto>> GetAllPermissionsAsync()
        {
            var permissions = await _unitOfWork.GetRepository<Permission>().GetAllAsync();
            return _mapper.Map<List<PermissionDto>>(permissions.OrderBy(p => p.Category).ThenBy(p => p.SortOrder));
        }

        public async Task<List<PermissionCategoryDto>> GetPermissionsByCategoryAsync()
        {
            var permissions = await _unitOfWork.GetRepository<Permission>().FindAsync(p => p.IsActive);
            var grouped = permissions.GroupBy(p => p.Category)
                .OrderBy(g => g.Key)
                .Select(g => new PermissionCategoryDto
                {
                    Category = g.Key,
                    Permissions = _mapper.Map<List<PermissionDto>>(g.OrderBy(p => p.SortOrder))
                }).ToList();

            return grouped;
        }

        public async Task<List<string>> GetUserPermissionsAsync(int userId)
        {
            var cacheKey = string.Format(USER_PERMISSIONS_CACHE_KEY, userId);

            if (_cache.TryGetValue(cacheKey, out List<string>? cachedPermissions))
            {
                return cachedPermissions ?? new List<string>();
            }

            var permissions = await GetEffectivePermissionsAsync(userId);

            _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(CACHE_EXPIRY_MINUTES));
            return permissions;
        }

        public async Task<List<string>> GetEffectivePermissionsAsync(int userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return new List<string>();

            // Get role permissions
            var rolePermissions = await GetRolePermissionsFromCacheAsync(user.Role);

            // Get user-specific permissions
            var userPermissions = await _unitOfWork.GetRepository<UserPermission>().FindAsync(up =>
                up.UserId == userId &&
                up.IsGranted &&
                (up.ExpiresAt == null || up.ExpiresAt > DateTime.UtcNow));

            var userPermissionList = new List<string>();
            foreach (var up in userPermissions)
            {
                var permission = await _unitOfWork.GetRepository<Permission>().GetByIdAsync(up.PermissionId);
                if (permission != null && permission.IsActive)
                {
                    userPermissionList.Add(permission.Name);
                }
            }

            // Combine and deduplicate
            var allPermissions = rolePermissions.Union(userPermissionList).Distinct().ToList();
            return allPermissions;
        }

        public async Task<RolePermissionDto> GetRolePermissionsAsync(UserRole role)
        {
            var rolePermissions = await _unitOfWork.GetRepository<RolePermission>().FindAsync(rp => rp.Role == role && rp.IsGranted);
            var permissions = new List<PermissionDto>();

            foreach (var rp in rolePermissions)
            {
                var permission = await _unitOfWork.GetRepository<Permission>().GetByIdAsync(rp.PermissionId);
                if (permission != null && permission.IsActive)
                {
                    permissions.Add(_mapper.Map<PermissionDto>(permission));
                }
            }

            return new RolePermissionDto
            {
                Role = role,
                RoleName = role.ToString(),
                Permissions = permissions.OrderBy(p => p.Category).ThenBy(p => p.SortOrder).ToList()
            };
        }

        public async Task<bool> UpdateRolePermissionsAsync(UpdateRolePermissionsDto updateRolePermissionsDto)
        {
            try
            {
                var currentUserId = GetCurrentUserIdSafe();

                // Remove existing role permissions
                var existingPermissions = await _unitOfWork.GetRepository<RolePermission>().FindAsync(rp => rp.Role == updateRolePermissionsDto.Role);
                foreach (var existing in existingPermissions)
                {
                    await _unitOfWork.GetRepository<RolePermission>().SoftDeleteAsync(existing, currentUserId);
                }

                // Add new permissions
                foreach (var permissionId in updateRolePermissionsDto.PermissionIds)
                {
                    var rolePermission = new RolePermission
                    {
                        Role = updateRolePermissionsDto.Role,
                        PermissionId = permissionId,
                        IsGranted = true,
                        CreatedByUserId = currentUserId,
                        UpdatedByUserId = currentUserId
                    };

                    await _unitOfWork.GetRepository<RolePermission>().AddAsync(rolePermission);
                }

                await _unitOfWork.GetRepository<RolePermission>().SaveChangesAsync();

                // Clear cache
                _cache.Remove(ROLE_PERMISSIONS_CACHE_KEY);

                _logger.LogInformation("Role permissions updated for {Role} by user {UserId}",
                    updateRolePermissionsDto.Role, currentUserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role permissions for {Role}", updateRolePermissionsDto.Role);
                return false;
            }
        }

        public async Task<bool> AssignPermissionToUserAsync(AssignPermissionDto assignPermissionDto)
        {
            try
            {
                var currentUserId = GetCurrentUserIdSafe();

                // Remove existing permission if it exists
                var existing = await _unitOfWork.GetRepository<UserPermission>().FirstOrDefaultAsync(up =>
                    up.UserId == assignPermissionDto.UserId &&
                    up.PermissionId == assignPermissionDto.PermissionId);

                if (existing != null)
                {
                    await _unitOfWork.GetRepository<UserPermission>().SoftDeleteAsync(existing, currentUserId);
                }

                // Add new permission
                var userPermission = new UserPermission
                {
                    UserId = assignPermissionDto.UserId,
                    PermissionId = assignPermissionDto.PermissionId,
                    IsGranted = assignPermissionDto.IsGranted,
                    Reason = assignPermissionDto.Reason,
                    ExpiresAt = assignPermissionDto.ExpiresAt,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                await _unitOfWork.GetRepository<UserPermission>().AddAsync(userPermission);
                await _unitOfWork.GetRepository<UserPermission>().SaveChangesAsync();

                // Clear user's permission cache
                var cacheKey = string.Format(USER_PERMISSIONS_CACHE_KEY, assignPermissionDto.UserId);
                _cache.Remove(cacheKey);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning permission {PermissionId} to user {UserId}",
                    assignPermissionDto.PermissionId, assignPermissionDto.UserId);
                return false;
            }
        }

        public async Task SeedDefaultPermissionsAsync()
        {
            try
            {
                var existingPermissions = await _unitOfWork.GetRepository<Permission>().GetAllAsync();
                if (existingPermissions.Any()) return;

                // Use system user ID of 1 for seeding, or null if not available
                var systemUserId = await GetSystemUserIdAsync() ?? 1;

                var defaultPermissions = GetDefaultPermissions();

                foreach (var permission in defaultPermissions)
                {
                    permission.CreatedByUserId = systemUserId;
                    permission.UpdatedByUserId = systemUserId;
                    await _unitOfWork.GetRepository<Permission>().AddAsync(permission);
                }

                await _unitOfWork.GetRepository<Permission>().SaveChangesAsync();

                // Seed default role permissions
                await SeedDefaultRolePermissionsAsync();

                _logger.LogInformation("Default permissions seeded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding default permissions");
                throw;
            }
        }

        private async Task<List<string>> GetRolePermissionsFromCacheAsync(UserRole role)
        {
            if (_cache.TryGetValue(ROLE_PERMISSIONS_CACHE_KEY, out Dictionary<UserRole, List<string>>? cachedRolePermissions))
            {
                return cachedRolePermissions?.GetValueOrDefault(role) ?? new List<string>();
            }

            // Load all role permissions
            var allRolePermissions = new Dictionary<UserRole, List<string>>();

            foreach (UserRole roleEnum in Enum.GetValues<UserRole>())
            {
                var rolePerms = await GetRolePermissionsAsync(roleEnum);
                allRolePermissions[roleEnum] = rolePerms.Permissions.Select(p => p.Name).ToList();
            }

            _cache.Set(ROLE_PERMISSIONS_CACHE_KEY, allRolePermissions, TimeSpan.FromMinutes(CACHE_EXPIRY_MINUTES));
            return allRolePermissions.GetValueOrDefault(role) ?? new List<string>();
        }

        private async Task<int?> GetSystemUserIdAsync()
        {
            try
            {
                var systemUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Dev);
                return systemUser?.Id;
            }
            catch
            {
                return null;
            }
        }

        private int? GetCurrentUserIdSafe()
        {
            try
            {
                return _userSessionService.GetCurrentUserId();
            }
            catch
            {
                return 1; // Use system user ID as fallback
            }
        }

        private List<Permission> GetDefaultPermissions()
        {
            return
            [
                // User Management
                new() { Name = "users.view", DisplayName = "View Users", Category = "Users", SortOrder = 1, IsSystemPermission = true },
                new() { Name = "users.create", DisplayName = "Create Users", Category = "Users", SortOrder = 2, IsSystemPermission = true },
                new() { Name = "users.edit", DisplayName = "Edit Users", Category = "Users", SortOrder = 3, IsSystemPermission = true },
                new() { Name = "users.delete", DisplayName = "Delete Users", Category = "Users", SortOrder = 4, IsSystemPermission = true },
                new() { Name = "users.manage", DisplayName = "Manage Users", Category = "Users", SortOrder = 5, IsSystemPermission = true },

                // Page Management
                new() { Name = "pages.view", DisplayName = "View Pages", Category = "Pages", SortOrder = 1, IsSystemPermission = true },
                new() { Name = "pages.create", DisplayName = "Create Pages", Category = "Pages", SortOrder = 2, IsSystemPermission = true },
                new() { Name = "pages.edit", DisplayName = "Edit Pages", Category = "Pages", SortOrder = 3, IsSystemPermission = true },
                new() { Name = "pages.delete", DisplayName = "Delete Pages", Category = "Pages", SortOrder = 4, IsSystemPermission = true },
                new() { Name = "pages.publish", DisplayName = "Publish Pages", Category = "Pages", SortOrder = 5, IsSystemPermission = true },
                new() { Name = "pages.manage", DisplayName = "Manage Pages", Category = "Pages", SortOrder = 6, IsSystemPermission = true },

                // Component Management
                new() { Name = "components.view", DisplayName = "View Components", Category = "Components", SortOrder = 1, IsSystemPermission = true },
                new() { Name = "components.create", DisplayName = "Create Components", Category = "Components", SortOrder = 2, IsSystemPermission = true },
                new() { Name = "components.edit", DisplayName = "Edit Components", Category = "Components", SortOrder = 3, IsSystemPermission = true },
                new() { Name = "components.delete", DisplayName = "Delete Components", Category = "Components", SortOrder = 4, IsSystemPermission = true },
                new() { Name = "components.manage", DisplayName = "Manage Components", Category = "Components", SortOrder = 5, IsSystemPermission = true },

                // Store Management
                new() { Name = "stores.view", DisplayName = "View Stores", Category = "Stores", SortOrder = 1, IsSystemPermission = true },
                new() { Name = "stores.create", DisplayName = "Create Stores", Category = "Stores", SortOrder = 2, IsSystemPermission = true },
                new() { Name = "stores.edit", DisplayName = "Edit Stores", Category = "Stores", SortOrder = 3, IsSystemPermission = true },
                new() { Name = "stores.delete", DisplayName = "Delete Stores", Category = "Stores", SortOrder = 4, IsSystemPermission = true },
                new() { Name = "stores.manage", DisplayName = "Manage Stores", Category = "Stores", SortOrder = 5, IsSystemPermission = true },

                // Company Management
                new() { Name = "company.view", DisplayName = "View Company", Category = "Company", SortOrder = 1, IsSystemPermission = true },
                new() { Name = "company.edit", DisplayName = "Edit Company", Category = "Company", SortOrder = 2, IsSystemPermission = true },
                new() { Name = "company.manage", DisplayName = "Manage Company", Category = "Company", SortOrder = 3, IsSystemPermission = true },

                // Job Management
                new() { Name = "jobs.view", DisplayName = "View Jobs", Category = "Jobs", SortOrder = 1, IsSystemPermission = true },
                new() { Name = "jobs.deployment", DisplayName = "Deployment Jobs", Category = "Jobs", SortOrder = 2, IsSystemPermission = true },
                new() { Name = "jobs.template-sync", DisplayName = "Template Sync Jobs", Category = "Jobs", SortOrder = 3, IsSystemPermission = true },
                new() { Name = "jobs.manage", DisplayName = "Manage Jobs", Category = "Jobs", SortOrder = 4, IsSystemPermission = true },

                // System
                new() { Name = "system.admin", DisplayName = "System Administration", Category = "System", SortOrder = 1, IsSystemPermission = true },
                new() { Name = "permissions.manage", DisplayName = "Manage Permissions", Category = "System", SortOrder = 2, IsSystemPermission = true },

                // Profile
                new() { Name = "profile.view", DisplayName = "View Profile", Category = "Profile", SortOrder = 1, IsSystemPermission = true },
                new() { Name = "profile.edit", DisplayName = "Edit Profile", Category = "Profile", SortOrder = 2, IsSystemPermission = true },
                new() { Name = "profile.manage", DisplayName = "Manage Profile", Category = "Profile", SortOrder = 3, IsSystemPermission = true }
            ];
        }

        private async Task SeedDefaultRolePermissionsAsync()
        {
            var systemUserId = await GetSystemUserIdAsync() ?? 1;
            var permissions = await _unitOfWork.GetRepository<Permission>().GetAllAsync();

            // Dev permissions (all permissions)
            var devPermissions = permissions.Select(p => new RolePermission
            {
                Role = UserRole.Dev,
                PermissionId = p.Id,
                IsGranted = true,
                CreatedByUserId = systemUserId,
                UpdatedByUserId = systemUserId
            });

            // Admin permissions (exclude system admin and some dev-only permissions)
            var adminPermissionNames = new[]
            {
                "users.view", "users.create", "users.edit", "users.manage",
                "pages.view", "pages.create", "pages.edit", "pages.delete", "pages.publish", "pages.manage",
                "components.view", "components.create", "components.edit", "components.manage",
                "stores.view", "stores.create", "stores.edit", "stores.delete", "stores.manage",
                "company.view", "company.edit", "company.manage",
                "jobs.view", "profile.view", "profile.edit", "profile.manage"
            };

            var adminPermissions = permissions
                .Where(p => adminPermissionNames.Contains(p.Name))
                .Select(p => new RolePermission
                {
                    Role = UserRole.Admin,
                    PermissionId = p.Id,
                    IsGranted = true,
                    CreatedByUserId = systemUserId,
                    UpdatedByUserId = systemUserId
                });

            // Customer permissions
            var customerPermissionNames = new[] { "pages.view", "profile.view", "profile.edit", "profile.manage" };
            var customerPermissions = permissions
                .Where(p => customerPermissionNames.Contains(p.Name))
                .Select(p => new RolePermission
                {
                    Role = UserRole.Customer,
                    PermissionId = p.Id,
                    IsGranted = true,
                    CreatedByUserId = systemUserId,
                    UpdatedByUserId = systemUserId
                });

            await _unitOfWork.GetRepository<RolePermission>().AddRangeAsync(devPermissions);
            await _unitOfWork.GetRepository<RolePermission>().AddRangeAsync(adminPermissions);
            await _unitOfWork.GetRepository<RolePermission>().AddRangeAsync(customerPermissions);
            await _unitOfWork.GetRepository<RolePermission>().SaveChangesAsync();
        }

        // Implement other interface methods...
        public async Task<PermissionDto> CreatePermissionAsync(CreatePermissionDto createPermissionDto)
        {
            var permission = _mapper.Map<Permission>(createPermissionDto);
            permission.IsSystemPermission = false;
            permission.CreatedByUserId = GetCurrentUserIdSafe();
            permission.UpdatedByUserId = GetCurrentUserIdSafe();

            await _unitOfWork.GetRepository<Permission>().AddAsync(permission);
            await _unitOfWork.GetRepository<Permission>().SaveChangesAsync();

            return _mapper.Map<PermissionDto>(permission);
        }

        public async Task<PermissionDto> UpdatePermissionAsync(int permissionId, UpdatePermissionDto updatePermissionDto)
        {
            var permission = await _unitOfWork.GetRepository<Permission>().GetByIdAsync(permissionId);
            if (permission == null)
                throw new ArgumentException("Permission not found");

            _mapper.Map(updatePermissionDto, permission);
            permission.UpdatedByUserId = GetCurrentUserIdSafe();
            permission.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.GetRepository<Permission>().Update(permission);
            await _unitOfWork.GetRepository<Permission>().SaveChangesAsync();

            return _mapper.Map<PermissionDto>(permission);
        }

        public async Task<bool> DeletePermissionAsync(int permissionId)
        {
            var permission = await _unitOfWork.GetRepository<Permission>().GetByIdAsync(permissionId);
            if (permission == null || permission.IsSystemPermission)
                return false;

            return await _unitOfWork.GetRepository<Permission>().SoftDeleteAsync(permissionId, GetCurrentUserIdSafe());
        }

        public async Task<List<RolePermissionDto>> GetAllRolePermissionsAsync()
        {
            var result = new List<RolePermissionDto>();
            foreach (UserRole role in Enum.GetValues<UserRole>())
            {
                result.Add(await GetRolePermissionsAsync(role));
            }
            return result;
        }

        public async Task<UserPermissionDto> GetUserPermissionsDetailAsync(int userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found");

            var effectivePermissions = await GetEffectivePermissionsAsync(userId);
            var userSpecificPermissions = await _unitOfWork.GetRepository<UserPermission>().FindAsync(up => up.UserId == userId);

            var permissions = new List<PermissionAssignmentDto>();

            foreach (var permissionName in effectivePermissions)
            {
                var permission = await _unitOfWork.GetRepository<Permission>().FirstOrDefaultAsync(p => p.Name == permissionName);
                if (permission != null)
                {
                    var userSpecific = userSpecificPermissions.FirstOrDefault(up => up.PermissionId == permission.Id);
                    permissions.Add(new PermissionAssignmentDto
                    {
                        PermissionId = permission.Id,
                        PermissionName = permission.Name,
                        PermissionDisplayName = permission.DisplayName,
                        Category = permission.Category,
                        IsGranted = true,
                        Source = userSpecific != null ? "Direct" : "Role",
                        Reason = userSpecific?.Reason,
                        ExpiresAt = userSpecific?.ExpiresAt
                    });
                }
            }

            return new UserPermissionDto
            {
                UserId = userId,
                UserName = user.Username,
                Email = user.Email,
                Permissions = permissions.OrderBy(p => p.Category).ThenBy(p => p.PermissionDisplayName).ToList()
            };
        }

        public async Task<bool> RemovePermissionFromUserAsync(int userId, int permissionId)
        {
            var userPermission = await _unitOfWork.GetRepository<UserPermission>().FirstOrDefaultAsync(up =>
                up.UserId == userId && up.PermissionId == permissionId);

            if (userPermission == null)
                return false;

            var result = await _unitOfWork.GetRepository<UserPermission>().SoftDeleteAsync(userPermission, GetCurrentUserIdSafe());

            if (result)
            {
                var cacheKey = string.Format(USER_PERMISSIONS_CACHE_KEY, userId);
                _cache.Remove(cacheKey);
            }

            return result;
        }

        public async Task<bool> BulkAssignPermissionsAsync(int userId, List<AssignPermissionDto> permissions)
        {
            try
            {
                foreach (var permission in permissions)
                {
                    permission.UserId = userId;
                    await AssignPermissionToUserAsync(permission);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk assigning permissions to user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> HasPermissionAsync(int userId, string permissionName)
        {
            var userPermissions = await GetUserPermissionsAsync(userId);
            return userPermissions.Contains(permissionName);
        }
    }
}