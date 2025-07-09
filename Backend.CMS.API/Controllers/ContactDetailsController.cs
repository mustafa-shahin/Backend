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
    public class ContactDetailsController : ControllerBase
    {
        private readonly IContactDetailsService _contactDetailsService;
        private readonly ILogger<ContactDetailsController> _logger;

        public ContactDetailsController(
            IContactDetailsService contactDetailsService,
            ILogger<ContactDetailsController> logger)
        {
            _contactDetailsService = contactDetailsService ?? throw new ArgumentNullException(nameof(contactDetailsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of contact details with optional filtering
        /// </summary>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <param name="searchTerm">Optional search term for contact information</param>
        /// <param name="contactType">Optional contact type filter</param>
        /// <param name="isDefault">Optional default status filter</param>
        /// <param name="entityType">Optional entity type filter (user, company, location)</param>
        /// <param name="entityId">Optional entity ID filter</param>
        /// <param name="sortBy">Sort field (default: CreatedAt)</param>
        /// <param name="sortDirection">Sort direction (Asc/Desc, default: Desc)</param>
        /// <param name="createdAfter">Optional filter for contact details created after this date</param>
        /// <param name="createdBefore">Optional filter for contact details created before this date</param>
        /// <returns>Paginated list of contact details</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<ContactDetailsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<ContactDetailsDto>>> GetContactDetails(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? contactType = null,
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
                var searchDto = new ContactDetailsSearchDto
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SearchTerm = searchTerm,
                    ContactType = contactType,
                    IsDefault = isDefault,
                    EntityType = entityType,
                    EntityId = entityId,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    CreatedAfter = createdAfter,
                    CreatedBefore = createdBefore
                };

                var result = await _contactDetailsService.GetContactDetailsPagedAsync(searchDto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting contact details");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact details");
                return StatusCode(500, new { Message = "An error occurred while retrieving contact details" });
            }
        }

        /// <summary>
        /// Get contact details by ID
        /// </summary>
        /// <param name="id">Contact details ID</param>
        /// <returns>Contact details information</returns>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ContactDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ContactDetailsDto>> GetContactDetails(int id)
        {
            try
            {
                var contactDetails = await _contactDetailsService.GetContactDetailsByIdAsync(id);
                return Ok(contactDetails);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Contact details not found: {ContactId}", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact details {ContactId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the contact details" });
            }
        }

        /// <summary>
        /// Get contact details by entity (user, company, location)
        /// </summary>
        /// <param name="entityType">Entity type (user, company, location)</param>
        /// <param name="entityId">Entity ID</param>
        /// <returns>List of contact details for the specified entity</returns>
        [HttpGet("entity/{entityType}/{entityId:int}")]
        [ProducesResponseType(typeof(List<ContactDetailsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ContactDetailsDto>>> GetContactDetailsByEntity(string entityType, int entityId)
        {
            try
            {
                var contactDetails = await _contactDetailsService.GetContactDetailsByEntityAsync(entityType, entityId);
                return Ok(contactDetails);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid entity: {EntityType} {EntityId}", entityType, entityId);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact details for entity {EntityType} {EntityId}", entityType, entityId);
                return StatusCode(500, new { Message = "An error occurred while retrieving contact details" });
            }
        }

        /// <summary>
        /// Create new contact details for an entity
        /// </summary>
        /// <param name="entityType">Entity type (user, company, location)</param>
        /// <param name="entityId">Entity ID</param>
        /// <param name="createContactDetailsDto">Contact details creation data</param>
        /// <returns>Created contact details information</returns>
        [HttpPost("entity/{entityType}/{entityId:int}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(ContactDetailsDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ContactDetailsDto>> CreateContactDetails(string entityType, int entityId, [FromBody] CreateContactDetailsDto createContactDetailsDto)
        {
            try
            {
                var contactDetails = await _contactDetailsService.CreateContactDetailsAsync(createContactDetailsDto, entityType, entityId);
                return CreatedAtAction(nameof(GetContactDetails), new { id = contactDetails.Id }, contactDetails);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Contact details creation failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating contact details for {EntityType} {EntityId}", entityType, entityId);
                return StatusCode(500, new { Message = "An error occurred while creating the contact details" });
            }
        }

        /// <summary>
        /// Update existing contact details
        /// </summary>
        /// <param name="id">Contact details ID</param>
        /// <param name="updateContactDetailsDto">Contact details update data</param>
        /// <returns>Updated contact details information</returns>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(ContactDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ContactDetailsDto>> UpdateContactDetails(int id, [FromBody] UpdateContactDetailsDto updateContactDetailsDto)
        {
            try
            {
                var contactDetails = await _contactDetailsService.UpdateContactDetailsAsync(id, updateContactDetailsDto);
                return Ok(contactDetails);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Contact details update failed for {ContactId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact details {ContactId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the contact details" });
            }
        }

        /// <summary>
        /// Delete contact details
        /// </summary>
        /// <param name="id">Contact details ID</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteContactDetails(int id)
        {
            try
            {
                var success = await _contactDetailsService.DeleteContactDetailsAsync(id);
                if (!success)
                {
                    return NotFound(new { Message = "Contact details not found" });
                }
                return Ok(new { Message = "Contact details deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting contact details {ContactId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the contact details" });
            }
        }

        /// <summary>
        /// Set contact details as default for an entity
        /// </summary>
        /// <param name="id">Contact details ID</param>
        /// <param name="entityType">Entity type (user, company, location)</param>
        /// <param name="entityId">Entity ID</param>
        /// <returns>Operation result</returns>
        [HttpPost("{id:int}/set-default/{entityType}/{entityId:int}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> SetDefaultContactDetails(int id, string entityType, int entityId)
        {
            try
            {
                var success = await _contactDetailsService.SetDefaultContactDetailsAsync(id, entityType, entityId);
                if (!success)
                {
                    return NotFound(new { Message = "Contact details not found or does not belong to the specified entity" });
                }
                return Ok(new { Message = "Default contact details set successfully" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default contact details {ContactId} for {EntityType} {EntityId}", id, entityType, entityId);
                return StatusCode(500, new { Message = "An error occurred while setting the default contact details" });
            }
        }

        /// <summary>
        /// Get recent contact details (non-paginated for quick access)
        /// </summary>
        /// <param name="count">Number of recent contact details to retrieve (default: 10, max: 50)</param>
        /// <returns>List of recent contact details</returns>
        [HttpGet("recent")]
        [ProducesResponseType(typeof(List<ContactDetailsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ContactDetailsDto>>> GetRecentContactDetails([FromQuery] int count = 10)
        {
            try
            {
                var contactDetails = await _contactDetailsService.GetRecentContactDetailsAsync(Math.Clamp(count, 1, 50));
                return Ok(contactDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent contact details");
                return StatusCode(500, new { Message = "An error occurred while retrieving recent contact details" });
            }
        }

        /// <summary>
        /// Get contact details statistics
        /// </summary>
        /// <returns>Contact details statistics</returns>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Dictionary<string, object>>> GetContactDetailsStatistics()
        {
            try
            {
                var stats = await _contactDetailsService.GetContactDetailsStatisticsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contact details statistics");
                return StatusCode(500, new { Message = "An error occurred while retrieving contact details statistics" });
            }
        }

        /// <summary>
        /// Bulk update contact details
        /// </summary>
        /// <param name="bulkUpdateDto">Bulk update data</param>
        /// <returns>Update result</returns>
        [HttpPost("bulk-update")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> BulkUpdateContactDetails([FromBody] BulkUpdateContactDetailsDto bulkUpdateDto)
        {
            try
            {
                if (bulkUpdateDto?.ContactDetailsIds?.Any() != true || bulkUpdateDto.UpdateDto == null)
                {
                    return BadRequest(new { Message = "Contact details IDs and update data are required" });
                }

                var success = await _contactDetailsService.BulkUpdateContactDetailsAsync(bulkUpdateDto.ContactDetailsIds, bulkUpdateDto.UpdateDto);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = bulkUpdateDto.ContactDetailsIds.Count,
                    SuccessCount = success ? bulkUpdateDto.ContactDetailsIds.Count : 0,
                    FailureCount = success ? 0 : bulkUpdateDto.ContactDetailsIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk updating contact details");
                return StatusCode(500, new { Message = "An error occurred while updating contact details" });
            }
        }

        /// <summary>
        /// Bulk delete contact details
        /// </summary>
        /// <param name="bulkDeleteDto">Bulk delete data</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("bulk-delete")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(BulkOperationResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BulkOperationResultDto>> BulkDeleteContactDetails([FromBody] BulkDeleteContactDetailsDto bulkDeleteDto)
        {
            try
            {
                if (bulkDeleteDto?.ContactDetailsIds?.Any() != true)
                {
                    return BadRequest(new { Message = "Contact details IDs are required" });
                }

                var success = await _contactDetailsService.BulkDeleteContactDetailsAsync(bulkDeleteDto.ContactDetailsIds);

                return Ok(new BulkOperationResultDto
                {
                    TotalRequested = bulkDeleteDto.ContactDetailsIds.Count,
                    SuccessCount = success ? bulkDeleteDto.ContactDetailsIds.Count : 0,
                    FailureCount = success ? 0 : bulkDeleteDto.ContactDetailsIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting contact details");
                return StatusCode(500, new { Message = "An error occurred while deleting contact details" });
            }
        }
    }
}