using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// Product variant management controller providing variant operations
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/product-variant")]
    [ApiVersion("1.0")]
     
    [EnableRateLimiting("ApiPolicy")]
    public class ProductVariantController : ControllerBase
    {
        private readonly IProductVariantService _variantService;
        private readonly ILogger<ProductVariantController> _logger;

        public ProductVariantController(
            IProductVariantService variantService,
            ILogger<ProductVariantController> logger)
        {
            _variantService = variantService;
            _logger = logger;
        }

        /// <summary>
        /// Get all variants with pagination
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <param name="standaloneOnly">Filter for standalone variants only</param>
        /// <returns>Paginated list of variants</returns>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<ProductVariantDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductVariantDto>>> GetVariants(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool standaloneOnly = false)
        {
            try
            {
                var result = await _variantService.GetVariantsAsync(page, pageSize, standaloneOnly);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving variants for page {Page}, pageSize {PageSize}, standaloneOnly {StandaloneOnly}",
                    page, pageSize, standaloneOnly);
                return StatusCode(500, new { Message = "An error occurred while retrieving variants" });
            }
        }

        /// <summary>
        /// Get standalone variants (variants without product association)
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of standalone variants</returns>
        [HttpGet("standalone")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<ProductVariantDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductVariantDto>>> GetStandaloneVariants(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _variantService.GetStandaloneVariantsAsync(page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving standalone variants for page {Page}, pageSize {PageSize}", page, pageSize);
                return StatusCode(500, new { Message = "An error occurred while retrieving standalone variants" });
            }
        }

        /// <summary>
        /// Get variant by ID
        /// </summary>
        /// <param name="id">Variant ID</param>
        /// <returns>Variant information</returns>
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductVariantDto>> GetVariant(int id)
        {
            try
            {
                var variant = await _variantService.GetVariantByIdAsync(id);
                if (variant == null)
                    return NotFound(new { Message = $"Variant with ID {id} not found" });

                return Ok(variant);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Product variant not found: {VariantId}", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving variant {VariantId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the variant" });
            }
        }

        /// <summary>
        /// Get variant by SKU
        /// </summary>
        /// <param name="sku">Variant SKU</param>
        /// <returns>Variant information</returns>
        [HttpGet("by-sku/{sku}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductVariantDto>> GetVariantBySKU(string sku)
        {
            try
            {
                var variant = await _variantService.GetVariantBySKUAsync(sku);
                if (variant == null)
                    return NotFound(new { Message = $"Variant with SKU '{sku}' not found" });

                return Ok(variant);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving variant by SKU {SKU}", sku);
                return StatusCode(500, new { Message = "An error occurred while retrieving the variant" });
            }
        }

        /// <summary>
        /// Get variants by product ID
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <returns>List of variants for the product</returns>
        [HttpGet("product/{productId:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ProductVariantDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ProductVariantDto>>> GetVariantsByProduct(int productId)
        {
            try
            {
                var variants = await _variantService.GetVariantsByProductIdAsync(productId);
                return Ok(variants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving variants for product {ProductId}", productId);
                return StatusCode(500, new { Message = "An error occurred while retrieving variants" });
            }
        }

        /// <summary>
        /// Get default variant for a product
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <returns>Default variant information</returns>
        [HttpGet("product/{productId:int}/default")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductVariantDto>> GetDefaultVariant(int productId)
        {
            try
            {
                var variant = await _variantService.GetDefaultVariantAsync(productId);
                if (variant == null)
                    return NotFound(new { Message = $"No default variant found for product {productId}" });

                return Ok(variant);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default variant for product {ProductId}", productId);
                return StatusCode(500, new { Message = "An error occurred while retrieving the default variant" });
            }
        }

        /// <summary>
        /// Create a new variant for a product
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="createVariantDto">Variant creation data</param>
        /// <returns>Created variant information</returns>
        [HttpPost("product/{productId:int}")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductVariantDto>> CreateVariant(int productId, [FromBody] CreateProductVariantDto createVariantDto)
        {
            try
            {
                _logger.LogInformation("CreateVariant called for product {ProductId} with data: {@CreateVariantDto}", productId, createVariantDto);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.SelectMany(x => x.Value.Errors).Select(x => x.ErrorMessage);
                    _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { Message = "Validation failed", Errors = errors });
                }

                if (createVariantDto == null)
                {
                    _logger.LogWarning("CreateVariantDto is null");
                    return BadRequest(new { Message = "Variant data is required" });
                }

                if (string.IsNullOrWhiteSpace(createVariantDto.Title))
                {
                    return BadRequest(new { Message = "Variant title is required" });
                }

                if (string.IsNullOrWhiteSpace(createVariantDto.SKU))
                {
                    return BadRequest(new { Message = "Variant SKU is required" });
                }

                var variant = await _variantService.CreateVariantAsync(productId, createVariantDto);
                return CreatedAtAction(nameof(GetVariant), new { id = variant.Id }, variant);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Variant creation failed for product {ProductId}: {Message}", productId, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating variant for product {ProductId}", productId);
                return StatusCode(500, new { Message = "An error occurred while creating the variant" });
            }
        }

        /// <summary>
        /// Create a new variant (standalone) - ProductId is optional
        /// </summary>
        /// <param name="createVariantDto">Variant creation data</param>
        /// <returns>Created variant information</returns>
        [HttpPost]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductVariantDto>> CreateVariantStandalone([FromBody] CreateProductVariantDto createVariantDto)
        {
            try
            {
                _logger.LogInformation("CreateVariantStandalone called with data: {@CreateVariantDto}", createVariantDto);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.SelectMany(x => x.Value.Errors).Select(x => x.ErrorMessage);
                    _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { Message = "Validation failed", Errors = errors });
                }

                if (createVariantDto == null)
                {
                    _logger.LogWarning("CreateVariantDto is null");
                    return BadRequest(new { Message = "Variant data is required" });
                }

                if (string.IsNullOrWhiteSpace(createVariantDto.Title))
                {
                    return BadRequest(new { Message = "Variant title is required" });
                }

                if (string.IsNullOrWhiteSpace(createVariantDto.SKU))
                {
                    return BadRequest(new { Message = "Variant SKU is required" });
                }

                // For standalone variants, use 0 or null as productId
                var productId = createVariantDto.ProductId ?? 0;
                var variant = await _variantService.CreateVariantAsync(productId, createVariantDto);
                return CreatedAtAction(nameof(GetVariant), new { id = variant.Id }, variant);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Variant creation failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating variant");
                return StatusCode(500, new { Message = "An error occurred while creating the variant" });
            }
        }

        /// <summary>
        /// Assign standalone variant to product
        /// </summary>
        /// <param name="variantId">Variant ID</param>
        /// <param name="productId">Product ID to assign to</param>
        /// <returns>Updated variant information</returns>
        [HttpPost("{variantId:int}/assign-to-product/{productId:int}")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductVariantDto>> AssignVariantToProduct(int variantId, int productId)
        {
            try
            {
                var variant = await _variantService.GetVariantByIdAsync(variantId);
                if (variant == null)
                    return NotFound(new { Message = $"Variant with ID {variantId} not found" });

                if (variant.ProductId != 0 && variant.ProductId != null)
                    return BadRequest(new { Message = "Variant is already assigned to a product" });

                // Update the variant to assign it to the product
                var updateDto = new UpdateProductVariantDto
                {
                    Id = variant.Id,
                    Title = variant.Title,
                    SKU = variant.SKU,
                    Price = variant.Price,
                    CompareAtPrice = variant.CompareAtPrice,
                    CostPerItem = variant.CostPerItem,
                    Quantity = variant.Quantity,
                    TrackQuantity = variant.TrackQuantity,
                    ContinueSellingWhenOutOfStock = variant.ContinueSellingWhenOutOfStock,
                    RequiresShipping = variant.RequiresShipping,
                    IsTaxable = variant.IsTaxable,
                    Weight = variant.Weight,
                    WeightUnit = variant.WeightUnit,
                    Barcode = variant.Barcode,
                    Position = variant.Position,
                    IsDefault = variant.IsDefault,
                    CustomFields = variant.CustomFields,
                    Option1 = variant.Option1,
                    Option2 = variant.Option2,
                    Option3 = variant.Option3,
                    Images = variant.Images.Select(i => new UpdateProductVariantImageDto
                    {
                        Id = i.Id,
                        FileId = i.FileId,
                        Alt = i.Alt,
                        Caption = i.Caption,
                        Position = i.Position,
                        IsFeatured = i.IsFeatured
                    }).ToList()
                };

                // This will need to be handled by the service differently to change ProductId
                // For now, this is a placeholder - the service would need a specific method for this
                var updatedVariant = await _variantService.UpdateVariantAsync(variantId, updateDto);

                return Ok(updatedVariant);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Variant assignment failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning variant {VariantId} to product {ProductId}", variantId, productId);
                return StatusCode(500, new { Message = "An error occurred while assigning the variant" });
            }
        }

        /// <summary>
        /// Update an existing variant
        /// </summary>
        /// <param name="id">Variant ID</param>
        /// <param name="updateVariantDto">Variant update data</param>
        /// <returns>Updated variant information</returns>
        [HttpPut("{id:int}")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductVariantDto>> UpdateVariant(int id, [FromBody] UpdateProductVariantDto updateVariantDto)
        {
            try
            {
                _logger.LogInformation("UpdateVariant called for variant {VariantId} with data: {@UpdateVariantDto}", id, updateVariantDto);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.SelectMany(x => x.Value.Errors).Select(x => x.ErrorMessage);
                    _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { Message = "Validation failed", Errors = errors });
                }

                if (updateVariantDto == null)
                {
                    _logger.LogWarning("UpdateVariantDto is null");
                    return BadRequest(new { Message = "Variant data is required" });
                }

                updateVariantDto.Id = id; // Ensure consistency
                var variant = await _variantService.UpdateVariantAsync(id, updateVariantDto);
                return Ok(variant);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Variant update failed for {VariantId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating variant {VariantId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the variant" });
            }
        }

        /// <summary>
        /// Delete a variant
        /// </summary>
        /// <param name="id">Variant ID</param>
        /// <returns>Deletion confirmation</returns>
        [HttpDelete("{id:int}")]
        [AdminOrDev]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteVariant(int id)
        {
            try
            {
                var success = await _variantService.DeleteVariantAsync(id);
                if (!success)
                    return NotFound(new { Message = "Variant not found" });

                return Ok(new { Message = "Variant deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting variant {VariantId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the variant" });
            }
        }

        /// <summary>
        /// Set variant as default for its product
        /// </summary>
        /// <param name="id">Variant ID</param>
        /// <returns>Updated variant information</returns>
        [HttpPut("{id:int}/set-default")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductVariantDto>> SetDefaultVariant(int id)
        {
            try
            {
                var variant = await _variantService.SetDefaultVariantAsync(id);
                return Ok(variant);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Set default variant failed for {VariantId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default variant {VariantId}", id);
                return StatusCode(500, new { Message = "An error occurred while setting default variant" });
            }
        }

        /// <summary>
        /// Reorder variants for a product
        /// </summary>
        /// <param name="reorderDto">Reorder data</param>
        /// <returns>Reordered variants</returns>
        [HttpPost("reorder")]
        [AdminOrDev]
        [ProducesResponseType(typeof(List<ProductVariantDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ProductVariantDto>>> ReorderVariants([FromBody] ReorderVariantsDto reorderDto)
        {
            try
            {
                if (reorderDto?.Variants?.Any() != true)
                {
                    return BadRequest(new { Message = "Variant order data is required" });
                }

                var variantOrders = reorderDto.Variants.Select(v => (v.Id, v.Position)).ToList();
                var variants = await _variantService.ReorderVariantsAsync(variantOrders);
                return Ok(variants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering variants");
                return StatusCode(500, new { Message = "An error occurred while reordering variants" });
            }
        }

        /// <summary>
        /// Update variant stock
        /// </summary>
        /// <param name="id">Variant ID</param>
        /// <param name="updateStockDto">Stock update data</param>
        /// <returns>Updated variant information</returns>
        [HttpPost("{id:int}/stock")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductVariantDto>> UpdateStock(int id, [FromBody] UpdateVariantStockDto updateStockDto)
        {
            try
            {
                if (updateStockDto.NewQuantity < 0)
                {
                    return BadRequest(new { Message = "Quantity cannot be negative" });
                }

                var variant = await _variantService.UpdateStockAsync(id, updateStockDto.NewQuantity);
                return Ok(variant);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Stock update failed for variant {VariantId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock for variant {VariantId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating stock" });
            }
        }

        /// <summary>
        /// Get low stock variants
        /// </summary>
        /// <param name="threshold">Stock threshold (default: 5)</param>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of low stock variants</returns>
        [HttpGet("low-stock")]
        [AdminOrDev]
        [ProducesResponseType(typeof(PagedResult<ProductVariantDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductVariantDto>>> GetLowStockVariants(
            [FromQuery] int threshold = 5,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _variantService.GetLowStockVariantsAsync(threshold, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving low stock variants for threshold {Threshold}, page {Page}, pageSize {PageSize}",
                    threshold, page, pageSize);
                return StatusCode(500, new { Message = "An error occurred while retrieving low stock variants" });
            }
        }

        /// <summary>
        /// Get out of stock variants
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of out of stock variants</returns>
        [HttpGet("out-of-stock")]
        [AdminOrDev]
        [ProducesResponseType(typeof(PagedResult<ProductVariantDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductVariantDto>>> GetOutOfStockVariants(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _variantService.GetOutOfStockVariantsAsync(page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving out of stock variants for page {Page}, pageSize {PageSize}", page, pageSize);
                return StatusCode(500, new { Message = "An error occurred while retrieving out of stock variants" });
            }
        }

        /// <summary>
        /// Get total stock for a product
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <returns>Total stock quantity</returns>
        [HttpGet("product/{productId:int}/total-stock")]
        [AdminOrDev]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<int>> GetTotalStock(int productId)
        {
            try
            {
                var totalStock = await _variantService.GetTotalStockAsync(productId);
                return Ok(new { TotalStock = totalStock });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total stock for product {ProductId}", productId);
                return StatusCode(500, new { Message = "An error occurred while retrieving total stock" });
            }
        }

        /// <summary>
        /// Validate variant SKU
        /// </summary>
        /// <param name="sku">SKU to validate</param>
        /// <param name="excludeVariantId">Variant ID to exclude from validation</param>
        /// <returns>Validation result</returns>
        [HttpGet("validate-sku")]
        [AdminOrDev]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<bool>> ValidateSKU(
            [FromQuery] string sku,
            [FromQuery] int? excludeVariantId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sku))
                    return BadRequest(new { Message = "SKU parameter is required" });

                var isValid = await _variantService.ValidateSKUAsync(sku, excludeVariantId);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating variant SKU");
                return StatusCode(500, new { Message = "An error occurred while validating the SKU" });
            }
        }

        /// <summary>
        /// Add image to variant
        /// </summary>
        /// <param name="id">Variant ID</param>
        /// <param name="createImageDto">Image creation data</param>
        /// <returns>Created image information</returns>
        [HttpPost("{id:int}/images")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductVariantImageDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddVariantImage(int id, [FromBody] CreateProductVariantImageDto createImageDto)
        {
            try
            {
                if (createImageDto == null)
                {
                    return BadRequest(new { message = "Image data is required" });
                }

                var image = await _variantService.AddVariantImageAsync(id, createImageDto);
                return CreatedAtAction(nameof(GetVariant), new { id }, image);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding image to variant {VariantId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Update variant image
        /// </summary>
        /// <param name="imageId">Image ID</param>
        /// <param name="updateImageDto">Image update data</param>
        /// <returns>Updated image information</returns>
        [HttpPut("images/{imageId:int}")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductVariantImageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateVariantImage(int imageId, [FromBody] UpdateProductVariantImageDto updateImageDto)
        {
            try
            {
                if (updateImageDto == null)
                {
                    return BadRequest(new { message = "Image data is required" });
                }

                updateImageDto.Id = imageId;
                var image = await _variantService.UpdateVariantImageAsync(imageId, updateImageDto);
                return Ok(image);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating variant image {ImageId}", imageId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Delete variant image
        /// </summary>
        /// <param name="imageId">Image ID</param>
        /// <returns>Deletion confirmation</returns>
        [HttpDelete("images/{imageId:int}")]
        [AdminOrDev]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteVariantImage(int imageId)
        {
            try
            {
                var result = await _variantService.DeleteVariantImageAsync(imageId);
                if (!result)
                    return NotFound(new { message = "Variant image not found" });

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting variant image {ImageId}", imageId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Reorder variant images
        /// </summary>
        /// <param name="id">Variant ID</param>
        /// <param name="imageOrders">Image reorder data</param>
        /// <returns>Reordered images</returns>
        [HttpPost("{id:int}/images/reorder")]
        [AdminOrDev]
        [ProducesResponseType(typeof(List<ProductVariantImageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReorderVariantImages(int id, [FromBody] List<(int ImageId, int Position)> imageOrders)
        {
            try
            {
                if (imageOrders?.Any() != true)
                {
                    return BadRequest(new { message = "Image order data is required" });
                }

                var images = await _variantService.ReorderVariantImagesAsync(id, imageOrders);
                return Ok(images);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering variant images for variant {VariantId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}