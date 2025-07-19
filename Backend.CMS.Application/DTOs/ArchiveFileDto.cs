using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Application.DTOs
{
    public class ArchiveFileDto : FileDto
    {
        public int? FileCount { get; set; }
        public long? UncompressedSize { get; set; }

        // Computed properties
        public string SizeInfo
        {
            get
            {
                if (!UncompressedSize.HasValue) return FileSizeFormatted;

                var compressed = FileSizeFormatted;
                var uncompressed = FormatBytes(UncompressedSize.Value);
                return $"{compressed} (uncompressed: {uncompressed})";
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }

    public class UpdateArchiveDto : UpdateFileDto
    {
    }

    public class ArchiveSearchDto : FileSearchDto
    {
        public int? MinFileCount { get; set; }
        public int? MaxFileCount { get; set; }
        public long? MinUncompressedSize { get; set; }
        public long? MaxUncompressedSize { get; set; }
    }
}
