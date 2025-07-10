using Asp.Versioning;
using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    public class PermissionController : ControllerBase
    {
        private readonly IPermissionService _permissionService;
        private readonly ILogger<PermissionController> _logger;

        public PermissionController(IPermissionService permissionService, ILogger<PermissionController> logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        [HttpGet]
        [DevOnly]
        public async Task<ActionResult<List<PermissionCategoryDto>>> GetAllPermissions()
        {
            var permissions = await _permissionService.GetPermissionsByCategoryAsync();
            return Ok(permissions);
        }

        [HttpGet("roles")]
        [DevOnly]
        public async Task<ActionResult<List<RolePermissionDto>>> GetRolePermissions()
        {
            var rolePermissions = await _permissionService.GetAllRolePermissionsAsync();
            return Ok(rolePermissions);
        }

        [HttpPut("roles")]
        [DevOnly]
        public async Task<ActionResult> UpdateRolePermissions([FromBody] UpdateRolePermissionsDto updateDto)
        {
            var success = await _permissionService.UpdateRolePermissionsAsync(updateDto);
            return success ? Ok() : BadRequest("Failed to update role permissions");
        }

        [HttpGet("users/{userId:int}")]
        [AdminOrDev]
        public async Task<ActionResult<UserPermissionDto>> GetUserPermissions(int userId)
        {
            try
            {
                var userPermissions = await _permissionService.GetUserPermissionsDetailAsync(userId);
                return Ok(userPermissions);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPost("users/{userId:int}")]
        [AdminOrDev]
        public async Task<ActionResult> AssignPermissionToUser(int userId, [FromBody] AssignPermissionDto assignDto)
        {
            assignDto.UserId = userId;
            var success = await _permissionService.AssignPermissionToUserAsync(assignDto);
            return success ? Ok() : BadRequest("Failed to assign permission");
        }

        [HttpDelete("users/{userId:int}/{permissionId:int}")]
        [AdminOrDev]
        public async Task<ActionResult> RemovePermissionFromUser(int userId, int permissionId)
        {
            var success = await _permissionService.RemovePermissionFromUserAsync(userId, permissionId);
            return success ? Ok() : NotFound("Permission not found");
        }

        [HttpPost("permissions")]
        [DevOnly]
        public async Task<ActionResult<PermissionDto>> CreatePermission([FromBody] CreatePermissionDto createDto)
        {
            var permission = await _permissionService.CreatePermissionAsync(createDto);
            return CreatedAtAction(nameof(GetAllPermissions), permission);
        }
    }
}