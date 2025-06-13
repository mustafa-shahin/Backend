using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _locationService;
        private readonly ILogger<LocationController> _logger;

        public LocationController(ILocationService locationService, ILogger<LocationController> logger)
        {
            _locationService = locationService;
            _logger = logger;
        }

        /// <summary>
        /// Get all locations
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<LocationDto>>> GetLocations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        {
            try
            {
                var allLocations = await _locationService.GetLocationsAsync();

                var totalCount = allLocations.Count;
                var items = allLocations
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new PagedResult<LocationDto>
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
                _logger.LogError(ex, "Error retrieving locations");
                return StatusCode(500, new { Message = "An error occurred while retrieving locations" });
            }
        }

        /// <summary>
        /// Get location by ID
        /// </summary>
        [HttpGet("{id:int}")]
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
        [HttpGet("main")]
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
        /// Get locations by company
        /// </summary>
        [HttpGet("company/{companyId:int}")]
        public async Task<ActionResult<List<LocationDto>>> GetLocationsByCompany(int companyId)
        {
            try
            {
                var locations = await _locationService.GetLocationsByCompanyAsync(companyId);
                return Ok(locations);
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
        [HttpPost]
        [Authorize(Roles = "Admin,Dev")]
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
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Dev")]
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
        /// Delete a location (soft delete)
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,Dev")]
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
        [HttpPost("{id:int}/set-main")]
        [Authorize(Roles = "Admin,Dev")]
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
        /// Search locations
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<List<LocationDto>>> SearchLocations(
            [FromQuery] string searchTerm,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var locations = await _locationService.SearchLocationsAsync(searchTerm, page, pageSize);
                return Ok(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching locations");
                return StatusCode(500, new { Message = "An error occurred while searching locations" });
            }
        }
    }
}