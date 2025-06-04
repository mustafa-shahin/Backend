using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Backend.CMS.Application.Common.Interfaces;

namespace Backend.CMS.Infrastructure.Data
{
    public class CmsDbContext : DbContext
    {
        private readonly string _tenantId;

        public CmsDbContext(DbContextOptions<CmsDbContext> options, ITenantService tenantService)
            : base(options)
        {
            _tenantId = tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("Tenant ID is required");
        }

        public DbSet<Page> Pages { get; set; }
        public DbSet<PageComponent> PageComponents { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<PagePermission> PagePermissions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply configurations
            modelBuilder.ApplyConfiguration(new PageConfiguration());
            modelBuilder.ApplyConfiguration(new PageComponentConfiguration());
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new RoleConfiguration());
            modelBuilder.ApplyConfiguration(new PermissionConfiguration());
            modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
            modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
            modelBuilder.ApplyConfiguration(new PagePermissionConfiguration());

            // Apply global query filter for multi-tenancy
            modelBuilder.Entity<Page>().HasQueryFilter(e => e.TenantId == _tenantId);
            modelBuilder.Entity<User>().HasQueryFilter(e => e.TenantId == _tenantId);
            modelBuilder.Entity<Role>().HasQueryFilter(e => e.TenantId == _tenantId);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedOn = DateTime.UtcNow;
                        if (entry.Entity is ITenantEntity tenantEntity)
                        {
                            tenantEntity.TenantId = _tenantId;
                        }
                        break;
                    case EntityState.Modified:
                        entry.Entity.ModifiedOn = DateTime.UtcNow;
                        break;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}