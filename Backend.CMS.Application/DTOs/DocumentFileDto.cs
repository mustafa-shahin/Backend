using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Application.DTOs
{
    public class DocumentFileDto : FileDto
    {
        public int? PageCount { get; set; }
        public bool HasThumbnail { get; set; }
    }

    public class UpdateDocumentDto : UpdateFileDto
    {
        public bool RegenerateThumbnail { get; set; }
    }

    public class DocumentSearchDto : FileSearchDto
    {
        public int? MinPageCount { get; set; }
        public int? MaxPageCount { get; set; }
        public bool? HasThumbnail { get; set; }
    }
}
