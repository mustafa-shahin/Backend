namespace Backend.CMS.Application.DTOs
{
    public class VideoSearchDto
    {
        public string? Name { get; set; }
        public int? FolderId { get; set; }
        public int? MinDuration { get; set; }
        public int? MaxDuration { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}