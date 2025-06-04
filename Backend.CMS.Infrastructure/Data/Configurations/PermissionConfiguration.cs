using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.Data.Configurations
{
    public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.Resource)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Action)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(e => new { e.Resource, e.Action })
                .IsUnique();

            builder.HasMany(e => e.RolePermissions)
                .WithOne(e => e.Permission)
                .HasForeignKey(e => e.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
