using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Caching;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IProductVariantRepository _variantRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ICacheService _cacheService;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductService> _logger;

        public ProductService(
            IProductRepository productRepository,
            IProductVariantRepository variantRepository,
            ICategoryRepository categoryRepository,
            ICacheService cacheService,
            IMapper mapper,
            ILogger<ProductService> logger)
        {
            _productRepository = productRepository;
            _variantRepository = variantRepository;
            _categoryRepository = categoryRepository;
            _cacheService = cacheService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ProductDto?> GetProductByIdAsync(int productId)
        {
            var cacheKey = CacheKeys.ProductById(productId);
            async Task<ProductDto?> FetchAndMapProductFromRepositoryAsync()
            {
                try
                {
                    var product = await _productRepository.GetWithDetailsAsync(productId);

                    if (product == null)
                    {
                        return null;
                    }
                    var productDto = _mapper.Map<ProductDto>(product);

                    if (productDto == null)
                    {
                        return null;
                    }
                    return productDto;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[ERROR] Exception during product fetch/map for ID '{productId}': {ex.GetType().Name} - {ex.Message}");
                    throw;
                }
            }

            var productDto = await _cacheService.GetAsync(cacheKey, FetchAndMapProductFromRepositoryAsync);

            if (productDto == null)
            {
                _logger.LogError($"[DEBUG] GetProductByIdAsync for ID '{productId}' completed. Product not found (or factory returned null).");
            }
            else
            {
                _logger.LogError($"[DEBUG] GetProductByIdAsync for ID '{productId}' completed. Product found (from cache or repository).");
            }

            return productDto;
        }

        public async Task<ProductDto?> GetProductBySlugAsync(string slug)
        {
            var cacheKey = CacheKeys.ProductBySlug(slug);
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var product = await _productRepository.GetBySlugAsync(slug);
                return product != null ? _mapper.Map<ProductDto>(product) : null;
            });
        }

        public async Task<List<ProductListDto>> GetProductsAsync(int page = 1, int pageSize = 20)
        {
            var cacheKey = CacheKeys.ProductsList(page, pageSize);
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var products = await _productRepository.GetPagedAsync(page, pageSize);
                return _mapper.Map<List<ProductListDto>>(products);
            }) ?? [];
        }

        public async Task<List<ProductDto>> GetProductsByCategoryAsync(int categoryId, int page = 1, int pageSize = 20)
        {
            var cacheKey = CacheKeys.ProductsByCategory(categoryId, page, pageSize);
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var products = await _productRepository.GetByCategoryAsync(categoryId, page, pageSize);
                return _mapper.Map<List<ProductDto>>(products);
            }) ?? [];
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

            await InvalidateProductCache();

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

            await _productRepository.SaveChangesAsync();

            await InvalidateProductCache();

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
            await InvalidateProductCache();

            _logger.LogInformation("Deleted product: {ProductName} (ID: {ProductId})", product.Name, product.Id);
            return true;
        }

        public async Task<List<ProductListDto>> SearchProductsAsync(ProductSearchDto searchDto)
        {
            var products = await _productRepository.SearchProductsAsync(searchDto);
            return _mapper.Map<List<ProductListDto>>(products);
        }

        public async Task<int> GetSearchCountAsync(ProductSearchDto searchDto)
        {
            return await _productRepository.GetSearchCountAsync(searchDto);
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

            await InvalidateProductCache();

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

            await InvalidateProductCache();

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

            await InvalidateProductCache();

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

            await _productRepository.SaveChangesAsync();
            await InvalidateProductCache();

            _logger.LogInformation("Duplicated product: {OriginalProductName} -> {NewProductName} (ID: {ProductId})",
                originalProduct.Name, newName, duplicatedProduct.Id);

            var createdProduct = await _productRepository.GetWithDetailsAsync(duplicatedProduct.Id);
            return _mapper.Map<ProductDto>(createdProduct!);
        }

        public async Task<List<ProductDto>> GetFeaturedProductsAsync(int count = 10)
        {

            //var cacheKey = CacheKeys.FeaturedProducts(count);
            //return await _cacheService.GetAsync(cacheKey, async () =>
            //{
            //    var products = await _productRepository.GetFeaturedProductsAsync(count);
            //    return _mapper.Map<List<ProductDto>>(products);
            //});
            var cacheKey = CacheKeys.FeaturedProducts(count);
            async Task<List<ProductDto>> GetProductsFromRepositoryAndMapAsync()
            {
                try
                {
                    var products = await _productRepository.GetFeaturedProductsAsync(count);
                    var productDtos = _mapper.Map<List<ProductDto>>(products);
                    return productDtos ?? [];
                }
                catch (Exception ex)
                {
                   _logger.LogError($"[ERROR] Error fetching or mapping featured products: {ex.Message}");
                    throw;
                }
            }
            var featuredProducts = await _cacheService.GetAsync(cacheKey, async () => await GetProductsFromRepositoryAndMapAsync());
            return featuredProducts ?? [];
        }

        public async Task<List<ProductDto>> GetRelatedProductsAsync(int productId, int count = 4)
        {
            var cacheKey = CacheKeys.RelatedProducts(productId, count);
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var products = await _productRepository.GetRelatedProductsAsync(productId, count);
                return products != null ? _mapper.Map<List<ProductDto>>(products) : new List<ProductDto>();
            }) ?? [];
        }

        public async Task<List<ProductDto>> GetRecentProductsAsync(int count = 10)
        {
            var cacheKey = CacheKeys.RecentProducts(count);
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var products = await _productRepository.GetRecentProductsAsync(count);
                return _mapper.Map<List<ProductDto>>(products);
            }) ?? [];
        }

        public async Task<Dictionary<string, object>> GetProductStatisticsAsync()
        {
            var cacheKey = CacheKeys.ProductStatistics;
            return await _cacheService.GetAsync(cacheKey, async () =>
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
                } ?? [];
            }) ?? [];
        }

        public async Task<(decimal min, decimal max)> GetPriceRangeAsync()
        {
            var cacheKey = CacheKeys.ProductPriceRange;
            var priceRange = await _cacheService.GetAsync(cacheKey, async () =>
            {
                var min = await _productRepository.GetMinPriceAsync();
                var max = await _productRepository.GetMaxPriceAsync();
                return new PriceRange { Min = min, Max = max };
            });

            return priceRange == null
                ? throw new InvalidOperationException("Price range data could not be retrieved.")
                : ((decimal min, decimal max))(priceRange.Min, priceRange.Max);
        }

        public async Task<List<string>> GetVendorsAsync()
        {
            var cacheKey = CacheKeys.ProductVendors;
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var vendors = await _productRepository.GetVendorsAsync();
                return vendors.ToList();
            }) ?? [];
        }

        public async Task<List<string>> GetTagsAsync()
        {
            var cacheKey = CacheKeys.ProductTags;
            return await _cacheService.GetAsync(cacheKey, async () =>
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

            await InvalidateProductCache();
        }

        public async Task<List<ProductDto>> GetLowStockProductsAsync(int threshold = 5)
        {
            var products = await _productRepository.FindAsync(p =>
                p.TrackQuantity && p.Quantity <= threshold && p.Quantity > 0);
            return _mapper.Map<List<ProductDto>>(products);
        }

        public async Task<List<ProductDto>> GetOutOfStockProductsAsync()
        {
            var products = await _productRepository.FindAsync(p =>
                p.TrackQuantity && p.Quantity <= 0);
            return _mapper.Map<List<ProductDto>>(products);
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

        private async Task InvalidateProductCache()
        {
            await _cacheService.RemoveByPatternAsync(CacheKeys.ProductsPattern);
        }
    }

    // Helper class for caching price range
    public class PriceRange
    {
        public decimal Min { get; set; }
        public decimal Max { get; set; }
    }
}