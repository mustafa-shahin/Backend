using Backend.CMS.API.Authorization;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AdminOrDev]
    public class CacheController : ControllerBase
    {
        private readonly ICacheInvalidationService _cacheService;
        private readonly ILogger<CacheController> _logger;

        public CacheController(ICacheInvalidationService cacheService, ILogger<CacheController> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<Dictionary<string, object>>> GetCacheStatistics()
        {
            try
            {
                var statistics = await _cacheService.GetCacheStatisticsAsync();
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache statistics");
                return StatusCode(500, new { Message = "An error occurred while getting cache statistics" });
            }
        }

        /// <summary>
        /// Get cache keys by pattern
        /// </summary>
        [HttpGet("keys")]
        public async Task<ActionResult<List<string>>> GetCacheKeys([FromQuery] string pattern = "*")
        {
            try
            {
                var keys = await _cacheService.GetCacheKeysAsync(pattern);
                return Ok(keys);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache keys");
                return StatusCode(500, new { Message = "An error occurred while getting cache keys" });
            }
        }

        /// <summary>
        /// Clear all cache
        /// </summary>
        [HttpDelete("all")]
        [DevOnly]
        public async Task<ActionResult> ClearAllCache()
        {
            try
            {
                await _cacheService.InvalidateAllCacheAsync();
                _logger.LogInformation("All cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all cache");
                return StatusCode(500, new { Message = "An error occurred while clearing cache" });
            }
        }

        /// <summary>
        /// Clear user cache
        /// </summary>
        [HttpDelete("users/{userId:int}")]
        public async Task<ActionResult> ClearUserCache(int userId)
        {
            try
            {
                await _cacheService.InvalidateUserCacheAsync(userId);
                _logger.LogInformation("User cache cleared for user {UserId} by {CurrentUserId}", userId, GetCurrentUserId());
                return Ok(new { Message = $"User cache cleared for user {userId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing user cache for user {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while clearing user cache" });
            }
        }

        /// <summary>
        /// Clear all user caches
        /// </summary>
        [HttpDelete("users")]
        public async Task<ActionResult> ClearAllUserCache()
        {
            try
            {
                await _cacheService.InvalidateUserCacheAsync(0); // This will clear all user cache patterns
                _logger.LogInformation("All user cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All user cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all user cache");
                return StatusCode(500, new { Message = "An error occurred while clearing user cache" });
            }
        }

        /// <summary>
        /// Clear page cache
        /// </summary>
        [HttpDelete("pages/{pageId:int}")]
        public async Task<ActionResult> ClearPageCache(int pageId)
        {
            try
            {
                await _cacheService.InvalidatePageCacheAsync(pageId);
                _logger.LogInformation("Page cache cleared for page {PageId} by user {UserId}", pageId, GetCurrentUserId());
                return Ok(new { Message = $"Page cache cleared for page {pageId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing page cache for page {PageId}", pageId);
                return StatusCode(500, new { Message = "An error occurred while clearing page cache" });
            }
        }

        /// <summary>
        /// Clear all page caches
        /// </summary>
        [HttpDelete("pages")]
        public async Task<ActionResult> ClearAllPageCache()
        {
            try
            {
                await _cacheService.InvalidatePageCacheAsync(0); // This will clear all page cache patterns
                _logger.LogInformation("All page cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All page cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all page cache");
                return StatusCode(500, new { Message = "An error occurred while clearing page cache" });
            }
        }

        /// <summary>
        /// Clear company cache
        /// </summary>
        [HttpDelete("company")]
        public async Task<ActionResult> ClearCompanyCache()
        {
            try
            {
                await _cacheService.InvalidateCompanyCacheAsync();
                _logger.LogInformation("Company cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "Company cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing company cache");
                return StatusCode(500, new { Message = "An error occurred while clearing company cache" });
            }
        }

        /// <summary>
        /// Clear location cache
        /// </summary>
        [HttpDelete("locations/{locationId:int}")]
        public async Task<ActionResult> ClearLocationCache(int locationId)
        {
            try
            {
                await _cacheService.InvalidateLocationCacheAsync(locationId);
                _logger.LogInformation("Location cache cleared for location {LocationId} by user {UserId}", locationId, GetCurrentUserId());
                return Ok(new { Message = $"Location cache cleared for location {locationId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing location cache for location {LocationId}", locationId);
                return StatusCode(500, new { Message = "An error occurred while clearing location cache" });
            }
        }

        /// <summary>
        /// Clear all location caches
        /// </summary>
        [HttpDelete("locations")]
        public async Task<ActionResult> ClearAllLocationCache()
        {
            try
            {
                await _cacheService.InvalidateLocationCacheAsync();
                _logger.LogInformation("All location cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All location cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all location cache");
                return StatusCode(500, new { Message = "An error occurred while clearing location cache" });
            }
        }

        /// <summary>
        /// Clear file cache
        /// </summary>
        [HttpDelete("files/{fileId:int}")]
        public async Task<ActionResult> ClearFileCache(int fileId)
        {
            try
            {
                await _cacheService.InvalidateFileCacheAsync(fileId);
                _logger.LogInformation("File cache cleared for file {FileId} by user {UserId}", fileId, GetCurrentUserId());
                return Ok(new { Message = $"File cache cleared for file {fileId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing file cache for file {FileId}", fileId);
                return StatusCode(500, new { Message = "An error occurred while clearing file cache" });
            }
        }

        /// <summary>
        /// Clear all file caches
        /// </summary>
        [HttpDelete("files")]
        public async Task<ActionResult> ClearAllFileCache()
        {
            try
            {
                await _cacheService.InvalidateFileCacheAsync();
                _logger.LogInformation("All file cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All file cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all file cache");
                return StatusCode(500, new { Message = "An error occurred while clearing file cache" });
            }
        }

        /// <summary>
        /// Clear folder cache
        /// </summary>
        [HttpDelete("folders/{folderId:int}")]
        public async Task<ActionResult> ClearFolderCache(int folderId)
        {
            try
            {
                await _cacheService.InvalidateFolderCacheAsync(folderId);
                _logger.LogInformation("Folder cache cleared for folder {FolderId} by user {UserId}", folderId, GetCurrentUserId());
                return Ok(new { Message = $"Folder cache cleared for folder {folderId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing folder cache for folder {FolderId}", folderId);
                return StatusCode(500, new { Message = "An error occurred while clearing folder cache" });
            }
        }

        /// <summary>
        /// Clear all folder caches
        /// </summary>
        [HttpDelete("folders")]
        public async Task<ActionResult> ClearAllFolderCache()
        {
            try
            {
                await _cacheService.InvalidateFolderCacheAsync();
                _logger.LogInformation("All folder cache cleared by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "All folder cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all folder cache");
                return StatusCode(500, new { Message = "An error occurred while clearing folder cache" });
            }
        }

        /// <summary>
        /// Warmup cache with commonly accessed data
        /// </summary>
        [HttpPost("warmup")]
        public async Task<ActionResult> WarmupCache()
        {
            try
            {
                await _cacheService.WarmupCacheAsync();
                _logger.LogInformation("Cache warmup initiated by user {UserId}", GetCurrentUserId());
                return Ok(new { Message = "Cache warmup completed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache warmup");
                return StatusCode(500, new { Message = "An error occurred during cache warmup" });
            }
        }

        /// <summary>
        /// Clear cache by pattern
        /// </summary>
        [HttpDelete("pattern")]
        [DevOnly]
        public async Task<ActionResult> ClearCacheByPattern([FromQuery] string pattern)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    return BadRequest(new { Message = "Pattern is required" });
                }

                // Safety check to prevent clearing all cache accidentally
                if (pattern == "*")
                {
                    return BadRequest(new { Message = "Use the /cache/all endpoint to clear all cache" });
                }

                var cacheService = _cacheService as CacheService;
                if (cacheService != null)
                {
                    await cacheService.RemoveByPatternAsync(pattern);
                    _logger.LogInformation("Cache cleared by pattern {Pattern} by user {UserId}", pattern, GetCurrentUserId());
                    return Ok(new { Message = $"Cache cleared for pattern: {pattern}" });
                }

                return StatusCode(500, new { Message = "Cache service not available" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache by pattern {Pattern}", pattern);
                return StatusCode(500, new { Message = "An error occurred while clearing cache" });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("userId");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid user token");
            }

            return userId;
        }
    }
}