using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Linq.Expressions;
using System.Text.Json;
using FileAccess = Backend.CMS.Domain.Entities.FileAccess;

namespace Backend.CMS.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        // Define the converters as static readonly fields
        private static readonly ValueConverter<Dictionary<string, object>, string> dictionaryConverter =
            new ValueConverter<Dictionary<string, object>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

        private static readonly ValueConverter<List<string>, string> listConverter =
            new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        public ApplicationDbContext(DbContextOptions options, IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Users and Authentication
        public DbSet<User> Users { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        // Pages and Components
        public DbSet<Page> Pages { get; set; }
        public DbSet<PageComponent> PageComponents { get; set; }
        public DbSet<PageVersion> PageVersions { get; set; }

        // Company 
        public DbSet<Company> Companies { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<LocationOpeningHour> LocationOpeningHours { get; set; }

        // Address and Contact Details
        public DbSet<Address> Addresses { get; set; }
        public DbSet<ContactDetails> ContactDetails { get; set; }

        // Component Templates
        public DbSet<ComponentTemplate> ComponentTemplates { get; set; }

        // Permissions
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }

        // Files
        public DbSet<FileEntity> Files { get; set; }
        public DbSet<Folder> Folders { get; set; }
        public DbSet<Backend.CMS.Domain.Entities.FileAccess> FileAccesses { get; set; }

        // Search and Indexing
        public DbSet<SearchIndex> SearchIndexes { get; set; }
        public DbSet<IndexingJob> IndexingJobs { get; set; }
        public DbSet<UserExternalLogin> UserExternalLogins { get; set; }

        // Product catalog DbSets
        public DbSet<Category> Categories { get; set; }
        public DbSet<CategoryImage> CategoryImages { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<ProductVariantImage> ProductVariantImages { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<ProductOption> ProductOptions { get; set; }
        public DbSet<ProductOptionValue> ProductOptionValues { get; set; }

        private void ConfigureIndexes(ModelBuilder modelBuilder)
        {
            // User indexes
            modelBuilder.Entity<User>()
                .HasIndex(u => new { u.Email, u.IsDeleted })
                .HasDatabaseName("IX_Users_Email_IsDeleted");

            modelBuilder.Entity<User>()
                .HasIndex(u => new { u.Role, u.IsActive, u.IsDeleted })
                .HasDatabaseName("IX_Users_Role_IsActive_IsDeleted");

            // Permission indexes
            modelBuilder.Entity<RolePermission>()
                .HasIndex(rp => new { rp.Role, rp.IsGranted, rp.IsDeleted })
                .HasDatabaseName("IX_RolePermissions_Role_IsGranted_IsDeleted");

            modelBuilder.Entity<UserPermission>()
                .HasIndex(up => new { up.UserId, up.IsGranted, up.ExpiresAt, up.IsDeleted })
                .HasDatabaseName("IX_UserPermissions_UserId_IsGranted_ExpiresAt_IsDeleted");

            // Page indexes
            modelBuilder.Entity<Page>()
                .HasIndex(p => new { p.Status, p.IsDeleted })
                .HasDatabaseName("IX_Pages_Status_IsDeleted");

            modelBuilder.Entity<Page>()
                .HasIndex(p => new { p.ParentPageId, p.Priority, p.IsDeleted })
                .HasDatabaseName("IX_Pages_ParentPageId_Priority_IsDeleted");

            // Session indexes
            modelBuilder.Entity<UserSession>()
                .HasIndex(s => new { s.UserId, s.IsRevoked, s.ExpiresAt })
                .HasDatabaseName("IX_UserSessions_UserId_IsRevoked_ExpiresAt");
        }

        private void ConfigureFileEntities(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<FileEntity>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Basic string properties
                entity.Property(e => e.OriginalFileName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.StoredFileName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
                entity.Property(e => e.FileExtension).HasMaxLength(10);
                entity.Property(e => e.FileType).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Alt).HasMaxLength(255);
                entity.Property(e => e.Hash).HasMaxLength(100);
                entity.Property(e => e.ProcessingStatus).HasMaxLength(50);

                // File content stored as byte arrays - REMOVED FilePath and ThumbnailPath
                entity.Property(e => e.FileContent)
                    .IsRequired()
                    .HasColumnType("bytea"); // PostgreSQL binary data type

                entity.Property(e => e.ThumbnailContent)
                    .HasColumnType("bytea"); // PostgreSQL binary data type, nullable

                // Numeric properties
                entity.Property(e => e.FileSize).IsRequired();
                entity.Property(e => e.DownloadCount).HasDefaultValue(0);
                entity.Property(e => e.Width);
                entity.Property(e => e.Height);
                entity.Property(e => e.Duration);

                // Boolean properties
                entity.Property(e => e.IsPublic).HasDefaultValue(false);
                entity.Property(e => e.IsProcessed).HasDefaultValue(true);

                // JSON properties
                entity.Property(e => e.Metadata)
                    .HasConversion(dictionaryConverter)
                    .HasColumnType("jsonb"); // PostgreSQL JSONB for better performance

                entity.Property(e => e.Tags)
                    .HasConversion(dictionaryConverter)
                    .HasColumnType("jsonb"); // PostgreSQL JSONB for better performance

                // Performance indexes
                entity.HasIndex(e => e.Hash)
                    .HasDatabaseName("IX_Files_Hash");

                entity.HasIndex(e => e.FileType)
                    .HasDatabaseName("IX_Files_FileType");

                entity.HasIndex(e => e.ContentType)
                    .HasDatabaseName("IX_Files_ContentType");

                entity.HasIndex(e => e.IsPublic)
                    .HasDatabaseName("IX_Files_IsPublic");

                entity.HasIndex(e => e.FolderId)
                    .HasDatabaseName("IX_Files_FolderId");

                // Composite indexes for common query patterns
                entity.HasIndex(e => new { e.FolderId, e.FileType })
                    .HasDatabaseName("IX_Files_Folder_Type")
                    .HasFilter("\"IsDeleted\" = false"); // Only index non-deleted files

                entity.HasIndex(e => new { e.IsPublic, e.CreatedAt })
                    .HasDatabaseName("IX_Files_Public_Created")
                    .HasFilter("\"IsDeleted\" = false");

                entity.HasIndex(e => new { e.FileType, e.IsPublic })
                    .HasDatabaseName("IX_Files_Type_Public")
                    .HasFilter("\"IsDeleted\" = false");

                entity.HasIndex(e => new { e.CreatedAt, e.IsDeleted })
                    .HasDatabaseName("IX_Files_Created_Deleted");

                // Foreign key relationship
                entity.HasOne(e => e.Folder)
                    .WithMany(f => f.Files)
                    .HasForeignKey(e => e.FolderId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Table name
                entity.ToTable("Files");
            });

            // Folder configuration
            modelBuilder.Entity<Folder>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Path).HasMaxLength(1000).IsRequired();
                entity.Property(e => e.FolderType).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.IsPublic).HasDefaultValue(false);

                entity.Property(e => e.Metadata)
                    .HasConversion(dictionaryConverter)
                    .HasColumnType("jsonb");

                // Indexes
                entity.HasIndex(e => e.Path).IsUnique();
                entity.HasIndex(e => e.ParentFolderId);
                entity.HasIndex(e => e.FolderType);
                entity.HasIndex(e => new { e.ParentFolderId, e.Name })
                    .HasFilter("\"IsDeleted\" = false");

                // Self-referencing relationship
                entity.HasOne(e => e.ParentFolder)
                    .WithMany(f => f.SubFolders)
                    .HasForeignKey(e => e.ParentFolderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.ToTable("Folders");
            });

            // FileAccess configuration
            modelBuilder.Entity<FileAccess>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.IpAddress).HasMaxLength(45); // IPv6 support
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.AccessType).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.AccessedAt).IsRequired();

                // Indexes for analytics
                entity.HasIndex(e => e.FileId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.AccessedAt);
                entity.HasIndex(e => new { e.FileId, e.AccessedAt });
                entity.HasIndex(e => new { e.UserId, e.AccessedAt });

                // Foreign key relationships
                entity.HasOne(e => e.File)
                    .WithMany()
                    .HasForeignKey(e => e.FileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.ToTable("FileAccess");
            });
        }

        private void ConfigureSearchEntities(ModelBuilder modelBuilder)
        {
            // SearchIndex configuration
            modelBuilder.Entity<SearchIndex>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(500);
                entity.Property(e => e.Content).HasColumnType("text");
                entity.Property(e => e.SearchVector).HasColumnType("text");
                entity.Property(e => e.Metadata).HasConversion(dictionaryConverter);

                entity.HasIndex(e => new { e.EntityType, e.EntityId }).IsUnique();
                entity.HasIndex(e => e.EntityType);
                entity.HasIndex(e => e.IsPublic);
                entity.HasIndex(e => e.LastIndexedAt);

                // PostgreSQL full-text search index
                entity.HasIndex(e => e.SearchVector).HasDatabaseName("IX_SearchIndex_SearchVector");
            });

            // IndexingJob configuration
            modelBuilder.Entity<IndexingJob>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.JobType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
                entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
                entity.Property(e => e.JobMetadata).HasConversion(dictionaryConverter);

                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.StartedAt);
                entity.HasIndex(e => e.JobType);
            });
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var dictionaryConverter = new ValueConverter<Dictionary<string, object>, string>(
     v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
     v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions)null!) ?? new Dictionary<string, object>()
 );
            // Configure global query filters for soft delete
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .HasQueryFilter(CreateSoftDeleteFilter(entityType.ClrType));
                }
            }

            modelBuilder.Entity<UserExternalLogin>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Provider, e.ExternalUserId }).IsUnique();
                entity.Property(e => e.Provider).HasMaxLength(50).IsRequired();
                entity.Property(e => e.ExternalUserId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Email).HasMaxLength(256);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.Claims).HasConversion(dictionaryConverter);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.ExternalLogins)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.Email).HasMaxLength(256);
                entity.Property(e => e.Username).HasMaxLength(256);
                entity.Property(e => e.FirstName).HasMaxLength(100);
                entity.Property(e => e.LastName).HasMaxLength(100);
                entity.Property(e => e.RecoveryCodes).HasConversion(listConverter);
                entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(50);

                entity.HasOne(e => e.Picture)
                    .WithMany()
                    .HasForeignKey(e => e.PictureFileId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.PictureFileId);

                // User relationships
                entity.HasMany(e => e.Sessions)
                    .WithOne(e => e.User)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.PasswordResetTokens)
                    .WithOne(e => e.User)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                //  User to Address relationship
                entity.HasMany(e => e.Addresses)
                    .WithOne()
                    .HasForeignKey("UserId")
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Cascade);

                //  User to ContactDetails relationship
                entity.HasMany(e => e.ContactDetails)
                    .WithOne()
                    .HasForeignKey("UserId")
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(u => u.ExternalLogins)
                      .WithOne(el => el.User)
                      .HasForeignKey(el => el.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Address configuration - polymorphic relationships
            modelBuilder.Entity<Address>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Street).HasMaxLength(500);
                entity.Property(e => e.Street2).HasMaxLength(500);
                entity.Property(e => e.City).HasMaxLength(100);
                entity.Property(e => e.State).HasMaxLength(100);
                entity.Property(e => e.Country).HasMaxLength(100);
                entity.Property(e => e.PostalCode).HasMaxLength(20);
                entity.Property(e => e.Region).HasMaxLength(100);
                entity.Property(e => e.District).HasMaxLength(100);
                entity.Property(e => e.AddressType).HasMaxLength(50);
                entity.Property(e => e.Notes).HasMaxLength(1000);

                entity.Property<int?>("UserId");
                entity.Property<int?>("CompanyId");
                entity.Property<int?>("LocationId");

                // Add indexes for better performance
                entity.HasIndex("UserId");
                entity.HasIndex("CompanyId");
                entity.HasIndex("LocationId");
            });

            // ContactDetails configuration - polymorphic relationships
            modelBuilder.Entity<ContactDetails>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PrimaryPhone).HasMaxLength(50);
                entity.Property(e => e.SecondaryPhone).HasMaxLength(50);
                entity.Property(e => e.Mobile).HasMaxLength(50);
                entity.Property(e => e.Fax).HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(256);
                entity.Property(e => e.SecondaryEmail).HasMaxLength(256);
                entity.Property(e => e.Website).HasMaxLength(500);
                entity.Property(e => e.LinkedInProfile).HasMaxLength(500);
                entity.Property(e => e.TwitterProfile).HasMaxLength(500);
                entity.Property(e => e.FacebookProfile).HasMaxLength(500);
                entity.Property(e => e.InstagramProfile).HasMaxLength(500);
                entity.Property(e => e.WhatsAppNumber).HasMaxLength(50);
                entity.Property(e => e.TelegramHandle).HasMaxLength(100);
                entity.Property(e => e.ContactType).HasMaxLength(50);
                entity.Property(e => e.AdditionalContacts).HasConversion(dictionaryConverter);


                entity.Property<int?>("UserId");
                entity.Property<int?>("CompanyId");
                entity.Property<int?>("LocationId");

                entity.HasIndex("UserId");
                entity.HasIndex("CompanyId");
                entity.HasIndex("LocationId");
            });



            // PasswordResetToken configuration
            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.Property(e => e.Token).HasMaxLength(500);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
            });

            // UserSession configuration
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.RefreshToken).IsUnique();
                entity.Property(e => e.RefreshToken).HasMaxLength(500);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
            });

            // Page configuration
            modelBuilder.Entity<Page>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Slug).IsUnique();
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.Slug).HasMaxLength(200);
                entity.Property(e => e.MetaTitle).HasMaxLength(200);
                entity.Property(e => e.MetaDescription).HasMaxLength(500);

                entity.HasOne(e => e.ParentPage)
                    .WithMany(e => e.ChildPages)
                    .HasForeignKey(e => e.ParentPageId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Components)
                    .WithOne(e => e.Page)
                    .HasForeignKey(e => e.PageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // PageComponent configuration
            modelBuilder.Entity<PageComponent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.Properties).HasConversion(dictionaryConverter);
                entity.Property(e => e.Styles).HasConversion(dictionaryConverter);
                entity.Property(e => e.Content).HasConversion(dictionaryConverter);
                entity.Property(e => e.Settings).HasConversion(dictionaryConverter); // ADD THIS LINE
                entity.Property(e => e.ResponsiveSettings).HasConversion(dictionaryConverter);
                entity.Property(e => e.AnimationSettings).HasConversion(dictionaryConverter);
                entity.Property(e => e.InteractionSettings).HasConversion(dictionaryConverter);

                entity.HasOne(e => e.ParentComponent)
                    .WithMany(e => e.ChildComponents)
                    .HasForeignKey(e => e.ParentComponentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // PageVersion configuration
            modelBuilder.Entity<PageVersion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.PageId, e.VersionNumber }).IsUnique();

                entity.HasOne(e => e.Page)
                    .WithMany()
                    .HasForeignKey(e => e.PageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ComponentTemplate configuration
            modelBuilder.Entity<ComponentTemplate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.DisplayName).HasMaxLength(200);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.Icon).HasMaxLength(100);
                entity.Property(e => e.DefaultProperties).HasConversion(dictionaryConverter);
                entity.Property(e => e.DefaultStyles).HasConversion(dictionaryConverter);
                entity.Property(e => e.Schema).HasConversion(dictionaryConverter);
                entity.Property(e => e.ConfigSchema).HasConversion(dictionaryConverter);
            });

            // Permission configuration
            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).HasMaxLength(100);
                entity.Property(e => e.DisplayName).HasMaxLength(200);
                entity.Property(e => e.Category).HasMaxLength(50);
            });

            // RolePermission configuration
            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Role, e.PermissionId }).IsUnique();
                entity.Property(e => e.Role).HasConversion<string>();

                entity.HasOne(e => e.Permission)
                    .WithMany(p => p.RolePermissions)
                    .HasForeignKey(e => e.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserPermission configuration
            modelBuilder.Entity<UserPermission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.PermissionId }).IsUnique();

                entity.HasOne(e => e.User)
                    .WithMany(u => u.UserPermissions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Permission)
                    .WithMany(p => p.UserPermissions)
                    .HasForeignKey(e => e.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<Company>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.BrandingSettings).HasConversion(dictionaryConverter);
                entity.Property(e => e.BusinessSettings).HasConversion(dictionaryConverter);

                entity.HasMany(e => e.Locations)
                    .WithOne(e => e.Company)
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Company to Address relationship
                entity.HasMany(e => e.Addresses)
                    .WithOne()
                    .HasForeignKey("CompanyId")
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Cascade);

                // Company to ContactDetails relationship
                entity.HasMany(e => e.ContactDetails)
                    .WithOne()
                    .HasForeignKey("CompanyId")
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Location configuration - relationships
            modelBuilder.Entity<Location>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.LocationCode).HasMaxLength(50);
                entity.Property(e => e.LocationType).HasMaxLength(50);
                entity.Property(e => e.LocationSettings).HasConversion(dictionaryConverter);
                entity.Property(e => e.AdditionalInfo).HasConversion(dictionaryConverter);

                entity.HasIndex(e => e.LocationCode).IsUnique();

                entity.HasMany(e => e.OpeningHours)
                    .WithOne(e => e.Location)
                    .HasForeignKey(e => e.LocationId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Location to Address relationship
                entity.HasMany(e => e.Addresses)
                    .WithOne()
                    .HasForeignKey("LocationId")
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Cascade);

                // Location to ContactDetails relationship
                entity.HasMany(e => e.ContactDetails)
                    .WithOne()
                    .HasForeignKey("LocationId")
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // LocationOpeningHour configuration
            modelBuilder.Entity<LocationOpeningHour>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.LocationId, e.DayOfWeek }).IsUnique();
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Slug).IsUnique();
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Slug).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.ShortDescription).HasMaxLength(500);
                entity.Property(e => e.MetaTitle).HasMaxLength(200);
                entity.Property(e => e.MetaDescription).HasMaxLength(500);
                entity.Property(e => e.MetaKeywords).HasMaxLength(500);
                entity.Property(e => e.CustomFields).HasConversion(dictionaryConverter);

                entity.HasOne(e => e.ParentCategory)
                    .WithMany(e => e.SubCategories)
                    .HasForeignKey(e => e.ParentCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Images)
                    .WithOne(e => e.Category)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ParentCategoryId);
                entity.HasIndex(e => new { e.IsActive, e.IsVisible });
                entity.HasIndex(e => e.SortOrder);
            });

            // CategoryImage configuration
            modelBuilder.Entity<CategoryImage>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Category)
                    .WithMany(e => e.Images)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.File)
                    .WithMany()
                    .HasForeignKey(e => e.FileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Alt).HasMaxLength(255);
                entity.Property(e => e.Caption).HasMaxLength(500);

                entity.HasIndex(e => e.CategoryId);
                entity.HasIndex(e => e.FileId);
                entity.HasIndex(e => new { e.CategoryId, e.Position });
                entity.HasIndex(e => new { e.CategoryId, e.IsFeatured });
            });

            // Product configuration
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Slug).IsUnique();
                entity.HasIndex(e => e.SKU).IsUnique();
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Slug).HasMaxLength(200).IsRequired();
                entity.Property(e => e.SKU).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).HasColumnType("text");
                entity.Property(e => e.ShortDescription).HasMaxLength(500);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CompareAtPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CostPerItem).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Weight).HasColumnType("decimal(18,3)");
                entity.Property(e => e.WeightUnit).HasMaxLength(10);
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.Type).HasConversion<string>();
                entity.Property(e => e.Vendor).HasMaxLength(200);
                entity.Property(e => e.Barcode).HasMaxLength(100);
                entity.Property(e => e.Tags).HasMaxLength(1000);
                entity.Property(e => e.Template).HasMaxLength(100);
                entity.Property(e => e.MetaTitle).HasMaxLength(200);
                entity.Property(e => e.MetaDescription).HasMaxLength(500);
                entity.Property(e => e.MetaKeywords).HasMaxLength(500);
                entity.Property(e => e.SearchKeywords).HasMaxLength(1000);
                entity.Property(e => e.CustomFields).HasConversion(dictionaryConverter);
                entity.Property(e => e.SEOSettings).HasConversion(dictionaryConverter);

                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.Price);
                entity.HasIndex(e => e.Vendor);
                entity.HasIndex(e => e.PublishedAt);
            });

            // ProductVariant configuration
            modelBuilder.Entity<ProductVariant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SKU).IsUnique();
                entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
                entity.Property(e => e.SKU).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CompareAtPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CostPerItem).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Weight).HasColumnType("decimal(18,3)");
                entity.Property(e => e.WeightUnit).HasMaxLength(10);
                entity.Property(e => e.Barcode).HasMaxLength(100);
                entity.Property(e => e.Option1).HasMaxLength(100);
                entity.Property(e => e.Option2).HasMaxLength(100);
                entity.Property(e => e.Option3).HasMaxLength(100);
                entity.Property(e => e.CustomFields).HasConversion(dictionaryConverter);

                entity.HasOne(e => e.Product)
                    .WithMany(e => e.Variants)
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Images)
                    .WithOne(e => e.ProductVariant)
                    .HasForeignKey(e => e.ProductVariantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ProductId);
                entity.HasIndex(e => new { e.ProductId, e.IsDefault });
                entity.HasIndex(e => new { e.ProductId, e.Position });
            });

            // ProductVariantImage configuration
            modelBuilder.Entity<ProductVariantImage>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.ProductVariant)
                    .WithMany(e => e.Images)
                    .HasForeignKey(e => e.ProductVariantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.File)
                    .WithMany()
                    .HasForeignKey(e => e.FileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Alt).HasMaxLength(255);
                entity.Property(e => e.Caption).HasMaxLength(500);

                entity.HasIndex(e => e.ProductVariantId);
                entity.HasIndex(e => e.FileId);
                entity.HasIndex(e => new { e.ProductVariantId, e.Position });
                entity.HasIndex(e => new { e.ProductVariantId, e.IsFeatured });
            });

            // ProductCategory configuration (many-to-many)
            modelBuilder.Entity<ProductCategory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ProductId, e.CategoryId }).IsUnique();

                entity.HasOne(e => e.Product)
                    .WithMany(e => e.ProductCategories)
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Category)
                    .WithMany(e => e.ProductCategories)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ProductImage configuration
            modelBuilder.Entity<ProductImage>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Product)
                    .WithMany(e => e.Images)
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.File)
                    .WithMany()
                    .HasForeignKey(e => e.FileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Alt).HasMaxLength(255);
                entity.Property(e => e.Caption).HasMaxLength(500);

                entity.HasIndex(e => e.ProductId);
                entity.HasIndex(e => e.FileId);
                entity.HasIndex(e => new { e.ProductId, e.Position });
                entity.HasIndex(e => new { e.ProductId, e.IsFeatured });
            });

            // ProductOption configuration
            modelBuilder.Entity<ProductOption>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();

                entity.HasOne(e => e.Product)
                    .WithMany(e => e.Options)
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ProductId);
                entity.HasIndex(e => new { e.ProductId, e.Position });
            });

            // ProductOptionValue configuration
            modelBuilder.Entity<ProductOptionValue>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Value).HasMaxLength(100).IsRequired();

                entity.HasOne(e => e.ProductOption)
                    .WithMany(e => e.Values)
                    .HasForeignKey(e => e.ProductOptionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ProductOptionId);
                entity.HasIndex(e => new { e.ProductOptionId, e.Position });
            });
            // Configure audit trail relationships WITHOUT circular references
            ConfigureAuditTrailRelationships(modelBuilder);

            // Set value comparers for collections to avoid EF warnings
            ConfigureSearchEntities(modelBuilder);
            SetValueComparers(modelBuilder);
            ConfigureIndexes(modelBuilder);
            ConfigureFileEntities(modelBuilder);
        }

        private void ConfigureAuditTrailRelationships(ModelBuilder modelBuilder)
        {
            // Get all entity types that inherit from BaseEntity
            var baseEntityTypes = modelBuilder.Model.GetEntityTypes()
                .Where(e => typeof(BaseEntity).IsAssignableFrom(e.ClrType))
                .ToList();

            foreach (var entityType in baseEntityTypes)
            {
                // Skip User entity to avoid circular reference
                if (entityType.ClrType == typeof(User))
                    continue;

                var entityBuilder = modelBuilder.Entity(entityType.ClrType);

                // Configure shadow foreign key properties explicitly
                entityBuilder.Property<int?>("CreatedByUserId");
                entityBuilder.Property<int?>("UpdatedByUserId");
                entityBuilder.Property<int?>("DeletedByUserId");

                // Configure relationships using shadow properties only (no navigation properties)
                entityBuilder
                    .HasOne(typeof(User))
                    .WithMany()
                    .HasForeignKey("CreatedByUserId")
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);

                entityBuilder
                    .HasOne(typeof(User))
                    .WithMany()
                    .HasForeignKey("UpdatedByUserId")
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);

                entityBuilder
                    .HasOne(typeof(User))
                    .WithMany()
                    .HasForeignKey("DeletedByUserId")
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);

                // Add indexes for better performance
                entityBuilder.HasIndex("CreatedByUserId");
                entityBuilder.HasIndex("UpdatedByUserId");
                entityBuilder.HasIndex("DeletedByUserId");
            }

            // Special handling for User entity - only configure the shadow properties without relationships
            var userEntityBuilder = modelBuilder.Entity<User>();
            userEntityBuilder.Property<int?>("CreatedByUserId");
            userEntityBuilder.Property<int?>("UpdatedByUserId");
            userEntityBuilder.Property<int?>("DeletedByUserId");

            // Add indexes for User audit fields
            userEntityBuilder.HasIndex("CreatedByUserId");
            userEntityBuilder.HasIndex("UpdatedByUserId");
            userEntityBuilder.HasIndex("DeletedByUserId");
        }

        private static LambdaExpression CreateSoftDeleteFilter(Type entityType)
        {
            var parameter = Expression.Parameter(entityType, "e");
            var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
            var condition = Expression.Equal(property, Expression.Constant(false));
            return Expression.Lambda(condition, parameter);
        }

        private void SetValueComparers(ModelBuilder modelBuilder)
        {
            var dictionaryComparer = new ValueComparer<Dictionary<string, object>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToDictionary(k => k.Key, k => k.Value));

            var listComparer = new ValueComparer<List<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            // Apply value comparers to all entities with dictionary/list properties
            ApplyComparersToEntity<User>(modelBuilder, dictionaryComparer, listComparer);
            ApplyComparersToEntity<Company>(modelBuilder, dictionaryComparer, listComparer);
            ApplyComparersToEntity<Location>(modelBuilder, dictionaryComparer, listComparer);
            ApplyComparersToEntity<ContactDetails>(modelBuilder, dictionaryComparer, listComparer);
            ApplyComparersToEntity<PageComponent>(modelBuilder, dictionaryComparer, listComparer); // ADD THIS LINE
            ApplyComparersToEntity<ComponentTemplate>(modelBuilder, dictionaryComparer, listComparer);
            ApplyComparersToEntity<FileEntity>(modelBuilder, dictionaryComparer, listComparer);
            ApplyComparersToEntity<Folder>(modelBuilder, dictionaryComparer, listComparer);
            ApplyComparersToEntity<SearchIndex>(modelBuilder, dictionaryComparer, listComparer);
            ApplyComparersToEntity<IndexingJob>(modelBuilder, dictionaryComparer, listComparer);
            ApplyComparersToEntity<Category>(modelBuilder, dictionaryComparer, listComparer);
            ApplyComparersToEntity<Product>(modelBuilder, dictionaryComparer, listComparer);
            ApplyComparersToEntity<ProductVariant>(modelBuilder, dictionaryComparer, listComparer);
        }

        private void ApplyComparersToEntity<T>(ModelBuilder modelBuilder, ValueComparer<Dictionary<string, object>> dictionaryComparer, ValueComparer<List<string>> listComparer) where T : class
        {
            var entity = modelBuilder.Entity<T>();
            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(Dictionary<string, object>))
                {
                    entity.Property(property.Name).Metadata.SetValueComparer(dictionaryComparer);
                }
                else if (property.PropertyType == typeof(List<string>))
                {
                    entity.Property(property.Name).Metadata.SetValueComparer(listComparer);
                }
            }
        }

        public override int SaveChanges()
        {
            UpdateAuditFields();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateAuditFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateAuditFields()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

            var currentUserId = GetCurrentUserId();

            foreach (var entry in entries)
            {
                var entity = (BaseEntity)entry.Entity;
                var now = DateTime.UtcNow;

                if (entry.State == EntityState.Added)
                {
                    entity.CreatedAt = now;
                    entity.UpdatedAt = now;
                    entity.CreatedByUserId = currentUserId;
                    entity.UpdatedByUserId = currentUserId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entity.UpdatedAt = now;
                    entity.UpdatedByUserId = currentUserId;
                }
            }
        }

        private int? GetCurrentUserId()
        {
            try
            {
                var httpContext = _httpContextAccessor?.HttpContext;
                if (httpContext?.User?.Identity?.IsAuthenticated != true)
                    return null;

                var userIdClaim = httpContext.User.FindFirst("sub") ??
                                 httpContext.User.FindFirst("userId") ??
                                 httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                    return userId;

                return null;
            }
            catch
            {
                return null; // Fail gracefully
            }
        }

        /// <summary>
        /// Include soft deleted entities in queries (for admin purposes)
        /// </summary>
        public IQueryable<T> IncludeDeleted<T>() where T : BaseEntity
        {
            return Set<T>().IgnoreQueryFilters();
        }

        /// <summary>
        /// Get only deleted entities
        /// </summary>
        public IQueryable<T> OnlyDeleted<T>() where T : BaseEntity
        {
            return Set<T>().IgnoreQueryFilters().Where(e => e.IsDeleted);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Configure for file storage performance
                optionsBuilder.UseNpgsql(options =>
                {
                    // Increase command timeout for large file operations
                    options.CommandTimeout(300); // 5 minutes

                    // Enable retry on failure for large operations
                    options.EnableRetryOnFailure(maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                });

                // Enable lazy loading for better performance with file metadata
                optionsBuilder.UseLazyLoadingProxies();

                // Enable sensitive data logging in development
#if DEBUG
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.EnableDetailedErrors();
#endif
            }

            base.OnConfiguring(optionsBuilder);
        }
    }
}