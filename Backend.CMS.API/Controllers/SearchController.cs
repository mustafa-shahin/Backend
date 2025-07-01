using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;
        private readonly ILogger<SearchController> _logger;

        public SearchController(ISearchService searchService, ILogger<SearchController> logger)
        {
            _searchService = searchService;
            _logger = logger;
        }

        /// <summary>
        /// Search across all indexed content
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<SearchResponseDto>> Search([FromBody] SearchRequestDto searchRequest)
        {
            try
            {
                var results = await _searchService.SearchAsync(searchRequest);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing search");
                return StatusCode(500, new { Message = "An error occurred while searching" });
            }
        }

        /// <summary>
        /// Get search suggestions
        /// </summary>
        [HttpGet("suggest")]
        [AllowAnonymous]
        public async Task<ActionResult<List<SearchResultDto>>> Suggest([FromQuery] string query, [FromQuery] int maxSuggestions = 5)
        {
            try
            {
                var suggestions = await _searchService.SuggestAsync(query, maxSuggestions);
                return Ok(suggestions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting search suggestions");
                return StatusCode(500, new { Message = "An error occurred while getting suggestions" });
            }
        }

        /// <summary>
        /// Get indexing status (Admin/Dev only)
        /// </summary>
        [HttpGet("indexing/status")]
        [Authorize(Roles = "Admin,Dev")]
        public async Task<ActionResult<IndexingStatusDto>> GetIndexingStatus()
        {
            try
            {
                var status = await _searchService.GetIndexingStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting indexing status");
                return StatusCode(500, new { Message = "An error occurred while getting indexing status" });
            }
        }

        /// <summary>
        /// Trigger full reindex (Admin/Dev only)
        /// </summary>
        [HttpPost("indexing/full")]
        [Authorize(Roles = "Admin,Dev")]
        public async Task<ActionResult> TriggerFullIndex()
        {
            try
            {
                var success = await _searchService.TriggerFullIndexAsync();
                return success
                    ? Ok(new { Message = "Full index started successfully" })
                    : BadRequest(new { Message = "Failed to start full index" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering full index");
                return StatusCode(500, new { Message = "An error occurred while starting full index" });
            }
        }

        /// <summary>
        /// Trigger incremental index (Admin/Dev only)
        /// </summary>
        [HttpPost("indexing/incremental")]
        [Authorize(Roles = "Admin,Dev")]
        public async Task<ActionResult> TriggerIncrementalIndex()
        {
            try
            {
                var success = await _searchService.TriggerIncrementalIndexAsync();
                return success
                    ? Ok(new { Message = "Incremental index started successfully" })
                    : BadRequest(new { Message = "Failed to start incremental index" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering incremental index");
                return StatusCode(500, new { Message = "An error occurred while starting incremental index" });
            }
        }

        /// <summary>
        /// Index specific entity (Admin/Dev only)
        /// </summary>
        [HttpPost("indexing/entity")]
        [Authorize(Roles = "Admin,Dev")]
        public async Task<ActionResult> IndexEntity([FromBody] IndexEntityRequestDto request)
        {
            try
            {
                var success = await _searchService.IndexEntityAsync(request.EntityType, request.EntityId);
                return success
                    ? Ok(new { Message = $"{request.EntityType} {request.EntityId} indexed successfully" })
                    : BadRequest(new { Message = $"Failed to index {request.EntityType} {request.EntityId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing entity");
                return StatusCode(500, new { Message = "An error occurred while indexing entity" });
            }
        }

        /// <summary>
        /// Remove entity from index (Admin/Dev only)
        /// </summary>
        [HttpDelete("indexing/entity")]
        [Authorize(Roles = "Admin,Dev")]
        public async Task<ActionResult> RemoveFromIndex([FromBody] IndexEntityRequestDto request)
        {
            try
            {
                var success = await _searchService.RemoveFromIndexAsync(request.EntityType, request.EntityId);
                return success
                    ? Ok(new { Message = $"{request.EntityType} {request.EntityId} removed from index successfully" })
                    : BadRequest(new { Message = $"Failed to remove {request.EntityType} {request.EntityId} from index" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing entity from index");
                return StatusCode(500, new { Message = "An error occurred while removing entity from index" });
            }
        }
    }
}