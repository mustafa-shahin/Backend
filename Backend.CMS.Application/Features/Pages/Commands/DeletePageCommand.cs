using MediatR;
using Microsoft.EntityFrameworkCore;
using Backend.CMS.Infrastructure.Data;

namespace Backend.CMS.Application.Features.Pages.Commands
{
    public class DeletePageCommand : IRequest<bool>
    {
        public Guid PageId { get; set; }
    }

    public class DeletePageCommandHandler : IRequestHandler<DeletePageCommand, bool>
    {
        private readonly CmsDbContext _context;

        public DeletePageCommandHandler(CmsDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(DeletePageCommand request, CancellationToken cancellationToken)
        {
            var page = await _context.Pages
                .Include(p => p.ChildPages)
                .FirstOrDefaultAsync(p => p.Id == request.PageId, cancellationToken);

            if (page == null)
            {
                return false;
            }

            if (page.ChildPages.Any())
            {
                throw new InvalidOperationException("Cannot delete page with child pages.");
            }

            page.IsDeleted = true;
            page.DeletedOn = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
