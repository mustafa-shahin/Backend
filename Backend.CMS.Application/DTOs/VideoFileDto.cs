using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Application.DTOs
{
    public class VideoFileDto : FileDto
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool HasThumbnail { get; set; }

        // Computed properties
        public string Resolution => Width.HasValue && Height.HasValue ? $"{Width}x{Height}" : string.Empty;
        public string FormattedDuration => Duration?.ToString(@"hh\:mm\:ss") ?? string.Empty;
    }

    public class UpdateVideoDto : UpdateFileDto
    {
        public bool RegenerateThumbnail { get; set; }
    }

    public class VideoSearchDto : FileSearchDto
    {
        public int? MinWidth { get; set; }
        public int? MaxWidth { get; set; }
        public int? MinHeight { get; set; }
        public int? MaxHeight { get; set; }
        public TimeSpan? MinDuration { get; set; }
        public TimeSpan? MaxDuration { get; set; }
        public bool? HasThumbnail { get; set; }
    }
}
