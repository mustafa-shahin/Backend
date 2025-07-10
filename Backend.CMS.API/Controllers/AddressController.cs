using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    [EnableRateLimiting("ApiPolicy")]
    public class AddressController : ControllerBase
    {
        private readonly IAddressService _addressService;
        private readonly ILogger<AddressController> _logger;

        public AddressController(
            IAddressService addressService,
            ILogger<AddressController> logger)
        {
            _addressService = addressService ?? throw new ArgumentNullException(nameof(addressService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of addresses with optional filtering
        /// </summary>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <param name="searchTerm">Optional search term for address components</param>
        /// <param name="country">Optional country filter</param>
        /// <param name="state">Optional state filter</param>
        /// <param name="city">Optional city filter</param>
        /// <param name="addressType">Optional address type filter</param>
        /// <param name="isDefault">Optional default status filter</param>
        /// <param name="entityType">Optional entity type filter (user, company, location)</param>
        /// <param name="entityId">Optional entity ID filter</param>
        /// <param name="sortBy">Sort field (default: CreatedAt)</param>
        /// <param name="sortDirection">Sort direction (Asc/Desc, default: Desc)</param>
        /// <param name="createdAfter">Optional filter for addresses created after this date</param>
        /// <param name="createdBefore">Optional filter for addresses created before this date</param>
        /// <returns>Paginated list of addresses</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<AddressDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<AddressDto>>> GetAddresses(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? country = null,
            [FromQuery] string? state = null,
            [FromQuery] string? city = null,
            [FromQuery] string? addressType = null,
            [FromQuery] bool? isDefault = null,
            [FromQuery] string? entityType = null,
            [FromQuery] int? entityId = null,
            [FromQuery] string sortBy = "CreatedAt",
            [FromQuery] string sortDirection = "Desc",
            [FromQuery] DateTime? createdAfter = null,
            [FromQuery] DateTime? createdBefore = null)
        {
            try
            {
                var searchDto = new AddressSearchDto
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SearchTerm = searchTerm,
                    Country = country,
                    State = state,
                    City = city,
                    AddressType = addressType,
                    IsDefault = isDefault,
                    EntityType = entityType,
                    EntityId = entityId,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    CreatedAfter = createdAfter,
                    CreatedBefore = createdBefore
                };

                var result = await _addressService.GetAddressesPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting addresses");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving addresses");
                return StatusCode(500, new { Message = "An error occurred while retrieving addresses" });
            }
        }

        /// <summary>
        /// Get address by ID
        /// </summary>
        /// <param name="id">Address ID</param>
        /// <returns>Address information</returns>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(AddressDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AddressDto>> GetAddress(int id)
        {
            try
            {
                var address = await _addressService.GetAddressByIdAsync(id);
                return Ok(address);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Address not found: {AddressId}", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving address {AddressId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the address" });
            }
        }

        /// <summary>
        /// Get addresses by entity (user, company, location)
        /// </summary>
        /// <param name="entityType">Entity type (user, company, location)</param>
        /// <param name="entityId">Entity ID</param>
        /// <returns>List of addresses for the specified entity</returns>
        [HttpGet("entity/{entityType}/{entityId:int}")]
        [ProducesResponseType(typeof(List<AddressDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<AddressDto>>> GetAddressesByEntity(string entityType, int entityId)
        {
            try
            {
                var addresses = await _addressService.GetAddressesByEntityAsync(entityType, entityId);
                return Ok(addresses);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid entity: {EntityType} {EntityId}", entityType, entityId);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving addresses for entity {EntityType} {EntityId}", entityType, entityId);
                return StatusCode(500, new { Message = "An error occurred while retrieving addresses" });
            }
        }

        /// <summary>
        /// Create a new address for an entity
        /// </summary>
        /// <param name="entityType">Entity type (user, company, location)</param>
        /// <param name="entityId">Entity ID</param>
        /// <param name="createAddressDto">Address creation data</param>
        /// <returns>Created address information</returns>
        [HttpPost("entity/{entityType}/{entityId:int}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(AddressDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AddressDto>> CreateAddress(string entityType, int entityId, [FromBody] CreateAddressDto createAddressDto)
        {
            try
            {
                var address = await _addressService.CreateAddressAsync(createAddressDto, entityType, entityId);
                return CreatedAtAction(nameof(GetAddress), new { id = address.Id }, address);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Address creation failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating address for {EntityType} {EntityId}", entityType, entityId);
                return StatusCode(500, new { Message = "An error occurred while creating the address" });
            }
        }

        /// <summary>
        /// Update an existing address
        /// </summary>
        /// <param name="id">Address ID</param>
        /// <param name="updateAddressDto">Address update data</param>
        /// <returns>Updated address information</returns>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(AddressDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AddressDto>> UpdateAddress(int id, [FromBody] UpdateAddressDto updateAddressDto)
        {
            try
            {
                var address = await _addressService.UpdateAddressAsync(id, updateAddressDto);
                return Ok(address);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Address update failed for {AddressId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating address {AddressId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the address" });
            }
        }

        /// <summary>
        /// Delete an address
        /// </summary>
        /// <param name="id">Address ID</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteAddress(int id)
        {
            try
            {
                var success = await _addressService.DeleteAddressAsync(id);
                if (!success)
                {
                    return NotFound(new { Message = "Address not found" });
                }
                return Ok(new { Message = "Address deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting address {AddressId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the address" });
            }
        }

        /// <summary>
        /// Set address as default for an entity
        /// </summary>
        /// <param name="id">Address ID</param>
        /// <param name="entityType">Entity type (user, company, location)</param>
        /// <param name="entityId">Entity ID</param>
        /// <returns>Operation result</returns>
        [HttpPost("{id:int}/set-default/{entityType}/{entityId:int}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> SetDefaultAddress(int id, string entityType, int entityId)
        {
            try
            {
                var success = await _addressService.SetDefaultAddressAsync(id, entityType, entityId);
                if (!success)
                {
                    return NotFound(new { Message = "Address not found or does not belong to the specified entity" });
                }
                return Ok(new { Message = "Default address set successfully" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default address {AddressId} for {EntityType} {EntityId}", id, entityType, entityId);
                return StatusCode(500, new { Message = "An error occurred while setting the default address" });
            }
        }

        /// <summary>
        /// Get recent addresses (non-paginated for quick access)
        /// </summary>
        /// <param name="count">Number of recent addresses to retrieve (default: 10, max: 50)</param>
        /// <returns>List of recent addresses</returns>
        [HttpGet("recent")]
        [ProducesResponseType(typeof(List<AddressDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<AddressDto>>> GetRecentAddresses([FromQuery] int count = 10)
        {
            try
            {
                var addresses = await _addressService.GetRecentAddressesAsync(Math.Clamp(count, 1, 50));
                return Ok(addresses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent addresses");
                return StatusCode(500, new { Message = "An error occurred while retrieving recent addresses" });
            }
        }

        /// <summary>
        /// Get address statistics
        /// </summary>
        /// <returns>Address statistics</returns>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Dictionary<string, object>>> GetAddressStatistics()
        {
            try
            {
                var stats = await _addressService.GetAddressStatisticsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting address statistics");
                return StatusCode(500, new { Message = "An error occurred while retrieving address statistics" });
            }
        }

        /// <summary>
        /// Bulk update addresses
        /// </summary>
        /// <param name="bulkUpdateDto">Bulk update data</param>
        /// <returns>Update result</returns>
        [HttpPost("bulk-update")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> BulkUpdateAddresses([FromBody] BulkUpdateAddressesDto bulkUpdateDto)
        {
            try
            {
                if (bulkUpdateDto?.AddressIds?.Any() != true || bulkUpdateDto.UpdateDto == null)
                {
                    return BadRequest(new { Message = "Address IDs and update data are required" });
                }

                var success = await _addressService.BulkUpdateAddressesAsync(bulkUpdateDto.AddressIds, bulkUpdateDto.UpdateDto);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = bulkUpdateDto.AddressIds.Count,
                    SuccessCount = success ? bulkUpdateDto.AddressIds.Count : 0,
                    FailureCount = success ? 0 : bulkUpdateDto.AddressIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk updating addresses");
                return StatusCode(500, new { Message = "An error occurred while updating addresses" });
            }
        }

        /// <summary>
        /// Bulk delete addresses
        /// </summary>
        /// <param name="bulkDeleteDto">Bulk delete data</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("bulk-delete")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> BulkDeleteAddresses([FromBody] BulkDeleteAddressesDto bulkDeleteDto)
        {
            try
            {
                if (bulkDeleteDto?.AddressIds?.Any() != true)
                {
                    return BadRequest(new { Message = "Address IDs are required" });
                }

                var success = await _addressService.BulkDeleteAddressesAsync(bulkDeleteDto.AddressIds);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = bulkDeleteDto.AddressIds.Count,
                    SuccessCount = success ? bulkDeleteDto.AddressIds.Count : 0,
                    FailureCount = success ? 0 : bulkDeleteDto.AddressIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting addresses");
                return StatusCode(500, new { Message = "An error occurred while deleting addresses" });
            }
        }
    }
}