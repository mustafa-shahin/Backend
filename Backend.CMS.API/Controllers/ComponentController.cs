using Backend.CMS.Application.DTOs.Components;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ComponentController : ControllerBase
    {
        private readonly IComponentService _componentService;
        private readonly ILogger<ComponentController> _logger;

        public ComponentController(IComponentService componentService, ILogger<ComponentController> logger)
        {
            _componentService = componentService;
            _logger = logger;
        }

        /// <summary>
        /// Get component library (all templates organized by category)
        /// </summary>
        [HttpGet("library")]
        public async Task<ActionResult<ComponentLibraryDto>> GetComponentLibrary()
        {
            try
            {
                var library = await _componentService.GetComponentLibraryAsync();
                return Ok(library);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving component library");
                return StatusCode(500, new { Message = "An error occurred while retrieving the component library" });
            }
        }

        /// <summary>
        /// Get all component templates
        /// </summary>
        [HttpGet("templates")]
        public async Task<ActionResult<List<ComponentTemplateDto>>> GetComponentTemplates()
        {
            try
            {
                var templates = await _componentService.GetComponentTemplatesAsync();
                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving component templates");
                return StatusCode(500, new { Message = "An error occurred while retrieving component templates" });
            }
        }

        /// <summary>
        /// Get component template by ID
        /// </summary>
        [HttpGet("templates/{id:int}")]
        public async Task<ActionResult<ComponentTemplateDto>> GetComponentTemplate(int id)
        {
            try
            {
                var template = await _componentService.GetComponentTemplateByIdAsync(id);
                return Ok(template);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Component template not found: {TemplateId}", id);
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving component template {TemplateId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the component template" });
            }
        }

        /// <summary>
        /// Validate component data against schema
        /// </summary>
        [HttpPost("validate")]
        public async Task<ActionResult<bool>> ValidateComponentData([FromBody] ValidateComponentDto validateComponentDto)
        {
            try
            {
                var isValid = await _componentService.ValidateComponentDataAsync(validateComponentDto.Type, validateComponentDto.Data);
                return Ok(new { IsValid = isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating component data");
                return StatusCode(500, new { Message = "An error occurred while validating component data" });
            }
        }
    }

    public class ValidateComponentDto
    {
        public ComponentType Type { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }
}