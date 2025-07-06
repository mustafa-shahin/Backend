using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IProductVariantRepository _variantRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IRepository<ProductImage> _productImageRepository;
        private readonly IRepository<FileEntity> _fileRepository;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductService> _logger;

        private const int DefaultPageSize = 10;

        public ProductService(
            IProductRepository productRepository,
            IProductVariantRepository variantRepository,
            ICategoryRepository categoryRepository,
            IRepository<ProductImage> productImageRepository,
            IRepository<FileEntity> fileRepository,
            ICacheService cacheService,
            ICacheInvalidationService cacheInvalidationService,
            ICacheKeyService cacheKeyService,
            IMapper mapper,
            ILogger<ProductService> logger)
        {
            _productRepository = productRepository;
            _variantRepository = variantRepository;
            _categoryRepository = categoryRepository;
            _productImageRepository = productImageRepository;
            _fileRepository = fileRepository;
            _cacheService = cacheService;
            _cacheInvalidationService = cacheInvalidationService;
            _cacheKeyService = cacheKeyService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ProductDto?> GetProductByIdAsync(int productId)
        {
            var cacheKey = _cacheKeyService.GetEntityKey<Product>(productId);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    var product = await _productRepository.GetWithDetailsAsync(productId);
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
            });
        }

        public async Task<ProductDto?> GetProductBySlugAsync(string slug)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("product", "slug", slug.ToLowerInvariant());
            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var product = await _productRepository.GetBySlugAsync(slug);
                return product != null ? _mapper.Map<ProductDto>(product) : null;
            });
        }

        public async Task<PagedResult<ProductDto>> GetProductsAsync(int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, 100);

            var cacheKey = _cacheKeyService.GetCollectionKey<Product>("paged", page, pageSize);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    // Get paginated result from repository
                    var pagedResult = await _productRepository.GetPagedResultAsync(
                        page,
                        pageSize,
                        predicate: null,
                        orderBy: query => query.OrderBy(p => p.Name)
                    );

                    // Map entities to DTOs
                    var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Items);

                    return new PagedResult<ProductDto>
                    {
                        Items = productDtos,
                        Page = pagedResult.Page,
                        PageSize = pagedResult.PageSize,
                        TotalCount = pagedResult.TotalCount
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching paginated products for page {Page}, pageSize {PageSize}", page, pageSize);
                    throw;
                }
            }) ?? PagedResult<ProductDto>.Empty(page, pageSize);
        }

        public async Task<PagedResult<ProductDto>> GetProductsByCategoryAsync(int categoryId, int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, 100);

            var cacheKey = _cacheKeyService.GetCollectionKey<Product>("by_category", categoryId, page, pageSize);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    // Get paginated result from repository with category filter
                    var pagedResult = await _productRepository.GetPagedResultAsync(
                        page,
                        pageSize,
                        predicate: p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId && !pc.IsDeleted),
                        orderBy: query => query.OrderBy(p => p.Name)
                    );

                    // Map entities to DTOs
                    var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Items);

                    return new PagedResult<ProductDto>
                    {
                        Items = productDtos,
                        Page = pagedResult.Page,
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
            }) ?? PagedResult<ProductDto>.Empty(page, pageSize);
        }

        public async Task<ProductDto> CreateProductAsync(CreateProductDto createProductDto)
        {
            // Validate slug uniqueness
            if (await _productRepository.SlugExistsAsync(createProductDto.Slug))
                throw new ArgumentException($"Product with slug '{createProductDto.Slug}' already exists");

            // Validate SKU uniqueness
            if (await _productRepository.SKUExistsAsync(createProductDto.SKU))
                throw new ArgumentException($"Product with SKU '{createProductDto.SKU}' already exists");

            // Validate categories
            if (createProductDto.CategoryIds.Any())
            {
                foreach (var categoryId in createProductDto.CategoryIds)
                {
                    var categoryExists = await _categoryRepository.GetByIdAsync(categoryId);
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

            await _productRepository.AddAsync(product);
            await _productRepository.SaveChangesAsync();

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
                    await _productRepository.AddProductCategoryAsync(pc);
                }
                await _productRepository.SaveChangesAsync();
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
                    await _variantRepository.AddAsync(variant);
                }
                await _variantRepository.SaveChangesAsync();
            }

            await InvalidateProductCacheAsync();

            _logger.LogInformation("Created product: {ProductName} (ID: {ProductId})", product.Name, product.Id);

            // Return the complete product with all relations
            var createdProduct = await _productRepository.GetWithDetailsAsync(product.Id);
            return _mapper.Map<ProductDto>(createdProduct!);
        }

        public async Task<ProductDto> UpdateProductAsync(int productId, UpdateProductDto updateProductDto)
        {
            var product = await _productRepository.GetWithDetailsAsync(productId);
            if (product == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            // Validate slug uniqueness
            if (await _productRepository.SlugExistsAsync(updateProductDto.Slug, productId))
                throw new ArgumentException($"Product with slug '{updateProductDto.Slug}' already exists");

            // Validate SKU uniqueness
            if (await _productRepository.SKUExistsAsync(updateProductDto.SKU, productId))
                throw new ArgumentException($"Product with SKU '{updateProductDto.SKU}' already exists");

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

            _productRepository.Update(product);

            // Update category associations
            await _productRepository.RemoveProductCategoriesAsync(product.Id);

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
                    await _productRepository.AddProductCategoryAsync(pc);
                }
            }

            // Update images
            await UpdateProductImagesAsync(productId, updateProductDto.Images);

            await _productRepository.SaveChangesAsync();

            await InvalidateProductCacheAsync();
            await _cacheInvalidationService.InvalidateEntityAsync<Product>(productId);

            _logger.LogInformation("Updated product: {ProductName} (ID: {ProductId})", product.Name, product.Id);

            // Return the complete updated product
            var updatedProduct = await _productRepository.GetWithDetailsAsync(product.Id);
            return _mapper.Map<ProductDto>(updatedProduct!);
        }

        public async Task<bool> DeleteProductAsync(int productId)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null) return false;

            await _productRepository.SoftDeleteAsync(product);
            await _cacheInvalidationService.InvalidateEntityAsync<Product>(productId);
            await InvalidateProductCacheAsync();

            _logger.LogInformation("Deleted product: {ProductName} (ID: {ProductId})", product.Name, product.Id);
            return true;
        }

        public async Task<PagedResult<ProductDto>> SearchProductsAsync(ProductSearchDto searchDto)
        {
            // Validate and normalize pagination parameters
            var page = Math.Max(1, searchDto.Page);
            var pageSize = searchDto.PageSize <= 0 ? DefaultPageSize : Math.Min(searchDto.PageSize, 100);

            var cacheKey = _cacheKeyService.GetQueryKey<Product>("search", searchDto, page, pageSize);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    // Get total count for pagination
                    var totalCount = await _productRepository.GetSearchCountAsync(searchDto);

                    // Get paginated search results
                    var products = await _productRepository.SearchProductsAsync(searchDto);
                    var productDtos = _mapper.Map<List<ProductDto>>(products);

                    return new PagedResult<ProductDto>
                    {
                        Items = productDtos,
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = totalCount
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error searching products with criteria: {@SearchDto}", searchDto);
                    throw;
                }
            }) ?? PagedResult<ProductDto>.Empty(page, pageSize);
        }

        public async Task<int> GetSearchCountAsync(ProductSearchDto searchDto)
        {
            // For primitive return types, we need to wrap in a class
            var cacheKey = _cacheKeyService.GetQueryKey<Product>("search_count", searchDto);
            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var count = await _productRepository.GetSearchCountAsync(searchDto);
                return new CountWrapper { Value = count };
            });

            return result?.Value ?? 0;
        }

        public async Task<bool> ValidateSlugAsync(string slug, int? excludeProductId = null)
        {
            return !await _productRepository.SlugExistsAsync(slug, excludeProductId);
        }

        public async Task<bool> ValidateSKUAsync(string sku, int? excludeProductId = null)
        {
            return !await _productRepository.SKUExistsAsync(sku, excludeProductId);
        }

        public async Task<ProductDto> PublishProductAsync(int productId)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            product.Status = ProductStatus.Active;
            product.PublishedAt = DateTime.UtcNow;
            _productRepository.Update(product);
            await _productRepository.SaveChangesAsync();

            await _cacheInvalidationService.InvalidateEntityAsync<Product>(productId);
            await InvalidateProductCacheAsync();

            _logger.LogInformation("Published product: {ProductName} (ID: {ProductId})", product.Name, product.Id);
            return _mapper.Map<ProductDto>(product);
        }

        public async Task<ProductDto> UnpublishProductAsync(int productId)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            product.Status = ProductStatus.Draft;
            product.PublishedAt = null;
            _productRepository.Update(product);
            await _productRepository.SaveChangesAsync();

            await _cacheInvalidationService.InvalidateEntityAsync<Product>(productId);
            await InvalidateProductCacheAsync();

            _logger.LogInformation("Unpublished product: {ProductName} (ID: {ProductId})", product.Name, product.Id);
            return _mapper.Map<ProductDto>(product);
        }

        public async Task<ProductDto> ArchiveProductAsync(int productId)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            product.Status = ProductStatus.Archived;
            _productRepository.Update(product);
            await _productRepository.SaveChangesAsync();

            await _cacheInvalidationService.InvalidateEntityAsync<Product>(productId);
            await InvalidateProductCacheAsync();

            _logger.LogInformation("Archived product: {ProductName} (ID: {ProductId})", product.Name, product.Id);
            return _mapper.Map<ProductDto>(product);
        }

        public async Task<ProductDto> DuplicateProductAsync(int productId, string newName)
        {
            var originalProduct = await _productRepository.GetWithDetailsAsync(productId);
            if (originalProduct == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            var duplicatedProduct = new Product
            {
                Name = newName,
                Slug = await GenerateUniqueSlugAsync(newName),
                Description = originalProduct.Description,
                ShortDescription = originalProduct.ShortDescription,
                SKU = await GenerateUniqueSKUAsync(originalProduct.SKU),
                Price = originalProduct.Price,
                CompareAtPrice = originalProduct.CompareAtPrice,
                CostPerItem = originalProduct.CostPerItem,
                TrackQuantity = originalProduct.TrackQuantity,
                Quantity = 0, // Reset quantity for duplicated product
                ContinueSellingWhenOutOfStock = originalProduct.ContinueSellingWhenOutOfStock,
                RequiresShipping = originalProduct.RequiresShipping,
                IsPhysicalProduct = originalProduct.IsPhysicalProduct,
                Weight = originalProduct.Weight,
                WeightUnit = originalProduct.WeightUnit,
                IsTaxable = originalProduct.IsTaxable,
                Status = ProductStatus.Draft, // Set as draft
                Type = originalProduct.Type,
                Vendor = originalProduct.Vendor,
                HasVariants = originalProduct.HasVariants,
                Tags = originalProduct.Tags,
                Template = originalProduct.Template,
                MetaTitle = originalProduct.MetaTitle,
                MetaDescription = originalProduct.MetaDescription,
                MetaKeywords = originalProduct.MetaKeywords,
                SearchKeywords = originalProduct.SearchKeywords,
                CustomFields = new Dictionary<string, object>(originalProduct.CustomFields),
                SEOSettings = new Dictionary<string, object>(originalProduct.SEOSettings)
            };

            await _productRepository.AddAsync(duplicatedProduct);
            await _productRepository.SaveChangesAsync();

            // Duplicate category associations
            foreach (var pc in originalProduct.ProductCategories)
            {
                var newPc = new ProductCategory
                {
                    ProductId = duplicatedProduct.Id,
                    CategoryId = pc.CategoryId,
                    SortOrder = pc.SortOrder
                };
                await _productRepository.AddProductCategoryAsync(newPc);
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
                await _productImageRepository.AddAsync(newImage);
            }

            await _productRepository.SaveChangesAsync();
            await InvalidateProductCacheAsync();

            _logger.LogInformation("Duplicated product: {OriginalProductName} -> {NewProductName} (ID: {ProductId})",
                originalProduct.Name, newName, duplicatedProduct.Id);

            var createdProduct = await _productRepository.GetWithDetailsAsync(duplicatedProduct.Id);
            return _mapper.Map<ProductDto>(createdProduct!);
        }

        public async Task<PagedResult<ProductDto>> GetFeaturedProductsAsync(int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, 100);

            var cacheKey = _cacheKeyService.GetCollectionKey<Product>("featured", page, pageSize);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    // Get paginated featured products
                    var pagedResult = await _productRepository.GetPagedResultAsync(
                        page,
                        pageSize,
                        predicate: p => p.Status == ProductStatus.Active,
                        orderBy: query => query.OrderBy(p => Guid.NewGuid()) // Random order for featured products
                    );

                    var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Items);

                    return new PagedResult<ProductDto>
                    {
                        Items = productDtos,
                        Page = pagedResult.Page,
                        PageSize = pagedResult.PageSize,
                        TotalCount = pagedResult.TotalCount
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching paginated featured products for page {Page}, pageSize {PageSize}", page, pageSize);
                    throw;
                }
            }) ?? PagedResult<ProductDto>.Empty(page, pageSize);
        }

        public async Task<PagedResult<ProductDto>> GetRelatedProductsAsync(int productId, int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, 100);

            var cacheKey = _cacheKeyService.GetCollectionKey<Product>("related", productId, page, pageSize);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    var product = await _productRepository.GetByIdAsync(productId);
                    if (product == null)
                    {
                        return PagedResult<ProductDto>.Empty(page, pageSize);
                    }

                    var productWithCategories = await _productRepository.GetWithCategoriesAsync(productId);
                    if (productWithCategories == null)
                    {
                        return PagedResult<ProductDto>.Empty(page, pageSize);
                    }

                    var categoryIds = productWithCategories.ProductCategories.Select(pc => pc.CategoryId).ToList();

                    if (!categoryIds.Any())
                    {
                        return PagedResult<ProductDto>.Empty(page, pageSize);
                    }

                    // Get paginated related products
                    var pagedResult = await _productRepository.GetPagedResultAsync(
                        page,
                        pageSize,
                        predicate: p => p.Id != productId &&
                                       p.Status == ProductStatus.Active &&
                                       p.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId) && !pc.IsDeleted),
                        orderBy: query => query.OrderBy(p => Guid.NewGuid()) // Random order
                    );

                    var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Items);

                    return new PagedResult<ProductDto>
                    {
                        Items = productDtos,
                        Page = pagedResult.Page,
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
            }) ?? PagedResult<ProductDto>.Empty(page, pageSize);
        }

        public async Task<PagedResult<ProductDto>> GetRecentProductsAsync(int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, 100);

            var cacheKey = _cacheKeyService.GetCollectionKey<Product>("recent", page, pageSize);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    // Get paginated recent products
                    var pagedResult = await _productRepository.GetPagedResultAsync(
                        page,
                        pageSize,
                        predicate: p => p.Status == ProductStatus.Active,
                        orderBy: query => query.OrderByDescending(p => p.CreatedAt)
                    );

                    var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Items);

                    return new PagedResult<ProductDto>
                    {
                        Items = productDtos,
                        Page = pagedResult.Page,
                        PageSize = pagedResult.PageSize,
                        TotalCount = pagedResult.TotalCount
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching paginated recent products for page {Page}, pageSize {PageSize}", page, pageSize);
                    throw;
                }
            }) ?? PagedResult<ProductDto>.Empty(page, pageSize);
        }

        public async Task<Dictionary<string, object>> GetProductStatisticsAsync()
        {
            var cacheKey = _cacheKeyService.GetCustomKey("product", "statistics");
            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var totalProducts = await _productRepository.CountAsync();
                var activeProducts = await _productRepository.CountAsync(p => p.Status == ProductStatus.Active);
                var draftProducts = await _productRepository.CountAsync(p => p.Status == ProductStatus.Draft);
                var archivedProducts = await _productRepository.CountAsync(p => p.Status == ProductStatus.Archived);

                return new Dictionary<string, object>
                {
                    ["TotalProducts"] = totalProducts,
                    ["ActiveProducts"] = activeProducts,
                    ["DraftProducts"] = draftProducts,
                    ["ArchivedProducts"] = archivedProducts,
                    ["LastUpdated"] = DateTime.UtcNow
                };
            }) ?? new Dictionary<string, object>();
        }

        public async Task<(decimal min, decimal max)> GetPriceRangeAsync()
        {
            var cacheKey = _cacheKeyService.GetCustomKey("product", "price_range");
            var priceRange = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var min = await _productRepository.GetMinPriceAsync();
                var max = await _productRepository.GetMaxPriceAsync();
                return new PriceRange { Min = min, Max = max };
            });

            return priceRange == null
                ? throw new InvalidOperationException("Price range data could not be retrieved.")
                : (priceRange.Min, priceRange.Max);
        }

        public async Task<List<string>> GetVendorsAsync()
        {
            var cacheKey = _cacheKeyService.GetCustomKey("product", "vendors");
            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var vendors = await _productRepository.GetVendorsAsync();
                return vendors.ToList();
            }) ?? [];
        }

        public async Task<List<string>> GetTagsAsync()
        {
            var cacheKey = _cacheKeyService.GetCustomKey("product", "tags");
            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var tags = await _productRepository.GetTagsAsync();
                return tags.ToList();
            }) ?? [];
        }

        public async Task UpdateStockAsync(int productId, int? variantId, int newQuantity)
        {
            if (variantId.HasValue)
            {
                await _variantRepository.UpdateStockAsync(variantId.Value, newQuantity);
            }
            else
            {
                var product = await _productRepository.GetByIdAsync(productId);
                if (product != null)
                {
                    product.Quantity = newQuantity;
                    _productRepository.Update(product);
                    await _productRepository.SaveChangesAsync();
                }
            }

            await _cacheInvalidationService.InvalidateEntityAsync<Product>(productId);
            await InvalidateProductCacheAsync();
        }

        public async Task<PagedResult<ProductDto>> GetLowStockProductsAsync(int threshold = 5, int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, 100);

            var cacheKey = _cacheKeyService.GetCustomKey("product", "low_stock", threshold, page, pageSize);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    // Get paginated low stock products
                    var pagedResult = await _productRepository.GetPagedResultAsync(
                        page,
                        pageSize,
                        predicate: p => p.TrackQuantity && p.Quantity <= threshold && p.Quantity > 0,
                        orderBy: query => query.OrderBy(p => p.Quantity).ThenBy(p => p.Name)
                    );

                    var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Items);

                    return new PagedResult<ProductDto>
                    {
                        Items = productDtos,
                        Page = pagedResult.Page,
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
            }) ?? PagedResult<ProductDto>.Empty(page, pageSize);
        }

        public async Task<PagedResult<ProductDto>> GetOutOfStockProductsAsync(int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, 100);

            var cacheKey = _cacheKeyService.GetCustomKey("product", "out_of_stock", page, pageSize);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                try
                {
                    // Get paginated out of stock products
                    var pagedResult = await _productRepository.GetPagedResultAsync(
                        page,
                        pageSize,
                        predicate: p => p.TrackQuantity && p.Quantity <= 0,
                        orderBy: query => query.OrderBy(p => p.Name)
                    );

                    var productDtos = _mapper.Map<List<ProductDto>>(pagedResult.Items);

                    return new PagedResult<ProductDto>
                    {
                        Items = productDtos,
                        Page = pagedResult.Page,
                        PageSize = pagedResult.PageSize,
                        TotalCount = pagedResult.TotalCount
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching paginated out of stock products for page {Page}, pageSize {PageSize}", page, pageSize);
                    throw;
                }
            }) ?? PagedResult<ProductDto>.Empty(page, pageSize);
        }

        // Image management methods
        public async Task<ProductImageDto> AddProductImageAsync(int productId, CreateProductImageDto createImageDto)
        {
            var product = await _productRepository.GetByIdAsync(productId);
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

            await _productImageRepository.AddAsync(productImage);
            await _productImageRepository.SaveChangesAsync();

            await _cacheInvalidationService.InvalidateEntityAsync<Product>(productId);
            await InvalidateProductCacheAsync();

            _logger.LogInformation("Added image to product {ProductId}: FileId {FileId}", productId, createImageDto.FileId);
            return _mapper.Map<ProductImageDto>(productImage);
        }

        public async Task<ProductImageDto> UpdateProductImageAsync(int imageId, UpdateProductImageDto updateImageDto)
        {
            var productImage = await _productImageRepository.GetByIdAsync(imageId);
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

            _productImageRepository.Update(productImage);
            await _productImageRepository.SaveChangesAsync();

            await _cacheInvalidationService.InvalidateEntityAsync<Product>(productImage.ProductId);
            await InvalidateProductCacheAsync();

            _logger.LogInformation("Updated product image {ImageId}", imageId);
            return _mapper.Map<ProductImageDto>(productImage);
        }

        public async Task<bool> DeleteProductImageAsync(int imageId)
        {
            var productImage = await _productImageRepository.GetByIdAsync(imageId);
            if (productImage == null) return false;

            var productId = productImage.ProductId;
            await _productImageRepository.SoftDeleteAsync(productImage);

            await _cacheInvalidationService.InvalidateEntityAsync<Product>(productId);
            await InvalidateProductCacheAsync();

            _logger.LogInformation("Deleted product image {ImageId} from product {ProductId}", imageId, productId);
            return true;
        }

        public async Task<List<ProductImageDto>> ReorderProductImagesAsync(int productId, List<(int ImageId, int Position)> imageOrders)
        {
            var imageIds = imageOrders.Select(io => io.ImageId).ToList();
            var images = await _productImageRepository.FindAsync(i => i.ProductId == productId && imageIds.Contains(i.Id));

            foreach (var image in images)
            {
                var newPosition = imageOrders.First(io => io.ImageId == image.Id).Position;
                image.Position = newPosition;
            }

            _productImageRepository.UpdateRange(images);
            await _productImageRepository.SaveChangesAsync();

            await _cacheInvalidationService.InvalidateEntityAsync<Product>(productId);
            await InvalidateProductCacheAsync();

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

            while (await _productRepository.SlugExistsAsync(slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }

        private async Task<string> GenerateUniqueSKUAsync(string baseSku)
        {
            var sku = baseSku;
            var counter = 1;

            while (await _productRepository.SKUExistsAsync(sku))
            {
                sku = $"{baseSku}-{counter}";
                counter++;
            }

            return sku;
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
            var file = await _fileRepository.GetByIdAsync(fileId);
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

                await _productImageRepository.AddAsync(productImage);
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedProductImageAsync(productId);
            await _productImageRepository.SaveChangesAsync();
        }

        private async Task UpdateProductImagesAsync(int productId, List<UpdateProductImageDto> images)
        {
            // Remove existing images
            var existingImages = await _productImageRepository.FindAsync(i => i.ProductId == productId);
            await _productImageRepository.SoftDeleteRangeAsync(existingImages);

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

                await _productImageRepository.AddAsync(productImage);
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedProductImageAsync(productId);
        }

        private async Task RemoveFeaturedFlagFromOtherProductImagesAsync(int productId, int? excludeImageId = null)
        {
            var otherImages = await _productImageRepository.FindAsync(i =>
                i.ProductId == productId &&
                i.IsFeatured &&
                (excludeImageId == null || i.Id != excludeImageId));

            foreach (var image in otherImages)
            {
                image.IsFeatured = false;
            }

            _productImageRepository.UpdateRange(otherImages);
        }

        private async Task EnsureSingleFeaturedProductImageAsync(int productId)
        {
            var featuredImages = await _productImageRepository.FindAsync(i => i.ProductId == productId && i.IsFeatured);
            var featuredImagesList = featuredImages.ToList();

            if (featuredImagesList.Count > 1)
            {
                // Keep only the first one as featured
                for (int i = 1; i < featuredImagesList.Count; i++)
                {
                    featuredImagesList[i].IsFeatured = false;
                }

                _productImageRepository.UpdateRange(featuredImagesList.Skip(1));
            }
            else if (!featuredImagesList.Any())
            {
                // If no featured image, make the first one featured
                var firstImage = await _productImageRepository.FirstOrDefaultAsync(i => i.ProductId == productId);
                if (firstImage != null)
                {
                    firstImage.IsFeatured = true;
                    _productImageRepository.Update(firstImage);
                }
            }
        }

        private async Task InvalidateProductCacheAsync()
        {
            try
            {
                await _cacheInvalidationService.InvalidateEntityTypeAsync<Product>();
                await _cacheInvalidationService.InvalidateByPatternAsync(_cacheKeyService.GetEntityPattern<Product>());

                _logger.LogDebug("Product cache invalidated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating product cache");
            }
        }
    }
}