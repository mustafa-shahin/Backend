using Backend.CMS.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Backend.CMS.Domain.Entities.Files
{
    public class OtherFileEntity : BaseFileEntity
    {
        public override FileType FileType => FileType.Other;

        [MaxLength(100)]
        public string? ApplicationName { get; set; }

        [MaxLength(50)]
        public string? ApplicationVersion { get; set; }

        public bool IsExecutable { get; set; } = false;

        public bool IsScript { get; set; } = false;

        [MaxLength(50)]
        public string? ScriptLanguage { get; set; }

        public bool IsText { get; set; } = false;

        public bool IsBinary { get; set; } = true;

        [MaxLength(50)]
        public string? Encoding { get; set; }

        public long? LineCount { get; set; }

        public bool HasDigitalSignature { get; set; } = false;

        [MaxLength(100)]
        public string? SignaturePublisher { get; set; }

        public DateTime? SignatureDate { get; set; }

        public bool SignatureValid { get; set; } = false;

        [MaxLength(100)]
        public string? FileFormat { get; set; }

        [MaxLength(50)]
        public string? FormatVersion { get; set; }

        public bool IsCompressed { get; set; } = false;

        public bool IsEncrypted { get; set; } = false;

        [MaxLength(50)]
        public string? EncryptionMethod { get; set; }

        public bool RequiresSpecialSoftware { get; set; } = false;

        [MaxLength(200)]
        public string? RequiredSoftware { get; set; }

        [MaxLength(1000)]
        public string? FileTypeDescription { get; set; }

        public bool IsPotentiallyDangerous { get; set; } = false;

        [MaxLength(500)]
        public string? SecurityWarning { get; set; }

        public bool IsSourceCode { get; set; } = false;

        [MaxLength(50)]
        public string? ProgrammingLanguage { get; set; }

        public bool IsDatabase { get; set; } = false;

        [MaxLength(50)]
        public string? DatabaseType { get; set; }

        public bool IsConfiguration { get; set; } = false;

        public bool IsLog { get; set; } = false;

        // Security analysis properties
        public bool IsSuspicious { get; set; } = false;

        public bool HasMacros { get; set; } = false;

        public string? SecurityAnalysisResult { get; set; }

        public DateTime? SecurityScanDate { get; set; }

        [MaxLength(50)]
        public string? ThreatLevel { get; set; }

        [MaxLength(200)]
        public string? DetectedFileType { get; set; }

        [MaxLength(200)]
        public string? MimeTypeDetected { get; set; }

        public bool RequiresSpecialHandling { get; set; } = false;

        // Other file type validation
        public override ValidationResult ValidateFileType()
        {
            var errors = new List<string>();

            if (LineCount.HasValue && LineCount < 0)
                errors.Add("Line count cannot be negative");

            if (SignatureDate.HasValue && SignatureDate > DateTime.UtcNow)
                errors.Add("Signature date cannot be in the future");

            if (HasDigitalSignature && string.IsNullOrEmpty(SignaturePublisher))
                errors.Add("Digital signature must have a publisher");

            if (IsScript && string.IsNullOrEmpty(ScriptLanguage))
                errors.Add("Script files must specify the script language");

            if (IsSourceCode && string.IsNullOrEmpty(ProgrammingLanguage))
                errors.Add("Source code files must specify the programming language");

            if (IsDatabase && string.IsNullOrEmpty(DatabaseType))
                errors.Add("Database files must specify the database type");

            return errors.Any() 
                ? new ValidationResult(string.Join("; ", errors))
                : ValidationResult.Success!;
        }

        // Other file type processing
        public override async Task<bool> ProcessFileAsync()
        {
            try
            {
                await AnalyzeFileFormatAsync();
                await CheckDigitalSignatureAsync();
                await PerformSecurityAnalysisAsync();
                await ExtractBasicMetadataAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task AnalyzeFileFormatAsync()
        {
            // Implementation for analyzing unknown file formats
            await Task.CompletedTask;
        }

        private async Task CheckDigitalSignatureAsync()
        {
            // Implementation for verifying digital signatures
            await Task.CompletedTask;
        }

        private async Task PerformSecurityAnalysisAsync()
        {
            // Implementation for security analysis of unknown file types
            await Task.CompletedTask;
        }

        private async Task ExtractBasicMetadataAsync()
        {
            // Implementation for extracting basic metadata
            await Task.CompletedTask;
        }

        // Helper properties
        public string FileCategory
        {
            get
            {
                if (IsExecutable) return "Executable";
                if (IsScript) return "Script";
                if (IsSourceCode) return "Source Code";
                if (IsDatabase) return "Database";
                if (IsConfiguration) return "Configuration";
                if (IsLog) return "Log File";
                if (IsText) return "Text File";
                return "Binary File";
            }
        }

        public string TechnicalInfo
        {
            get
            {
                var info = new List<string>();
                
                if (!string.IsNullOrEmpty(FileFormat)) info.Add(FileFormat);
                if (!string.IsNullOrEmpty(FormatVersion)) info.Add($"v{FormatVersion}");
                if (IsCompressed) info.Add("Compressed");
                if (IsEncrypted) info.Add("Encrypted");
                
                return info.Any() ? string.Join(", ", info) : "Unknown format";
            }
        }

        public string SecurityInfo
        {
            get
            {
                var info = new List<string>();
                
                if (HasDigitalSignature)
                {
                    var sigInfo = SignatureValid ? "Valid signature" : "Invalid signature";
                    if (!string.IsNullOrEmpty(SignaturePublisher)) sigInfo += $" ({SignaturePublisher})";
                    info.Add(sigInfo);
                }
                
                if (IsPotentiallyDangerous) info.Add("Potentially dangerous");
                if (IsEncrypted) info.Add($"Encrypted ({EncryptionMethod})");
                
                return info.Any() ? string.Join(", ", info) : "No security features";
            }
        }

        public string DevelopmentInfo
        {
            get
            {
                var info = new List<string>();
                
                if (IsSourceCode && !string.IsNullOrEmpty(ProgrammingLanguage)) 
                    info.Add($"{ProgrammingLanguage} source");
                    
                if (IsScript && !string.IsNullOrEmpty(ScriptLanguage)) 
                    info.Add($"{ScriptLanguage} script");
                    
                if (LineCount.HasValue) info.Add($"{LineCount:N0} lines");
                
                return info.Any() ? string.Join(", ", info) : string.Empty;
            }
        }

        public string UsageInfo
        {
            get
            {
                if (RequiresSpecialSoftware && !string.IsNullOrEmpty(RequiredSoftware))
                    return $"Requires: {RequiredSoftware}";
                    
                if (!string.IsNullOrEmpty(ApplicationName))
                {
                    var appInfo = ApplicationName;
                    if (!string.IsNullOrEmpty(ApplicationVersion)) appInfo += $" {ApplicationVersion}";
                    return $"Associated with: {appInfo}";
                }
                
                return string.Empty;
            }
        }

        public bool HasSecurityConcerns => IsPotentiallyDangerous || 
                                         (HasDigitalSignature && !SignatureValid) || 
                                         IsExecutable;

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

        public string SafetyRating
        {
            get
            {
                if (IsPotentiallyDangerous) return "Dangerous";
                if (IsExecutable) return "Caution Required";
                if (HasDigitalSignature && SignatureValid) return "Trusted";
                if (IsText || IsSourceCode) return "Safe";
                return "Unknown";
            }
        }
    }
}