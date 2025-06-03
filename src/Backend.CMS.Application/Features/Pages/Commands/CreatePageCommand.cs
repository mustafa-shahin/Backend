using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Backend.CMS.Application.Features.Pages.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;

namespace Backend.CMS.Application.Features.Pages.Commands
{
    public class CreatePageCommand : IRequest<PageDto>
    {
        public CreatePageDto PageData { get; set; } = null!;
    }

    public class CreatePageCommandHandler : IRequestHandler<CreatePageCommand, PageDto>
    {
        private readonly CmsDbContext _context;
        private readonly IMapper _mapper;

        public CreatePageCommandHandler(CmsDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<PageDto> Handle(CreatePageCommand request, CancellationToken cancellationToken)
        {
            // Validate slug uniqueness
            var slugExists = await _context.Pages
                .AnyAsync(p => p.Slug == request.PageData.Slug, cancellationToken);

            if (slugExists)
            {
                throw new InvalidOperationException($"A page with slug '{request.PageData.Slug}' already exists.");
            }

            var page = _mapper.Map<Page>(request.PageData);

            _context.Pages.Add(page);
            await _context.SaveChangesAsync(cancellationToken);

            return _mapper.Map<PageDto>(page);
        }
    }
}
