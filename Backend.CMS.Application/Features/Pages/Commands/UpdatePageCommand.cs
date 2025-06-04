using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Backend.CMS.Application.Features.Pages.DTOs;
using Backend.CMS.Infrastructure.Data;

namespace Backend.CMS.Application.Features.Pages.Commands
{
    public class UpdatePageCommand : IRequest<PageDto>
    {
        public UpdatePageDto PageData { get; set; } = null!;
    }

    public class UpdatePageCommandHandler : IRequestHandler<UpdatePageCommand, PageDto>
    {
        private readonly CmsDbContext _context;
        private readonly IMapper _mapper;

        public UpdatePageCommandHandler(CmsDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<PageDto> Handle(UpdatePageCommand request, CancellationToken cancellationToken)
        {
            var page = await _context.Pages
                .Include(p => p.Components)
                .FirstOrDefaultAsync(p => p.Id == request.PageData.Id, cancellationToken);

            if (page == null)
            {
                throw new InvalidOperationException($"Page with ID '{request.PageData.Id}' not found.");
            }

            // Check slug uniqueness if changed
            if (page.Slug != request.PageData.Slug)
            {
                var slugExists = await _context.Pages
                    .AnyAsync(p => p.Slug == request.PageData.Slug && p.Id != page.Id, cancellationToken);

                if (slugExists)
                {
                    throw new InvalidOperationException($"A page with slug '{request.PageData.Slug}' already exists.");
                }
            }

            _mapper.Map(request.PageData, page);

            await _context.SaveChangesAsync(cancellationToken);

            return _mapper.Map<PageDto>(page);
        }
    }
}
