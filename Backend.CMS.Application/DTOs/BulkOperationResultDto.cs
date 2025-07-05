using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Application.DTOs
{

    /// <summary>
    /// Bulk operation result DTO
    /// </summary>
    public class BulkOperationResultDto
    {
        public int TotalRequested { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<CategoryDto> SuccessfulCategories { get; set; } = new();
        public List<LocationDto> SuccessfulLocations { get; set; } = new();
        public List<object> SuccessfulFiles { get; set; } = new();
        public List<BulkOperationErrorDto> Errors { get; set; } = new();
        public bool IsPartialSuccess => SuccessCount > 0 && FailureCount > 0;
        public bool IsCompleteSuccess => SuccessCount > 0 && FailureCount == 0;
        public bool IsCompleteFailure => SuccessCount == 0 && FailureCount > 0;
        public double SuccessRate => TotalRequested > 0 ? (double)SuccessCount / TotalRequested : 0;
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Bulk operation error DTO
    /// </summary>
    public class BulkOperationErrorDto
    {
        public int? EntityId { get; set; }
        public string? EntityType { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

}
