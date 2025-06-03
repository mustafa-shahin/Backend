using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Backend.CMS.Application.Features.Pages.DTOs;
using Backend.CMS.Infrastructure.Data;

namespace Backend.CMS.Application.Features.Pages.Queries
{
    public class GetPageBySlugQuery : IRequest<PageDto?>
    {
        public string Slug { get; set; } = string.Empty;
    }

    public class GetPageBySlugQueryHandler : IRequestHandler<GetPageBySlugQuery, PageDto?>
    {
        private readonly CmsDbContext _context;
        private readonly IMapper _mapper;

        public GetPageBySlugQueryHandler(CmsDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<PageDto?> Handle(GetPageBySlugQuery request, CancellationToken cancellationToken)
        {
            var page = await _context.Pages
                .Include(p => p.Components.OrderBy(c => c.Order))
                .Include(p => p.ParentPage)
                .FirstOrDefaultAsync(p => p.Slug == request.Slug && !p.IsDeleted, cancellationToken);

            return page == null ? null : _mapper.Map<PageDto>(page);
        }
    }
}
