
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Frontend.Interfaces;
using Frontend.Services;
using Microsoft.AspNetCore.Components;

namespace Frontend.Pages.Products
{
    public partial class Products : ComponentBase
    {
        private string? statusFilter;
        private string? typeFilter;
        private string? variantsFilter;

        private async Task<PaginatedResult<ProductListDto>> LoadProducts(int page, int pageSize, string? search)
        {
            try
            {
                var searchDto = new ProductSearchDto
                {
                    SearchTerm = search,
                    Page = page,
                    PageSize = pageSize,
                    SortBy = "Name",
                    SortDirection = "Asc"
                };

                // Apply filters
                if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<ProductStatus>(statusFilter, out var status))
                {
                    searchDto.Status = status;
                }

                if (!string.IsNullOrEmpty(typeFilter) && Enum.TryParse<ProductType>(typeFilter, out var type))
                {
                    searchDto.Type = type;
                }

                if (!string.IsNullOrEmpty(variantsFilter) && bool.TryParse(variantsFilter, out var hasVariants))
                {
                    searchDto.HasVariants = hasVariants;
                }

                var result = await ProductService.SearchProductsAsync(searchDto);

                // Convert ProductDto to ProductListDto if needed
                var listItems = result.Data.Select(p => new ProductListDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Status = p.Status,
                    Type = p.Type,
                    HasVariants = p.HasVariants,
                    FeaturedImageUrl = p.FeaturedImageUrl,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    IsAvailable = p.IsAvailable,
                }).ToList();

