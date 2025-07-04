namespace Backend.CMS.Application.DTOs
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public PaginationMetadata Pagination { get; set; } = new();
        public SearchMetadata? SearchInfo { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class PaginationMetadata
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
        public int? PreviousPage { get; set; }
        public int? NextPage { get; set; }
        public int FirstPage { get; set; } = 1;
        public int LastPage { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public PageSizeInfo PageSizeInfo { get; set; } = new();
    }

    public class PageSizeInfo
    {
        public int Requested { get; set; }
        public int Actual { get; set; }
        public int Minimum { get; set; } = 5;
        public int Maximum { get; set; } = 100;
        public int Default { get; set; } = 10;
        public List<int> AvailableSizes { get; set; } = new() { 5, 10, 20, 50, 100 };
        public bool IsOptimal { get; set; }
        public string OptimizationReason { get; set; } = string.Empty;
    }

    public class SearchMetadata
    {
        public string? SearchTerm { get; set; }
        public Dictionary<string, object> Filters { get; set; } = new();
        public string SortBy { get; set; } = string.Empty;
        public string SortDirection { get; set; } = string.Empty;
        public int FilteredCount { get; set; }
        public int UnfilteredCount { get; set; }
        public TimeSpan SearchDuration { get; set; }
        public bool HasFilters { get; set; }
    }

    // Extension methods for easier pagination creation
    public static class PagedResultExtensions
    {
        public static PagedResult<T> ToPagedResult<T>(
            this IEnumerable<T> items,
            int page,
            int pageSize,
            int totalCount,
            SearchMetadata? searchInfo = null,
            Dictionary<string, object>? metadata = null)
        {
            var itemsList = items.ToList();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return new PagedResult<T>
            {
                Items = itemsList,
                Pagination = new PaginationMetadata
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = totalPages,
                    HasPreviousPage = page > 1,
                    HasNextPage = page < totalPages,
                    PreviousPage = page > 1 ? page - 1 : null,
                    NextPage = page < totalPages ? page + 1 : null,
                    FirstPage = 1,
                    LastPage = totalPages,
                    StartIndex = ((page - 1) * pageSize) + 1,
                    EndIndex = Math.Min(page * pageSize, totalCount),
                    PageSizeInfo = new PageSizeInfo
                    {
                        Requested = pageSize,
                        Actual = itemsList.Count,
                        IsOptimal = true
                    }
                },
                SearchInfo = searchInfo,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
        }

        public static PagedResult<TDestination> Map<TSource, TDestination>(
            this PagedResult<TSource> source,
            Func<TSource, TDestination> mapper)
        {
            return new PagedResult<TDestination>
            {
                Items = source.Items.Select(mapper).ToList(),
                Pagination = source.Pagination,
                SearchInfo = source.SearchInfo,
                Metadata = source.Metadata
            };
        }
    }

    // Pagination configuration
    public class PaginationConfiguration
    {
        public int DefaultPageSize { get; set; } = 10;
        public int MinPageSize { get; set; } = 5;
        public int MaxPageSize { get; set; } = 100;
        public List<int> AllowedPageSizes { get; set; } = new() { 5, 10, 20, 50, 100 };
        public bool EnableDynamicPageSize { get; set; } = true;
        public int OptimalItemsPerRequest { get; set; } = 10;
        public bool EnablePageSizeOptimization { get; set; } = true;
    }
}