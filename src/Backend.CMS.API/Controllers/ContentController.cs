using MediatR;
using Microsoft.AspNetCore.Mvc;
using Backend.CMS.Application.Features.Pages.DTOs;
using Backend.CMS.Application.Features.Pages.Queries;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContentController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ContentController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("pages/{slug}")]
        public async Task<ActionResult<PageDto>> GetPageBySlug(string slug)
        {
            var result = await _mediator.Send(new GetPageBySlugQuery { Slug = slug });

            if (result == null || result.Status != PageStatus.Published)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpGet("pages")]
        public async Task<ActionResult<List<PageListDto>>> GetPublishedPages()
        {
            var query = new GetPagesQuery
            {
                Status = PageStatus.Published,
                PageSize = 100,
                SortBy = "Priority",
                SortDescending = false
            };

            var result = await _mediator.Send(query);
            return Ok(result.Items);
        }
    }
}
