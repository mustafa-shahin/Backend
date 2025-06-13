

namespace Backend.CMS.Application.DTOs
{
    public class IndexingJobsResponseDto
    {
        public List<IndexingJobDto> Jobs { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class IndexingJobDto
    {
        public int Id { get; set; }
        public string JobType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalEntities { get; set; }
        public int ProcessedEntities { get; set; }
        public int FailedEntities { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan? Duration { get; set; }
        public int ProgressPercentage { get; set; }
    }

    public class IndexingJobDetailDto : IndexingJobDto
    {
        public Dictionary<string, object> JobMetadata { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class TriggerJobResponseDto
    {
        public string JobId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string JobType { get; set; } = string.Empty;
    }

    public class TriggerIncrementalIndexDto
    {
        public DateTime? Since { get; set; }
    }

    public class TriggerEntityIndexDto
    {
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
    }

    public class IndexingJobStatisticsDto
    {
        public int TotalJobs { get; set; }
        public int RunningJobs { get; set; }
        public int CompletedJobs { get; set; }
        public int FailedJobs { get; set; }
        public int JobsLast24Hours { get; set; }
        public int JobsLast7Days { get; set; }
        public int JobsLast30Days { get; set; }
        public DateTime? LastFullIndex { get; set; }
        public DateTime? LastIncrementalIndex { get; set; }
        public TimeSpan? AverageJobDuration { get; set; }
        public Dictionary<string, int> JobTypeBreakdown { get; set; } = new();
    }
    public class IndexingStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public DateTime? LastFullIndex { get; set; }
        public DateTime? LastIncrementalIndex { get; set; }
        public int TotalIndexedEntities { get; set; }
        public DateTime? NextScheduledIndex { get; set; }
        public List<IndexingJobDto> RecentJobs { get; set; } = new();
    }
    public class IndexEntityRequestDto
    {
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
    }
}
