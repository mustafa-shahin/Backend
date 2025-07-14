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
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _locationService;
        private readonly ILogger<LocationController> _logger;

        public LocationController(
            ILocationService locationService,
            ILogger<LocationController> logger)
        {
            _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of locations with optional filtering
        /// </summary>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <param name="searchTerm">Optional search term for location name, code, or description</param>
        /// <param name="locationType">Optional location type filter</param>
        /// <param name="isActive">Optional active status filter</param>
        /// <param name="isMainLocation">Optional main location filter</param>
        /// <param name="companyId">Optional company ID filter</param>
        /// <param name="sortBy">Sort field (default: Name)</param>
        /// <param name="sortDirection">Sort direction (Asc/Desc, default: Asc)</param>
        /// <param name="createdAfter">Optional filter for locations created after this date</param>
        /// <param name="createdBefore">Optional filter for locations created before this date</param>
        /// <returns>Paginated list of locations</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<LocationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<LocationDto>>> GetLocations(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? locationType = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isMainLocation = null,
            [FromQuery] int? companyId = null,
            [FromQuery] string sortBy = "Name",
            [FromQuery] string sortDirection = "Asc",
            [FromQuery] DateTime? createdAfter = null,
            [FromQuery] DateTime? createdBefore = null)
        {
            try
            {
                var searchDto = new LocationSearchDto
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SearchTerm = searchTerm,
                    LocationType = locationType,
                    IsActive = isActive,
                    IsMainLocation = isMainLocation,
                    CompanyId = companyId,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    CreatedAfter = createdAfter,
                    CreatedBefore = createdBefore
                };

                var result = await _locationService.GetLocationsPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting locations");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving locations");
                return StatusCode(500, new { Message = "An error occurred while retrieving locations" });
            }
        }

        /// <summary>
        /// Advanced location search with filtering and pagination
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>Paginated search results</returns>
        [HttpPost("search")]
        [ProducesResponseType(typeof(PaginatedResult<LocationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<LocationDto>>> SearchLocations([FromBody] LocationSearchDto searchDto)
        {
            try
            {
                if (searchDto == null)
                {
                    return BadRequest(new { Message = "Search criteria is required" });
                }

                var result = await _locationService.SearchLocationsPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid search criteria for locations");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching locations");
                return StatusCode(500, new { Message = "An error occurred while searching locations" });
            }
        }

        /// <summary>
        /// Get location by ID
        /// </summary>
        /// <param name="id">Location ID</param>
        /// <returns>Location information</returns>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<LocationDto>> GetLocation(int id)
        {
            try
            {
                var location = await _locationService.GetLocationByIdAsync(id);
                return Ok(location);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Location not found: {LocationId}", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving location {LocationId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the location" });
            }
        }

        /// <summary>
        /// Get main location
        /// </summary>
        /// <returns>Main location information</returns>
        [HttpGet("main")]
        [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<LocationDto>> GetMainLocation()
        {
            try
            {
                var location = await _locationService.GetMainLocationAsync();
                return Ok(location);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Main location not found: {Message}", ex.Message);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving main location");
                return StatusCode(500, new { Message = "An error occurred while retrieving the main location" });
            }
        }

        /// <summary>
        /// Get locations by company with pagination
        /// </summary>
        /// <param name="companyId">Company ID</param>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <param name="searchTerm">Optional search term for location name, code, or description</param>
        /// <param name="sortBy">Sort field (default: Name)</param>
        /// <param name="sortDirection">Sort direction (Asc/Desc, default: Asc)</param>
        /// <returns>Paginated list of locations for the specified company</returns>
        [HttpGet("company/{companyId:int}")]
        [ProducesResponseType(typeof(PaginatedResult<LocationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<LocationDto>>> GetLocationsByCompany(
            int companyId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string sortBy = "Name",
            [FromQuery] string sortDirection = "Asc")
        {
            try
            {
                var searchDto = new LocationSearchDto
                {
                    CompanyId = companyId,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SearchTerm = searchTerm,
                    SortBy = sortBy,
                    SortDirection = sortDirection
                };

                var locations = await _locationService.GetLocationsPagedAsync(searchDto);
                return Ok(locations);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid company ID: {CompanyId}", companyId);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving locations for company {CompanyId}", companyId);
                return StatusCode(500, new { Message = "An error occurred while retrieving locations" });
            }
        }

        /// <summary>
        /// Create a new location
        /// </summary>
        /// <param name="createLocationDto">Location creation data</param>
        /// <returns>Created location information</returns>
        [HttpPost]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(LocationDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<LocationDto>> CreateLocation([FromBody] CreateLocationDto createLocationDto)
        {
            try
            {
                var location = await _locationService.CreateLocationAsync(createLocationDto);
                return CreatedAtAction(nameof(GetLocation), new { id = location.Id }, location);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Location creation failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating location");
                return StatusCode(500, new { Message = "An error occurred while creating the location" });
            }
        }

        /// <summary>
        /// Update an existing location
        /// </summary>
        /// <param name="id">Location ID</param>
        /// <param name="updateLocationDto">Location update data</param>
        /// <returns>Updated location information</returns>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<LocationDto>> UpdateLocation(int id, [FromBody] UpdateLocationDto updateLocationDto)
        {
            try
            {
                var location = await _locationService.UpdateLocationAsync(id, updateLocationDto);
                return Ok(location);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Location update failed for {LocationId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location {LocationId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the location" });
            }
        }

        /// <summary>
        /// Delete a location
        /// </summary>
        /// <param name="id">Location ID</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteLocation(int id)
        {
            try
            {
                var success = await _locationService.DeleteLocationAsync(id);
                if (!success)
                {
                    return NotFound(new { Message = "Location not found" });
                }
                return Ok(new { Message = "Location deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting location {LocationId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the location" });
            }
        }

        /// <summary>
        /// Set location as main location
        /// </summary>
        /// <param name="id">Location ID</param>
        /// <returns>Operation result</returns>
        [HttpPost("{id:int}/set-main")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> SetMainLocation(int id)
        {
            try
            {
                var success = await _locationService.SetMainLocationAsync(id);
                if (!success)
                {
                    return NotFound(new { Message = "Location not found" });
                }
                return Ok(new { Message = "Main location set successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting main location {LocationId}", id);
                return StatusCode(500, new { Message = "An error occurred while setting the main location" });
            }
        }

        /// <summary>
        /// Get recent locations with pagination
        /// </summary>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-50, default: 10)</param>
        /// <param name="sortBy">Sort field (default: CreatedAt)</param>
        /// <param name="sortDirection">Sort direction (Asc/Desc, default: Desc)</param>
        /// <returns>Paginated list of recent locations</returns>
        [HttpGet("recent")]
        [ProducesResponseType(typeof(PaginatedResult<LocationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<LocationDto>>> GetRecentLocations(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string sortBy = "CreatedAt",
            [FromQuery] string sortDirection = "Desc")
        {
            try
            {
                var searchDto = new LocationSearchDto
                {
                    PageNumber = pageNumber,
                    PageSize = Math.Clamp(pageSize, 1, 50),
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    CreatedAfter = DateTime.UtcNow.AddMonths(-6)
                };

                var locations = await _locationService.GetLocationsPagedAsync(searchDto);
                return Ok(locations);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting recent locations");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent locations");
                return StatusCode(500, new { Message = "An error occurred while retrieving recent locations" });
            }
        }

        /// <summary>
        /// Get location statistics
        /// </summary>
        /// <returns>Location statistics</returns>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Dictionary<string, object>>> GetLocationStatistics()
        {
            try
            {
                var stats = await _locationService.GetLocationStatisticsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location statistics");
                return StatusCode(500, new { Message = "An error occurred while retrieving location statistics" });
            }
        }

        /// <summary>
        /// Bulk update locations
        /// </summary>
        /// <param name="bulkUpdateDto">Bulk update data</param>
        /// <returns>Update result</returns>
        [HttpPost("bulk-update")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> BulkUpdateLocations([FromBody] BulkUpdateLocationsDto bulkUpdateDto)
        {
            try
            {
                if (bulkUpdateDto?.LocationIds?.Any() != true || bulkUpdateDto.UpdateDto == null)
                {
                    return BadRequest(new { Message = "Location IDs and update data are required" });
                }

                var success = await _locationService.BulkUpdateLocationsAsync(bulkUpdateDto.LocationIds, bulkUpdateDto.UpdateDto);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = bulkUpdateDto.LocationIds.Count,
                    SuccessCount = success ? bulkUpdateDto.LocationIds.Count : 0,
                    FailureCount = success ? 0 : bulkUpdateDto.LocationIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk updating locations");
                return StatusCode(500, new { Message = "An error occurred while updating locations" });
            }
        }

        /// <summary>
        /// Bulk delete locations
        /// </summary>
        /// <param name="bulkDeleteDto">Bulk delete data</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("bulk-delete")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> BulkDeleteLocations([FromBody] BulkDeleteLocationsDto bulkDeleteDto)
        {
            try
            {
                if (bulkDeleteDto?.LocationIds?.Any() != true)
                {
                    return BadRequest(new { Message = "Location IDs are required" });
                }

                var success = await _locationService.BulkDeleteLocationsAsync(bulkDeleteDto.LocationIds);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = bulkDeleteDto.LocationIds.Count,
                    SuccessCount = success ? bulkDeleteDto.LocationIds.Count : 0,
                    FailureCount = success ? 0 : bulkDeleteDto.LocationIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting locations");
                return StatusCode(500, new { Message = "An error occurred while deleting locations" });
            }
        }

        /// <summary>
        /// Check if location code exists
        /// </summary>
        /// <param name="locationCode">Location code to check</param>
        /// <param name="excludeLocationId">Optional location ID to exclude from check</param>
        /// <returns>Existence check result</returns>
        [HttpGet("check-code")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> CheckLocationCode([FromQuery] string locationCode, [FromQuery] int? excludeLocationId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(locationCode))
                {
                    return BadRequest(new { Message = "Location code is required" });
                }

                var exists = await _locationService.LocationCodeExistsAsync(locationCode, excludeLocationId);
                return Ok(new { exists, locationCode, excludeLocationId });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking location code existence");
                return StatusCode(500, new { Message = "An error occurred while checking location code" });
            }
        }
    }
}