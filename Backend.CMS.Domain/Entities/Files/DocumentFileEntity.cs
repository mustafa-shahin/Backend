using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Domain.Entities.Files
{
    public class DocumentFileEntity : BaseFileEntity
    {
        public override FileType FileType => FileType.Document;

        public int? PageCount { get; set; }

        [MaxLength(100)]
        public string? Author { get; set; }

        [MaxLength(200)]
        public string? DocumentTitle { get; set; }

        [MaxLength(500)]
        public string? Subject { get; set; }

        [MaxLength(1000)]
        public string? Keywords { get; set; }

        [MaxLength(100)]
        public string? Creator { get; set; }

        [MaxLength(100)]
        public string? Producer { get; set; }

        public DateTime? CreationDate { get; set; }

        public DateTime? ModificationDate { get; set; }

        [MaxLength(20)]
        public string? DocumentVersion { get; set; }

        public bool IsPasswordProtected { get; set; } = false;

        public bool AllowPrinting { get; set; } = true;

        public bool AllowCopying { get; set; } = true;

        public bool AllowModification { get; set; } = true;

        public bool IsDigitallySigned { get; set; } = false;

        [MaxLength(100)]
        public string? SignatureAuthor { get; set; }

        public DateTime? SignatureDate { get; set; }

        public bool HasComments { get; set; } = false;

        public bool HasAnnotations { get; set; } = false;

        public bool HasBookmarks { get; set; } = false;

        public bool HasForms { get; set; } = false;

        public bool HasEmbeddedFiles { get; set; } = false;

        [MaxLength(50)]
        public string? Language { get; set; }

        public byte[]? ThumbnailContent { get; set; }

        public int? ThumbnailPageNumber { get; set; } = 1;

        public long? WordCount { get; set; }

        public long? CharacterCount { get; set; }

        public long? ParagraphCount { get; set; }

        public long? LineCount { get; set; }

        [MaxLength(50)]
        public string? DocumentFormat { get; set; }

        public bool IsOptimizedForWeb { get; set; } = false;

        // Document-specific validation
        public override ValidationResult ValidateFileType()
        {
            var errors = new List<string>();

            if (PageCount.HasValue && PageCount <= 0)
                errors.Add("Page count must be greater than 0");

            if (ThumbnailPageNumber.HasValue && PageCount.HasValue && ThumbnailPageNumber > PageCount)
                errors.Add("Thumbnail page number cannot exceed total page count");

            if (CreationDate.HasValue && CreationDate > DateTime.UtcNow)
                errors.Add("Creation date cannot be in the future");

            if (ModificationDate.HasValue && ModificationDate > DateTime.UtcNow)
                errors.Add("Modification date cannot be in the future");

            if (CreationDate.HasValue && ModificationDate.HasValue && ModificationDate < CreationDate)
                errors.Add("Modification date cannot be earlier than creation date");

            if (WordCount.HasValue && WordCount < 0)
                errors.Add("Word count cannot be negative");

            if (CharacterCount.HasValue && CharacterCount < 0)
                errors.Add("Character count cannot be negative");

            return errors.Any() 
                ? new ValidationResult(string.Join("; ", errors))
                : ValidationResult.Success!;
        }

        // Document-specific processing
        public override async Task<bool> ProcessFileAsync()
        {
            try
            {
                await ExtractDocumentMetadataAsync();
                await GenerateDocumentThumbnailAsync();
                await ExtractTextContentAsync();
                await AnalyzeDocumentSecurityAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task ExtractDocumentMetadataAsync()
        {
            // Implementation for extracting document metadata
            await Task.CompletedTask;
        }

        private async Task GenerateDocumentThumbnailAsync()
        {
            // Implementation for generating document thumbnail (first page)
            await Task.CompletedTask;
        }

        private async Task ExtractTextContentAsync()
        {
            // Implementation for extracting text content for search indexing
            await Task.CompletedTask;
        }

        private async Task AnalyzeDocumentSecurityAsync()
        {
            // Implementation for analyzing document security features
            await Task.CompletedTask;
        }

        // Helper properties
        public string DocumentInfo
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(DocumentTitle)) parts.Add(DocumentTitle);
                if (!string.IsNullOrEmpty(Author)) parts.Add($"by {Author}");
                if (PageCount.HasValue) parts.Add($"{PageCount} pages");
                return parts.Any() ? string.Join(", ", parts) : OriginalFileName;
            }
        }

        public string SecurityInfo
        {
            get
            {
                var features = new List<string>();
                if (IsPasswordProtected) features.Add("Password Protected");
                if (IsDigitallySigned) features.Add("Digitally Signed");
                if (!AllowPrinting) features.Add("Printing Restricted");
                if (!AllowCopying) features.Add("Copying Restricted");
                if (!AllowModification) features.Add("Modification Restricted");
                return features.Any() ? string.Join(", ", features) : "No Restrictions";
            }
        }

        public string ContentInfo
        {
            get
            {
                var info = new List<string>();
                if (WordCount.HasValue) info.Add($"{WordCount:N0} words");
                if (CharacterCount.HasValue) info.Add($"{CharacterCount:N0} characters");
                if (ParagraphCount.HasValue) info.Add($"{ParagraphCount:N0} paragraphs");
                return info.Any() ? string.Join(", ", info) : string.Empty;
            }
        }

        public string FeaturesInfo
        {
            get
            {
                var features = new List<string>();
                if (HasComments) features.Add("Comments");
                if (HasAnnotations) features.Add("Annotations");
                if (HasBookmarks) features.Add("Bookmarks");
                if (HasForms) features.Add("Forms");
                if (HasEmbeddedFiles) features.Add("Embedded Files");
                return features.Any() ? string.Join(", ", features) : "None";
            }
        }

        public bool HasThumbnail => ThumbnailContent?.Length > 0;

        public string FormattedFileSize
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
                if (FileSize < 1024 * 1024 * 1024) return $"{FileSize / (1024.0 * 1024):F1} MB";
                return $"{FileSize / (1024.0 * 1024 * 1024):F1} GB";
            }
        }
    }
}