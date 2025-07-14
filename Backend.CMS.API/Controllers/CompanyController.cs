using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// Company management controller providing company and location operations with API versioning and pagination
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    [EnableRateLimiting("ApiPolicy")]
    public class CompanyController : ControllerBase
    {
        private readonly ICompanyService _companyService;
        private readonly ILocationService _locationService;
        private readonly ILogger<CompanyController> _logger;

        public CompanyController(
            ICompanyService companyService,
            ILocationService locationService,
            ILogger<CompanyController> logger)
        {
            _companyService = companyService;
            _locationService = locationService;
            _logger = logger;
        }

        /// <summary>
        /// Get company information
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CompanyDto>> GetCompany()
        {
            try
            {
                var company = await _companyService.GetCompanyAsync();
                return Ok(company);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Company not found: {Message}", ex.Message);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving company information");
                return StatusCode(500, new { Message = "An error occurred while retrieving company information" });
            }
        }

        /// <summary>
        /// Update company information
        /// </summary>
        [HttpPut]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CompanyDto>> UpdateCompany([FromBody] UpdateCompanyDto updateCompanyDto)
        {
            try
            {
                var company = await _companyService.UpdateCompanyAsync(updateCompanyDto);
                return Ok(company);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Company update failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating company information");
                return StatusCode(500, new { Message = "An error occurred while updating company information" });
            }
        }

        /// <summary>
        /// Get paginated list of company locations with optional filtering
        /// </summary>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <param name="searchTerm">Optional search term for location name or code</param>
        /// <param name="locationType">Optional location type filter</param>
        /// <param name="isActive">Optional active status filter</param>
        /// <param name="sortBy">Sort field (default: Name)</param>
        /// <param name="sortDirection">Sort direction (Asc/Desc, default: Asc)</param>
        /// <returns>Paginated list of locations</returns>
        [HttpGet("locations")]
        [ProducesResponseType(typeof(PaginatedResult<LocationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PaginatedResult<LocationDto>>> GetLocations(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? locationType = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] string sortBy = "Name",
            [FromQuery] string sortDirection = "Asc")
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
                    SortBy = sortBy,
                    SortDirection = sortDirection
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
                _logger.LogError(ex, "Error retrieving company locations");
                return StatusCode(500, new { Message = "An error occurred while retrieving company locations" });
            }
        }

        /// <summary>
        /// Advanced location search with filtering and pagination
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>Paginated search results</returns>
        [HttpPost("locations/search")]
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
        [HttpGet("locations/{id:int}")]
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
        /// Create a new company location
        /// </summary>
        [HttpPost("locations")]
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
        [HttpPut("locations/{id:int}")]
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
        [HttpDelete("locations/{id:int}")]
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
        [HttpPost("locations/{id:int}/set-main")]
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
        /// Get recent locations (non-paginated for quick access)
        /// </summary>
        /// <param name="count">Number of recent locations to retrieve (default: 10, max: 50)</param>
        /// <returns>List of recent locations</returns>
        [HttpGet("locations/recent")]
        [ProducesResponseType(typeof(List<LocationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<LocationDto>>> GetRecentLocations([FromQuery] int count = 10)
        {
            try
            {
                var locations = await _locationService.GetRecentLocationsAsync(Math.Clamp(count, 1, 50));
                return Ok(locations);
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
        [HttpGet("locations/statistics")]
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
        [HttpPost("locations/bulk-update")]
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
        [HttpDelete("locations/bulk-delete")]
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
    }
}