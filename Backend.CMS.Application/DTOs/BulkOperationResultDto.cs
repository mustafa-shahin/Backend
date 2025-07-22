namespace Backend.CMS.Application.DTOs
{
    public class BulkOperationResultDto
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<BulkOperationErrorDto> DetailedErrors { get; set; } = new();
        public bool IsSuccess => FailureCount == 0;
        
        public int TotalRequested { get; set; }
        public List<FileDto> SuccessfulFiles { get; set; } = new();
        public bool IsPartialSuccess => SuccessCount > 0 && FailureCount > 0;
        public bool IsCompleteSuccess => SuccessCount > 0 && FailureCount == 0;
    }

    public class BulkOperationErrorDto
    {
        public string FileName { get; set; } = string.Empty;
        public int? FileId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public Exception? Exception { get; set; }
    }
}