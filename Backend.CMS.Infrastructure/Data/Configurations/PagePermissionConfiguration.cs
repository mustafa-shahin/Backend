using Backend.CMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.CMS.Infrastructure.Data.Configurations
{
    public class PagePermissionConfiguration : IEntityTypeConfiguration<PagePermission>
    {
        public void Configure(EntityTypeBuilder<PagePermission> builder)
        {
            builder.HasKey(e => new { e.PageId, e.RoleId });
        }
    }
}
