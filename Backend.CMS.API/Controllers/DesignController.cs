using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.CMS.Application.Common.Models;
using Backend.CMS.Application.Features.Pages.Commands;
using Backend.CMS.Application.Features.Pages.DTOs;
using Backend.CMS.Application.Features.Pages.Queries;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class DesignController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DesignController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("pages")]
        public async Task<ActionResult<PagedResult<PageListDto>>> GetPages([FromQuery] GetPagesQuery query)
        {
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpGet("pages/{id}")]
        public async Task<ActionResult<PageDto>> GetPageForDesign(Guid id)
        {
            var result = await _mediator.Send(new GetPageByIdQuery { PageId = id });

            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpPut("pages/{id}/components")]
        public async Task<ActionResult<PageDto>> SavePageDesign(Guid id, [FromBody] List<PageComponentDto> components)
        {
            var result = await _mediator.Send(new UpdatePageComponentsCommand
            {
                Data = new UpdatePageComponentsDto
                {
                    PageId = id,
                    Components = components
                }
            });

            return Ok(result);
        }

        [HttpGet("component-types")]
        public ActionResult<List<ComponentTypeDto>> GetAvailableComponentTypes()
        {
            // This would come from a configuration or database
            var componentTypes = new List<ComponentTypeDto>
            {
                new() { Type = "hero", Name = "Hero Section", Icon = "hero-icon", Category = "Layout" },
                new() { Type = "text", Name = "Text Block", Icon = "text-icon", Category = "Content" },
                new() { Type = "image", Name = "Image", Icon = "image-icon", Category = "Media" },
                new() { Type = "gallery", Name = "Image Gallery", Icon = "gallery-icon", Category = "Media" },
                new() { Type = "video", Name = "Video", Icon = "video-icon", Category = "Media" },
                new() { Type = "columns", Name = "Columns", Icon = "columns-icon", Category = "Layout" },
                new() { Type = "accordion", Name = "Accordion", Icon = "accordion-icon", Category = "Interactive" },
                new() { Type = "tabs", Name = "Tabs", Icon = "tabs-icon", Category = "Interactive" },
                new() { Type = "cta", Name = "Call to Action", Icon = "cta-icon", Category = "Marketing" },
                new() { Type = "form", Name = "Form", Icon = "form-icon", Category = "Interactive" },
                new() { Type = "map", Name = "Map", Icon = "map-icon", Category = "Interactive" },
                new() { Type = "testimonial", Name = "Testimonial", Icon = "testimonial-icon", Category = "Content" },
                new() { Type = "pricing", Name = "Pricing Table", Icon = "pricing-icon", Category = "Marketing" },
                new() { Type = "team", Name = "Team Members", Icon = "team-icon", Category = "Content" },
                new() { Type = "blog", Name = "Blog Posts", Icon = "blog-icon", Category = "Dynamic" },
                new() { Type = "products", Name = "Products", Icon = "products-icon", Category = "Dynamic" },
                new() { Type = "custom", Name = "Custom HTML", Icon = "code-icon", Category = "Advanced" }
            };

            return Ok(componentTypes);
        }
    }

    public class ComponentTypeDto
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}
