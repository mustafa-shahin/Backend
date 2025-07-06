using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.API.Controllers
{
    /// <summary>
    /// Folder management controller providing folder operations with pagination
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/folder")]
    [ApiVersion("1.0")]
    [ApiVersion("2.0")]
    [Authorize]
    [EnableRateLimiting("ApiPolicy")]
    public class FolderController : ControllerBase
    {
        private readonly IFolderService _folderService;
        private readonly ILogger<FolderController> _logger;

        public FolderController(IFolderService folderService, ILogger<FolderController> logger)
        {
            _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get paginated list of folders with optional filtering
        /// </summary>
        /// <param name="parentFolderId">Optional parent folder ID to filter by</param>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated list of folders</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<FolderDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<FolderDto>>> GetFolders(
            [FromQuery] int? parentFolderId = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _folderService.GetFoldersPagedAsync(parentFolderId, pageNumber, pageSize);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters for getting folders");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving folders");
                return StatusCode(500, new { Message = "An error occurred while retrieving folders" });
            }
        }

        /// <summary>
        /// Search folders with pagination
        /// </summary>
        /// <param name="searchTerm">Search term to filter folders by name or description</param>
        /// <param name="pageNumber">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (1-100, default: 10)</param>
        /// <returns>Paginated search results</returns>
        [HttpGet("search")]
        [ProducesResponseType(typeof(PagedResult<FolderDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<FolderDto>>> SearchFolders(
            [FromQuery][Required] string searchTerm,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return BadRequest(new { Message = "Search term is required" });
                }

                var result = await _folderService.SearchFoldersPagedAsync(searchTerm, pageNumber, pageSize);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid search parameters");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching folders");
                return StatusCode(500, new { Message = "An error occurred while searching folders" });
            }
        }

        /// <summary>
        /// Create a new folder
        /// </summary>
        /// <param name="createFolderDto">Folder creation data</param>
        /// <returns>Created folder information</returns>
        [HttpPost]
        [ProducesResponseType(typeof(FolderDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FolderDto>> CreateFolder([FromBody] CreateFolderDto createFolderDto)
        {
            try
            {
                if (createFolderDto == null)
                {
                    return BadRequest(new { Message = "Folder data is required" });
                }

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
        /// <param name="id">Folder ID</param>
        /// <returns>Folder information</returns>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(FolderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="parentFolderId">Optional parent folder ID</param>
        /// <returns>List of folders</returns>
        [HttpGet("all")]
        [ProducesResponseType(typeof(List<FolderDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="rootFolderId">Optional root folder ID</param>
        /// <returns>Folder tree structure</returns>
        [HttpGet("tree")]
        [ProducesResponseType(typeof(FolderTreeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="id">Folder ID</param>
        /// <param name="updateFolderDto">Update data</param>
        /// <returns>Updated folder information</returns>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(FolderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FolderDto>> UpdateFolder(int id, [FromBody] UpdateFolderDto updateFolderDto)
        {
            try
            {
                if (updateFolderDto == null)
                {
                    return BadRequest(new { Message = "Update data is required" });
                }

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
        /// <param name="id">Folder ID</param>
        /// <param name="deleteFiles">Whether to delete contained files (default: false)</param>
        /// <returns>Deletion result</returns>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteFolder(int id, [FromQuery] bool deleteFiles = false)
        {
            try
            {
                var success = await _folderService.DeleteFolderAsync(id, deleteFiles);
                if (!success)
                {
                    return NotFound(new { Message = "Folder not found" });
                }
                return Ok(new { Message = "Folder deleted successfully", FolderId = id });
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
        /// <param name="moveFolderDto">Move operation data</param>
        /// <returns>Updated folder information</returns>
        [HttpPost("move")]
        [ProducesResponseType(typeof(FolderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FolderDto>> MoveFolder([FromBody] MoveFolderDto moveFolderDto)
        {
            try
            {
                if (moveFolderDto == null)
                {
                    return BadRequest(new { Message = "Move data is required" });
                }

                var folder = await _folderService.MoveFolderAsync(moveFolderDto);
                return Ok(folder);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Folder move failed: {Message}", ex.Message);
                return BadRequest(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
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
        /// <param name="id">Folder ID</param>
        /// <param name="renameFolderDto">Rename data</param>
        /// <returns>Success status</returns>
        [HttpPost("{id:int}/rename")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> RenameFolder(int id, [FromBody] RenameFolderDto renameFolderDto)
        {
            try
            {
                if (renameFolderDto == null || string.IsNullOrWhiteSpace(renameFolderDto.NewName))
                {
                    return BadRequest(new { Message = "New name is required" });
                }

                var success = await _folderService.RenameFolderAsync(id, renameFolderDto.NewName);
                if (!success)
                {
                    return BadRequest(new { Message = "Failed to rename folder. Name may already exist or be invalid." });
                }
                return Ok(new { Message = "Folder renamed successfully", FolderId = id, NewName = renameFolderDto.NewName });
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
        /// <param name="copyFolderDto">Copy operation data</param>
        /// <returns>New folder information</returns>
        [HttpPost("copy")]
        [ProducesResponseType(typeof(FolderDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FolderDto>> CopyFolder([FromBody] CopyFolderDto copyFolderDto)
        {
            try
            {
                if (copyFolderDto == null)
                {
                    return BadRequest(new { Message = "Copy data is required" });
                }

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
        /// <param name="id">Folder ID</param>
        /// <returns>Folder path</returns>
        [HttpGet("{id:int}/path")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetFolderPath(int id)
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
        /// <param name="id">Folder ID</param>
        /// <returns>Breadcrumb hierarchy</returns>
        [HttpGet("{id:int}/breadcrumbs")]
        [ProducesResponseType(typeof(List<FolderDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="path">Folder path</param>
        /// <returns>Folder information</returns>
        [HttpGet("by-path")]
        [ProducesResponseType(typeof(FolderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<FolderDto>> GetFolderByPath([FromQuery][Required] string path)
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
        /// Validate folder name availability
        /// </summary>
        /// <param name="name">Folder name to validate</param>
        /// <param name="parentFolderId">Optional parent folder ID</param>
        /// <param name="excludeFolderId">Optional folder ID to exclude from validation</param>
        /// <returns>Validation result</returns>
        [HttpGet("validate-name")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> ValidateFolderName(
            [FromQuery][Required] string name,
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
        /// <param name="id">Folder ID</param>
        /// <returns>Folder statistics</returns>
        [HttpGet("{id:int}/statistics")]
        [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="folderType">System folder type</param>
        /// <returns>System folder information</returns>
        [HttpPost("system/{folderType}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(FolderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="userId">User ID</param>
        /// <returns>User avatar folder information</returns>
        [HttpGet("user-avatars/{userId:int}")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(FolderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <returns>Company assets folder information</returns>
        [HttpGet("company-assets")]
        [Authorize(Roles = "Admin,Dev")]
        [ProducesResponseType(typeof(FolderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        /// <param name="id">Folder ID</param>
        /// <returns>Existence status</returns>
        [HttpGet("{id:int}/exists")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> FolderExists(int id)
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