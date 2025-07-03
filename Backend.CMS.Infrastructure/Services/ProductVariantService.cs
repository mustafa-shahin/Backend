using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class ProductVariantService : IProductVariantService
    {
        private readonly IProductVariantRepository _variantRepository;
        private readonly IProductRepository _productRepository;
        private readonly IRepository<ProductVariantImage> _variantImageRepository;
        private readonly IRepository<FileEntity> _fileRepository;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductVariantService> _logger;
        private readonly IRepository<ProductImage> _productImageRepository;

        public ProductVariantService(
            IProductVariantRepository variantRepository,
            IProductRepository productRepository,
            IRepository<ProductVariantImage> variantImageRepository,
            IRepository<FileEntity> fileRepository,
            ICacheService cacheService,
            ICacheInvalidationService cacheInvalidationService,
            ICacheKeyService cacheKeyService,
            IMapper mapper,
            ILogger<ProductVariantService> logger,
            IRepository<ProductImage> productImageRepository)
        {
            _variantRepository = variantRepository;
            _productRepository = productRepository;
            _variantImageRepository = variantImageRepository;
            _fileRepository = fileRepository;
            _cacheService = cacheService;
            _cacheInvalidationService = cacheInvalidationService;
            _cacheKeyService = cacheKeyService;
            _mapper = mapper;
            _logger = logger;
            _productImageRepository = productImageRepository;
        }

        public async Task<ProductVariantDto> GetVariantByIdAsync(int variantId)
        {
            var cacheKey = _cacheKeyService.GetEntityKey<ProductVariant>(variantId);
            var variant = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var dbVariant = await _variantRepository.GetByIdAsync(variantId);
                if (dbVariant == null)
                    return null;

                return _mapper.Map<ProductVariantDto>(dbVariant);
            });

            if (variant == null)
                throw new ArgumentException($"Product variant with ID {variantId} not found");

            return variant;
        }

        public async Task<List<ProductVariantDto>> GetVariantsAsync()
        {
            var cacheKey = _cacheKeyService.GetCollectionKey<ProductVariant>("all");
            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var variants = await _variantRepository.GetAllAsync();
                return _mapper.Map<List<ProductVariantDto>>(variants);
            }) ?? [];
        }

        public async Task<ProductVariantDto?> GetVariantBySKUAsync(string sku)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("product_variant", "sku", sku.ToLowerInvariant());
            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var variant = await _variantRepository.GetBySKUAsync(sku);
                return variant != null ? _mapper.Map<ProductVariantDto>(variant) : null;
            });
        }

        public async Task<List<ProductVariantDto>> GetVariantsByProductIdAsync(int productId)
        {
            var cacheKey = _cacheKeyService.GetCollectionKey<ProductVariant>("by_product", productId);
            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var variants = await _variantRepository.GetByProductIdAsync(productId);
                return _mapper.Map<List<ProductVariantDto>>(variants);
            }) ?? [];
        }

        public async Task<ProductVariantDto?> GetDefaultVariantAsync(int productId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("product_variant", "default", productId);
            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var variant = await _variantRepository.GetDefaultVariantAsync(productId);
                return variant != null ? _mapper.Map<ProductVariantDto>(variant) : null;
            });
        }

        public async Task<ProductVariantDto> CreateVariantAsync(int productId, CreateProductVariantDto createVariantDto)
        {
            // Validate product exists
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                throw new ArgumentException($"Product with ID {productId} not found");

            // Validate SKU uniqueness
            if (await _variantRepository.SKUExistsAsync(createVariantDto.SKU))
                throw new ArgumentException($"Product variant with SKU '{createVariantDto.SKU}' already exists");

            // Validate images
            if (createVariantDto.Images.Any())
            {
                await ValidateImagesAsync(createVariantDto.Images.Select(i => i.FileId).ToList());
            }

            var variant = _mapper.Map<ProductVariant>(createVariantDto);
            variant.ProductId = productId;

            // If this is the first variant for the product, make it default
            var existingVariants = await _variantRepository.GetByProductIdAsync(productId);
            if (!existingVariants.Any())
            {
                variant.IsDefault = true;
            }

            await _variantRepository.AddAsync(variant);
            await _variantRepository.SaveChangesAsync();

            // Add images
            if (createVariantDto.Images.Any())
            {
                await AddVariantImagesAsync(variant.Id, createVariantDto.Images);
            }

            // Update product to indicate it has variants
            if (!product.HasVariants)
            {
                product.HasVariants = true;
                _productRepository.Update(product);
                await _productRepository.SaveChangesAsync();
            }

            await InvalidateVariantCacheAsync(productId);

            _logger.LogInformation("Created variant: {VariantTitle} for product {ProductId}", variant.Title, productId);

            // Return the complete variant with images
            var createdVariant = await _variantRepository.GetByIdAsync(variant.Id);
            return _mapper.Map<ProductVariantDto>(createdVariant!);
        }

        public async Task<ProductVariantDto> UpdateVariantAsync(int variantId, UpdateProductVariantDto updateVariantDto)
        {
            var variant = await _variantRepository.GetByIdAsync(variantId);
            if (variant == null)
                throw new ArgumentException($"Product variant with ID {variantId} not found");

            // Validate SKU uniqueness
            if (await _variantRepository.SKUExistsAsync(updateVariantDto.SKU, variantId))
                throw new ArgumentException($"Product variant with SKU '{updateVariantDto.SKU}' already exists");

            // Validate images
            if (updateVariantDto.Images.Any())
            {
                await ValidateImagesAsync(updateVariantDto.Images.Select(i => i.FileId).ToList());
            }

            _mapper.Map(updateVariantDto, variant);

            // Update images
            await UpdateVariantImagesAsync(variantId, updateVariantDto.Images);

            _variantRepository.Update(variant);
            await _variantRepository.SaveChangesAsync();

            await _cacheInvalidationService.InvalidateEntityAsync<ProductVariant>(variantId);
            await InvalidateVariantCacheAsync(variant.ProductId);

            _logger.LogInformation("Updated variant: {VariantTitle} (ID: {VariantId})", variant.Title, variant.Id);
            return _mapper.Map<ProductVariantDto>(variant);
        }

        public async Task<bool> DeleteVariantAsync(int variantId)
        {
            var variant = await _variantRepository.GetByIdAsync(variantId);
            if (variant == null) return false;

            var productId = variant.ProductId;

            // Check if this is the last variant
            var remainingVariants = await _variantRepository.GetByProductIdAsync(productId);
            var variantsAfterDeletion = remainingVariants.Where(v => v.Id != variantId).ToList();

            await _variantRepository.SoftDeleteAsync(variant);

            // If no variants remain, update product
            if (!variantsAfterDeletion.Any())
            {
                var product = await _productRepository.GetByIdAsync(productId);
                if (product != null)
                {
                    product.HasVariants = false;
                    _productRepository.Update(product);
                    await _productRepository.SaveChangesAsync();
                }
            }
            // If the deleted variant was default, make another one default
            else if (variant.IsDefault && variantsAfterDeletion.Any())
            {
                var newDefaultVariant = variantsAfterDeletion.OrderBy(v => v.Position).First();
                newDefaultVariant.IsDefault = true;
                _variantRepository.Update(newDefaultVariant);
                await _variantRepository.SaveChangesAsync();
            }

            await _cacheInvalidationService.InvalidateEntityAsync<ProductVariant>(variantId);
            await InvalidateVariantCacheAsync(productId);

            _logger.LogInformation("Deleted variant: {VariantTitle} (ID: {VariantId})", variant.Title, variant.Id);
            return true;
        }

        public async Task<bool> ValidateSKUAsync(string sku, int? excludeVariantId = null)
        {
            return !await _variantRepository.SKUExistsAsync(sku, excludeVariantId);
        }

        public async Task<ProductVariantDto> SetDefaultVariantAsync(int variantId)
        {
            var variant = await _variantRepository.GetByIdAsync(variantId);
            if (variant == null)
                throw new ArgumentException($"Product variant with ID {variantId} not found");

            // Remove default flag from other variants of the same product
            var allVariants = await _variantRepository.GetByProductIdAsync(variant.ProductId);
            foreach (var v in allVariants)
            {
                v.IsDefault = v.Id == variantId;
            }

            _variantRepository.UpdateRange(allVariants);
            await _variantRepository.SaveChangesAsync();

            await _cacheInvalidationService.InvalidateEntitiesAsync<ProductVariant>(allVariants.Select(v => (object)v.Id));
            await InvalidateVariantCacheAsync(variant.ProductId);

            _logger.LogInformation("Set default variant: {VariantTitle} (ID: {VariantId})", variant.Title, variant.Id);
            return _mapper.Map<ProductVariantDto>(variant);
        }

        public async Task<List<ProductVariantDto>> ReorderVariantsAsync(List<(int VariantId, int Position)> variantOrders)
        {
            var variantIds = variantOrders.Select(vo => vo.VariantId).ToList();
            var variants = await _variantRepository.FindAsync(v => variantIds.Contains(v.Id));

            var productIds = new HashSet<int>();

            foreach (var variant in variants)
            {
                var newPosition = variantOrders.First(vo => vo.VariantId == variant.Id).Position;
                variant.Position = newPosition;
                productIds.Add(variant.ProductId);
            }

            _variantRepository.UpdateRange(variants);
            await _variantRepository.SaveChangesAsync();

            await _cacheInvalidationService.InvalidateEntitiesAsync<ProductVariant>(variantIds.Cast<object>());

            foreach (var productId in productIds)
            {
                await InvalidateVariantCacheAsync(productId);
            }

            return _mapper.Map<List<ProductVariantDto>>(variants);
        }

        public async Task<ProductVariantDto> UpdateStockAsync(int variantId, int newQuantity)
        {
            var variant = await _variantRepository.GetByIdAsync(variantId);
            if (variant == null)
                throw new ArgumentException($"Product variant with ID {variantId} not found");

            variant.Quantity = newQuantity;
            _variantRepository.Update(variant);
            await _variantRepository.SaveChangesAsync();

            await _cacheInvalidationService.InvalidateEntityAsync<ProductVariant>(variantId);
            await InvalidateVariantCacheAsync(variant.ProductId);

            _logger.LogInformation("Updated stock for variant {VariantId}: {NewQuantity}", variantId, newQuantity);
            return _mapper.Map<ProductVariantDto>(variant);
        }

        public async Task<List<ProductVariantDto>> GetLowStockVariantsAsync(int threshold = 5)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("product_variant", "low_stock", threshold);
            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var variants = await _variantRepository.GetLowStockVariantsAsync(threshold);
                return _mapper.Map<List<ProductVariantDto>>(variants);
            }) ?? [];
        }

        public async Task<List<ProductVariantDto>> GetOutOfStockVariantsAsync()
        {
            var cacheKey = _cacheKeyService.GetCustomKey("product_variant", "out_of_stock");
            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var variants = await _variantRepository.GetOutOfStockVariantsAsync();
                return _mapper.Map<List<ProductVariantDto>>(variants);
            }) ?? [];
        }

        public async Task<int> GetTotalStockAsync(int productId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("product_variant", "total_stock", productId);
            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var total = await _variantRepository.GetTotalStockAsync(productId);
                return new CountWrapper { Value = total };
            });

            return result?.Value ?? 0;
        }

        // Image management methods
        public async Task<ProductVariantImageDto> AddVariantImageAsync(int variantId, CreateProductVariantImageDto createImageDto)
        {
            var variant = await _variantRepository.GetByIdAsync(variantId);
            if (variant == null)
                throw new ArgumentException($"Product variant with ID {variantId} not found");

            await ValidateImageAsync(createImageDto.FileId);

            var variantImage = _mapper.Map<ProductVariantImage>(createImageDto);
            variantImage.ProductVariantId = variantId;

            // If this is set as featured, remove featured flag from other images
            if (createImageDto.IsFeatured)
            {
                await RemoveFeaturedFlagFromOtherVariantImagesAsync(variantId);
            }

            await _variantImageRepository.AddAsync(variantImage);
            await _variantImageRepository.SaveChangesAsync();

            await _cacheInvalidationService.InvalidateEntityAsync<ProductVariant>(variantId);
            await InvalidateVariantCacheAsync(variant.ProductId);

            _logger.LogInformation("Added image to variant {VariantId}: FileId {FileId}", variantId, createImageDto.FileId);
            return _mapper.Map<ProductVariantImageDto>(variantImage);
        }

        public async Task<ProductVariantImageDto> UpdateVariantImageAsync(int imageId, UpdateProductVariantImageDto updateImageDto)
        {
            var variantImage = await _variantImageRepository.GetByIdAsync(imageId);
            if (variantImage == null)
                throw new ArgumentException($"Product variant image with ID {imageId} not found");

            await ValidateImageAsync(updateImageDto.FileId);

            var oldIsFeatured = variantImage.IsFeatured;
            _mapper.Map(updateImageDto, variantImage);

            // If this image is being set as featured, remove featured flag from other images
            if (updateImageDto.IsFeatured && !oldIsFeatured)
            {
                await RemoveFeaturedFlagFromOtherVariantImagesAsync(variantImage.ProductVariantId, imageId);
            }

            _variantImageRepository.Update(variantImage);
            await _variantImageRepository.SaveChangesAsync();

            var variant = await _variantRepository.GetByIdAsync(variantImage.ProductVariantId);
            if (variant != null)
            {
                await _cacheInvalidationService.InvalidateEntityAsync<ProductVariant>(variant.Id);
                await InvalidateVariantCacheAsync(variant.ProductId);
            }

            _logger.LogInformation("Updated variant image {ImageId}", imageId);
            return _mapper.Map<ProductVariantImageDto>(variantImage);
        }

        public async Task<bool> DeleteVariantImageAsync(int imageId)
        {
            var variantImage = await _variantImageRepository.GetByIdAsync(imageId);
            if (variantImage == null) return false;

            var variantId = variantImage.ProductVariantId;
            var variant = await _variantRepository.GetByIdAsync(variantId);

            await _variantImageRepository.SoftDeleteAsync(variantImage);

            if (variant != null)
            {
                await _cacheInvalidationService.InvalidateEntityAsync<ProductVariant>(variantId);
                await InvalidateVariantCacheAsync(variant.ProductId);
            }

            _logger.LogInformation("Deleted variant image {ImageId} from variant {VariantId}", imageId, variantId);
            return true;
        }

        public async Task<List<ProductVariantImageDto>> ReorderVariantImagesAsync(int variantId, List<(int ImageId, int Position)> imageOrders)
        {
            var imageIds = imageOrders.Select(io => io.ImageId).ToList();
            var images = await _variantImageRepository.FindAsync(i => i.ProductVariantId == variantId && imageIds.Contains(i.Id));

            foreach (var image in images)
            {
                var newPosition = imageOrders.First(io => io.ImageId == image.Id).Position;
                image.Position = newPosition;
            }

            _variantImageRepository.UpdateRange(images);
            await _variantImageRepository.SaveChangesAsync();

            var variant = await _variantRepository.GetByIdAsync(variantId);
            if (variant != null)
            {
                await _cacheInvalidationService.InvalidateEntityAsync<ProductVariant>(variantId);
                await InvalidateVariantCacheAsync(variant.ProductId);
            }

            return _mapper.Map<List<ProductVariantImageDto>>(images.OrderBy(i => i.Position));
        }

        // Private helper methods
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

        private async Task AddVariantImagesAsync(int variantId, List<CreateProductVariantImageDto> images)
        {
            foreach (var imageDto in images)
            {
                var variantImage = _mapper.Map<ProductVariantImage>(imageDto);
                variantImage.ProductVariantId = variantId;
                variantImage.CreatedAt = DateTime.UtcNow;
                variantImage.UpdatedAt = DateTime.UtcNow;

                await _variantImageRepository.AddAsync(variantImage);
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedVariantImageAsync(variantId);
            await _variantImageRepository.SaveChangesAsync();
        }

        private async Task UpdateVariantImagesAsync(int variantId, List<UpdateProductVariantImageDto> images)
        {
            // Remove existing images (soft delete)
            var existingImages = await _variantImageRepository.FindAsync(i => i.ProductVariantId == variantId);
            await _variantImageRepository.SoftDeleteRangeAsync(existingImages);

            // Add new images
            foreach (var imageDto in images)
            {
                var variantImage = new ProductVariantImage
                {
                    ProductVariantId = variantId,
                    FileId = imageDto.FileId,
                    Alt = imageDto.Alt,
                    Caption = imageDto.Caption,
                    Position = imageDto.Position,
                    IsFeatured = imageDto.IsFeatured,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _variantImageRepository.AddAsync(variantImage);
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedVariantImageAsync(variantId);
            await _variantImageRepository.SaveChangesAsync();
        }

        private async Task RemoveFeaturedFlagFromOtherVariantImagesAsync(int variantId, int? excludeImageId = null)
        {
            var otherImages = await _variantImageRepository.FindAsync(i =>
                i.ProductVariantId == variantId &&
                i.IsFeatured &&
                (excludeImageId == null || i.Id != excludeImageId));

            foreach (var image in otherImages)
            {
                image.IsFeatured = false;
            }

            _variantImageRepository.UpdateRange(otherImages);
        }

        private async Task EnsureSingleFeaturedVariantImageAsync(int variantId)
        {
            var featuredImages = await _variantImageRepository.FindAsync(i => i.ProductVariantId == variantId && i.IsFeatured);
            var featuredImagesList = featuredImages.ToList();

            if (featuredImagesList.Count > 1)
            {
                // Keep only the first one as featured
                for (int i = 1; i < featuredImagesList.Count; i++)
                {
                    featuredImagesList[i].IsFeatured = false;
                }

                _variantImageRepository.UpdateRange(featuredImagesList.Skip(1));
            }
            else if (!featuredImagesList.Any())
            {
                // If no featured image, make the first one featured
                var firstImage = await _variantImageRepository.FirstOrDefaultAsync(i => i.ProductVariantId == variantId);
                if (firstImage != null)
                {
                    firstImage.IsFeatured = true;
                    _variantImageRepository.Update(firstImage);
                }
            }
        }

        private async Task InvalidateVariantCacheAsync(int productId)
        {
            try
            {
                // Invalidate specific cache patterns
                await _cacheInvalidationService.InvalidateEntityTypeAsync<ProductVariant>();
                await _cacheInvalidationService.InvalidateByPatternAsync(_cacheKeyService.GetEntityPattern<ProductVariant>());

                // Invalidate product-specific variant caches
                var productVariantPattern = _cacheKeyService.GetCustomKey("product_variant", "*", productId);
                await _cacheInvalidationService.InvalidateByPatternAsync(productVariantPattern);

                // Also invalidate related product cache since variants affect product data
                await _cacheInvalidationService.InvalidateEntityAsync<Product>(productId);

                _logger.LogDebug("Variant cache invalidated successfully for product {ProductId}", productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating variant cache for product {ProductId}", productId);
            }
        }
    }
}