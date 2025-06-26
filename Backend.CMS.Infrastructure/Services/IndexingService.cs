using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace Backend.CMS.Infrastructure.Services
{
    public class IndexingService : IIndexingService, IDisposable
    {
        private readonly IRepository<SearchIndex> _searchIndexRepository;
        private readonly IRepository<IndexingJob> _indexingJobRepository;
        private readonly IPageRepository _pageRepository;
        private readonly IRepository<FileEntity> _fileRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly ILogger<IndexingService> _logger;
        private readonly SemaphoreSlim _indexingSemaphore;
        private readonly ConcurrentDictionary<string, DateTime> _lastIndexedTimes;
        private readonly int _batchSize;
        private readonly TimeSpan _indexingTimeout;
        private readonly bool _enableParallelProcessing;
        private bool _disposed = false;

        public IndexingService(
            IRepository<SearchIndex> searchIndexRepository,
            IRepository<IndexingJob> indexingJobRepository,
            IPageRepository pageRepository,
            IRepository<FileEntity> fileRepository,
            IUserRepository userRepository,
            IUserSessionService userSessionService,
            IConfiguration configuration,
            ILogger<IndexingService> logger)
        {
            _searchIndexRepository = searchIndexRepository ?? throw new ArgumentNullException(nameof(searchIndexRepository));
            _indexingJobRepository = indexingJobRepository ?? throw new ArgumentNullException(nameof(indexingJobRepository));
            _pageRepository = pageRepository ?? throw new ArgumentNullException(nameof(pageRepository));
            _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _indexingSemaphore = new SemaphoreSlim(1, 1);
            _lastIndexedTimes = new ConcurrentDictionary<string, DateTime>();

            // Load configuration
            _batchSize = int.TryParse(configuration["Indexing:BatchSize"], out var batchSize) ? batchSize : 100;
            _indexingTimeout = TimeSpan.FromMinutes(int.TryParse(configuration["Indexing:TimeoutMinutes"], out var timeout) ? timeout : 60);
            _enableParallelProcessing = bool.TryParse(configuration["Indexing:EnableParallelProcessing"], out var parallel) && parallel;

            _logger.LogInformation("IndexingService initialized - BatchSize: {BatchSize}, Timeout: {Timeout}min, Parallel: {Parallel}",
                _batchSize, _indexingTimeout.TotalMinutes, _enableParallelProcessing);
        }

        public async Task<bool> IndexPagesAsync(IEnumerable<int>? pageIds = null)
        {
            using var cancellationTokenSource = new CancellationTokenSource(_indexingTimeout);
            var cancellationToken = cancellationTokenSource.Token;

            try
            {
                var pages = pageIds?.Any() == true
                    ? await _pageRepository.FindAsync(p => pageIds.Contains(p.Id))
                    : await _pageRepository.GetAllAsync();

                var pagesList = pages.ToList();
                if (!pagesList.Any())
                {
                    _logger.LogInformation("No pages found to index");
                    return true;
                }

                _logger.LogInformation("Starting to index {Count} pages", pagesList.Count);

                var successCount = 0;
                var errorCount = 0;

                if (_enableParallelProcessing)
                {
                    var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
                    var tasks = pagesList.Select(async page =>
                    {
                        await semaphore.WaitAsync(cancellationToken);
                        try
                        {
                            await IndexPageAsync(page, cancellationToken);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error indexing page {PageId}", page.Id);
                            Interlocked.Increment(ref errorCount);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks);
                    semaphore.Dispose();
                }
                else
                {
                    foreach (var batch in pagesList.Chunk(_batchSize))
                    {
                        foreach (var page in batch)
                        {
                            try
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                await IndexPageAsync(page, cancellationToken);
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error indexing page {PageId}", page.Id);
                                errorCount++;
                            }
                        }

                        // Save in batches to avoid memory issues
                        await _searchIndexRepository.SaveChangesAsync();
                    }
                }

                // Final save
                await _searchIndexRepository.SaveChangesAsync();

                _logger.LogInformation("Page indexing completed - Success: {SuccessCount}, Errors: {ErrorCount}",
                    successCount, errorCount);

                return errorCount == 0;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Page indexing was cancelled due to timeout");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during page indexing");
                return false;
            }
        }

        public async Task<bool> IndexFilesAsync(IEnumerable<int>? fileIds = null)
        {
            using var cancellationTokenSource = new CancellationTokenSource(_indexingTimeout);
            var cancellationToken = cancellationTokenSource.Token;

            try
            {
                var files = fileIds?.Any() == true
                    ? await _fileRepository.FindAsync(f => fileIds.Contains(f.Id))
                    : await _fileRepository.GetAllAsync();

                var filesList = files.ToList();
                if (!filesList.Any())
                {
                    _logger.LogInformation("No files found to index");
                    return true;
                }

                _logger.LogInformation("Starting to index {Count} files", filesList.Count);

                var successCount = 0;
                var errorCount = 0;

                foreach (var batch in filesList.Chunk(_batchSize))
                {
                    foreach (var file in batch)
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await IndexFileAsync(file, cancellationToken);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error indexing file {FileId}", file.Id);
                            errorCount++;
                        }
                    }

                    await _searchIndexRepository.SaveChangesAsync();
                }

                _logger.LogInformation("File indexing completed - Success: {SuccessCount}, Errors: {ErrorCount}",
                    successCount, errorCount);

                return errorCount == 0;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("File indexing was cancelled due to timeout");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file indexing");
                return false;
            }
        }

        public async Task<bool> IndexUsersAsync(IEnumerable<int>? userIds = null)
        {
            using var cancellationTokenSource = new CancellationTokenSource(_indexingTimeout);
            var cancellationToken = cancellationTokenSource.Token;

            try
            {
                var users = userIds?.Any() == true
                    ? await _userRepository.FindAsync(u => userIds.Contains(u.Id))
                    : await _userRepository.GetAllAsync();

                var usersList = users.ToList();
                if (!usersList.Any())
                {
                    _logger.LogInformation("No users found to index");
                    return true;
                }

                _logger.LogInformation("Starting to index {Count} users", usersList.Count);

                var successCount = 0;
                var errorCount = 0;

                foreach (var batch in usersList.Chunk(_batchSize))
                {
                    foreach (var user in batch)
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await IndexUserAsync(user, cancellationToken);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error indexing user {UserId}", user.Id);
                            errorCount++;
                        }
                    }

                    await _searchIndexRepository.SaveChangesAsync();
                }

                _logger.LogInformation("User indexing completed - Success: {SuccessCount}, Errors: {ErrorCount}",
                    successCount, errorCount);

                return errorCount == 0;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("User indexing was cancelled due to timeout");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user indexing");
                return false;
            }
        }


        public async Task<bool> RemoveFromIndexAsync(string entityType, int entityId)
        {
            if (string.IsNullOrWhiteSpace(entityType) || entityId <= 0)
                return false;

            try
            {
                var existingIndex = await _searchIndexRepository.FirstOrDefaultAsync(
                    si => si.EntityType == entityType && si.EntityId == entityId);

                if (existingIndex != null)
                {
                    await _searchIndexRepository.SoftDeleteAsync(existingIndex, _userSessionService.GetCurrentUserId());
                    await _searchIndexRepository.SaveChangesAsync();

                    _logger.LogDebug("Removed {EntityType} {EntityId} from search index", entityType, entityId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing {EntityType} {EntityId} from search index", entityType, entityId);
                return false;
            }
        }

        public async Task<bool> FullReindexAsync()
        {
            await _indexingSemaphore.WaitAsync();
            try
            {
                var job = await CreateIndexingJobAsync("Full", new Dictionary<string, object>
                {
                    { "description", "Full reindex of all entities" },
                    { "startTime", DateTime.UtcNow }
                });

                try
                {
                    await UpdateIndexingJobAsync(job.Id, "Running");

                    // Mark existing indexes as deleted instead of physically deleting them
                    // This allows search to continue working during reindexing
                    var existingIndexes = await _searchIndexRepository.GetAllAsync();
                    var existingList = existingIndexes.ToList();

                    if (existingList.Any())
                    {
                        foreach (var batch in existingList.Chunk(_batchSize))
                        {
                            foreach (var index in batch)
                            {
                                index.IsDeleted = true;
                                index.DeletedAt = DateTime.UtcNow;
                                index.DeletedByUserId = _userSessionService.GetCurrentUserId();
                            }
                        }
                        await _searchIndexRepository.SaveChangesAsync();
                    }

                    var totalEntities = 0;
                    var processedEntities = 0;
                    var failedEntities = 0;

                    // Count total entities
                    var pageCount = await _pageRepository.CountAsync();
                    var fileCount = await _fileRepository.CountAsync();
                    var userCount = await _userRepository.CountAsync();
                    totalEntities = pageCount + fileCount + userCount;

                    await UpdateIndexingJobAsync(job.Id, "Running", totalEntities: totalEntities);

                    // Index pages
                    if (await IndexPagesAsync())
                    {
                        processedEntities += pageCount;
                    }
                    else
                    {
                        failedEntities += pageCount;
                    }
                    await UpdateIndexingJobAsync(job.Id, "Running", processedEntities, failedEntities);

                    // Index files
                    if (await IndexFilesAsync())
                    {
                        processedEntities += fileCount;
                    }
                    else
                    {
                        failedEntities += fileCount;
                    }
                    await UpdateIndexingJobAsync(job.Id, "Running", processedEntities, failedEntities);

                    // Index users
                    if (await IndexUsersAsync())
                    {
                        processedEntities += userCount;
                    }
                    else
                    {
                        failedEntities += userCount;
                    }
                    await UpdateIndexingJobAsync(job.Id, "Running", processedEntities, failedEntities);

                    await UpdateIndexingJobAsync(job.Id, "Running", processedEntities, failedEntities);

                    // Clean up old deleted indexes
                    await CleanupDeletedIndexesAsync();

                    var success = failedEntities == 0;
                    await CompleteIndexingJobAsync(job.Id, success,
                        success ? null : $"Failed to index {failedEntities} out of {totalEntities} entities");

                    return success;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during full reindex");
                    await CompleteIndexingJobAsync(job.Id, false, ex.Message);
                    return false;
                }
            }
            finally
            {
                _indexingSemaphore.Release();
            }
        }

        public async Task<bool> IncrementalIndexAsync(DateTime? since = null)
        {
            var sinceDate = since ?? DateTime.UtcNow.AddHours(-1);
            var job = await CreateIndexingJobAsync("Incremental", new Dictionary<string, object>
            {
                { "since", sinceDate },
                { "description", $"Incremental index since {sinceDate:yyyy-MM-dd HH:mm:ss}" }
            });

            try
            {
                await UpdateIndexingJobAsync(job.Id, "Running");

                var processedEntities = 0;
                var failedEntities = 0;

                // Process updated entities
                var tasks = new List<Task<(int processed, int failed)>>
                {
                    IndexUpdatedPagesAsync(sinceDate),
                    IndexUpdatedFilesAsync(sinceDate),
                    IndexUpdatedUsersAsync(sinceDate),
                };

                var results = await Task.WhenAll(tasks);

                foreach (var (processed, failed) in results)
                {
                    processedEntities += processed;
                    failedEntities += failed;
                }

                await _searchIndexRepository.SaveChangesAsync();

                var success = failedEntities == 0;
                await CompleteIndexingJobAsync(job.Id, success,
                    success ? null : $"Failed to index {failedEntities} entities");

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during incremental index");
                await CompleteIndexingJobAsync(job.Id, false, ex.Message);
                return false;
            }
        }

        public async Task<IndexingJob> CreateIndexingJobAsync(string jobType, Dictionary<string, object>? metadata = null)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();
            var job = new IndexingJob
            {
                JobType = jobType ?? "Unknown",
                Status = "Pending",
                StartedAt = DateTime.UtcNow,
                TotalEntities = 0,
                ProcessedEntities = 0,
                FailedEntities = 0,
                JobMetadata = metadata ?? new Dictionary<string, object>(),
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            await _indexingJobRepository.AddAsync(job);
            await _indexingJobRepository.SaveChangesAsync();

            _logger.LogInformation("Created indexing job {JobId} of type {JobType}", job.Id, jobType);
            return job;
        }

        public async Task<bool> UpdateIndexingJobAsync(int jobId, string status, int? processedEntities = null,
            int? failedEntities = null, string? errorMessage = null, int? totalEntities = null)
        {
            try
            {
                var job = await _indexingJobRepository.GetByIdAsync(jobId);
                if (job == null)
                {
                    _logger.LogWarning("Indexing job {JobId} not found for update", jobId);
                    return false;
                }

                var currentUserId = _userSessionService.GetCurrentUserId();
                job.Status = status;
                if (processedEntities.HasValue) job.ProcessedEntities = processedEntities.Value;
                if (failedEntities.HasValue) job.FailedEntities = failedEntities.Value;
                if (totalEntities.HasValue) job.TotalEntities = totalEntities.Value;
                if (!string.IsNullOrEmpty(errorMessage)) job.ErrorMessage = errorMessage;
                job.UpdatedAt = DateTime.UtcNow;
                job.UpdatedByUserId = currentUserId;

                _indexingJobRepository.Update(job);
                await _indexingJobRepository.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating indexing job {JobId}", jobId);
                return false;
            }
        }

        public async Task<bool> CompleteIndexingJobAsync(int jobId, bool success = true, string? errorMessage = null)
        {
            try
            {
                var job = await _indexingJobRepository.GetByIdAsync(jobId);
                if (job == null)
                {
                    _logger.LogWarning("Indexing job {JobId} not found for completion", jobId);
                    return false;
                }

                var currentUserId = _userSessionService.GetCurrentUserId();
                job.Status = success ? "Completed" : "Failed";
                job.CompletedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(errorMessage)) job.ErrorMessage = errorMessage;
                job.UpdatedAt = DateTime.UtcNow;
                job.UpdatedByUserId = currentUserId;

                _indexingJobRepository.Update(job);
                await _indexingJobRepository.SaveChangesAsync();

                _logger.LogInformation("Completed indexing job {JobId} with status {Status}", jobId, job.Status);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing indexing job {JobId}", jobId);
                return false;
            }
        }

        #region Private Helper Methods

        private async Task<(int processed, int failed)> IndexUpdatedPagesAsync(DateTime since)
        {
            var updatedPages = await _pageRepository.FindAsync(p => p.UpdatedAt >= since);
            var processed = 0;
            var failed = 0;

            foreach (var page in updatedPages)
            {
                try
                {
                    await IndexPageAsync(page);
                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error indexing updated page {PageId}", page.Id);
                    failed++;
                }
            }

            return (processed, failed);
        }

        private async Task<(int processed, int failed)> IndexUpdatedFilesAsync(DateTime since)
        {
            var updatedFiles = await _fileRepository.FindAsync(f => f.UpdatedAt >= since);
            var processed = 0;
            var failed = 0;

            foreach (var file in updatedFiles)
            {
                try
                {
                    await IndexFileAsync(file);
                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error indexing updated file {FileId}", file.Id);
                    failed++;
                }
            }

            return (processed, failed);
        }

        private async Task<(int processed, int failed)> IndexUpdatedUsersAsync(DateTime since)
        {
            var updatedUsers = await _userRepository.FindAsync(u => u.UpdatedAt >= since);
            var processed = 0;
            var failed = 0;

            foreach (var user in updatedUsers)
            {
                try
                {
                    await IndexUserAsync(user);
                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error indexing updated user {UserId}", user.Id);
                    failed++;
                }
            }

            return (processed, failed);
        }


        private async Task IndexPageAsync(Page page, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = ExtractPageContent(page);
            var searchVector = GenerateSearchVector(page.Title, content, page.MetaKeywords, page.Description);

            var searchIndex = await GetOrCreateSearchIndexAsync("Page", page.Id);
            searchIndex.Title = TruncateString(page.Title, 500);
            searchIndex.Content = TruncateString(content, 10000);
            searchIndex.SearchVector = TruncateString(searchVector, 5000);
            searchIndex.IsPublic = page.Status == Domain.Enums.PageStatus.Published && !page.RequiresLogin;
            searchIndex.LastIndexedAt = DateTime.UtcNow;
            searchIndex.Metadata = new Dictionary<string, object>
            {
                { "slug", page.Slug ?? "" },
                { "status", page.Status.ToString() },
                { "requiresLogin", page.RequiresLogin },
                { "adminOnly", page.AdminOnly },
                { "parentPageId", page.ParentPageId ?? 0 },
                { "priority", page.Priority }
            };

            if (searchIndex.Id == 0)
            {
                await _searchIndexRepository.AddAsync(searchIndex);
            }
            else
            {
                _searchIndexRepository.Update(searchIndex);
            }

            _lastIndexedTimes[$"Page_{page.Id}"] = DateTime.UtcNow;
        }

        private async Task IndexFileAsync(FileEntity file, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = $"{file.OriginalFileName} {file.Description} {file.Alt}".Trim();
            var searchVector = GenerateSearchVector(file.OriginalFileName, content);

            var searchIndex = await GetOrCreateSearchIndexAsync("File", file.Id);
            searchIndex.Title = TruncateString(file.OriginalFileName, 500);
            searchIndex.Content = TruncateString(content, 10000);
            searchIndex.SearchVector = TruncateString(searchVector, 5000);
            searchIndex.IsPublic = file.IsPublic;
            searchIndex.LastIndexedAt = DateTime.UtcNow;
            searchIndex.Metadata = new Dictionary<string, object>
            {
                { "fileType", file.FileType.ToString() },
                { "contentType", file.ContentType ?? "" },
                { "fileSize", file.FileSize },
                { "folderId", file.FolderId ?? 0 },
            };

            if (searchIndex.Id == 0)
            {
                await _searchIndexRepository.AddAsync(searchIndex);
            }
            else
            {
                _searchIndexRepository.Update(searchIndex);
            }

            _lastIndexedTimes[$"File_{file.Id}"] = DateTime.UtcNow;
        }

        private async Task IndexUserAsync(User user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = $"{user.FirstName} {user.LastName} {user.Username} {user.Email}".Trim();
            var searchVector = GenerateSearchVector(user.FullName, content);

            var searchIndex = await GetOrCreateSearchIndexAsync("User", user.Id);
            searchIndex.Title = TruncateString(user.FullName, 500);
            searchIndex.Content = TruncateString(content, 10000);
            searchIndex.SearchVector = TruncateString(searchVector, 5000);
            searchIndex.IsPublic = false; // User data is never public in search
            searchIndex.LastIndexedAt = DateTime.UtcNow;
            searchIndex.Metadata = new Dictionary<string, object>
            {
                { "role", user.Role.ToString() },
                { "isActive", user.IsActive },
                { "isLocked", user.IsLocked },
                { "emailVerified", user.EmailVerifiedAt.HasValue }
            };

            if (searchIndex.Id == 0)
            {
                await _searchIndexRepository.AddAsync(searchIndex);
            }
            else
            {
                _searchIndexRepository.Update(searchIndex);
            }

            _lastIndexedTimes[$"User_{user.Id}"] = DateTime.UtcNow;
        }


        private async Task<SearchIndex> GetOrCreateSearchIndexAsync(string entityType, int entityId)
        {
            var existing = await _searchIndexRepository.FirstOrDefaultAsync(
                si => si.EntityType == entityType && si.EntityId == entityId && !si.IsDeleted);

            if (existing != null)
                return existing;

            var currentUserId = _userSessionService.GetCurrentUserId();
            return new SearchIndex
            {
                EntityType = entityType,
                EntityId = entityId,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private string ExtractPageContent(Page page)
        {
            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine(page.Name);
            contentBuilder.AppendLine(page.Title);
            contentBuilder.AppendLine(page.Description);

            return CleanText(contentBuilder.ToString());
        }

        private void ExtractComponentContent(PageComponent component, StringBuilder contentBuilder)
        {
            contentBuilder.AppendLine(component.Name);

            // Extract text from unified config
            if (component.Config?.Any() == true)
            {
                foreach (var kvp in component.Config)
                {
                    if (kvp.Value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
                    {
                        // Extract text content from common properties
                        var key = kvp.Key.ToLowerInvariant();
                        if (key.Contains("text") || key.Contains("content") ||
                            key.Contains("title") || key.Contains("alt") ||
                            key.Contains("description") || key.Contains("label"))
                        {
                            contentBuilder.AppendLine(stringValue);
                        }
                    }
                }
            }

            if (component.ChildComponents?.Any() == true)
            {
                foreach (var child in component.ChildComponents.Where(c => !c.IsDeleted))
                {
                    ExtractComponentContent(child, contentBuilder);
                }
            }
        }

        private string GenerateSearchVector(params string?[] texts)
        {
            var cleanedTexts = texts
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => CleanText(t!))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct();

            return string.Join(" ", cleanedTexts).Trim();
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove HTML tags
            text = Regex.Replace(text, "<.*?>", " ", RegexOptions.Compiled);

            // Remove extra whitespace
            text = Regex.Replace(text, @"\s+", " ", RegexOptions.Compiled);

            // Remove special characters but keep alphanumeric and common punctuation
            text = Regex.Replace(text, @"[^\w\s\-\.,!?]", " ", RegexOptions.Compiled);

            return text.Trim();
        }

        private string TruncateString(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input ?? string.Empty;

            return input.Substring(0, maxLength - 3) + "...";
        }

        private async Task CleanupDeletedIndexesAsync()
        {
            try
            {
                // Remove indexes that have been marked as deleted for more than 24 hours
                var cutoffDate = DateTime.UtcNow.AddDays(-1);
                var oldDeletedIndexes = await _searchIndexRepository.FindAsync(
                    si => si.IsDeleted && si.DeletedAt.HasValue && si.DeletedAt.Value < cutoffDate);

                var indexesToDelete = oldDeletedIndexes.ToList();
                if (indexesToDelete.Any())
                {
                    _searchIndexRepository.RemoveRange(indexesToDelete);
                    await _searchIndexRepository.SaveChangesAsync();

                    _logger.LogInformation("Cleaned up {Count} old deleted search indexes", indexesToDelete.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during cleanup of deleted indexes");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _indexingSemaphore?.Dispose();
                _lastIndexedTimes.Clear();
                _disposed = true;
            }
        }

        #endregion
    }
}