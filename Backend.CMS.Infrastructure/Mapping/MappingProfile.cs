using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            ConfigureAddressMappings();
            ConfigureContactDetailsMappings();
            ConfigureUserMappings();
            ConfigureCompanyMappings();
            ConfigureLocationMappings();
            ConfigurePageMappings();
            ConfigureSearchMappings();
            ConfigureCategoryMappings();
            ConfigureProductMappings();
            ConfigureProductVariantMappings();
            ConfigureProductImageMappings();
            ConfigureProductOptionMappings();
            ConfigureDesignerMapping();
            ConfigureImageMappings();
            ConfigureFileMappings();
        }

        private void ConfigureAddressMappings()
        {
            CreateMap<Address, AddressDto>();

            CreateMap<CreateAddressDto, Address>()
                .IgnoreAuditProperties();

            CreateMap<UpdateAddressDto, Address>()
                .IgnoreBaseEntityProperties();
        }

        private void ConfigureContactDetailsMappings()
        {
            CreateMap<ContactDetails, ContactDetailsDto>();

            CreateMap<CreateContactDetailsDto, ContactDetails>()
                .IgnoreAuditProperties();

            CreateMap<UpdateContactDetailsDto, ContactDetails>()
                .IgnoreBaseEntityProperties();
        }

        private void ConfigureUserMappings()
        {
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.ToString()))
                .ForMember(dest => dest.PictureUrl, opt => opt.MapFrom(src => src.PictureUrl))
                .ForMember(dest => dest.Addresses, opt => opt.MapFrom(src => src.Addresses.Where(a => !a.IsDeleted)))
                .ForMember(dest => dest.ContactDetails, opt => opt.MapFrom(src => src.ContactDetails.Where(c => !c.IsDeleted)));


            CreateMap<CreateUserDto, User>()
                .ForMember(dest => dest.PasswordHash, opt => opt.MapFrom(src => BCrypt.Net.BCrypt.HashPassword(src.Password)))
                .IgnoreAuditProperties()
                .ForMember(dest => dest.Picture, opt => opt.Ignore())
                .ForMember(dest => dest.Addresses, opt => opt.Ignore())
                .ForMember(dest => dest.ContactDetails, opt => opt.Ignore())
                .ForMember(dest => dest.Sessions, opt => opt.Ignore())
                .ForMember(dest => dest.PasswordResetTokens, opt => opt.Ignore());

            CreateMap<UpdateUserDto, User>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
                .ForMember(dest => dest.Picture, opt => opt.Ignore())
                .ForMember(dest => dest.Addresses, opt => opt.Ignore())
                .ForMember(dest => dest.ContactDetails, opt => opt.Ignore())
                .ForMember(dest => dest.Sessions, opt => opt.Ignore())
                .ForMember(dest => dest.PasswordResetTokens, opt => opt.Ignore());
            CreateMap<User, UserCacheDto>()
                .ForMember(dest => dest.RecoveryCodes, opt => opt.MapFrom(src => src.RecoveryCodes ?? new List<string>()));

            CreateMap<UserCacheDto, User>()
               .ForMember(dest => dest.Sessions, opt => opt.Ignore())
               .ForMember(dest => dest.PasswordResetTokens, opt => opt.Ignore())
               .ForMember(dest => dest.Addresses, opt => opt.Ignore())
               .ForMember(dest => dest.ContactDetails, opt => opt.Ignore())
               .ForMember(dest => dest.UserPermissions, opt => opt.Ignore())
               .ForMember(dest => dest.UserPermissions, opt => opt.Ignore())
               .ForMember(dest => dest.ExternalLogins, opt => opt.Ignore())
               .ForMember(dest => dest.Picture, opt => opt.Ignore());

            CreateMap<UserSession, UserSessionCacheDto>();
            CreateMap<UserSessionCacheDto, UserSession>()
                .ForMember(dest => dest.User, opt => opt.Ignore());
        }

        private void ConfigureCompanyMappings()
        {
            CreateMap<Company, CompanyDto>()
                .ForMember(dest => dest.Locations, opt => opt.MapFrom(src => src.Locations.Where(l => !l.IsDeleted)))
                .ForMember(dest => dest.Addresses, opt => opt.MapFrom(src => src.Addresses.Where(a => !a.IsDeleted)))
                .ForMember(dest => dest.ContactDetails, opt => opt.MapFrom(src => src.ContactDetails.Where(c => !c.IsDeleted)));

            CreateMap<UpdateCompanyDto, Company>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.Locations, opt => opt.Ignore())
                .ForMember(dest => dest.Addresses, opt => opt.Ignore())
                .ForMember(dest => dest.ContactDetails, opt => opt.Ignore());
        }

        private void ConfigureLocationMappings()
        {
            CreateMap<Location, LocationDto>()
                .ForMember(dest => dest.OpeningHours, opt => opt.MapFrom(src => src.OpeningHours.Where(oh => !oh.IsDeleted)))
                .ForMember(dest => dest.Addresses, opt => opt.MapFrom(src => src.Addresses.Where(a => !a.IsDeleted)))
                .ForMember(dest => dest.ContactDetails, opt => opt.MapFrom(src => src.ContactDetails.Where(c => !c.IsDeleted)));

            CreateMap<CreateLocationDto, Location>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.Company, opt => opt.Ignore())
                .ForMember(dest => dest.CompanyId, opt => opt.Ignore())
                .ForMember(dest => dest.OpeningHours, opt => opt.Ignore())
                .ForMember(dest => dest.Addresses, opt => opt.Ignore())
                .ForMember(dest => dest.ContactDetails, opt => opt.Ignore());

            CreateMap<UpdateLocationDto, Location>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.Company, opt => opt.Ignore())
                .ForMember(dest => dest.CompanyId, opt => opt.Ignore())
                .ForMember(dest => dest.OpeningHours, opt => opt.Ignore())
                .ForMember(dest => dest.Addresses, opt => opt.Ignore())
                .ForMember(dest => dest.ContactDetails, opt => opt.Ignore());

            // Location Opening Hours mappings
            CreateMap<LocationOpeningHour, LocationOpeningHourDto>();

            CreateMap<CreateLocationOpeningHourDto, LocationOpeningHour>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.Location, opt => opt.Ignore())
                .ForMember(dest => dest.LocationId, opt => opt.Ignore());

            CreateMap<UpdateLocationOpeningHourDto, LocationOpeningHour>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.Location, opt => opt.Ignore())
                .ForMember(dest => dest.LocationId, opt => opt.Ignore());
        }

        private void ConfigurePageMappings()
        {
            CreateMap<Page, PageDto>()
                .ForMember(dest => dest.ChildPages, opt => opt.MapFrom(src =>
                    src.ChildPages.Where(cp => !cp.IsDeleted).OrderBy(cp => cp.Priority).ThenBy(cp => cp.Name)));

            CreateMap<Page, PageListDto>()
                .ForMember(dest => dest.HasChildren, opt => opt.MapFrom(src =>
                    src.ChildPages.Any(cp => !cp.IsDeleted)));

            CreateMap<CreatePageDto, Page>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.ChildPages, opt => opt.Ignore())
                .ForMember(dest => dest.ParentPage, opt => opt.Ignore())
                .ForMember(dest => dest.PublishedOn, opt => opt.Ignore())
                .ForMember(dest => dest.PublishedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Versions, opt => opt.Ignore());

            CreateMap<UpdatePageDto, Page>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.ChildPages, opt => opt.Ignore())
                .ForMember(dest => dest.ParentPage, opt => opt.Ignore())
                .ForMember(dest => dest.PublishedOn, opt => opt.Ignore())
                .ForMember(dest => dest.PublishedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Versions, opt => opt.Ignore());
        }


        private void ConfigureSearchMappings()
        {
            CreateMap<IndexingJob, IndexingJobDto>();
            CreateMap<SearchIndex, SearchResultDto>()
                .ForMember(dest => dest.EntityType, opt => opt.MapFrom(src => src.EntityType))
                .ForMember(dest => dest.EntityId, opt => opt.MapFrom(src => src.EntityId))
                .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))
                .ForMember(dest => dest.Excerpt, opt => opt.MapFrom(src => src.Content.Length > 200 ? src.Content.Substring(0, 200) + "..." : src.Content))
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata))
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => src.UpdatedAt));
        }

        private void ConfigureCategoryMappings()
        {
            CreateMap<Category, CategoryDto>()
                .ForMember(dest => dest.ParentCategoryName, opt => opt.MapFrom(src => src.ParentCategory != null ? src.ParentCategory.Name : null))
                .ForMember(dest => dest.SubCategories, opt => opt.MapFrom(src => src.SubCategories.Where(sc => !sc.IsDeleted)))
                .ForMember(dest => dest.Images, opt => opt.MapFrom(src => src.Images.Where(i => !i.IsDeleted).OrderBy(i => i.Position)))
                .ForMember(dest => dest.FeaturedImageUrl, opt => opt.MapFrom(src => src.FeaturedImageUrl))
                .ForMember(dest => dest.ProductCount, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.FeaturedImageUrl, opt => opt.MapFrom(src => src.Images.OrderBy(i => i.Position).FirstOrDefault() != null ? $"/api/files/{src.Images.OrderBy(i => i.Position).FirstOrDefault().FileId}/download" : null));

            CreateMap<Category, CategoryTreeDto>()
                .ForMember(dest => dest.Children, opt => opt.MapFrom(src => src.SubCategories.Where(sc => !sc.IsDeleted)))
                .ForMember(dest => dest.FeaturedImageUrl, opt => opt.MapFrom(src => src.FeaturedImageUrl))
                .ForMember(dest => dest.ProductCount, opt => opt.Ignore()); // Will be set by service

            CreateMap<CreateCategoryDto, Category>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.Images, opt => opt.Ignore())
                .ForMember(dest => dest.ParentCategory, opt => opt.Ignore())
                .ForMember(dest => dest.SubCategories, opt => opt.Ignore())
                .ForMember(dest => dest.ProductCategories, opt => opt.Ignore());

            CreateMap<UpdateCategoryDto, Category>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.Images, opt => opt.Ignore())
                .ForMember(dest => dest.ParentCategory, opt => opt.Ignore())
                .ForMember(dest => dest.SubCategories, opt => opt.Ignore())
                .ForMember(dest => dest.ProductCategories, opt => opt.Ignore());
        }

        private void ConfigureProductMappings()
        {
            CreateMap<Product, ProductDto>()
                .ForMember(dest => dest.Categories, opt => opt.MapFrom(src =>
                    src.ProductCategories.Where(pc => !pc.IsDeleted).Select(pc => pc.Category)))
                .ForMember(dest => dest.Variants, opt => opt.MapFrom(src =>
                    src.Variants.Where(v => !v.IsDeleted)))
                .ForMember(dest => dest.Images, opt => opt.MapFrom(src =>
                    src.Images.Where(i => !i.IsDeleted).OrderBy(i => i.Position)))
                .ForMember(dest => dest.Options, opt => opt.MapFrom(src =>
                    src.Options.Where(o => !o.IsDeleted)))
                .ForMember(dest => dest.FeaturedImageUrl, opt => opt.MapFrom(src => src.FeaturedImageUrl))
                .ForMember(dest => dest.StatusName, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.TypeName, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.IsAvailable, opt => opt.MapFrom(src => src.Status == ProductStatus.Active && (src.HasVariants ? src.Variants.Any(v => v.Quantity > 0 || v.ContinueSellingWhenOutOfStock) : src.Quantity > 0 || src.ContinueSellingWhenOutOfStock)))
                .ForMember(dest => dest.DiscountPercentage, opt => opt.MapFrom(src => src.CompareAtPrice.HasValue && src.CompareAtPrice > src.Price ? Math.Round(((src.CompareAtPrice.Value - src.Price) / src.CompareAtPrice.Value) * 100, 2) : (decimal?)null))
                .ForMember(dest => dest.FeaturedImageUrl, opt => opt.MapFrom(src => src.Images.OrderBy(i => i.Position).FirstOrDefault() != null ? $"/api/files/{src.Images.OrderBy(i => i.Position).FirstOrDefault().FileId}/download" : null))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status));

            CreateMap<Product, ProductListDto>()
                .ForMember(dest => dest.CategoryNames, opt => opt.MapFrom(src =>
                    src.ProductCategories.Where(pc => !pc.IsDeleted).Select(pc => pc.Category.Name).ToList()))
                .ForMember(dest => dest.FeaturedImageUrl, opt => opt.MapFrom(src => src.FeaturedImageUrl))
                .ForMember(dest => dest.StatusName, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type));

            CreateMap<CreateProductDto, Product>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.ProductCategories, opt => opt.Ignore())
                .ForMember(dest => dest.Variants, opt => opt.Ignore())
                .ForMember(dest => dest.Images, opt => opt.Ignore())
                .ForMember(dest => dest.Options, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type));

            CreateMap<UpdateProductDto, Product>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.ProductCategories, opt => opt.Ignore())
                .ForMember(dest => dest.Variants, opt => opt.Ignore())
                .ForMember(dest => dest.Images, opt => opt.Ignore())
                .ForMember(dest => dest.Options, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type));
        }

        private void ConfigureProductVariantMappings()
        {
            CreateMap<ProductVariant, ProductVariantDto>()
                .ForMember(dest => dest.Images, opt => opt.MapFrom(src => src.Images.Where(i => !i.IsDeleted).OrderBy(i => i.Position)))
                .ForMember(dest => dest.FeaturedImageUrl, opt => opt.MapFrom(src => src.FeaturedImageUrl))
                .ForMember(dest => dest.IsAvailable, opt => opt.MapFrom(src => src.Quantity > 0 || src.ContinueSellingWhenOutOfStock))
                .ForMember(dest => dest.DiscountPercentage, opt => opt.MapFrom(src => src.CompareAtPrice.HasValue && src.CompareAtPrice > src.Price ? Math.Round(((src.CompareAtPrice.Value - src.Price) / src.CompareAtPrice.Value) * 100, 2) : (decimal?)null))
                .ForMember(dest => dest.DisplayTitle, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.Title) ? src.Title : string.Join(" / ", new[] { src.Option1, src.Option2, src.Option3 }.Where(o => !string.IsNullOrEmpty(o)))))
                .ForMember(dest => dest.FeaturedImageUrl, opt => opt.MapFrom(src => src.Images.OrderBy(i => i.Position).FirstOrDefault() != null ? $"/api/files/{src.Images.OrderBy(i => i.Position).FirstOrDefault().FileId}/download" : null));

            CreateMap<CreateProductVariantDto, ProductVariant>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.Images, opt => opt.Ignore());

            CreateMap<UpdateProductVariantDto, ProductVariant>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.Images, opt => opt.Ignore());
        }

        private void ConfigureProductImageMappings()
        {
            CreateMap<ProductImage, ProductImageDto>()
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => $"/api/files/{src.FileId}/download"))
                .ForMember(dest => dest.ThumbnailUrl, opt => opt.MapFrom(src => $"/api/files/{src.FileId}/thumbnail"));

            CreateMap<CreateProductImageDto, ProductImage>();
            CreateMap<UpdateProductImageDto, ProductImage>();

            CreateMap<CreateProductImageDto, ProductImage>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.File, opt => opt.Ignore());

            CreateMap<UpdateProductImageDto, ProductImage>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.File, opt => opt.Ignore());
        }

        private void ConfigureProductOptionMappings()
        {
            CreateMap<ProductOption, ProductOptionDto>()
                .ForMember(dest => dest.Values, opt => opt.MapFrom(src =>
                    src.Values.Where(v => !v.IsDeleted)));

            CreateMap<CreateProductOptionDto, ProductOption>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.Values, opt => opt.Ignore());

            CreateMap<UpdateProductOptionDto, ProductOption>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.Values, opt => opt.Ignore());

            CreateMap<ProductOptionValue, ProductOptionValueDto>();

            CreateMap<CreateProductOptionValueDto, ProductOptionValue>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.ProductOption, opt => opt.Ignore())
                .ForMember(dest => dest.ProductOptionId, opt => opt.Ignore());

            CreateMap<UpdateProductOptionValueDto, ProductOptionValue>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.ProductOption, opt => opt.Ignore())
                .ForMember(dest => dest.ProductOptionId, opt => opt.Ignore());
        }

        private void ConfigureImageMappings()
        {
            // Category Image mappings
            CreateMap<CategoryImage, CategoryImageDto>()
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.ImageUrl))
                .ForMember(dest => dest.ThumbnailUrl, opt => opt.MapFrom(src => src.ThumbnailUrl));
            CreateMap<CreateCategoryImageDto, CategoryImage>();
            CreateMap<UpdateCategoryImageDto, CategoryImage>();

            CreateMap<CreateCategoryImageDto, CategoryImage>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.Category, opt => opt.Ignore())
                .ForMember(dest => dest.CategoryId, opt => opt.Ignore())
                .ForMember(dest => dest.File, opt => opt.Ignore());

            CreateMap<UpdateCategoryImageDto, CategoryImage>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.Category, opt => opt.Ignore())
                .ForMember(dest => dest.CategoryId, opt => opt.Ignore())
                .ForMember(dest => dest.File, opt => opt.Ignore());

            // Product Variant Image mappings
            CreateMap<ProductVariantImage, ProductVariantImageDto>()
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.ImageUrl))
                .ForMember(dest => dest.ThumbnailUrl, opt => opt.MapFrom(src => src.ThumbnailUrl));

            CreateMap<CreateProductVariantImageDto, ProductVariantImage>();
            CreateMap<UpdateProductVariantImageDto, ProductVariantImage>();

            CreateMap<CreateProductVariantImageDto, ProductVariantImage>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.ProductVariant, opt => opt.Ignore())
                .ForMember(dest => dest.ProductVariantId, opt => opt.Ignore())
                .ForMember(dest => dest.File, opt => opt.Ignore());

            CreateMap<UpdateProductVariantImageDto, ProductVariantImage>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.ProductVariant, opt => opt.Ignore())
                .ForMember(dest => dest.ProductVariantId, opt => opt.Ignore())
                .ForMember(dest => dest.File, opt => opt.Ignore());
        }

        private void ConfigureFileMappings()
        {

            CreateMap<FileUploadDto, FileEntity>()
                .IgnoreAuditProperties()
                .ForMember(dest => dest.StoredFileName, opt => opt.Ignore())
                .ForMember(dest => dest.FileContent, opt => opt.Ignore())
                .ForMember(dest => dest.FileSize, opt => opt.Ignore())
                .ForMember(dest => dest.ContentType, opt => opt.Ignore())
                .ForMember(dest => dest.FileExtension, opt => opt.Ignore())
                .ForMember(dest => dest.FileType, opt => opt.Ignore())
                .ForMember(dest => dest.Hash, opt => opt.Ignore())
                .ForMember(dest => dest.ThumbnailContent, opt => opt.Ignore())
                .ForMember(dest => dest.Width, opt => opt.Ignore())
                .ForMember(dest => dest.Height, opt => opt.Ignore())
                .ForMember(dest => dest.Duration, opt => opt.Ignore())
                .ForMember(dest => dest.IsProcessed, opt => opt.Ignore())
                .ForMember(dest => dest.ProcessingStatus, opt => opt.Ignore())
                .ForMember(dest => dest.DownloadCount, opt => opt.Ignore())
                .ForMember(dest => dest.LastAccessedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Folder, opt => opt.Ignore());

            CreateMap<UpdateFileDto, FileEntity>()
                .IgnoreBaseEntityProperties()
                .ForMember(dest => dest.OriginalFileName, opt => opt.Ignore())
                .ForMember(dest => dest.StoredFileName, opt => opt.Ignore())
                .ForMember(dest => dest.FileContent, opt => opt.Ignore())
                .ForMember(dest => dest.FileSize, opt => opt.Ignore())
                .ForMember(dest => dest.ContentType, opt => opt.Ignore())
                .ForMember(dest => dest.FileExtension, opt => opt.Ignore())
                .ForMember(dest => dest.FileType, opt => opt.Ignore())
                .ForMember(dest => dest.Hash, opt => opt.Ignore())
                .ForMember(dest => dest.ThumbnailContent, opt => opt.Ignore())
                .ForMember(dest => dest.Width, opt => opt.Ignore())
                .ForMember(dest => dest.Height, opt => opt.Ignore())
                .ForMember(dest => dest.Duration, opt => opt.Ignore())
                .ForMember(dest => dest.IsProcessed, opt => opt.Ignore())
                .ForMember(dest => dest.ProcessingStatus, opt => opt.Ignore())
                .ForMember(dest => dest.DownloadCount, opt => opt.Ignore())
                .ForMember(dest => dest.LastAccessedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Folder, opt => opt.Ignore());
        }
        private void ConfigureDesignerMapping()
        {
            CreateMap<Page, DesignerPageDto>()
                .ForMember(dest => dest.PublishedAt, opt => opt.MapFrom(src => src.PublishedOn))
                .ForMember(dest => dest.HasUnsavedChanges, opt => opt.MapFrom(src => false));

            CreateMap<PageVersion, PageVersionDto>();
        }
    }

    // Extension methods to reduce repetition
    public static class MappingExtensions
    {
        public static IMappingExpression<TSource, TDestination> IgnoreAuditProperties<TSource, TDestination>(
            this IMappingExpression<TSource, TDestination> mapping)
            where TDestination : class
        {
            return mapping
                .ForMember("Id", opt => opt.Ignore())
                .ForMember("CreatedAt", opt => opt.Ignore())
                .ForMember("UpdatedAt", opt => opt.Ignore())
                .ForMember("IsDeleted", opt => opt.Ignore())
                .ForMember("DeletedAt", opt => opt.Ignore());
        }

        public static IMappingExpression<TSource, TDestination> IgnoreBaseEntityProperties<TSource, TDestination>(
            this IMappingExpression<TSource, TDestination> mapping)
            where TDestination : class
        {
            return mapping
                .ForMember("Id", opt => opt.Ignore())
                .ForMember("CreatedAt", opt => opt.Ignore())
                .ForMember("IsDeleted", opt => opt.Ignore())
                .ForMember("DeletedAt", opt => opt.Ignore());
        }
    }
}