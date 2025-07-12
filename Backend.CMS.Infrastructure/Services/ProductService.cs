using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductService> _logger;

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 100;

        public ProductService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<ProductService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ProductDto?> GetProductByIdAsync(int productId)
        {
            try
            {
                var product = await _unitOfWork.Products.GetWithDetailsAsync(productId);
                if (product == null)
                {
                    _logger.LogDebug("GetProductByIdAsync for ID '{ProductId}' completed. Product not found", productId);
                    return null;
                }

                var productDto = _mapper.Map<ProductDto>(product);
                _logger.LogDebug("GetProductByIdAsync for ID '{ProductId}' completed. Product found", productId);
                return productDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during product fetch/map for ID '{ProductId}'", productId);
                throw;
            }
        }

        public async Task<ProductDto?> GetProductBySlugAsync(string slug)
        {
            var product = await _unitOfWork.Products.GetBySlugAsync(slug);
            return product != null ? _mapper.Map<ProductDto>(product) : null;
        }

        public async Task<PagedResult<ProductDto>> GetProductsAsync(int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            try
            {
                // Get paginated result using the repository's built-in pagination
                var pagedResult = await _unitOfWork.Products.GetPagedResultAsync(
                    page,
                    pageSize,
                    predicate: null,
                    orderBy: query => query.OrderBy(p => p.Name)
                );

                // Map entities to DTOs
                var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Data);

                return new PagedResult<ProductDto>
                {
                    Data = productDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = pagedResult.TotalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated products for page {Page}, pageSize {PageSize}", page, pageSize);
                throw;
            }
        }

        public async Task<PagedResult<ProductDto>> GetProductsByCategoryAsync(int categoryId, int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            try
            {
                // Use the repository's dedicated pagination method for categories
                var pagedResult = await _unitOfWork.Products.GetPagedByCategoryAsync(categoryId, page, pageSize);

                // Map entities to DTOs
                var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Data);

                return new PagedResult<ProductDto>
                {
                    Data = productDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = pagedResult.TotalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated products by category {CategoryId} for page {Page}, pageSize {PageSize}",
                    categoryId, page, pageSize);
                throw;
            }
        }

        public async Task<ProductDto> CreateProductAsync(CreateProductDto createProductDto)
        {
            // Validate slug uniqueness
            if (await _unitOfWork.Products.SlugExistsAsync(createProductDto.Slug))
                throw new ArgumentException($"Product with slug '{createProductDto.Slug}' already exists");

            // Validate categories
            if (createProductDto.CategoryIds.Any())
            {
                foreach (var categoryId in createProductDto.CategoryIds)
                {
                    var categoryExists = await _unitOfWork.Categories.GetByIdAsync(categoryId);
                    if (categoryExists == null)
                        throw new ArgumentException($"Category with ID {categoryId} not found");
                }
            }

            // Validate images
            if (createProductDto.Images.Any())
            {
                await ValidateImagesAsync(createProductDto.Images.Select(i => i.FileId).ToList());
            }

            var product = _mapper.Map<Product>(createProductDto);

            // Set published date if status is active
            if (product.Status == ProductStatus.Active)
                product.PublishedAt = DateTime.UtcNow;

            await _unitOfWork.Products.AddAsync(product);
            await _unitOfWork.Products.SaveChangesAsync();

            // Add category associations
            if (createProductDto.CategoryIds.Any())
            {
                var productCategories = createProductDto.CategoryIds.Select((categoryId, index) => new ProductCategory
                {
                    ProductId = product.Id,
                    CategoryId = categoryId,
                    SortOrder = index
                }).ToList();

                foreach (var pc in productCategories)
                {
                    await _unitOfWork.Products.AddProductCategoryAsync(pc);
                }
                await _unitOfWork.Products.SaveChangesAsync();
            }

            // Add images
            if (createProductDto.Images.Any())
            {
                await AddProductImagesAsync(product.Id, createProductDto.Images);
            }

            // Create variants if specified
            if (createProductDto.HasVariants && createProductDto.Variants.Any())
            {
                foreach (var variantDto in createProductDto.Variants)
                {
                    var variant = _mapper.Map<ProductVariant>(variantDto);
                    variant.ProductId = product.Id;
                    await _unitOfWork.ProductVariants.AddAsync(variant);
                }
                await _unitOfWork.ProductVariants.SaveChangesAsync();
            }

            _logger.LogInformation("Created product: {ProductName} (ID: {ProductId})", product.Name, product.Id);

            // Return the complete product with all relations
            var createdProduct = await _unitOfWork.Products.GetWithDetailsAsync(product.Id);
            return _mapper.Map<ProductDto>(createdProduct!);
        }

        public async Task<ProductDto> UpdateProductAsync(int productId, UpdateProductDto updateProductDto)
        {
            var product = await _unitOfWork.Products.GetWithDetailsAsync(productId);
            if (product == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            // Validate slug uniqueness
            if (await _unitOfWork.Products.SlugExistsAsync(updateProductDto.Slug, productId))
                throw new ArgumentException($"Product with slug '{updateProductDto.Slug}' already exists");


            // Validate images
            if (updateProductDto.Images.Any())
            {
                await ValidateImagesAsync(updateProductDto.Images.Select(i => i.FileId).ToList());
            }

            var oldStatus = product.Status;
            _mapper.Map(updateProductDto, product);

            // Set published date if status changed to active
            if (oldStatus != ProductStatus.Active && product.Status == ProductStatus.Active)
                product.PublishedAt = DateTime.UtcNow;
            else if (product.Status != ProductStatus.Active)
                product.PublishedAt = null;

            _unitOfWork.Products.Update(product);

            // Update category associations
            await _unitOfWork.Products.RemoveProductCategoriesAsync(product.Id);

            if (updateProductDto.CategoryIds.Any())
            {
                var productCategories = updateProductDto.CategoryIds.Select((categoryId, index) => new ProductCategory
                {
                    ProductId = product.Id,
                    CategoryId = categoryId,
                    SortOrder = index
                }).ToList();

                foreach (var pc in productCategories)
                {
                    await _unitOfWork.Products.AddProductCategoryAsync(pc);
                }
            }

            // Update images
            await UpdateProductImagesAsync(productId, updateProductDto.Images);

            await _unitOfWork.Products.SaveChangesAsync();

            _logger.LogInformation("Updated product: {ProductName} (ID: {ProductId})", product.Name, product.Id);

            // Return the complete updated product
            var updatedProduct = await _unitOfWork.Products.GetWithDetailsAsync(product.Id);
            return _mapper.Map<ProductDto>(updatedProduct!);
        }

        public async Task<bool> DeleteProductAsync(int productId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null) return false;

            await _unitOfWork.Products.SoftDeleteAsync(product);

            _logger.LogInformation("Deleted product: {ProductName} (ID: {ProductId})", product.Name, product.Id);
            return true;
        }

        public async Task<PagedResult<ProductDto>> SearchProductsAsync(ProductSearchDto searchDto)
        {
            // Validate and normalize pagination parameters
            var page = Math.Max(1, searchDto.Page);
            var pageSize = searchDto.PageSize <= 0 ? DefaultPageSize : Math.Min(searchDto.PageSize, MaxPageSize);

            try
            {
                // Use the repository's search method that returns a paged result
                var pagedResult = await _unitOfWork.Products.SearchProductsPagedAsync(searchDto);
                var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Data);

                return new PagedResult<ProductDto>
                {
                    Data = productDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = pagedResult.TotalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products with criteria: {@SearchDto}", searchDto);
                throw;
            }
        }

        public async Task<int> GetSearchCountAsync(ProductSearchDto searchDto)
        {
            var count = await _unitOfWork.Products.GetSearchCountAsync(searchDto);
            return count;
        }

        public async Task<bool> ValidateSlugAsync(string slug, int? excludeProductId = null)
        {
            return !await _unitOfWork.Products.SlugExistsAsync(slug, excludeProductId);
        }

        public async Task<ProductDto> PublishProductAsync(int productId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            product.Status = ProductStatus.Active;
            product.PublishedAt = DateTime.UtcNow;
            _unitOfWork.Products.Update(product);
            await _unitOfWork.Products.SaveChangesAsync();

            _logger.LogInformation("Published product: {ProductName} (ID: {ProductId})", product.Name, product.Id);
            return _mapper.Map<ProductDto>(product);
        }

        public async Task<ProductDto> UnpublishProductAsync(int productId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            product.Status = ProductStatus.Draft;
            product.PublishedAt = null;
            _unitOfWork.Products.Update(product);
            await _unitOfWork.Products.SaveChangesAsync();

            _logger.LogInformation("Unpublished product: {ProductName} (ID: {ProductId})", product.Name, product.Id);
            return _mapper.Map<ProductDto>(product);
        }

        public async Task<ProductDto> ArchiveProductAsync(int productId)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            product.Status = ProductStatus.Archived;
            _unitOfWork.Products.Update(product);
            await _unitOfWork.Products.SaveChangesAsync();

            _logger.LogInformation("Archived product: {ProductName} (ID: {ProductId})", product.Name, product.Id);
            return _mapper.Map<ProductDto>(product);
        }

        public async Task<ProductDto> DuplicateProductAsync(int productId, string newName)
        {
            var originalProduct = await _unitOfWork.Products.GetWithDetailsAsync(productId);
            if (originalProduct == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            var duplicatedProduct = new Product
            {
                Name = newName,
                Slug = await GenerateUniqueSlugAsync(newName),
                Description = originalProduct.Description,
                ShortDescription = originalProduct.ShortDescription,
                RequiresShipping = originalProduct.RequiresShipping,
                Status = ProductStatus.Draft, // Set as draft
                Type = originalProduct.Type,
                Vendor = originalProduct.Vendor,
                HasVariants = originalProduct.HasVariants,
                MetaTitle = originalProduct.MetaTitle,
                MetaDescription = originalProduct.MetaDescription,
                MetaKeywords = originalProduct.MetaKeywords,
                SearchKeywords = originalProduct.SearchKeywords,
                CustomFields = new Dictionary<string, object>(originalProduct.CustomFields),
                SEOSettings = new Dictionary<string, object>(originalProduct.SEOSettings)
            };

            await _unitOfWork.Products.AddAsync(duplicatedProduct);
            await _unitOfWork.Products.SaveChangesAsync();

            // Duplicate category associations
            foreach (var pc in originalProduct.ProductCategories)
            {
                var newPc = new ProductCategory
                {
                    ProductId = duplicatedProduct.Id,
                    CategoryId = pc.CategoryId,
                    SortOrder = pc.SortOrder
                };
                await _unitOfWork.Products.AddProductCategoryAsync(newPc);
            }

            // Duplicate images
            foreach (var image in originalProduct.Images.Where(i => !i.IsDeleted))
            {
                var newImage = new ProductImage
                {
                    ProductId = duplicatedProduct.Id,
                    FileId = image.FileId,
                    Alt = image.Alt,
                    Caption = image.Caption,
                    Position = image.Position,
                    IsFeatured = image.IsFeatured
                };
                await _unitOfWork.GetRepository<ProductImage>().AddAsync(newImage);
            }

            await _unitOfWork.Products.SaveChangesAsync();

            _logger.LogInformation("Duplicated product: {OriginalProductName} -> {NewProductName} (ID: {ProductId})",
                originalProduct.Name, newName, duplicatedProduct.Id);

            var createdProduct = await _unitOfWork.Products.GetWithDetailsAsync(duplicatedProduct.Id);
            return _mapper.Map<ProductDto>(createdProduct!);
        }

        public async Task<PagedResult<ProductDto>> GetFeaturedProductsAsync(int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            try
            {
                // Use the repository's dedicated pagination method for featured products
                var pagedResult = await _unitOfWork.Products.GetFeaturedProductsPagedAsync(page, pageSize);
                var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Data);

                return new PagedResult<ProductDto>
                {
                    Data = productDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = pagedResult.TotalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated featured products for page {Page}, pageSize {PageSize}", page, pageSize);
                throw;
            }
        }

        public async Task<PagedResult<ProductDto>> GetRelatedProductsAsync(int productId, int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            try
            {
                // Use the repository's dedicated pagination method for related products
                var pagedResult = await _unitOfWork.Products.GetRelatedProductsPagedAsync(productId, page, pageSize);
                var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Data);

                return new PagedResult<ProductDto>
                {
                    Data = productDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = pagedResult.TotalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated related products for product {ProductId}, page {Page}, pageSize {PageSize}",
                    productId, page, pageSize);
                throw;
            }
        }

        public async Task<PagedResult<ProductDto>> GetRecentProductsAsync(int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            try
            {
                // Use the repository's dedicated pagination method for recent products
                var pagedResult = await _unitOfWork.Products.GetRecentProductsPagedAsync(page, pageSize);
                var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Data);

                return new PagedResult<ProductDto>
                {
                    Data = productDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = pagedResult.TotalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated recent products for page {Page}, pageSize {PageSize}", page, pageSize);
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetProductStatisticsAsync()
        {
            var totalProducts = await _unitOfWork.Products.CountAsync();
            var activeProducts = await _unitOfWork.Products.CountAsync(p => p.Status == ProductStatus.Active);
            var draftProducts = await _unitOfWork.Products.CountAsync(p => p.Status == ProductStatus.Draft);
            var archivedProducts = await _unitOfWork.Products.CountAsync(p => p.Status == ProductStatus.Archived);

            return new Dictionary<string, object>
            {
                ["TotalProducts"] = totalProducts,
                ["ActiveProducts"] = activeProducts,
                ["DraftProducts"] = draftProducts,
                ["ArchivedProducts"] = archivedProducts,
                ["LastUpdated"] = DateTime.UtcNow
            };
        }

        public async Task<(decimal min, decimal max)> GetPriceRangeAsync()
        {
            var min = await _unitOfWork.Products.GetMinPriceAsync();
            var max = await _unitOfWork.Products.GetMaxPriceAsync();
            return (min, max);
        }

        public async Task<List<string>> GetVendorsAsync()
        {
            var vendors = await _unitOfWork.Products.GetVendorsAsync();
            return vendors.ToList();
        }

        public async Task<PagedResult<ProductDto>> GetLowStockProductsAsync(int threshold = 5, int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            try
            {
                // Use the repository's dedicated pagination method for low stock products
                var pagedResult = await _unitOfWork.Products.GetLowStockProductsPagedAsync(threshold, page, pageSize);
                var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Data);

                return new PagedResult<ProductDto>
                {
                    Data = productDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = pagedResult.TotalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated low stock products for threshold {Threshold}, page {Page}, pageSize {PageSize}",
                    threshold, page, pageSize);
                throw;
            }
        }

        public async Task<PagedResult<ProductDto>> GetOutOfStockProductsAsync(int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            try
            {
                // Use the repository's dedicated pagination method for out of stock products
                var pagedResult = await _unitOfWork.Products.GetOutOfStockProductsPagedAsync(page, pageSize);
                var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Data);

                return new PagedResult<ProductDto>
                {
                    Data = productDtos,
                    PageNumber = pagedResult.PageNumber,
                    PageSize = pagedResult.PageSize,
                    TotalCount = pagedResult.TotalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated out of stock products for page {Page}, pageSize {PageSize}", page, pageSize);
                throw;
            }
        }

        // Image management methods
        public async Task<ProductImageDto> AddProductImageAsync(int productId, CreateProductImageDto createImageDto)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            await ValidateImageAsync(createImageDto.FileId);

            var productImage = _mapper.Map<ProductImage>(createImageDto);
            productImage.ProductId = productId;

            // If this is set as featured, remove featured flag from other images
            if (createImageDto.IsFeatured)
            {
                await RemoveFeaturedFlagFromOtherProductImagesAsync(productId);
            }

            await _unitOfWork.GetRepository<ProductImage>().AddAsync(productImage);
            await _unitOfWork.GetRepository<ProductImage>().SaveChangesAsync();

            _logger.LogInformation("Added image to product {ProductId}: FileId {FileId}", productId, createImageDto.FileId);
            return _mapper.Map<ProductImageDto>(productImage);
        }

        public async Task<ProductImageDto> UpdateProductImageAsync(int imageId, UpdateProductImageDto updateImageDto)
        {
            var productImage = await _unitOfWork.GetRepository<ProductImage>().GetByIdAsync(imageId);
            if (productImage == null)
                throw new ArgumentException($"Product image with ID {imageId} not found");

            await ValidateImageAsync(updateImageDto.FileId);

            var oldIsFeatured = productImage.IsFeatured;
            _mapper.Map(updateImageDto, productImage);

            // If this image is being set as featured, remove featured flag from other images
            if (updateImageDto.IsFeatured && !oldIsFeatured)
            {
                await RemoveFeaturedFlagFromOtherProductImagesAsync(productImage.ProductId, imageId);
            }

            _unitOfWork.GetRepository<ProductImage>().Update(productImage);
            await _unitOfWork.GetRepository<ProductImage>().SaveChangesAsync();

            _logger.LogInformation("Updated product image {ImageId}", imageId);
            return _mapper.Map<ProductImageDto>(productImage);
        }

        public async Task<bool> DeleteProductImageAsync(int imageId)
        {
            var productImage = await _unitOfWork.GetRepository<ProductImage>().GetByIdAsync(imageId);
            if (productImage == null) return false;

            var productId = productImage.ProductId;
            await _unitOfWork.GetRepository<ProductImage>().SoftDeleteAsync(productImage);

            _logger.LogInformation("Deleted product image {ImageId} from product {ProductId}", imageId, productId);
            return true;
        }

        public async Task<List<ProductImageDto>> ReorderProductImagesAsync(int productId, List<(int ImageId, int Position)> imageOrders)
        {
            var imageIds = imageOrders.Select(io => io.ImageId).ToList();
            var images = await _unitOfWork.GetRepository<ProductImage>().FindAsync(i => i.ProductId == productId && imageIds.Contains(i.Id));

            foreach (var image in images)
            {
                var newPosition = imageOrders.First(io => io.ImageId == image.Id).Position;
                image.Position = newPosition;
            }

            _unitOfWork.GetRepository<ProductImage>().UpdateRange(images);
            await _unitOfWork.GetRepository<ProductImage>().SaveChangesAsync();

            return _mapper.Map<List<ProductImageDto>>(images.OrderBy(i => i.Position));
        }

        // Private helper methods
        private async Task<string> GenerateUniqueSlugAsync(string name)
        {
            var baseSlug = name.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("&", "and");

            var slug = baseSlug;
            var counter = 1;

            while (await _unitOfWork.Products.SlugExistsAsync(slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }


        private async Task ValidateImagesAsync(List<int> fileIds)
        {
            foreach (var fileId in fileIds)
            {
                await ValidateImageAsync(fileId);
            }
        }

        private async Task ValidateImageAsync(int fileId)
        {
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
            if (file == null)
                throw new ArgumentException($"File with ID {fileId} not found");

            if (file.FileType != Domain.Enums.FileType.Image)
                throw new ArgumentException($"File with ID {fileId} is not an image");
        }

        private async Task AddProductImagesAsync(int productId, List<CreateProductImageDto> images)
        {
            foreach (var imageDto in images)
            {
                var productImage = _mapper.Map<ProductImage>(imageDto);
                productImage.ProductId = productId;

                await _unitOfWork.GetRepository<ProductImage>().AddAsync(productImage);
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedProductImageAsync(productId);
            await _unitOfWork.GetRepository<ProductImage>().SaveChangesAsync();
        }

        private async Task UpdateProductImagesAsync(int productId, List<UpdateProductImageDto> images)
        {
            // Remove existing images
            var existingImages = await _unitOfWork.GetRepository<ProductImage>().FindAsync(i => i.ProductId == productId);
            await _unitOfWork.GetRepository<ProductImage>().SoftDeleteRangeAsync(existingImages);

            // Add new images
            foreach (var imageDto in images)
            {
                var productImage = new ProductImage
                {
                    ProductId = productId,
                    FileId = imageDto.FileId,
                    Alt = imageDto.Alt,
                    Caption = imageDto.Caption,
                    Position = imageDto.Position,
                    IsFeatured = imageDto.IsFeatured
                };

                await _unitOfWork.GetRepository<ProductImage>().AddAsync(productImage);
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedProductImageAsync(productId);
        }

        private async Task RemoveFeaturedFlagFromOtherProductImagesAsync(int productId, int? excludeImageId = null)
        {
            var otherImages = await _unitOfWork.GetRepository<ProductImage>().FindAsync(i =>
                i.ProductId == productId &&
                i.IsFeatured &&
                (excludeImageId == null || i.Id != excludeImageId));

            foreach (var image in otherImages)
            {
                image.IsFeatured = false;
            }

            _unitOfWork.GetRepository<ProductImage>().UpdateRange(otherImages);
        }

        private async Task EnsureSingleFeaturedProductImageAsync(int productId)
        {
            var featuredImages = await _unitOfWork.GetRepository<ProductImage>().FindAsync(i => i.ProductId == productId && i.IsFeatured);
            var featuredImagesList = featuredImages.ToList();

            if (featuredImagesList.Count > 1)
            {
                // Keep only the first one as featured
                for (int i = 1; i < featuredImagesList.Count; i++)
                {
                    featuredImagesList[i].IsFeatured = false;
                }

                _unitOfWork.GetRepository<ProductImage>().UpdateRange(featuredImagesList.Skip(1));
            }
            else if (!featuredImagesList.Any())
            {
                // If no featured image, make the first one featured
                var firstImage = await _unitOfWork.GetRepository<ProductImage>().FirstOrDefaultAsync(i => i.ProductId == productId);
                if (firstImage != null)
                {
                    firstImage.IsFeatured = true;
                    _unitOfWork.GetRepository<ProductImage>().Update(firstImage);
                }
            }
        }
    }
}