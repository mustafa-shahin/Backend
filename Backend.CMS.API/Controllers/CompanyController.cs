using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
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
        /// Get all company locations
        /// </summary>
        [HttpGet("locations")]
        public async Task<ActionResult<List<LocationDto>>> GetLocations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        {
            try
            {
                var locations = await _locationService.GetLocationsAsync(page, pageSize);
                var totalCount = locations.Count;
                var result = new PagedResult<LocationDto>
                {
                    Items = locations,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving company locations");
                return StatusCode(500, new { Message = "An error occurred while retrieving company locations" });
            }
        }

        /// <summary>
        /// Get location by ID
        /// </summary>
        [HttpGet("locations/{id:int}")]
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
    }
}