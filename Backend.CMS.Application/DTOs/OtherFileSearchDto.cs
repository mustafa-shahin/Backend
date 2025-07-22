namespace Backend.CMS.Application.DTOs
{
    public class OtherFileSearchDto
    {
        public string? Name { get; set; }
        public int? FolderId { get; set; }
        public string? ContentType { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}