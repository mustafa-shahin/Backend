using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Application.DTOs
{
    public class ImageFileDto : FileDto
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool HasThumbnail { get; set; }

        // Computed properties
        public string Dimensions => Width.HasValue && Height.HasValue 
            ? $"{Width}x{Height}" 
            : string.Empty;
    }
    
    public class UpdateImageDto : UpdateFileDto
    {
        public bool RegenerateThumbnail { get; set; }
    }

    public class ImageSearchDto : FileSearchDto
    {
        public int? MinWidth { get; set; }
        public int? MaxWidth { get; set; }
        public int? MinHeight { get; set; }
        public int? MaxHeight { get; set; }
    }
}
