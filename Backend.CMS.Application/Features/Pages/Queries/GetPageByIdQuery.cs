using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Backend.CMS.Application.Features.Pages.DTOs;
using Backend.CMS.Infrastructure.Data;

namespace Backend.CMS.Application.Features.Pages.Queries
{
    public class GetPageByIdQuery : IRequest<PageDto?>
    {
        public Guid PageId { get; set; }
    }

    public class GetPageByIdQueryHandler : IRequestHandler<GetPageByIdQuery, PageDto?>
    {
        private readonly CmsDbContext _context;
        private readonly IMapper _mapper;

        public GetPageByIdQueryHandler(CmsDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<PageDto?> Handle(GetPageByIdQuery request, CancellationToken cancellationToken)
        {
            var page = await _context.Pages
                .Include(p => p.Components.OrderBy(c => c.Order))
                .Include(p => p.ParentPage)
                .FirstOrDefaultAsync(p => p.Id == request.PageId && !p.IsDeleted, cancellationToken);

            return page == null ? null : _mapper.Map<PageDto>(page);
        }
    }
}
