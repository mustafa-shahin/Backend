using Asp.Versioning;
using Backend.CMS.API.Authorization;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Extensions;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [AdminOrDev]
    public class IndexingJobController : ControllerBase
    {
        private readonly IIndexingService _indexingService;
        private readonly IRepository<IndexingJob> _indexingJobRepository;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<IndexingJobController> _logger;

        public IndexingJobController(
            IIndexingService indexingService,
            IRepository<IndexingJob> indexingJobRepository,
            IBackgroundJobClient backgroundJobClient,
            ILogger<IndexingJobController> logger)
        {
            _indexingService = indexingService;
            _indexingJobRepository = indexingJobRepository;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
        }

        /// <summary>
        /// Get all indexing jobs with pagination and filtering
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IndexingJobsResponseDto>> GetIndexingJobs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? jobType = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var query = _indexingJobRepository.GetQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(j => j.Status.ToLower() == status.ToLower());
                }

                if (!string.IsNullOrEmpty(jobType))
                {
                    query = query.Where(j => j.JobType.ToLower() == jobType.ToLower());
                }

                if (startDate.HasValue)
                {
                    query = query.Where(j => j.StartedAt >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(j => j.StartedAt <= endDate.Value.AddDays(1));
                }

                // Order by StartedAt descending
                query = query.OrderByDescending(j => j.StartedAt);

                var totalCount = await _indexingJobRepository.CountAsync(query);
                var jobs = await _indexingJobRepository.GetPagedAsync(query, page, pageSize);

                var jobDtos = jobs.Select(MapToIndexingJobDto).ToList();

                return Ok(new IndexingJobsResponseDto
                {
                    Jobs = jobDtos,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving indexing jobs");
                return StatusCode(500, new { Message = "An error occurred while retrieving indexing jobs" });
            }
        }

        /// <summary>
        /// Get specific indexing job by ID
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<IndexingJobDetailDto>> GetIndexingJob(int id)
        {
            try
            {
                var job = await _indexingJobRepository.GetByIdAsync(id);
                if (job == null || job.IsDeleted)
                {
                    return NotFound(new { Message = "Indexing job not found" });
                }

                var jobDto = MapToIndexingJobDetailDto(job);
                return Ok(jobDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving indexing job {JobId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrieving the indexing job" });
            }
        }

        /// <summary>
        /// Trigger full reindex
        /// </summary>
        [HttpPost("trigger/full")]
        public async Task<ActionResult<TriggerJobResponseDto>> TriggerFullReindex()
        {
            try
            {
                var jobId = _backgroundJobClient.Enqueue<IndexingBackgroundJob>(job => job.RunFullIndexAsync());

                _logger.LogInformation("Full reindex job triggered with ID: {JobId} by user {UserId}",
                    jobId, GetCurrentUserId());

                return Ok(new TriggerJobResponseDto
                {
                    JobId = jobId,
                    Message = "Full reindex job has been queued successfully",
                    JobType = "Full"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering full reindex");
                return StatusCode(500, new { Message = "An error occurred while triggering full reindex" });
            }
        }

        /// <summary>
        /// Trigger incremental index
        /// </summary>
        [HttpPost("trigger/incremental")]
        public async Task<ActionResult<TriggerJobResponseDto>> TriggerIncrementalIndex([FromBody] TriggerIncrementalIndexDto? request = null)
        {
            try
            {
                var since = request?.Since ?? DateTime.UtcNow.AddHours(-1);
                var jobId = _backgroundJobClient.Enqueue<IndexingBackgroundJob>(job => job.RunIncrementalIndexAsync());

                _logger.LogInformation("Incremental index job triggered with ID: {JobId} for changes since {Since} by user {UserId}",
                    jobId, since, GetCurrentUserId());

                return Ok(new TriggerJobResponseDto
                {
                    JobId = jobId,
                    Message = $"Incremental index job has been queued successfully for changes since {since:yyyy-MM-dd HH:mm}",
                    JobType = "Incremental"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering incremental index");
                return StatusCode(500, new { Message = "An error occurred while triggering incremental index" });
            }
        }

        /// <summary>
        /// Trigger indexing for specific entity
        /// </summary>
        [HttpPost("trigger/entity")]
        public async Task<ActionResult<TriggerJobResponseDto>> TriggerEntityIndex([FromBody] TriggerEntityIndexDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.EntityType) || request.EntityId <= 0)
                {
                    return BadRequest(new { Message = "EntityType and EntityId are required" });
                }

                var validEntityTypes = new[] { "page", "file", "user", "componenttemplate" };
                if (!validEntityTypes.Contains(request.EntityType.ToLower()))
                {
                    return BadRequest(new { Message = $"Invalid entity type. Valid types are: {string.Join(", ", validEntityTypes)}" });
                }

                var jobId = _backgroundJobClient.Enqueue<IndexingBackgroundJob>(
                    job => job.IndexEntityAsync(request.EntityType, request.EntityId));

                _logger.LogInformation("Entity index job triggered with ID: {JobId} for {EntityType} {EntityId} by user {UserId}",
                    jobId, request.EntityType, request.EntityId, GetCurrentUserId());

                return Ok(new TriggerJobResponseDto
                {
                    JobId = jobId,
                    Message = $"Entity index job has been queued successfully for {request.EntityType} {request.EntityId}",
                    JobType = "EntitySpecific"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering entity index for {EntityType} {EntityId}",
                    request?.EntityType, request?.EntityId);
                return StatusCode(500, new { Message = "An error occurred while triggering entity index" });
            }
        }

        /// <summary>
        /// Get indexing job statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<IndexingJobStatisticsDto>> GetIndexingJobStatistics()
        {
            try
            {
                var now = DateTime.UtcNow;
                var last24Hours = now.AddDays(-1);
                var last7Days = now.AddDays(-7);
                var last30Days = now.AddDays(-30);

                var allJobs = await _indexingJobRepository.GetAllAsync();
                var jobsList = allJobs.ToList();

                var statistics = new IndexingJobStatisticsDto
                {
                    TotalJobs = jobsList.Count,
                    RunningJobs = jobsList.Count(j => j.Status == "Running" || j.Status == "Pending"),
                    CompletedJobs = jobsList.Count(j => j.Status == "Completed"),
                    FailedJobs = jobsList.Count(j => j.Status == "Failed"),
                    JobsLast24Hours = jobsList.Count(j => j.StartedAt >= last24Hours),
                    JobsLast7Days = jobsList.Count(j => j.StartedAt >= last7Days),
                    JobsLast30Days = jobsList.Count(j => j.StartedAt >= last30Days),
                    LastFullIndex = jobsList
                        .Where(j => j.JobType == "Full" && j.Status == "Completed")
                        .OrderByDescending(j => j.CompletedAt)
                        .FirstOrDefault()?.CompletedAt,
                    LastIncrementalIndex = jobsList
                        .Where(j => j.JobType == "Incremental" && j.Status == "Completed")
                        .OrderByDescending(j => j.CompletedAt)
                        .FirstOrDefault()?.CompletedAt,
                    AverageJobDuration = CalculateAverageJobDuration(jobsList.Where(j => j.CompletedAt.HasValue)),
                    JobTypeBreakdown = jobsList
                        .GroupBy(j => j.JobType)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting indexing job statistics");
                return StatusCode(500, new { Message = "An error occurred while retrieving statistics" });
            }
        }

        /// <summary>
        /// Get recent indexing jobs (for dashboard)
        /// </summary>
        [HttpGet("recent")]
        public async Task<ActionResult<List<IndexingJobDto>>> GetRecentIndexingJobs([FromQuery] int count = 10)
        {
            try
            {
                var recentJobs = await _indexingJobRepository.FindAsync(
                    j => true, // No filter, get all
                    orderBy: j => j.OrderByDescending(x => x.StartedAt),
                    take: count);

                var jobDtos = recentJobs.Select(MapToIndexingJobDto).ToList();
                return Ok(jobDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent indexing jobs");
                return StatusCode(500, new { Message = "An error occurred while retrieving recent jobs" });
            }
        }

        /// <summary>
        /// Cancel a pending or running indexing job
        /// </summary>
        [HttpPost("{id:int}/cancel")]
        [DevOnly] // Only devs can cancel jobs
        public async Task<ActionResult> CancelIndexingJob(int id)
        {
            try
            {
                var job = await _indexingJobRepository.GetByIdAsync(id);
                if (job == null || job.IsDeleted)
                {
                    return NotFound(new { Message = "Indexing job not found" });
                }

                if (job.Status != "Pending" && job.Status != "Running")
                {
                    return BadRequest(new { Message = "Only pending or running jobs can be cancelled" });
                }

                // Update job status
                await _indexingService.UpdateIndexingJobAsync(id, "Cancelled",
                    errorMessage: $"Job cancelled by user {GetCurrentUserId()} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");

                _logger.LogInformation("Indexing job {JobId} cancelled by user {UserId}", id, GetCurrentUserId());

                return Ok(new { Message = "Indexing job cancelled successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling indexing job {JobId}", id);
                return StatusCode(500, new { Message = "An error occurred while cancelling the job" });
            }
        }

        /// <summary>
        /// Retry a failed indexing job
        /// </summary>
        [HttpPost("{id:int}/retry")]
        public async Task<ActionResult<TriggerJobResponseDto>> RetryIndexingJob(int id)
        {
            try
            {
                var job = await _indexingJobRepository.GetByIdAsync(id);
                if (job == null || job.IsDeleted)
                {
                    return NotFound(new { Message = "Indexing job not found" });
                }

                if (job.Status != "Failed")
                {
                    return BadRequest(new { Message = "Only failed jobs can be retried" });
                }

                string newJobId;
                switch (job.JobType.ToLower())
                {
                    case "full":
                        newJobId = _backgroundJobClient.Enqueue<IndexingBackgroundJob>(j => j.RunFullIndexAsync());
                        break;
                    case "incremental":
                        newJobId = _backgroundJobClient.Enqueue<IndexingBackgroundJob>(j => j.RunIncrementalIndexAsync());
                        break;
                    default:
                        return BadRequest(new { Message = "Cannot retry this job type" });
                }

                _logger.LogInformation("Indexing job {JobId} retried as new job {NewJobId} by user {UserId}",
                    id, newJobId, GetCurrentUserId());

                return Ok(new TriggerJobResponseDto
                {
                    JobId = newJobId,
                    Message = $"Job has been retried successfully. New job ID: {newJobId}",
                    JobType = job.JobType
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrying indexing job {JobId}", id);
                return StatusCode(500, new { Message = "An error occurred while retrying the job" });
            }
        }

        /// <summary>
        /// Delete old completed/failed indexing jobs
        /// </summary>
        [HttpDelete("cleanup")]
        [DevOnly]
        public async Task<ActionResult> CleanupOldJobs([FromQuery] int olderThanDays = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
                var oldJobs = await _indexingJobRepository.FindAsync(j =>
                    (j.Status == "Completed" || j.Status == "Failed") &&
                    j.StartedAt < cutoffDate);

                var deletedCount = 0;
                var currentUserId = GetCurrentUserId();

                foreach (var job in oldJobs)
                {
                    await _indexingJobRepository.SoftDeleteAsync(job, currentUserId);
                    deletedCount++;
                }

                await _indexingJobRepository.SaveChangesAsync();

                _logger.LogInformation("Cleanup completed: {DeletedCount} old indexing jobs deleted by user {UserId}",
                    deletedCount, currentUserId);

                return Ok(new
                {
                    Message = $"Successfully deleted {deletedCount} old indexing jobs",
                    DeletedCount = deletedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during indexing jobs cleanup");
                return StatusCode(500, new { Message = "An error occurred during cleanup" });
            }
        }

        #region Private Helper Methods

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("userId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid user token");
            }
            return userId;
        }

        private IndexingJobDto MapToIndexingJobDto(IndexingJob job)
        {
            return new IndexingJobDto
            {
                Id = job.Id,
                JobType = job.JobType,
                Status = job.Status,
                StartedAt = job.StartedAt,
                CompletedAt = job.CompletedAt,
                TotalEntities = job.TotalEntities,
                ProcessedEntities = job.ProcessedEntities,
                FailedEntities = job.FailedEntities,
                ErrorMessage = job.ErrorMessage,
                Duration = job.CompletedAt.HasValue ? job.CompletedAt.Value - job.StartedAt : null,
                ProgressPercentage = job.TotalEntities > 0 ? (int)((double)job.ProcessedEntities / job.TotalEntities * 100) : 0
            };
        }

        private IndexingJobDetailDto MapToIndexingJobDetailDto(IndexingJob job)
        {
            var basicDto = MapToIndexingJobDto(job);
            return new IndexingJobDetailDto
            {
                Id = basicDto.Id,
                JobType = basicDto.JobType,
                Status = basicDto.Status,
                StartedAt = basicDto.StartedAt,
                CompletedAt = basicDto.CompletedAt,
                TotalEntities = basicDto.TotalEntities,
                ProcessedEntities = basicDto.ProcessedEntities,
                FailedEntities = basicDto.FailedEntities,
                ErrorMessage = basicDto.ErrorMessage,
                Duration = basicDto.Duration,
                ProgressPercentage = basicDto.ProgressPercentage,
                JobMetadata = job.JobMetadata,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt
            };
        }

        private TimeSpan? CalculateAverageJobDuration(IEnumerable<IndexingJob> completedJobs)
        {
            var durations = completedJobs
                .Where(j => j.CompletedAt.HasValue)
                .Select(j => j.CompletedAt!.Value - j.StartedAt)
                .Where(d => d.TotalSeconds > 0)
                .ToList();

            if (!durations.Any())
                return null;

            var averageTicks = (long)durations.Average(d => d.Ticks);
            return new TimeSpan(averageTicks);
        }

        #endregion
    }
}