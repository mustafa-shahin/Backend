using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class ProductVariantService : IProductVariantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductVariantService> _logger;

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 100;

        public ProductVariantService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<ProductVariantService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ProductVariantDto> GetVariantByIdAsync(int variantId)
        {
            var dbVariant = await _unitOfWork.ProductVariants.GetByIdAsync(variantId);
            if (dbVariant == null)
                throw new ArgumentException($"Product variant with ID {variantId} not found");

            return _mapper.Map<ProductVariantDto>(dbVariant);
        }

        public async Task<PaginatedResult<ProductVariantDto>> GetVariantsAsync(int page = 1, int pageSize = 10, bool standaloneOnly = false)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            try
            {
                // Calculate skip and take
                var skip = (page - 1) * pageSize;
                var take = pageSize;

                int totalCount;
                IEnumerable<ProductVariant> variants;

                if (standaloneOnly)
                {
                    // Get standalone variants (variants without product association or with ProductId = 0)
                    totalCount = await _unitOfWork.ProductVariants.GetStandaloneVariantCountAsync();
                    variants = await _unitOfWork.ProductVariants.GetStandaloneVariantsAsync(skip, take);
                }
                else
                {
                    // Get all variants
                    totalCount = await _unitOfWork.ProductVariants.CountAsync();
                    variants = await _unitOfWork.ProductVariants.GetPagedAsync(page, pageSize);
                }

                var variantDtos = _mapper.Map<List<ProductVariantDto>>(variants);

                return new PaginatedResult<ProductVariantDto>(variantDtos, page, pageSize, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated variants for page {Page}, pageSize {PageSize}, standaloneOnly {StandaloneOnly}",
                    page, pageSize, standaloneOnly);
                throw;
            }
        }

        public async Task<PaginatedResult<ProductVariantDto>> GetStandaloneVariantsAsync(int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            try
            {
                // Calculate skip and take
                var skip = (page - 1) * pageSize;
                var take = pageSize;

                // Get total count and data
                var totalCount = await _unitOfWork.ProductVariants.GetStandaloneVariantCountAsync();
                var variants = await _unitOfWork.ProductVariants.GetStandaloneVariantsAsync(skip, take);

                var variantDtos = _mapper.Map<List<ProductVariantDto>>(variants);

                return new PaginatedResult<ProductVariantDto>(variantDtos, page, pageSize, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated standalone variants for page {Page}, pageSize {PageSize}", page, pageSize);
                throw;
            }
        }

        public async Task<List<ProductVariantDto>> GetVariantsByProductIdAsync(int productId)
        {
            var variants = await _unitOfWork.ProductVariants.GetByProductIdAsync(productId);
            return _mapper.Map<List<ProductVariantDto>>(variants);
        }

        public async Task<ProductVariantDto?> GetDefaultVariantAsync(int productId)
        {
            var variant = await _unitOfWork.ProductVariants.GetDefaultVariantAsync(productId);
            return variant != null ? _mapper.Map<ProductVariantDto>(variant) : null;
        }

        public async Task<ProductVariantDto> CreateVariantAsync(int productId, CreateProductVariantDto createVariantDto)
        {
            // Validate product exists if productId is provided and > 0
            if (productId > 0)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(productId);
                if (product == null)
                    throw new ArgumentException($"Product with ID {productId} not found");
            }

            // Validate images
            if (createVariantDto.Images.Any())
            {
                await ValidateImagesAsync(createVariantDto.Images.Select(i => i.FileId).ToList());
            }

            var variant = _mapper.Map<ProductVariant>(createVariantDto);
            variant.ProductId = productId;

            // If this is the first variant for the product and productId > 0, make it default
            if (productId > 0)
            {
                var existingVariants = await _unitOfWork.ProductVariants.GetByProductIdAsync(productId);
                if (!existingVariants.Any())
                {
                    variant.IsDefault = true;
                }
            }

            await _unitOfWork.ProductVariants.AddAsync(variant);
            await _unitOfWork.ProductVariants.SaveChangesAsync();

            // Add images
            if (createVariantDto.Images.Any())
            {
                await AddVariantImagesAsync(variant.Id, createVariantDto.Images);
            }

            // Update product to indicate it has variants (only if productId > 0)
            if (productId > 0)
            {
                var product = await _unitOfWork.Products.GetByIdAsync(productId);
                if (product != null && !product.HasVariants)
                {
                    product.HasVariants = true;
                    _unitOfWork.Products.Update(product);
                    await _unitOfWork.Products.SaveChangesAsync();
                }
            }

            _logger.LogInformation("Created variant: {VariantTitle} for product {ProductId}", variant.Title, productId);

            // Return the complete variant with images
            var createdVariant = await _unitOfWork.ProductVariants.GetByIdAsync(variant.Id);
            return _mapper.Map<ProductVariantDto>(createdVariant!);
        }

        public async Task<ProductVariantDto> UpdateVariantAsync(int variantId, UpdateProductVariantDto updateVariantDto)
        {
            var variant = await _unitOfWork.ProductVariants.GetByIdAsync(variantId);
            if (variant == null)
                throw new ArgumentException($"Product variant with ID {variantId} not found");

            // Validate images
            if (updateVariantDto.Images.Any())
            {
                await ValidateImagesAsync(updateVariantDto.Images.Select(i => i.FileId).ToList());
            }

            _mapper.Map(updateVariantDto, variant);

            // Update images
            await UpdateVariantImagesAsync(variantId, updateVariantDto.Images);

            _unitOfWork.ProductVariants.Update(variant);
            await _unitOfWork.ProductVariants.SaveChangesAsync();

            _logger.LogInformation("Updated variant: {VariantTitle} (ID: {VariantId})", variant.Title, variant.Id);
            return _mapper.Map<ProductVariantDto>(variant);
        }

        public async Task<bool> DeleteVariantAsync(int variantId)
        {
            var variant = await _unitOfWork.ProductVariants.GetByIdAsync(variantId);
            if (variant == null) return false;

            var productId = variant.ProductId;

            // Check if this is the last variant (only for regular products, not standalone variants)
            if (productId > 0)
            {
                var remainingVariants = await _unitOfWork.ProductVariants.GetByProductIdAsync(productId);
                var variantsAfterDeletion = remainingVariants.Where(v => v.Id != variantId).ToList();

                await _unitOfWork.ProductVariants.SoftDeleteAsync(variant);

                // If no variants remain, update product
                if (!variantsAfterDeletion.Any())
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(productId);
                    if (product != null)
                    {
                        product.HasVariants = false;
                        _unitOfWork.Products.Update(product);
                        await _unitOfWork.Products.SaveChangesAsync();
                    }
                }
                // If the deleted variant was default, make another one default
                else if (variant.IsDefault && variantsAfterDeletion.Any())
                {
                    var newDefaultVariant = variantsAfterDeletion.OrderBy(v => v.Position).First();
                    newDefaultVariant.IsDefault = true;
                    _unitOfWork.ProductVariants.Update(newDefaultVariant);
                    await _unitOfWork.ProductVariants.SaveChangesAsync();
                }
            }
            else
            {
                // For standalone variants, just delete
                await _unitOfWork.ProductVariants.SoftDeleteAsync(variant);
            }

            _logger.LogInformation("Deleted variant: {VariantTitle} (ID: {VariantId})", variant.Title, variant.Id);
            return true;
        }

        public async Task<ProductVariantDto> SetDefaultVariantAsync(int variantId)
        {
            var variant = await _unitOfWork.ProductVariants.GetByIdAsync(variantId);
            if (variant == null)
                throw new ArgumentException($"Product variant with ID {variantId} not found");

            // Only set default for variants that belong to a product (not standalone variants)
            if (variant.ProductId <= 0)
                throw new ArgumentException("Cannot set default variant for standalone variants");

            // Remove default flag from other variants of the same product
            var allVariants = await _unitOfWork.ProductVariants.GetByProductIdAsync(variant.ProductId);
            foreach (var v in allVariants)
            {
                v.IsDefault = v.Id == variantId;
            }

            _unitOfWork.ProductVariants.UpdateRange(allVariants);
            await _unitOfWork.ProductVariants.SaveChangesAsync();

            _logger.LogInformation("Set default variant: {VariantTitle} (ID: {VariantId})", variant.Title, variant.Id);
            return _mapper.Map<ProductVariantDto>(variant);
        }

        public async Task<List<ProductVariantDto>> ReorderVariantsAsync(List<(int VariantId, int Position)> variantOrders)
        {
            var variantIds = variantOrders.Select(vo => vo.VariantId).ToList();
            var variants = await _unitOfWork.ProductVariants.FindAsync(v => variantIds.Contains(v.Id));

            foreach (var variant in variants)
            {
                var newPosition = variantOrders.First(vo => vo.VariantId == variant.Id).Position;
                variant.Position = newPosition;
            }

            _unitOfWork.ProductVariants.UpdateRange(variants);
            await _unitOfWork.ProductVariants.SaveChangesAsync();

            return _mapper.Map<List<ProductVariantDto>>(variants);
        }

        public async Task<ProductVariantDto> UpdateStockAsync(int variantId, int newQuantity)
        {
            var variant = await _unitOfWork.ProductVariants.GetByIdAsync(variantId);
            if (variant == null)
                throw new ArgumentException($"Product variant with ID {variantId} not found");

            variant.Quantity = newQuantity;
            _unitOfWork.ProductVariants.Update(variant);
            await _unitOfWork.ProductVariants.SaveChangesAsync();

            _logger.LogInformation("Updated stock for variant {VariantId}: {NewQuantity}", variantId, newQuantity);
            return _mapper.Map<ProductVariantDto>(variant);
        }

        public async Task<PaginatedResult<ProductVariantDto>> GetLowStockVariantsAsync(int threshold = 5, int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            try
            {
                // Calculate skip and take
                var skip = (page - 1) * pageSize;
                var take = pageSize;

                // Get total count and data
                var totalCount = await _unitOfWork.ProductVariants.GetLowStockVariantsCountAsync(threshold);
                var variants = await _unitOfWork.ProductVariants.GetLowStockVariantsAsync(threshold, skip, take);

                var variantDtos = _mapper.Map<List<ProductVariantDto>>(variants);

                return new PaginatedResult<ProductVariantDto>(variantDtos, page, pageSize, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated low stock variants for threshold {Threshold}, page {Page}, pageSize {PageSize}",
                    threshold, page, pageSize);
                throw;
            }
        }

        public async Task<PaginatedResult<ProductVariantDto>> GetOutOfStockVariantsAsync(int page = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            try
            {
                // Calculate skip and take
                var skip = (page - 1) * pageSize;
                var take = pageSize;

                // Get total count and data
                var totalCount = await _unitOfWork.ProductVariants.GetOutOfStockVariantsCountAsync();
                var variants = await _unitOfWork.ProductVariants.GetOutOfStockVariantsAsync(skip, take);

                var variantDtos = _mapper.Map<List<ProductVariantDto>>(variants);

                return new PaginatedResult<ProductVariantDto>(variantDtos, page, pageSize, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching paginated out of stock variants for page {Page}, pageSize {PageSize}", page, pageSize);
                throw;
            }
        }

        public async Task<int> GetTotalStockAsync(int productId)
        {
            var total = await _unitOfWork.ProductVariants.GetTotalStockAsync(productId);
            return total;
        }

        // Image management methods
        public async Task<ProductVariantImageDto> AddVariantImageAsync(int variantId, CreateProductVariantImageDto createImageDto)
        {
            var variant = await _unitOfWork.ProductVariants.GetByIdAsync(variantId);
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

            await _unitOfWork.GetRepository<ProductVariantImage>().AddAsync(variantImage);
            await _unitOfWork.GetRepository<ProductVariantImage>().SaveChangesAsync();

            _logger.LogInformation("Added image to variant {VariantId}: FileId {FileId}", variantId, createImageDto.FileId);
            return _mapper.Map<ProductVariantImageDto>(variantImage);
        }

        public async Task<ProductVariantImageDto> UpdateVariantImageAsync(int imageId, UpdateProductVariantImageDto updateImageDto)
        {
            var variantImage = await _unitOfWork.GetRepository<ProductVariantImage>().GetByIdAsync(imageId);
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

            _unitOfWork.GetRepository<ProductVariantImage>().Update(variantImage);
            await _unitOfWork.GetRepository<ProductVariantImage>().SaveChangesAsync();

            _logger.LogInformation("Updated variant image {ImageId}", imageId);
            return _mapper.Map<ProductVariantImageDto>(variantImage);
        }

        public async Task<bool> DeleteVariantImageAsync(int imageId)
        {
            var variantImage = await _unitOfWork.GetRepository<ProductVariantImage>().GetByIdAsync(imageId);
            if (variantImage == null) return false;

            var variantId = variantImage.ProductVariantId;

            await _unitOfWork.GetRepository<ProductVariantImage>().SoftDeleteAsync(variantImage);

            _logger.LogInformation("Deleted variant image {ImageId} from variant {VariantId}", imageId, variantId);
            return true;
        }

        public async Task<List<ProductVariantImageDto>> ReorderVariantImagesAsync(int variantId, List<(int ImageId, int Position)> imageOrders)
        {
            var imageIds = imageOrders.Select(io => io.ImageId).ToList();
            var images = await _unitOfWork.GetRepository<ProductVariantImage>().FindAsync(i => i.ProductVariantId == variantId && imageIds.Contains(i.Id));

            foreach (var image in images)
            {
                var newPosition = imageOrders.First(io => io.ImageId == image.Id).Position;
                image.Position = newPosition;
            }

            _unitOfWork.GetRepository<ProductVariantImage>().UpdateRange(images);
            await _unitOfWork.GetRepository<ProductVariantImage>().SaveChangesAsync();

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
            var file = await _unitOfWork.Files.GetByIdAsync(fileId);
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

                await _unitOfWork.GetRepository<ProductVariantImage>().AddAsync(variantImage);
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedVariantImageAsync(variantId);
            await _unitOfWork.GetRepository<ProductVariantImage>().SaveChangesAsync();
        }

        private async Task UpdateVariantImagesAsync(int variantId, List<UpdateProductVariantImageDto> images)
        {
            // Remove existing images (soft delete)
            var existingImages = await _unitOfWork.GetRepository<ProductVariantImage>().FindAsync(i => i.ProductVariantId == variantId);
            await _unitOfWork.GetRepository<ProductVariantImage>().SoftDeleteRangeAsync(existingImages);

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

                await _unitOfWork.GetRepository<ProductVariantImage>().AddAsync(variantImage);
            }

            // Ensure only one image is marked as featured
            await EnsureSingleFeaturedVariantImageAsync(variantId);
            await _unitOfWork.GetRepository<ProductVariantImage>().SaveChangesAsync();
        }

        private async Task RemoveFeaturedFlagFromOtherVariantImagesAsync(int variantId, int? excludeImageId = null)
        {
            var otherImages = await _unitOfWork.GetRepository<ProductVariantImage>().FindAsync(i =>
                i.ProductVariantId == variantId &&
                i.IsFeatured &&
                (excludeImageId == null || i.Id != excludeImageId));

            foreach (var image in otherImages)
            {
                image.IsFeatured = false;
            }

            _unitOfWork.GetRepository<ProductVariantImage>().UpdateRange(otherImages);
        }

        private async Task EnsureSingleFeaturedVariantImageAsync(int variantId)
        {
            var featuredImages = await _unitOfWork.GetRepository<ProductVariantImage>().FindAsync(i => i.ProductVariantId == variantId && i.IsFeatured);
            var featuredImagesList = featuredImages.ToList();

            if (featuredImagesList.Count > 1)
            {
                // Keep only the first one as featured
                for (int i = 1; i < featuredImagesList.Count; i++)
                {
                    featuredImagesList[i].IsFeatured = false;
                }

                _unitOfWork.GetRepository<ProductVariantImage>().UpdateRange(featuredImagesList.Skip(1));
            }
            else if (!featuredImagesList.Any())
            {
                // If no featured image, make the first one featured
                var firstImage = await _unitOfWork.GetRepository<ProductVariantImage>().FirstOrDefaultAsync(i => i.ProductVariantId == variantId);
                if (firstImage != null)
                {
                    firstImage.IsFeatured = true;
                    _unitOfWork.GetRepository<ProductVariantImage>().Update(firstImage);
                }
            }
        }
    }
}