namespace Backend.CMS.Application.DTOs
{
    public class SearchRequestDto
    {
        public string Query { get; set; } = string.Empty;
        public List<string> EntityTypes { get; set; } = new(); // Page, File, User, etc.
        public bool PublicOnly { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public Dictionary<string, object> Filters { get; set; } = new();
    }

    public class SearchResultDto
    {
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public float Score { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime LastModified { get; set; }
    }

    public class SearchResponseDto
    {
        public List<SearchResultDto> Results { get; set; } = new();
        public int TotalResults { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalResults / PageSize);
        public string Query { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
    }

}