                return new PaginatedResult<ProductListDto>(listItems, result.PageNumber, result.PageSize, result.TotalCount);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load products: {ex.Message}");
                return new PaginatedResult<ProductListDto>();
            }
        }

        private CreateProductDto CreateNewProduct()
        {
            return new CreateProductDto
            {
                Status = ProductStatus.Draft,
                Type = ProductType.Physical,
                RequiresShipping = true,
                HasVariants = false
            };
        }

        private CreateProductDto ConvertToCreateDto(ProductDto product)
        {
            return new CreateProductDto
            {
                Name = product.Name,
                Slug = product.Slug,
                Description = product.Description,
                ShortDescription = product.ShortDescription,
                ContinueSellingWhenOutOfStock = product.ContinueSellingWhenOutOfStock,
                RequiresShipping = product.RequiresShipping,
                Status = product.Status,
                Type = product.Type,
                Vendor = product.Vendor,
                HasVariants = product.HasVariants,
                MetaTitle = product.MetaTitle,
                MetaDescription = product.MetaDescription,
                MetaKeywords = product.MetaKeywords,
                SearchKeywords = product.SearchKeywords,
                CustomFields = product.CustomFields,
                SEOSettings = product.SEOSettings,
                CategoryIds = product.Categories?.Select(c => c.Id).ToList() ?? new List<int>(),
                Images = product.Images?.Select(i => new CreateProductImageDto
                {
                    FileId = i.FileId,
                    Alt = i.Alt,
                    Caption = i.Caption,
                    Position = i.Position,
                    IsFeatured = i.IsFeatured
                }).ToList() ?? new List<CreateProductImageDto>(),
                Variants = product.Variants?.Select(v => new CreateProductVariantDto
                {
                    Title = v.Title,
                    Price = v.Price,
                    CompareAtPrice = v.CompareAtPrice,
                    CostPerItem = v.CostPerItem,
                    Quantity = v.Quantity,
                    TrackQuantity = v.TrackQuantity,
                    ContinueSellingWhenOutOfStock = v.ContinueSellingWhenOutOfStock,
                    RequiresShipping = v.RequiresShipping,
                    IsTaxable = v.IsTaxable,
                    Weight = v.Weight,
                    WeightUnit = v.WeightUnit,
                    Barcode = v.Barcode,
                    Position = v.Position,
                    IsDefault = v.IsDefault,
                    CustomFields = v.CustomFields,
                    Option1 = v.Option1,
                    Option2 = v.Option2,
                    Option3 = v.Option3,
                    Images = v.Images?.Select(vi => new CreateProductVariantImageDto
                    {
                        FileId = vi.FileId,
                        Alt = vi.Alt,
                        Caption = vi.Caption,
                        Position = vi.Position,
                        IsFeatured = vi.IsFeatured
                    }).ToList() ?? new List<CreateProductVariantImageDto>()
                }).ToList() ?? new List<CreateProductVariantDto>()
            };
        }

        private UpdateProductDto MapCreateToUpdate(CreateProductDto createDto)
        {
            return new UpdateProductDto
            {
                Name = createDto.Name,
                Slug = createDto.Slug,
                Description = createDto.Description,
                ShortDescription = createDto.ShortDescription,
                ContinueSellingWhenOutOfStock = createDto.ContinueSellingWhenOutOfStock,
                RequiresShipping = createDto.RequiresShipping,
                Status = createDto.Status,
                Type = createDto.Type,
                Vendor = createDto.Vendor,
                HasVariants = createDto.HasVariants,
                MetaTitle = createDto.MetaTitle,
                MetaDescription = createDto.MetaDescription,
                MetaKeywords = createDto.MetaKeywords,
                SearchKeywords = createDto.SearchKeywords,
                CustomFields = createDto.CustomFields,
                SEOSettings = createDto.SEOSettings,
                CategoryIds = createDto.CategoryIds,
                Images = createDto.Images?.Select(i => new UpdateProductImageDto
                {
                    Id = 0, // Will be handled by the service
                    FileId = i.FileId,
                    Alt = i.Alt,
                    Caption = i.Caption,
                    Position = i.Position,
                    IsFeatured = i.IsFeatured
                }).ToList() ?? new List<UpdateProductImageDto>(),
                Variants = createDto.Variants?.Select(v => new UpdateProductVariantDto
                {
                    Id = 0, // Will be handled by the service
                    Title = v.Title,
                    Price = v.Price,
                    CompareAtPrice = v.CompareAtPrice,
                    CostPerItem = v.CostPerItem,
                    Quantity = v.Quantity,
                    TrackQuantity = v.TrackQuantity,
                    ContinueSellingWhenOutOfStock = v.ContinueSellingWhenOutOfStock,
                    RequiresShipping = v.RequiresShipping,
                    IsTaxable = v.IsTaxable,
                    Weight = v.Weight,
                    WeightUnit = v.WeightUnit,
                    Barcode = v.Barcode,
                    Position = v.Position,
                    IsDefault = v.IsDefault,
                    CustomFields = v.CustomFields,
                    Option1 = v.Option1,
                    Option2 = v.Option2,
                    Option3 = v.Option3,
                    Images = v.Images?.Select(vi => new UpdateProductVariantImageDto
                    {
                        Id = 0, // Will be handled by the service
                        FileId = vi.FileId,
                        Alt = vi.Alt,
                        Caption = vi.Caption,
                        Position = vi.Position,
                        IsFeatured = vi.IsFeatured
                    }).ToList() ?? new List<UpdateProductVariantImageDto>()
                }).ToList() ?? new List<UpdateProductVariantDto>()
            };
        }

        private async Task<Dictionary<string, string>> ValidateProduct(CreateProductDto model, bool isEdit)
        {
            var errors = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                errors["Name"] = "Product name is required";
            }

            if (string.IsNullOrWhiteSpace(model.Slug))
            {
                errors["Slug"] = "Product slug is required";
            }
            else
            {
                var isValidSlug = await ProductService.ValidateSlugAsync(model.Slug, isEdit ? null : null);
                if (!isValidSlug)
                {
                    errors["Slug"] = "This slug is already in use";
                }
            }

            return errors;
        }

        private async Task OnFilterChanged()
        {
            await Task.CompletedTask;
            StateHasChanged();
        }

        private async Task OnProductCreated(ProductDto product)
        {
            NotificationService.ShowSuccess($"Product '{product.Name}' created successfully");
            await Task.CompletedTask;
        }

        private async Task OnProductUpdated(ProductDto product)
        {
            NotificationService.ShowSuccess($"Product '{product.Name}' updated successfully");
            await Task.CompletedTask;
        }

        private async Task OnProductDeleted(int productId)
        {
            NotificationService.ShowSuccess("Product deleted successfully");
            await Task.CompletedTask;
        }

        private string GetStatusClass(ProductStatus status)
        {
            return status switch
            {
                ProductStatus.Active => "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400",
                ProductStatus.Draft => "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400",
                ProductStatus.Archived => "bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-400",
                _ => "bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-400"
            };
        }

        private string GetStatusIcon(ProductStatus status)
        {
            return status switch
            {
                ProductStatus.Active => "fas fa-check-circle",
                ProductStatus.Draft => "fas fa-edit",
                ProductStatus.Archived => "fas fa-archive",
                _ => "fas fa-question-circle"
            };
        }

        private string GetTypeClass(ProductType type)
        {
            return type switch
            {
                ProductType.Physical => "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400",
                ProductType.Digital => "bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400",
                ProductType.Service => "bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400",
                ProductType.GiftCard => "bg-pink-100 text-pink-800 dark:bg-pink-900/30 dark:text-pink-400",
                _ => "bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-400"
            };
        }

        private string GetTypeIcon(ProductType type)
        {
            return type switch
            {
                ProductType.Physical => "fas fa-box",
                ProductType.Digital => "fas fa-download",
                ProductType.Service => "fas fa-handshake",
                ProductType.GiftCard => "fas fa-gift",
                _ => "fas fa-question-circle"
            };
        }
    }
}