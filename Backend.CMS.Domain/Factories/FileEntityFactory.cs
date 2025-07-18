using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Domain.Factories
{
    public static class FileEntityFactory
    {
        /// <summary>
        /// Creates the appropriate file entity type based on content type and file extension
        /// </summary>
        /// <param name="contentType">MIME content type</param>
        /// <param name="fileExtension">File extension (with or without dot)</param>
        /// <returns>Appropriate file entity instance</returns>
        public static BaseFileEntity CreateFileEntity(string contentType, string fileExtension)
        {
            var fileType = DetermineFileType(contentType, fileExtension);
            
            return fileType switch
            {
                FileType.Image => new ImageFileEntity(),
                FileType.Video => new VideoFileEntity(),
                FileType.Audio => new AudioFileEntity(),
                FileType.Document => new DocumentFileEntity(),
                FileType.Archive => new ArchiveFileEntity(),
                FileType.Other => new OtherFileEntity(),
                _ => new OtherFileEntity()
            };
        }

        /// <summary>
        /// Creates the appropriate file entity type based on FileType enum
        /// </summary>
        /// <param name="fileType">File type enum</param>
        /// <returns>Appropriate file entity instance</returns>
        public static BaseFileEntity CreateFileEntity(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => new ImageFileEntity(),
                FileType.Video => new VideoFileEntity(),
                FileType.Audio => new AudioFileEntity(),
                FileType.Document => new DocumentFileEntity(),
                FileType.Archive => new ArchiveFileEntity(),
                FileType.Other => new OtherFileEntity(),
                _ => new OtherFileEntity()
            };
        }

        /// <summary>
        /// Determines the file type based on content type and extension
        /// </summary>
        /// <param name="contentType">MIME content type</param>
        /// <param name="fileExtension">File extension</param>
        /// <returns>FileType enum value</returns>
        public static FileType DetermineFileType(string contentType, string fileExtension)
        {
            // Normalize extension
            var extension = fileExtension?.TrimStart('.').ToLowerInvariant() ?? string.Empty;
            var mimeType = contentType?.ToLowerInvariant() ?? string.Empty;

            // Check MIME type first
            if (mimeType.StartsWith("image/"))
                return FileType.Image;

            if (mimeType.StartsWith("video/"))
                return FileType.Video;

            if (mimeType.StartsWith("audio/"))
                return FileType.Audio;

            // Check by extension for more specific detection
            return extension switch
            {
                // Image formats
                "jpg" or "jpeg" or "png" or "gif" or "bmp" or "webp" or "svg" or "tiff" or "tif" 
                or "ico" or "heic" or "heif" or "raw" or "cr2" or "nef" or "arw" or "dng" => FileType.Image,

                // Video formats
                "mp4" or "avi" or "mkv" or "mov" or "wmv" or "flv" or "webm" or "m4v" or "3gp" 
                or "mpg" or "mpeg" or "ts" or "vob" or "asf" or "rm" or "rmvb" => FileType.Video,

                // Audio formats
                "mp3" or "wav" or "flac" or "aac" or "ogg" or "wma" or "m4a" or "opus" or "ape" 
                or "ac3" or "dts" or "ra" or "au" or "aiff" or "amr" => FileType.Audio,

                // Document formats
                "pdf" or "doc" or "docx" or "xls" or "xlsx" or "ppt" or "pptx" or "odt" or "ods" 
                or "odp" or "rtf" or "txt" or "csv" or "xml" or "html" or "htm" or "md" or "tex" 
                or "ps" or "eps" or "pub" or "pages" or "numbers" or "key" => FileType.Document,

                // Archive formats
                "zip" or "rar" or "7z" or "tar" or "gz" or "bz2" or "xz" or "lz" or "z" or "lzma" 
                or "cab" or "iso" or "dmg" or "deb" or "rpm" or "msi" or "apk" or "jar" 
                or "war" or "ear" => FileType.Archive,

                // Default to Other
                _ => DetermineByMimeType(mimeType) ?? FileType.Other
            };
        }

        /// <summary>
        /// Fallback method to determine file type by MIME type patterns
        /// </summary>
        private static FileType? DetermineByMimeType(string mimeType)
        {
            return mimeType switch
            {
                var mt when mt.Contains("pdf") => FileType.Document,
                var mt when mt.Contains("msword") => FileType.Document,
                var mt when mt.Contains("officedocument") => FileType.Document,
                var mt when mt.Contains("opendocument") => FileType.Document,
                var mt when mt.Contains("text") => FileType.Document,
                var mt when mt.Contains("zip") => FileType.Archive,
                var mt when mt.Contains("compressed") => FileType.Archive,
                var mt when mt.Contains("archive") => FileType.Archive,
                _ => null
            };
        }

        /// <summary>
        /// Gets the appropriate file type for a given file extension
        /// </summary>
        /// <param name="fileExtension">File extension</param>
        /// <returns>FileType enum value</returns>
        public static FileType GetFileTypeByExtension(string fileExtension)
        {
            return DetermineFileType(string.Empty, fileExtension);
        }

        /// <summary>
        /// Gets the appropriate file type for a given MIME type
        /// </summary>
        /// <param name="contentType">MIME content type</param>
        /// <returns>FileType enum value</returns>
        public static FileType GetFileTypeByMimeType(string contentType)
        {
            return DetermineFileType(contentType, string.Empty);
        }

        /// <summary>
        /// Checks if a file type supports thumbnails
        /// </summary>
        /// <param name="fileType">File type to check</param>
        /// <returns>True if thumbnails are supported</returns>
        public static bool SupportsThumbnails(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => true,
                FileType.Video => true,
                FileType.Document => true,
                _ => false
            };
        }

        /// <summary>
        /// Checks if a file type supports preview
        /// </summary>
        /// <param name="fileType">File type to check</param>
        /// <returns>True if preview is supported</returns>
        public static bool SupportsPreview(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => true,
                FileType.Video => true,
                FileType.Audio => true,
                FileType.Document => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets common file extensions for a file type
        /// </summary>
        /// <param name="fileType">File type</param>
        /// <returns>Array of common extensions</returns>
        public static string[] GetCommonExtensions(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => ["jpg", "jpeg", "png", "gif", "bmp", "webp", "svg"],
                FileType.Video => ["mp4", "avi", "mkv", "mov", "wmv", "webm"],
                FileType.Audio => ["mp3", "wav", "flac", "aac", "ogg", "m4a"],
                FileType.Document => ["pdf", "doc", "docx", "txt", "rtf", "html"],
                FileType.Archive => ["zip", "rar", "7z", "tar", "gz"],
                FileType.Other => [],
                _ => []
            };
        }
    }
}