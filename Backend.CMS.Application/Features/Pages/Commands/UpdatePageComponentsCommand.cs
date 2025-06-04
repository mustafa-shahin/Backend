using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Backend.CMS.Application.Features.Pages.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;

namespace Backend.CMS.Application.Features.Pages.Commands
{
    public class UpdatePageComponentsCommand : IRequest<PageDto>
    {
        public UpdatePageComponentsDto Data { get; set; } = null!;
    }

    public class UpdatePageComponentsCommandHandler : IRequestHandler<UpdatePageComponentsCommand, PageDto>
    {
        private readonly CmsDbContext _context;
        private readonly IMapper _mapper;

        public UpdatePageComponentsCommandHandler(CmsDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<PageDto> Handle(UpdatePageComponentsCommand request, CancellationToken cancellationToken)
        {
            var page = await _context.Pages
                .Include(p => p.Components)
                .FirstOrDefaultAsync(p => p.Id == request.Data.PageId, cancellationToken);

            if (page == null)
            {
                throw new InvalidOperationException($"Page with ID '{request.Data.PageId}' not found.");
            }

            // Remove existing components
            _context.PageComponents.RemoveRange(page.Components);

            // Add new components
            var newComponents = request.Data.Components.Select((c, index) => new PageComponent
            {
                PageId = page.Id,
                ComponentType = c.ComponentType,
                ComponentName = c.ComponentName,
                Order = index,
                ContainerName = c.ContainerName,
                Settings = c.Settings,
                Content = c.Content,
                IsActive = c.IsActive
            }).ToList();

            page.Components = newComponents;

            await _context.SaveChangesAsync(cancellationToken);

            return _mapper.Map<PageDto>(page);
        }
    }
}
