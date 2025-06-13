using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Caching;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class ProductVariantService : IProductVariantService
    {
        private readonly IProductVariantRepository _variantRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICacheService _cacheService;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductVariantService> _logger;

        public ProductVariantService(
            IProductVariantRepository variantRepository,
            IProductRepository productRepository,
            ICacheService cacheService,
            IMapper mapper,
            ILogger<ProductVariantService> logger)
        {
            _variantRepository = variantRepository;
            _productRepository = productRepository;
            _cacheService = cacheService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ProductVariantDto> GetVariantByIdAsync(int variantId)
        {
            var cacheKey = CacheKeys.ProductVariantById(variantId);
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var variant = await _variantRepository.GetByIdAsync(variantId);
                if (variant == null)
                    throw new ArgumentException($"Product variant with ID {variantId} not found");

                return _mapper.Map<ProductVariantDto>(variant);
            });
        }

        public async Task<ProductVariantDto?> GetVariantBySKUAsync(string sku)
        {
            var cacheKey = CacheKeys.ProductVariantBySKU(sku);
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var variant = await _variantRepository.GetBySKUAsync(sku);
                return variant != null ? _mapper.Map<ProductVariantDto>(variant) : null;
            });
        }

        public async Task<List<ProductVariantDto>> GetVariantsByProductIdAsync(int productId)
        {
            var cacheKey = CacheKeys.ProductVariantsByProduct(productId);
            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var variants = await _variantRepository.GetByProductIdAsync(productId);
                return _mapper.Map<List<ProductVariantDto>>(variants);
            });
        }

        public async Task<ProductVariantDto?> GetDefaultVariantAsync(int productId)
        {
            var cacheKey = CacheKeys.ProductDefaultVariant(productId);
            return await _cacheService.GetAsync(cacheKey, async () =>
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

            // Update product to indicate it has variants
            if (!product.HasVariants)
            {
                product.HasVariants = true;
                _productRepository.Update(product);
                await _productRepository.SaveChangesAsync();
            }

            await InvalidateVariantCache(productId);

            _logger.LogInformation("Created variant: {VariantTitle} for product {ProductId}", variant.Title, productId);
            return _mapper.Map<ProductVariantDto>(variant);
        }

        public async Task<ProductVariantDto> UpdateVariantAsync(int variantId, UpdateProductVariantDto updateVariantDto)
        {
            var variant = await _variantRepository.GetByIdAsync(variantId);
            if (variant == null)
                throw new ArgumentException($"Product variant with ID {variantId} not found");

            // Validate SKU uniqueness
            if (await _variantRepository.SKUExistsAsync(updateVariantDto.SKU, variantId))
                throw new ArgumentException($"Product variant with SKU '{updateVariantDto.SKU}' already exists");

            _mapper.Map(updateVariantDto, variant);
            _variantRepository.Update(variant);
            await _variantRepository.SaveChangesAsync();

            await InvalidateVariantCache(variant.ProductId);

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

            await InvalidateVariantCache(productId);

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

            await InvalidateVariantCache(variant.ProductId);

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

            foreach (var productId in productIds)
            {
                await InvalidateVariantCache(productId);
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

            await InvalidateVariantCache(variant.ProductId);

            _logger.LogInformation("Updated stock for variant {VariantId}: {NewQuantity}", variantId, newQuantity);
            return _mapper.Map<ProductVariantDto>(variant);
        }

        public async Task<List<ProductVariantDto>> GetLowStockVariantsAsync(int threshold = 5)
        {
            var variants = await _variantRepository.GetLowStockVariantsAsync(threshold);
            return _mapper.Map<List<ProductVariantDto>>(variants);
        }

        public async Task<List<ProductVariantDto>> GetOutOfStockVariantsAsync()
        {
            var variants = await _variantRepository.GetOutOfStockVariantsAsync();
            return _mapper.Map<List<ProductVariantDto>>(variants);
        }

        public async Task<int> GetTotalStockAsync(int productId)
        {
            return await _variantRepository.GetTotalStockAsync(productId);
        }

        // Private helper methods
        private async Task InvalidateVariantCache(int productId)
        {
            await _cacheService.RemoveByPatternAsync(CacheKeys.ProductVariantsPattern(productId));
            await _cacheService.RemoveByPatternAsync(CacheKeys.ProductsPattern);
        }
    }
}