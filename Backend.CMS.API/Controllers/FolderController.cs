using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FolderController : ControllerBase
    {
        private readonly IFolderService _folderService;
        private readonly ILogger<FolderController> _logger;

        public FolderController(IFolderService folderService, ILogger<FolderController> logger)
        {
            _folderService = folderService;
            _logger = logger;
        }

        /// <summary>
        /// Get folders with pagination
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<FolderDto>>> GetFolders(
            [FromQuery] int? parentFolderId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var allFolders = await _folderService.GetFoldersAsync(parentFolderId);

                var totalCount = allFolders.Count;
                var items = allFolders
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new PagedResult<FolderDto>
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
                _logger.LogError(ex, "Error retrieving folders");
                return StatusCode(500, new { Message = "An error occurred while retrieving folders" });
            }
        }

        /// <summary>
        /// Create a new folder
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<FolderDto>> CreateFolder([FromBody] CreateFolderDto createFolderDto)
        {
            try
            {
                var folder = await _folderService.CreateFolderAsync(createFolderDto);
                return CreatedAtAction(nameof(GetFolder), new { id = folder.Id }, folder);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Folder creation failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating folder");
                return StatusCode(500, new { Message = "An error occurred while creating the folder" });
            }
        }

        /// <summary>
        /// Get folder by ID
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<FolderDto>> GetFolder(int id)
        {
            try
            {
                var folder = await _folderService.GetFolderByIdAsync(id);
                return Ok(folder);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Folder not found: {FolderId}", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving folder {FolderId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the folder" });
            }
        }

        /// <summary>
        /// Get all folders without pagination (for dropdown lists, etc.)
        /// </summary>
        [HttpGet("all")]
        public async Task<ActionResult<List<FolderDto>>> GetAllFolders([FromQuery] int? parentFolderId = null)
        {
            try
            {
                var folders = await _folderService.GetFoldersAsync(parentFolderId);
                return Ok(folders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all folders");
                return StatusCode(500, new { Message = "An error occurred while retrieving folders" });
            }
        }

        /// <summary>
        /// Get folder tree structure
        /// </summary>
        [HttpGet("tree")]
        public async Task<ActionResult<FolderTreeDto>> GetFolderTree([FromQuery] int? rootFolderId = null)
        {
            try
            {
                var folderTree = await _folderService.GetFolderTreeAsync(rootFolderId);
                return Ok(folderTree);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving folder tree");
                return StatusCode(500, new { Message = "An error occurred while retrieving the folder tree" });
            }
        }

        /// <summary>
        /// Update an existing folder
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<FolderDto>> UpdateFolder(int id, [FromBody] UpdateFolderDto updateFolderDto)
        {
            try
            {
                var folder = await _folderService.UpdateFolderAsync(id, updateFolderDto);
                return Ok(folder);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Folder update failed for {FolderId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating folder {FolderId}", id);
                return StatusCode(500, new { Message = "An error occurred while updating the folder" });
            }
        }

        /// <summary>
        /// Delete a folder (soft delete)
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> DeleteFolder(int id, [FromQuery] bool deleteFiles = false)
        {
            try
            {
                var success = await _folderService.DeleteFolderAsync(id, deleteFiles);
                if (!success)
                {
                    return NotFound(new { Message = "Folder not found" });
                }
                return Ok(new { Message = "Folder deleted successfully" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Folder deletion failed for {FolderId}: {Message}", id, ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting folder {FolderId}", id);
                return StatusCode(500, new { Message = "An error occurred while deleting the folder" });
            }
        }

        /// <summary>
        /// Move a folder to a different parent
        /// </summary>
        [HttpPost("move")]
        public async Task<ActionResult<FolderDto>> MoveFolder([FromBody] MoveFolderDto moveFolderDto)
        {
            try
            {
                var folder = await _folderService.MoveFolderAsync(moveFolderDto);
                return Ok(folder);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Folder move failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving folder");
                return StatusCode(500, new { Message = "An error occurred while moving the folder" });
            }
        }

        /// <summary>
        /// Rename a folder
        /// </summary>
        [HttpPost("{id:int}/rename")]
        public async Task<ActionResult> RenameFolder(int id, [FromBody] RenameFolderDto renameFolderDto)
        {
            try
            {
                var success = await _folderService.RenameFolderAsync(id, renameFolderDto.NewName);
                if (!success)
                {
                    return BadRequest(new { Message = "Failed to rename folder. Name may already exist or be invalid." });
                }
                return Ok(new { Message = "Folder renamed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming folder {FolderId}", id);
                return StatusCode(500, new { Message = "An error occurred while renaming the folder" });
            }
        }

        /// <summary>
        /// Copy a folder to a different location
        /// </summary>
        [HttpPost("copy")]
        public async Task<ActionResult<FolderDto>> CopyFolder([FromBody] CopyFolderDto copyFolderDto)
        {
            try
            {
                var folder = await _folderService.CopyFolderAsync(
                    copyFolderDto.FolderId,
                    copyFolderDto.DestinationFolderId,
                    copyFolderDto.NewName);
                return CreatedAtAction(nameof(GetFolder), new { id = folder.Id }, folder);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Folder copy failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying folder");
                return StatusCode(500, new { Message = "An error occurred while copying the folder" });
            }
        }

        /// <summary>
        /// Get folder path
        /// </summary>
        [HttpGet("{id:int}/path")]
        public async Task<ActionResult<string>> GetFolderPath(int id)
        {
            try
            {
                var path = await _folderService.GetFolderPathAsync(id);
                if (string.IsNullOrEmpty(path))
                {
                    return NotFound(new { Message = "Folder not found" });
                }
                return Ok(new { Path = path });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder path for {FolderId}", id);
                return StatusCode(500, new { Message = "An error occurred while getting the folder path" });
            }
        }

        /// <summary>
        /// Get folder breadcrumbs for navigation
        /// </summary>
        [HttpGet("{id:int}/breadcrumbs")]
        public async Task<ActionResult<List<FolderDto>>> GetFolderBreadcrumbs(int id)
        {
            try
            {
                var breadcrumbs = await _folderService.GetFolderBreadcrumbsAsync(id);
                return Ok(breadcrumbs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder breadcrumbs for {FolderId}", id);
                return StatusCode(500, new { Message = "An error occurred while getting folder breadcrumbs" });
            }
        }

        /// <summary>
        /// Get folder by path
        /// </summary>
        [HttpGet("by-path")]
        public async Task<ActionResult<FolderDto>> GetFolderByPath([FromQuery] string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return BadRequest(new { Message = "Path parameter is required" });
                }

                var folder = await _folderService.GetFolderByPathAsync(path);
                if (folder == null)
                {
                    return NotFound(new { Message = "Folder not found at specified path" });
                }
                return Ok(folder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder by path {Path}", path);
                return StatusCode(500, new { Message = "An error occurred while retrieving the folder" });
            }
        }

        /// <summary>
        /// Search folders
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<List<FolderDto>>> SearchFolders([FromQuery] string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return BadRequest(new { Message = "Search term is required" });
                }

                var folders = await _folderService.SearchFoldersAsync(searchTerm);
                return Ok(folders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching folders");
                return StatusCode(500, new { Message = "An error occurred while searching folders" });
            }
        }

        /// <summary>
        /// Validate folder name availability
        /// </summary>
        [HttpGet("validate-name")]
        public async Task<ActionResult<bool>> ValidateFolderName(
            [FromQuery] string name,
            [FromQuery] int? parentFolderId = null,
            [FromQuery] int? excludeFolderId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { Message = "Name parameter is required" });
                }

                var isValid = await _folderService.ValidateFolderNameAsync(name, parentFolderId, excludeFolderId);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating folder name");
                return StatusCode(500, new { Message = "An error occurred while validating the folder name" });
            }
        }

        /// <summary>
        /// Get folder statistics
        /// </summary>
        [HttpGet("{id:int}/statistics")]
        public async Task<ActionResult<Dictionary<string, object>>> GetFolderStatistics(int id)
        {
            try
            {
                var statistics = await _folderService.GetFolderStatisticsAsync(id);
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder statistics for {FolderId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving folder statistics" });
            }
        }

        /// <summary>
        /// Get or create system folder by type (Admin/Dev only)
        /// </summary>
        [HttpPost("system/{folderType}")]
        [Authorize(Roles = "Admin,Dev")]
        public async Task<ActionResult<FolderDto>> GetOrCreateSystemFolder(FolderType folderType)
        {
            try
            {
                var folder = await _folderService.GetOrCreateSystemFolderAsync(folderType);
                return Ok(folder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting/creating system folder {FolderType}", folderType);
                return StatusCode(500, new { Message = "An error occurred while getting/creating the system folder" });
            }
        }

        /// <summary>
        /// Get user avatar folder (Admin/Dev only)
        /// </summary>
        [HttpGet("user-avatars/{userId:int}")]
        [Authorize(Roles = "Admin,Dev")]
        public async Task<ActionResult<FolderDto>> GetUserAvatarFolder(int userId)
        {
            try
            {
                var folder = await _folderService.GetUserAvatarFolderAsync(userId);
                return Ok(folder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user avatar folder for user {UserId}", userId);
                return StatusCode(500, new { Message = "An error occurred while getting the user avatar folder" });
            }
        }

        /// <summary>
        /// Get company assets folder (Admin/Dev only)
        /// </summary>
        [HttpGet("company-assets")]
        [Authorize(Roles = "Admin,Dev")]
        public async Task<ActionResult<FolderDto>> GetCompanyAssetsFolder()
        {
            try
            {
                var folder = await _folderService.GetCompanyAssetsFolderAsync();
                return Ok(folder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting company assets folder");
                return StatusCode(500, new { Message = "An error occurred while getting the company assets folder" });
            }
        }

        /// <summary>
        /// Check if folder exists
        /// </summary>
        [HttpGet("{id:int}/exists")]
        public async Task<ActionResult<bool>> FolderExists(int id)
        {
            try
            {
                var exists = await _folderService.FolderExistsAsync(id);
                return Ok(new { Exists = exists });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if folder exists {FolderId}", id);
                return StatusCode(500, new { Message = "An error occurred while checking folder existence" });
            }
        }
    }
}