using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Backend.CMS.Domain.Entities;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Data.Configurations
{
    public class PageComponentConfiguration : IEntityTypeConfiguration<PageComponent>
    {
        public void Configure(EntityTypeBuilder<PageComponent> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.ComponentType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.ComponentName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.ContainerName)
                .HasMaxLength(100);

            // Configure JSON columns for PostgreSQL
            builder.Property(e => e.Settings)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions)null!)!
                );

            builder.Property(e => e.Content)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions)null!)!
                );

            builder.HasIndex(e => new { e.PageId, e.Order });
        }
    }
}
