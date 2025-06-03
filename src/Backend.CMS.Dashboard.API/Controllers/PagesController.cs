using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.CMS.Application.Common.Models;
using Backend.CMS.Application.Features.Pages.Commands;
using Backend.CMS.Application.Features.Pages.DTOs;
using Backend.CMS.Application.Features.Pages.Queries;
namespace Backend.CMS.Dashboard.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PagesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PagesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<PageListDto>>> GetPages([FromQuery] GetPagesQuery query)
        {
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PageDto>> GetPage(Guid id)
        {
            var result = await _mediator.Send(new GetPageByIdQuery { PageId = id });

            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpGet("by-slug/{slug}")]
        public async Task<ActionResult<PageDto>> GetPageBySlug(string slug)
        {
            var result = await _mediator.Send(new GetPageBySlugQuery { Slug = slug });

            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PageDto>> CreatePage([FromBody] CreatePageDto createPageDto)
        {
            var result = await _mediator.Send(new CreatePageCommand { PageData = createPageDto });
            return CreatedAtAction(nameof(GetPage), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PageDto>> UpdatePage(Guid id, [FromBody] UpdatePageDto updatePageDto)
        {
            if (id != updatePageDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            var result = await _mediator.Send(new UpdatePageCommand { PageData = updatePageDto });
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeletePage(Guid id)
        {
            var result = await _mediator.Send(new DeletePageCommand { PageId = id });

            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpPut("{id}/components")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PageDto>> UpdatePageComponents(Guid id, [FromBody] List<PageComponentDto> components)
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
    }
}
