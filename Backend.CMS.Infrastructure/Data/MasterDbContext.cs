using Microsoft.EntityFrameworkCore;
using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.Data
{
    public class MasterDbContext : DbContext
    {
        public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options)
        {
        }

        public DbSet<Tenant> Tenants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Identifier).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.Identifier).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.ConnectionString).IsRequired();
            });
        }
    }
}
