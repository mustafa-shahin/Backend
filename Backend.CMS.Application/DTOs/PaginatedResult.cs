namespace Backend.CMS.Application.DTOs
{
    /// <summary>
    /// Represents a paginated result set with metadata about pagination
    /// </summary>
    /// <typeparam name="T">The type of items in the result set</typeparam>
    public class PaginatedResult<T>
    {
        /// <summary>
        /// The items for the current page
        /// </summary>
        public IReadOnlyList<T> Data { get; set; } = new List<T>();

        /// <summary>
        /// Current page number (1-based)
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Number of items per page
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of items across all pages
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

        /// <summary>
        /// Whether there is a previous page
        /// </summary>
        public bool HasPreviousPage => PageNumber > 1;

        /// <summary>
        /// Whether there is a next page
        /// </summary>
        public bool HasNextPage => PageNumber < TotalPages;

        /// <summary>
        /// Index of first item on current page (1-based)
        /// </summary>
        public int FirstItemIndex => PageSize > 0 && TotalCount > 0 ? ((PageNumber - 1) * PageSize) + 1 : 0;

        /// <summary>
        /// Index of last item on current page (1-based)
        /// </summary>
        public int LastItemIndex => Math.Min(PageNumber * PageSize, TotalCount);

        /// <summary>
        /// Creates an empty paged result
        /// </summary>
        public PaginatedResult()
        {
        }

        /// <summary>
        /// Creates a paged result with the specified parameters
        /// </summary>
        /// <param name="data">The items for the current page</param>
        /// <param name="pageNumber">Current page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="totalCount">Total number of items across all pages</param>
        public PaginatedResult(IReadOnlyList<T> data, int pageNumber, int pageSize, int totalCount)
        {
            Data = data ?? new List<T>();
            PageNumber = Math.Max(1, pageNumber);
            PageSize = Math.Max(1, pageSize);
            TotalCount = Math.Max(0, totalCount);
        }

        // Deconstruct method to allow for easy destructuring of the PagedResult object
        public void Deconstruct(out IReadOnlyList<T> data, out int totalCount)
        {
            data = Data;
            totalCount = TotalCount;
        }

        /// <summary>
        /// Creates a paged result from a list of items
        /// </summary>
        /// <param name="allItems">All items to paginate</param>
        /// <param name="pageNumber">Current page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <returns>A paged result containing the items for the specified page</returns>
        public static PaginatedResult<T> Create(IReadOnlyList<T> allItems, int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            var totalCount = allItems?.Count ?? 0;
            var skip = (pageNumber - 1) * pageSize;

            var data = allItems?.Skip(skip).Take(pageSize).ToList() ?? new List<T>();

            return new PaginatedResult<T>(data, pageNumber, pageSize, totalCount);
        }

        /// <summary>
        /// Creates an empty paged result
        /// </summary>
        /// <param name="pageNumber">Current page number</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>An empty paged result</returns>
        public static PaginatedResult<T> Empty(int pageNumber = 1, int pageSize = 10)
        {
            return new PaginatedResult<T>(new List<T>(), pageNumber, pageSize, 0);
        }

        /// <summary>
        /// Maps the current paged result to a new type
        /// </summary>
        /// <typeparam name="TDestination">The destination type</typeparam>
        /// <param name="mapper">Function to map from T to TDestination</param>
        /// <returns>A new paged result with mapped data</returns>
        public PaginatedResult<TDestination> Map<TDestination>(Func<T, TDestination> mapper)
        {
            var mappedData = Data.Select(mapper).ToList();
            return new PaginatedResult<TDestination>(mappedData, PageNumber, PageSize, TotalCount);
        }
    }

    /// <summary>
    /// Pagination request parameters
    /// </summary>
    public class PaginationRequest
    {
        private int _pageNumber = 1;
        private int _pageSize = 10;

        /// <summary>
        /// Page number (1-based)
        /// </summary>
        public int PageNumber
        {
            get => _pageNumber;
            set => _pageNumber = Math.Max(1, value);
        }

        /// <summary>
        /// Number of items per page (1-100)
        /// </summary>
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = Math.Clamp(value, 1, 100);
        }

        /// <summary>
        /// Creates a new pagination request
        /// </summary>
        public PaginationRequest() { }

        /// <summary>
        /// Creates a new pagination request with specified parameters
        /// </summary>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Page size (1-100)</param>
        public PaginationRequest(int pageNumber, int pageSize)
        {
            PageNumber = pageNumber;
            PageSize = pageSize;
        }
    }
}