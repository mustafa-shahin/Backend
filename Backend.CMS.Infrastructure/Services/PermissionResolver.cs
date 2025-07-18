﻿using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class PermissionResolver : IPermissionResolver
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PermissionResolver> _logger;

        public PermissionResolver(
            IUnitOfWork unitOfWork,
            IMemoryCache cache,
            ILogger<PermissionResolver> logger)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<string>> GetUserPermissionsAsync(int userId)
        {
            var cacheKey = $"user_permissions_{userId}";

            if (_cache.TryGetValue(cacheKey, out List<string>? cachedPermissions))
                return cachedPermissions ?? new List<string>();

            try
            {
                // Get user to determine role
                var userRepo = _unitOfWork.GetRepository<Permission>().GetType().Assembly
                    .GetTypes()
                    .FirstOrDefault(t => t.Name == "UserRepository");

                if (userRepo == null)
                    return new List<string>();

                // Get user permissions from database
                var userPermissions = await _unitOfWork.GetRepository<UserPermission>().FindAsync(up =>
                    up.UserId == userId &&
                    up.IsGranted &&
                    (up.ExpiresAt == null || up.ExpiresAt > DateTime.UtcNow));

                var permissions = new List<string>();
                foreach (var up in userPermissions)
                {
                    var permission = await _unitOfWork.GetRepository<Permission>().GetByIdAsync(up.PermissionId);
                    if (permission != null && permission.IsActive)
                    {
                        permissions.Add(permission.Name);
                    }
                }

                _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(15));
                return permissions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user permissions for user {UserId}", userId);
                return new List<string>();
            }
        }

        public async Task<List<string>> GetRolePermissionsAsync(UserRole role)
        {
            var cacheKey = $"role_permissions_{role}";

            if (_cache.TryGetValue(cacheKey, out List<string>? cachedPermissions))
                return cachedPermissions ?? new List<string>();

            try
            {
                var rolePermissions = await _unitOfWork.GetRepository<RolePermission>().FindAsync(rp =>
                    rp.Role == role && rp.IsGranted);

                var permissions = new List<string>();
                foreach (var rp in rolePermissions)
                {
                    var permission = await _unitOfWork.GetRepository<Permission>().GetByIdAsync(rp.PermissionId);
                    if (permission != null && permission.IsActive)
                    {
                        permissions.Add(permission.Name);
                    }
                }

                _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(30));
                return permissions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get role permissions for role {Role}", role);
                return GetBasicPermissionsForRole(role);
            }
        }

        private List<string> GetBasicPermissionsForRole(UserRole role)
        {
            return role switch
            {
                UserRole.Dev => new List<string>
        {
            "system.admin",
            "permissions.manage",
            "jobs.manage",
            "users.manage",
            "pages.manage",
            "components.manage",
            "stores.manage",
            "company.manage",
            "roles.assign" 
        },
                UserRole.Admin => new List<string>
        {
            "users.manage",
            "pages.manage",
            "components.manage",
            "stores.manage",
            "company.manage"
        },
                UserRole.Customer => new List<string>
        {
            "pages.view",
            "profile.manage"
        },
                _ => new List<string>()
            };
        }

    }
}