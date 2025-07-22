namespace Backend.CMS.Application.DTOs
{
    public class ArchiveSearchDto
    {
        public string? Name { get; set; }
        public int? FolderId { get; set; }
        public string? ContentType { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}