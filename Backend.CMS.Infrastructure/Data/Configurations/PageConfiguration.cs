using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.Data.Configurations
{
    public class PageConfiguration : IEntityTypeConfiguration<Page>
    {
        public void Configure(EntityTypeBuilder<Page> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.Slug)
                .IsRequired()
                .HasMaxLength(200);

            builder.HasIndex(e => new { e.TenantId, e.Slug })
                .IsUnique();

            builder.Property(e => e.Description)
                .HasMaxLength(500);

            builder.Property(e => e.MetaTitle)
                .HasMaxLength(200);

            builder.Property(e => e.MetaDescription)
                .HasMaxLength(500);

            builder.Property(e => e.MetaKeywords)
                .HasMaxLength(500);

            builder.Property(e => e.Template)
                .HasMaxLength(100);

            builder.HasOne(e => e.ParentPage)
                .WithMany(e => e.ChildPages)
                .HasForeignKey(e => e.ParentPageId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(e => e.Components)
                .WithOne(e => e.Page)
                .HasForeignKey(e => e.PageId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.Permissions)
                .WithOne(e => e.Page)
                .HasForeignKey(e => e.PageId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
