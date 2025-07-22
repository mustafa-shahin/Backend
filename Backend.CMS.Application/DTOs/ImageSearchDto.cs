namespace Backend.CMS.Application.DTOs
{
    public class ImageSearchDto
    {
        public string? Name { get; set; }
        public int? FolderId { get; set; }
        public int? MinWidth { get; set; }
        public int? MaxWidth { get; set; }
        public int? MinHeight { get; set; }
        public int? MaxHeight { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}