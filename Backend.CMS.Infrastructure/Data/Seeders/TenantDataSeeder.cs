using Microsoft.EntityFrameworkCore;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.Data.Seeders
{
    public static class TenantDataSeeder
    {
        public static async Task SeedAsync(CmsDbContext context, string tenantId)
        {
            // Seed permissions
            if (!await context.Permissions.AnyAsync())
            {
                var permissions = new[]
                {
                    new Permission { Name = "View Pages", Resource = "Pages", Action = "View" },
                    new Permission { Name = "Create Pages", Resource = "Pages", Action = "Create" },
                    new Permission { Name = "Edit Pages", Resource = "Pages", Action = "Edit" },
                    new Permission { Name = "Delete Pages", Resource = "Pages", Action = "Delete" },
                    new Permission { Name = "Publish Pages", Resource = "Pages", Action = "Publish" },
                    new Permission { Name = "Design Pages", Resource = "Pages", Action = "Design" }
                };

                context.Permissions.AddRange(permissions);
                await context.SaveChangesAsync();
            }

            // Seed roles
            if (!await context.Roles.AnyAsync())
            {
                var adminRole = new Role
                {
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    Description = "Full system access",
                    TenantId = tenantId
                };

                var editorRole = new Role
                {
                    Name = "Editor",
                    NormalizedName = "EDITOR",
                    Description = "Can create and edit content",
                    TenantId = tenantId
                };

                context.Roles.AddRange(adminRole, editorRole);
                await context.SaveChangesAsync();

                // Assign all permissions to admin role
                var allPermissions = await context.Permissions.ToListAsync();
                foreach (var permission in allPermissions)
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = adminRole.Id,
                        PermissionId = permission.Id
                    });
                }

                // Assign limited permissions to editor role
                var editorPermissions = allPermissions.Where(p => p.Action != "Delete" && p.Action != "Publish").ToList();
                foreach (var permission in editorPermissions)
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = editorRole.Id,
                        PermissionId = permission.Id
                    });
                }

                await context.SaveChangesAsync();
            }

            // Seed admin user
            if (!await context.Users.AnyAsync())
            {
                var adminRole = await context.Roles.FirstAsync(r => r.NormalizedName == "ADMIN");

                var adminUser = new User
                {
                    Email = "admin@demo.com",
                    Username = "admin",
                    FirstName = "Admin",
                    LastName = "User",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                    IsActive = true,
                    TenantId = tenantId
                };

                context.Users.Add(adminUser);
                await context.SaveChangesAsync();

                context.UserRoles.Add(new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id
                });

                await context.SaveChangesAsync();
            }

            // Seed sample pages
            if (!await context.Pages.AnyAsync())
            {
                var homePage = new Page
                {
                    Name = "Home",
                    Title = "Welcome to Our Website",
                    Slug = "home",
                    Description = "This is the home page",
                    Status = PageStatus.Published,
                    Template = "default",
                    Priority = 1,
                    PublishedOn = DateTime.UtcNow,
                    TenantId = tenantId
                };

                var aboutPage = new Page
                {
                    Name = "About Us",
                    Title = "About Our Company",
                    Slug = "about",
                    Description = "Learn more about us",
                    Status = PageStatus.Published,
                    Template = "default",
                    Priority = 2,
                    PublishedOn = DateTime.UtcNow,
                    TenantId = tenantId
                };

                context.Pages.AddRange(homePage, aboutPage);
                await context.SaveChangesAsync();
            }
        }
    }
}
