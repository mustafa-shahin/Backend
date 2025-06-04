using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Backend.CMS.Application.Common.Models;
using Backend.CMS.Application.Features.Pages.DTOs;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Data;

namespace Backend.CMS.Application.Features.Pages.Queries
{
    public class GetPagesQuery : IRequest<PagedResult<PageListDto>>
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SearchTerm { get; set; }
        public PageStatus? Status { get; set; }
        public Guid? ParentPageId { get; set; }
        public string? SortBy { get; set; } = "CreatedOn";
        public bool SortDescending { get; set; } = true;
    }

    public class GetPagesQueryHandler : IRequestHandler<GetPagesQuery, PagedResult<PageListDto>>
    {
        private readonly CmsDbContext _context;
        private readonly IMapper _mapper;

        public GetPagesQueryHandler(CmsDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<PagedResult<PageListDto>> Handle(GetPagesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Pages
                .Include(p => p.ParentPage)
                .Where(p => !p.IsDeleted);

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                query = query.Where(p =>
                    p.Name.Contains(request.SearchTerm) ||
                    p.Title.Contains(request.SearchTerm) ||
                    p.Slug.Contains(request.SearchTerm));
            }

            if (request.Status.HasValue)
            {
                query = query.Where(p => p.Status == request.Status.Value);
            }

            if (request.ParentPageId.HasValue)
            {
                query = query.Where(p => p.ParentPageId == request.ParentPageId.Value);
            }

            // Apply sorting
            query = request.SortBy?.ToLower() switch
            {
                "name" => request.SortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                "title" => request.SortDescending ? query.OrderByDescending(p => p.Title) : query.OrderBy(p => p.Title),
                "status" => request.SortDescending ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
                "publishedon" => request.SortDescending ? query.OrderByDescending(p => p.PublishedOn) : query.OrderBy(p => p.PublishedOn),
                _ => request.SortDescending ? query.OrderByDescending(p => p.CreatedOn) : query.OrderBy(p => p.CreatedOn),
            };

            // Get total count
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply pagination
            var pages = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(p => new PageListDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Title = p.Title,
                    Slug = p.Slug,
                    Status = p.Status,
                    CreatedOn = p.CreatedOn,
                    PublishedOn = p.PublishedOn,
                    ParentPageName = p.ParentPage != null ? p.ParentPage.Name : null
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<PageListDto>
            {
                Items = pages,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }
    }
}
