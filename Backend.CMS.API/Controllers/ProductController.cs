using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IProductVariantService _variantService;
        private readonly ILogger<ProductController> _logger;

        public ProductController(
            IProductService productService,
            IProductVariantService variantService,
            ILogger<ProductController> logger)
        {
            _productService = productService;
            _variantService = variantService;
            _logger = logger;
  
        }

        /// <summary>
        /// Get all products with pagination
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var allProducts = await _productService.GetProductsAsync();
                var totalCount = allProducts.Count;
                var items = allProducts
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new PagedResult<ProductDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products");
                return StatusCode(500, new { Message = "An error occurred while retrieving products" });
            }
        }

        /// <summary>
        /// Get product by ID
        /// </summary>
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<ActionResult<ProductDto>> GetProduct(int id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                    return NotFound(new { Message = $"Product with ID {id} not found" });

                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Product not found: {ProductId}", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product {ProductId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the product" });
            }
        }

        /// <summary>
        /// Get product by slug
        /// </summary>
        [HttpGet("by-slug/{slug}")]
        [AllowAnonymous]
        public async Task<ActionResult<ProductDto>> GetProductBySlug(string slug)
        {
            try
            {
                var product = await _productService.GetProductBySlugAsync(slug);
                if (product == null)
                    return NotFound(new { Message = $"Product with slug '{slug}' not found" });

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product by slug {Slug}", slug);
                return StatusCode(500, new { Message = "An error occurred while retrieving the product" });
            }
        }

        /// <summary>
        /// Get products by category
        /// </summary>
        [HttpGet("category/{categoryId:int}")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ProductDto>>> GetProductsByCategory(
            int categoryId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var products = await _productService.GetProductsByCategoryAsync(categoryId, page, pageSize);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products by category {CategoryId}", categoryId);
                return StatusCode(500, new { Message = "An error occurred while retrieving products" });
            }
        }

        /// <summary>
        /// Search products
        /// </summary>
        [HttpPost("search")]
        [AllowAnonymous]
        public async Task<ActionResult> SearchProducts([FromBody] ProductSearchDto searchDto)
        {
            try
            {
                var products = await _productService.SearchProductsAsync(searchDto);
                var totalCount = await _productService.GetSearchCountAsync(searchDto);

                return Ok(new
                {
                    Products = products,
                    TotalCount = totalCount,
                    Page = searchDto.Page,
                    PageSize = searchDto.PageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / searchDto.PageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products");
                return StatusCode(500, new { Message = "An error occurred while searching products" });
            }
        }

        /// <summary>
        /// Get featured products
        /// </summary>
        [HttpGet("featured")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ProductDto>>> GetFeaturedProducts([FromQuery] int count = 10)
        {
            try
            {
                var products = await _productService.GetFeaturedProductsAsync(count);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving featured products");
                return StatusCode(500, new { Message = "An error occurred while retrieving featured products" });
            }
        }

        /// <summary>
        /// Get recent products
        /// </summary>
        [HttpGet("recent")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ProductDto>>> GetRecentProducts([FromQuery] int count = 10)
        {
            try
            {
                var products = await _productService.GetRecentProductsAsync(count);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent products");
                return StatusCode(500, new { Message = "An error occurred while retrieving recent products" });
            }
        }

        /// <summary>
        /// Get related products
        /// </summary>
        [HttpGet("{id:int}/related")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ProductDto>>> GetRelatedProducts(int id, [FromQuery] int count = 4)
        {
            try
            {
                var products = await _productService.GetRelatedProductsAsync(id, count);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving related products for {ProductId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving related products" });
            }
        }

        /// <summary>
        /// Create a new product
        /// </summary>
        [HttpPost]
        [AdminOrDev]
        public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductDto createProductDto)
        {
            try
            {
                // Log the incoming request for debugging
                _logger.LogInformation("CreateProduct called with data: {@CreateProductDto}", createProductDto);

                // Validate the model state
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.SelectMany(x => x.Value.Errors).Select(x => x.ErrorMessage);
                    _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { Message = "Validation failed", Errors = errors });
                }

                // Additional validation
                if (createProductDto == null)
                {
                    _logger.LogWarning("CreateProductDto is null");
                    return BadRequest(new { Message = "Product data is required" });
                }

                if (string.IsNullOrWhiteSpace(createProductDto.Name))
                {
                    return BadRequest(new { Message = "Product name is required" });
                }

                if (string.IsNullOrWhiteSpace(createProductDto.SKU))
                {
                    return BadRequest(new { Message = "Product SKU is required" });
                }

                if (string.IsNullOrWhiteSpace(createProductDto.Slug))
                {
                    return BadRequest(new { Message = "Product slug is required" });
                }

                var product = await _productService.CreateProductAsync(createProductDto);
                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Product creation failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                return StatusCode(500, new { Message = "An error occurred while creating the product" });
            }
        }

        /// <summary>
        /// Update an existing product
        /// </summary>
        [HttpPut("{id:int}")]
        [AdminOrDev]
        public async Task<ActionResult<ProductDto>> UpdateProduct(int id, [FromBody] UpdateProductDto updateProductDto)
        {
            try
            {
                var product = await _productService.UpdateProductAsync(id, updateProductDto);
                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Product update failed for {ProductId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {ProductId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the product" });
            }
        }

        /// <summary>
        /// Delete a product
        /// </summary>
        [HttpDelete("{id:int}")]
        [AdminOrDev]
        public async Task<ActionResult> DeleteProduct(int id)
        {
            try
            {
                var success = await _productService.DeleteProductAsync(id);
                if (!success)
                    return NotFound(new { Message = "Product not found" });

                return Ok(new { Message = "Product deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {ProductId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the product" });
            }
        }

        /// <summary>
        /// Publish a product
        /// </summary>
        [HttpPost("{id:int}/publish")]
        [AdminOrDev]
        public async Task<ActionResult<ProductDto>> PublishProduct(int id)
        {
            try
            {
                var product = await _productService.PublishProductAsync(id);
                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Product publish failed for {ProductId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing product {ProductId}", id);
                return StatusCode(500, new { Message = "An error occurred while publishing the product" });
            }
        }

        /// <summary>
        /// Unpublish a product
        /// </summary>
        [HttpPost("{id:int}/unpublish")]
        [AdminOrDev]
        public async Task<ActionResult<ProductDto>> UnpublishProduct(int id)
        {
            try
            {
                var product = await _productService.UnpublishProductAsync(id);
                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Product unpublish failed for {ProductId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpublishing product {ProductId}", id);
                return StatusCode(500, new { Message = "An error occurred while unpublishing the product" });
            }
        }

        /// <summary>
        /// Archive a product
        /// </summary>
        [HttpPost("{id:int}/archive")]
        [AdminOrDev]
        public async Task<ActionResult<ProductDto>> ArchiveProduct(int id)
        {
            try
            {
                var product = await _productService.ArchiveProductAsync(id);
                return Ok(product);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Product archive failed for {ProductId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving product {ProductId}", id);
                return StatusCode(500, new { Message = "An error occurred while archiving the product" });
            }
        }

        /// <summary>
        /// Duplicate a product
        /// </summary>
        [HttpPost("{id:int}/duplicate")]
        [AdminOrDev]
        public async Task<ActionResult<ProductDto>> DuplicateProduct(int id, [FromBody] DuplicateProductDto duplicateDto)
        {
            try
            {
                var product = await _productService.DuplicateProductAsync(id, duplicateDto.NewName);
                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Product duplication failed for {ProductId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error duplicating product {ProductId}", id);
                return StatusCode(500, new { Message = "An error occurred while duplicating the product" });
            }
        }

        /// <summary>
        /// Update product stock
        /// </summary>
        [HttpPost("{id:int}/stock")]
        [AdminOrDev]
        public async Task<ActionResult> UpdateStock(int id, [FromBody] UpdateStockDto updateStockDto)
        {
            try
            {
                await _productService.UpdateStockAsync(id, updateStockDto.VariantId, updateStockDto.NewQuantity);
                return Ok(new { Message = "Stock updated successfully" });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Stock update failed for product {ProductId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock for product {ProductId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating stock" });
            }
        }

        /// <summary>
        /// Get product statistics
        /// </summary>
        [HttpGet("statistics")]
        [AdminOrDev]
        public async Task<ActionResult<Dictionary<string, object>>> GetStatistics()
        {
            try
            {
                var statistics = await _productService.GetProductStatisticsAsync();
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product statistics");
                return StatusCode(500, new { Message = "An error occurred while retrieving statistics" });
            }
        }

        /// <summary>
        /// Get price range
        /// </summary>
        [HttpGet("price-range")]
        [AllowAnonymous]
        public async Task<ActionResult> GetPriceRange()
        {
            try
            {
                var (min, max) = await _productService.GetPriceRangeAsync();
                return Ok(new { MinPrice = min, MaxPrice = max });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving price range");
                return StatusCode(500, new { Message = "An error occurred while retrieving price range" });
            }
        }

        /// <summary>
        /// Get all vendors
        /// </summary>
        [HttpGet("vendors")]
        [AllowAnonymous]
        public async Task<ActionResult<List<string>>> GetVendors()
        {
            try
            {
                var vendors = await _productService.GetVendorsAsync();
                return Ok(vendors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving vendors");
                return StatusCode(500, new { Message = "An error occurred while retrieving vendors" });
            }
        }

        /// <summary>
        /// Get all product tags
        /// </summary>
        [HttpGet("tags")]
        [AllowAnonymous]
        public async Task<ActionResult<List<string>>> GetTags()
        {
            try
            {
                var tags = await _productService.GetTagsAsync();
                return Ok(tags);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tags");
                return StatusCode(500, new { Message = "An error occurred while retrieving tags" });
            }
        }

        /// <summary>
        /// Get low stock products
        /// </summary>
        [HttpGet("low-stock")]
        [AdminOrDev]
        public async Task<ActionResult<List<ProductDto>>> GetLowStockProducts([FromQuery] int threshold = 5)
        {
            try
            {
                var products = await _productService.GetLowStockProductsAsync(threshold);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving low stock products");
                return StatusCode(500, new { Message = "An error occurred while retrieving low stock products" });
            }
        }

        /// <summary>
        /// Get out of stock products
        /// </summary>
        [HttpGet("out-of-stock")]
        [AdminOrDev]
        public async Task<ActionResult<List<ProductDto>>> GetOutOfStockProducts()
        {
            try
            {
                var products = await _productService.GetOutOfStockProductsAsync();
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving out of stock products");
                return StatusCode(500, new { Message = "An error occurred while retrieving out of stock products" });
            }
        }

        /// <summary>
        /// Validate product slug
        /// </summary>
        [HttpGet("validate-slug")]
        [AdminOrDev]
        public async Task<ActionResult<bool>> ValidateSlug([FromQuery] string slug, [FromQuery] int? excludeProductId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                    return BadRequest(new { Message = "Slug parameter is required" });

                var isValid = await _productService.ValidateSlugAsync(slug, excludeProductId);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating product slug");
                return StatusCode(500, new { Message = "An error occurred while validating the slug" });
            }
        }

        /// <summary>
        /// Validate product SKU
        /// </summary>
        [HttpGet("validate-sku")]
        [AdminOrDev]
        public async Task<ActionResult<bool>> ValidateSKU([FromQuery] string sku, [FromQuery] int? excludeProductId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sku))
                    return BadRequest(new { Message = "SKU parameter is required" });

                var isValid = await _productService.ValidateSKUAsync(sku, excludeProductId);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating product SKU");
                return StatusCode(500, new { Message = "An error occurred while validating the SKU" });
            }
        }

        /// <summary>
        /// Get product variants
        /// </summary>
        [HttpGet("{id:int}/variants")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ProductVariantDto>>> GetProductVariants(int id)
        {
            try
            {
                var variants = await _variantService.GetVariantsByProductIdAsync(id);
                return Ok(variants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving variants for product {ProductId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving product variants" });
            }
        }
        [HttpPost("{productId}/variant")]
        public async Task<IActionResult> CreateProductVariant(int productId, [FromBody] CreateProductVariantDto createVariantDto)
        {
            try
            {
                var variant = await _variantService.CreateVariantAsync(productId, createVariantDto);
                return Ok(variant);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product variant for product {ProductId}", productId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPut("{productId}/variant/{variantId}/set-default")]
        public async Task<IActionResult> SetDefaultVariant(int productId, int variantId)
        {
            try
            {
                var variant = await _variantService.SetDefaultVariantAsync(variantId);
                return Ok(variant);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default variant {VariantId} for product {ProductId}", variantId, productId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}