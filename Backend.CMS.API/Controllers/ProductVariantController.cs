using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<ProductVariantDto>>> GetVariants(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _variantService.GetVariantsPagedAsync(page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving variants");
                return StatusCode(500, new { Message = "An error occurred while retrieving variants" });
            }
        }

        /// <summary>
        /// Get variant by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [AllowAnonymous]
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
        [HttpGet("by-sku/{sku}")]
        [AllowAnonymous]
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
        [HttpGet("product/{productId:int}")]
        [AllowAnonymous]
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
        [HttpGet("product/{productId:int}/default")]
        [AllowAnonymous]
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
        [HttpPost("product/{productId:int}")]
        [AdminOrDev]
        public async Task<ActionResult<ProductVariantDto>> CreateVariant(int productId, [FromBody] CreateProductVariantDto createVariantDto)
        {
            try
            {
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
        /// Create a new variant (standalone)
        /// </summary>
        [HttpPost]
        [AdminOrDev]
        public async Task<ActionResult<ProductVariantDto>> CreateVariantStandalone([FromBody] CreateProductVariantDto createVariantDto)
        {
            try
            {
                // Extract productId from the DTO or require it
                if (!createVariantDto.ProductId.HasValue)
                    return BadRequest(new { Message = "ProductId is required" });

                var variant = await _variantService.CreateVariantAsync(createVariantDto.ProductId.Value, createVariantDto);
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
        /// Update an existing variant
        /// </summary>
        [HttpPut("{id:int}")]
        [AdminOrDev]
        public async Task<ActionResult<ProductVariantDto>> UpdateVariant(int id, [FromBody] UpdateProductVariantDto updateVariantDto)
        {
            try
            {
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
        [HttpDelete("{id:int}")]
        [AdminOrDev]
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
        [HttpPost("{id:int}/set-default")]
        [AdminOrDev]
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
        [HttpPost("reorder")]
        [AdminOrDev]
        public async Task<ActionResult<List<ProductVariantDto>>> ReorderVariants([FromBody] ReorderVariantsDto reorderDto)
        {
            try
            {
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
        [HttpPost("{id:int}/stock")]
        [AdminOrDev]
        public async Task<ActionResult<ProductVariantDto>> UpdateStock(int id, [FromBody] UpdateVariantStockDto updateStockDto)
        {
            try
            {
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
        [HttpGet("low-stock")]
        [AdminOrDev]
        public async Task<ActionResult<List<ProductVariantDto>>> GetLowStockVariants([FromQuery] int threshold = 5)
        {
            try
            {
                var variants = await _variantService.GetLowStockVariantsAsync(threshold);
                return Ok(variants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving low stock variants");
                return StatusCode(500, new { Message = "An error occurred while retrieving low stock variants" });
            }
        }

        /// <summary>
        /// Get out of stock variants
        /// </summary>
        [HttpGet("out-of-stock")]
        [AdminOrDev]
        public async Task<ActionResult<List<ProductVariantDto>>> GetOutOfStockVariants()
        {
            try
            {
                var variants = await _variantService.GetOutOfStockVariantsAsync();
                return Ok(variants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving out of stock variants");
                return StatusCode(500, new { Message = "An error occurred while retrieving out of stock variants" });
            }
        }

        /// <summary>
        /// Get total stock for a product
        /// </summary>
        [HttpGet("product/{productId:int}/total-stock")]
        [AdminOrDev]
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
        [HttpGet("validate-sku")]
        [AdminOrDev]
        public async Task<ActionResult<bool>> ValidateSKU([FromQuery] string sku, [FromQuery] int? excludeVariantId = null)
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
    }
}