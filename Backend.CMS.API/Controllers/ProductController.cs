using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// Product management controller providing product operations
    /// </summary>
    [ApiController]
    [Route("api/{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]

    [EnableRateLimiting("ApiPolicy")]
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
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of products</returns>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _productService.GetProductsAsync(page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products for page {Page}, pageSize {PageSize}", page, pageSize);
                return StatusCode(500, new { Message = "An error occurred while retrieving products" });
            }
        }

        /// <summary>
        /// Get product by ID
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <returns>Product information</returns>
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="slug">Product slug</param>
        /// <returns>Product information</returns>
        [HttpGet("by-slug/{slug}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="categoryId">Category ID</param>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of products in category</returns>
        [HttpGet("category/{categoryId:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetProductsByCategory(
            int categoryId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _productService.GetProductsByCategoryAsync(categoryId, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products by category {CategoryId} for page {Page}, pageSize {PageSize}",
                    categoryId, page, pageSize);
                return StatusCode(500, new { Message = "An error occurred while retrieving products" });
            }
        }

        /// <summary>
        /// Search products
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>Paginated search results</returns>
        [HttpPost("search")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductDto>>> SearchProducts([FromBody] ProductSearchDto searchDto)
        {
            try
            {
                if (searchDto == null)
                {
                    return BadRequest(new { Message = "Search criteria is required" });
                }

                var result = await _productService.SearchProductsAsync(searchDto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products with criteria: {@SearchDto}", searchDto);
                return StatusCode(500, new { Message = "An error occurred while searching products" });
            }
        }

        /// <summary>
        /// Get featured products
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of featured products</returns>
        [HttpGet("featured")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetFeaturedProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _productService.GetFeaturedProductsAsync(page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving featured products for page {Page}, pageSize {PageSize}", page, pageSize);
                return StatusCode(500, new { Message = "An error occurred while retrieving featured products" });
            }
        }

        /// <summary>
        /// Get recent products
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of recent products</returns>
        [HttpGet("recent")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetRecentProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _productService.GetRecentProductsAsync(page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent products for page {Page}, pageSize {PageSize}", page, pageSize);
                return StatusCode(500, new { Message = "An error occurred while retrieving recent products" });
            }
        }

        /// <summary>
        /// Get related products
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of related products</returns>
        [HttpGet("{id:int}/related")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetRelatedProducts(
            int id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _productService.GetRelatedProductsAsync(id, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving related products for {ProductId}, page {Page}, pageSize {PageSize}",
                    id, page, pageSize);
                return StatusCode(500, new { Message = "An error occurred while retrieving related products" });
            }
        }

        /// <summary>
        /// Create a new product
        /// </summary>
        /// <param name="createProductDto">Product creation data</param>
        /// <returns>Created product information</returns>
        [HttpPost]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductDto createProductDto)
        {
            try
            {
                _logger.LogInformation("CreateProduct called with data: {@CreateProductDto}", createProductDto);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.SelectMany(x => x.Value.Errors).Select(x => x.ErrorMessage);
                    _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { Message = "Validation failed", Errors = errors });
                }

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
        /// <param name="id">Product ID</param>
        /// <param name="updateProductDto">Product update data</param>
        /// <returns>Updated product information</returns>
        [HttpPut("{id:int}")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductDto>> UpdateProduct(int id, [FromBody] UpdateProductDto updateProductDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.SelectMany(x => x.Value.Errors).Select(x => x.ErrorMessage);
                    return BadRequest(new { Message = "Validation failed", Errors = errors });
                }

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
        /// <param name="id">Product ID</param>
        /// <returns>Deletion confirmation</returns>
        [HttpDelete("{id:int}")]
        [AdminOrDev]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="id">Product ID</param>
        /// <returns>Published product information</returns>
        [HttpPost("{id:int}/publish")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="id">Product ID</param>
        /// <returns>Unpublished product information</returns>
        [HttpPost("{id:int}/unpublish")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="id">Product ID</param>
        /// <returns>Archived product information</returns>
        [HttpPost("{id:int}/archive")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="id">Product ID to duplicate</param>
        /// <param name="duplicateDto">Duplication parameters</param>
        /// <returns>Duplicated product information</returns>
        [HttpPost("{id:int}/duplicate")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductDto>> DuplicateProduct(int id, [FromBody] DuplicateProductDto duplicateDto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(duplicateDto?.NewName))
                {
                    return BadRequest(new { Message = "New product name is required" });
                }

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
        /// <param name="id">Product ID</param>
        /// <param name="updateStockDto">Stock update data</param>
        /// <returns>Success confirmation</returns>
        [HttpPost("{id:int}/stock")]
        [AdminOrDev]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> UpdateStock(int id, [FromBody] UpdateStockDto updateStockDto)
        {
            try
            {
                if (updateStockDto.NewQuantity < 0)
                {
                    return BadRequest(new { Message = "Quantity cannot be negative" });
                }

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
        /// <returns>Product statistics</returns>
        [HttpGet("statistics")]
        [AdminOrDev]
        [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <returns>Minimum and maximum prices</returns>
        [HttpGet("price-range")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <returns>List of vendors</returns>
        [HttpGet("vendors")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <returns>List of tags</returns>
        [HttpGet("tags")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="threshold">Stock threshold (default: 5)</param>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of low stock products</returns>
        [HttpGet("low-stock")]
        [AdminOrDev]
        [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetLowStockProducts(
            [FromQuery] int threshold = 5,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _productService.GetLowStockProductsAsync(threshold, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving low stock products for threshold {Threshold}, page {Page}, pageSize {PageSize}",
                    threshold, page, pageSize);
                return StatusCode(500, new { Message = "An error occurred while retrieving low stock products" });
            }
        }

        /// <summary>
        /// Get out of stock products
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of out of stock products</returns>
        [HttpGet("out-of-stock")]
        [AdminOrDev]
        [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetOutOfStockProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _productService.GetOutOfStockProductsAsync(page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving out of stock products for page {Page}, pageSize {PageSize}", page, pageSize);
                return StatusCode(500, new { Message = "An error occurred while retrieving out of stock products" });
            }
        }

        /// <summary>
        /// Validate product slug
        /// </summary>
        /// <param name="slug">Slug to validate</param>
        /// <param name="excludeProductId">Product ID to exclude from validation</param>
        /// <returns>Validation result</returns>
        [HttpGet("validate-slug")]
        [AdminOrDev]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<bool>> ValidateSlug(
            [FromQuery] string slug,
            [FromQuery] int? excludeProductId = null)
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
        /// <param name="sku">SKU to validate</param>
        /// <param name="excludeProductId">Product ID to exclude from validation</param>
        /// <returns>Validation result</returns>
        [HttpGet("validate-sku")]
        [AdminOrDev]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<bool>> ValidateSKU(
            [FromQuery] string sku,
            [FromQuery] int? excludeProductId = null)
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
        /// <param name="id">Product ID</param>
        /// <returns>List of product variants</returns>
        [HttpGet("{id:int}/variants")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ProductVariantDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        /// <summary>
        /// Create product variant
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="createVariantDto">Variant creation data</param>
        /// <returns>Created variant information</returns>
        [HttpPost("{productId:int}/variant")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateProductVariant(int productId, [FromBody] CreateProductVariantDto createVariantDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.SelectMany(x => x.Value.Errors).Select(x => x.ErrorMessage);
                    return BadRequest(new { message = "Validation failed", errors });
                }

                var variant = await _variantService.CreateVariantAsync(productId, createVariantDto);
                return CreatedAtAction(
                    actionName: "GetVariant",
                    controllerName: "ProductVariant",
                    routeValues: new { id = variant.Id },
                    value: variant);
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

        /// <summary>
        /// Set default variant for product
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="variantId">Variant ID to set as default</param>
        /// <returns>Updated variant information</returns>
        [HttpPut("{productId:int}/variant/{variantId:int}/set-default")]
        [AdminOrDev]
        [ProducesResponseType(typeof(ProductVariantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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