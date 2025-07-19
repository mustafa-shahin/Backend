using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Application.DTOs
{
    public class AudioFileDto : FileDto
    {
        public TimeSpan? Duration { get; set; }

        // Computed properties
        public string FormattedDuration => Duration?.ToString(@"mm\:ss") ?? string.Empty;
    }

    public class UpdateAudioDto : UpdateFileDto
    {
    }

    public class AudioSearchDto : FileSearchDto
    {
        public TimeSpan? MinDuration { get; set; }
        public TimeSpan? MaxDuration { get; set; }
    }
}
