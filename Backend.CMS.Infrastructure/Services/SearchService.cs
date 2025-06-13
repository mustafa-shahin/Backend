using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Backend.CMS.Infrastructure.Services
{
    public class SearchService : ISearchService
    {
        private readonly IRepository<SearchIndex> _searchIndexRepository;
        private readonly IRepository<IndexingJob> _indexingJobRepository;
        private readonly IIndexingService _indexingService;
        private readonly ICacheService _cacheService;
        private readonly IMapper _mapper;
        private readonly ILogger<SearchService> _logger;

        public SearchService(
            IRepository<SearchIndex> searchIndexRepository,
            IRepository<IndexingJob> indexingJobRepository,
            IIndexingService indexingService,
            ICacheService cacheService,
            IMapper mapper,
            ILogger<SearchService> logger)
        {
            _searchIndexRepository = searchIndexRepository;
            _indexingJobRepository = indexingJobRepository;
            _indexingService = indexingService;
            _cacheService = cacheService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<SearchResponseDto> SearchAsync(SearchRequestDto searchRequest)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = GenerateSearchCacheKey(searchRequest);

            try
            {
                // Try to get from cache first
                var cachedResult = await _cacheService.GetAsync<SearchResponseDto>(cacheKey);
                if (cachedResult != null)
                {
                    _logger.LogInformation("Search results retrieved from cache for query: {Query}", searchRequest.Query);
                    return cachedResult;
                }

                var query = await _searchIndexRepository.GetAllAsync();
                var searchIndexes = query.AsQueryable();

                // Apply entity type filter
                if (searchRequest.EntityTypes.Any())
                {
                    searchIndexes = searchIndexes.Where(si => searchRequest.EntityTypes.Contains(si.EntityType));
                }

                // Apply public filter
                if (searchRequest.PublicOnly)
                {
                    searchIndexes = searchIndexes.Where(si => si.IsPublic);
                }

                // Apply search query
                if (!string.IsNullOrWhiteSpace(searchRequest.Query))
                {
                    var searchTerms = searchRequest.Query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    searchIndexes = searchIndexes.Where(si =>
                        searchTerms.All(term =>
                            si.Title.ToLowerInvariant().Contains(term) ||
                            si.Content.ToLowerInvariant().Contains(term) ||
                            si.SearchVector.ToLowerInvariant().Contains(term)));
                }

                // Apply additional filters
                foreach (var filter in searchRequest.Filters)
                {
                    // Implementation depends on specific filter requirements
                    // This is a basic example
                    if (filter.Key == "category" && filter.Value is string categoryValue)
                    {
                        searchIndexes = searchIndexes.Where(si =>
                            si.Metadata.ContainsKey("category") &&
                            si.Metadata["category"].ToString() == categoryValue);
                    }
                }

                // Calculate total results
                var totalResults = searchIndexes.Count();

                // Apply pagination and ordering
                var results = searchIndexes
                    .OrderByDescending(si => CalculateRelevanceScore(si, searchRequest.Query))
                    .ThenByDescending(si => si.LastIndexedAt)
                    .Skip((searchRequest.Page - 1) * searchRequest.PageSize)
                    .Take(searchRequest.PageSize)
                    .ToList();

                // Convert to DTOs
                var searchResults = results.Select(si => new SearchResultDto
                {
                    EntityType = si.EntityType,
                    EntityId = si.EntityId,
                    Title = si.Title,
                    Excerpt = GenerateExcerpt(si.Content, searchRequest.Query),
                    Url = GenerateEntityUrl(si.EntityType, si.EntityId, si.Metadata),
                    Score = CalculateRelevanceScore(si, searchRequest.Query),
                    Metadata = si.Metadata,
                    LastModified = si.UpdatedAt
                }).ToList();

                stopwatch.Stop();

                var response = new SearchResponseDto
                {
                    Results = searchResults,
                    TotalResults = totalResults,
                    Page = searchRequest.Page,
                    PageSize = searchRequest.PageSize,
                    Query = searchRequest.Query,
                    Duration = stopwatch.Elapsed
                };

                // Cache the results for 5 minutes
                await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing search for query: {Query}", searchRequest.Query);
                stopwatch.Stop();

                return new SearchResponseDto
                {
                    Results = new List<SearchResultDto>(),
                    TotalResults = 0,
                    Page = searchRequest.Page,
                    PageSize = searchRequest.PageSize,
                    Query = searchRequest.Query,
                    Duration = stopwatch.Elapsed
                };
            }
        }

        public async Task<List<SearchResultDto>> SuggestAsync(string query, int maxSuggestions = 5)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                    return new List<SearchResultDto>();

                var cacheKey = $"suggestions:{query.ToLowerInvariant()}:{maxSuggestions}";

                var cachedSuggestions = await _cacheService.GetAsync<List<SearchResultDto>>(cacheKey);
                if (cachedSuggestions != null)
                    return cachedSuggestions;

                var searchIndexes = await _searchIndexRepository.GetAllAsync();
                var suggestions = searchIndexes.AsQueryable()
                    .Where(si => si.IsPublic && si.Title.ToLowerInvariant().Contains(query.ToLowerInvariant()))
                    .OrderByDescending(si => si.Title.ToLowerInvariant().StartsWith(query.ToLowerInvariant()))
                    .ThenBy(si => si.Title.Length)
                    .Take(maxSuggestions)
                    .Select(si => new SearchResultDto
                    {
                        EntityType = si.EntityType,
                        EntityId = si.EntityId,
                        Title = si.Title,
                        Url = GenerateEntityUrl(si.EntityType, si.EntityId, si.Metadata),
                        Metadata = si.Metadata
                    })
                    .ToList();

                // Cache suggestions for 10 minutes
                await _cacheService.SetAsync(cacheKey, suggestions, TimeSpan.FromMinutes(10));

                return suggestions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating suggestions for query: {Query}", query);
                return new List<SearchResultDto>();
            }
        }

        public async Task<IndexingStatusDto> GetIndexingStatusAsync()
        {
            try
            {
                var jobs = await _indexingJobRepository.GetAllAsync();
                var recentJobs = jobs.OrderByDescending(j => j.StartedAt)
                                   .Take(10)
                                   .ToList();

                var lastFullIndex = jobs.Where(j => j.JobType == "Full" && j.Status == "Completed")
                                       .OrderByDescending(j => j.CompletedAt)
                                       .FirstOrDefault()?.CompletedAt;

                var lastIncrementalIndex = jobs.Where(j => j.JobType == "Incremental" && j.Status == "Completed")
                                              .OrderByDescending(j => j.CompletedAt)
                                              .FirstOrDefault()?.CompletedAt;

                var totalIndexedEntities = await _searchIndexRepository.CountAsync();

                var runningJob = jobs.FirstOrDefault(j => j.Status == "Running");
                var status = runningJob != null ? "Running" : "Idle";

                return new IndexingStatusDto
                {
                    Status = status,
                    LastFullIndex = lastFullIndex,
                    LastIncrementalIndex = lastIncrementalIndex,
                    TotalIndexedEntities = totalIndexedEntities,
                    NextScheduledIndex = null, // This would depend on your scheduling implementation
                    RecentJobs = _mapper.Map<List<IndexingJobDto>>(recentJobs)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting indexing status");
                return new IndexingStatusDto { Status = "Error" };
            }
        }

        public async Task<bool> TriggerFullIndexAsync()
        {
            try
            {
                // Check if there's already a running job
                var runningJob = await _indexingJobRepository.FirstOrDefaultAsync(j => j.Status == "Running");
                if (runningJob != null)
                {
                    _logger.LogWarning("Cannot start full index - another indexing job is already running");
                    return false;
                }

                return await _indexingService.FullReindexAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering full index");
                return false;
            }
        }

        public async Task<bool> TriggerIncrementalIndexAsync()
        {
            try
            {
                // Check if there's already a running job
                var runningJob = await _indexingJobRepository.FirstOrDefaultAsync(j => j.Status == "Running");
                if (runningJob != null)
                {
                    _logger.LogWarning("Cannot start incremental index - another indexing job is already running");
                    return false;
                }

                return await _indexingService.IncrementalIndexAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering incremental index");
                return false;
            }
        }

        public async Task<bool> IndexEntityAsync(string entityType, int entityId)
        {
            try
            {
                return entityType.ToLowerInvariant() switch
                {
                    "page" => await _indexingService.IndexPagesAsync(new[] { entityId }),
                    "file" => await _indexingService.IndexFilesAsync(new[] { entityId }),
                    "user" => await _indexingService.IndexUsersAsync(new[] { entityId }),
                    "componenttemplate" => await _indexingService.IndexComponentTemplatesAsync(new[] { entityId }),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing {EntityType} {EntityId}", entityType, entityId);
                return false;
            }
        }

        public async Task<bool> RemoveFromIndexAsync(string entityType, int entityId)
        {
            try
            {
                return await _indexingService.RemoveFromIndexAsync(entityType, entityId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing {EntityType} {EntityId} from index", entityType, entityId);
                return false;
            }
        }

        // Private helper methods
        private string GenerateSearchCacheKey(SearchRequestDto searchRequest)
        {
            var keyParts = new List<string>
            {
                "search",
                searchRequest.Query?.ToLowerInvariant() ?? "empty",
                string.Join(",", searchRequest.EntityTypes.OrderBy(et => et)),
                searchRequest.PublicOnly.ToString(),
                searchRequest.Page.ToString(),
                searchRequest.PageSize.ToString()
            };

            if (searchRequest.Filters.Any())
            {
                var filterString = string.Join(",", searchRequest.Filters.OrderBy(f => f.Key).Select(f => $"{f.Key}:{f.Value}"));
                keyParts.Add(filterString);
            }

            return string.Join(":", keyParts);
        }

        private float CalculateRelevanceScore(SearchIndex searchIndex, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return 1.0f;

            var score = 0.0f;
            var searchTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var title = searchIndex.Title.ToLowerInvariant();
            var content = searchIndex.Content.ToLowerInvariant();

            foreach (var term in searchTerms)
            {
                // Title matches are worth more
                if (title.Contains(term))
                {
                    score += title.StartsWith(term) ? 3.0f : 2.0f;
                }

                // Content matches
                if (content.Contains(term))
                {
                    score += 1.0f;
                }
            }

            // Boost score based on entity type
            score *= searchIndex.EntityType switch
            {
                "Page" => 1.2f,
                "ComponentTemplate" => 1.1f,
                "File" => 1.0f,
                "User" => 0.8f,
                _ => 1.0f
            };

            return score;
        }

        private string GenerateExcerpt(string content, string query, int maxLength = 200)
        {
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            if (string.IsNullOrWhiteSpace(query))
                return content.Length > maxLength ? content.Substring(0, maxLength) + "..." : content;

            var searchTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var contentLower = content.ToLowerInvariant();

            // Find the first occurrence of any search term
            var firstMatchIndex = searchTerms.Select(term => contentLower.IndexOf(term))
                                            .Where(index => index >= 0)
                                            .DefaultIfEmpty(-1)
                                            .Min();

            if (firstMatchIndex < 0)
                return content.Length > maxLength ? content.Substring(0, maxLength) + "..." : content;

            // Calculate excerpt start position
            var startIndex = Math.Max(0, firstMatchIndex - maxLength / 4);
            var endIndex = Math.Min(content.Length, startIndex + maxLength);

            var excerpt = content.Substring(startIndex, endIndex - startIndex);

            // Add ellipsis if needed
            if (startIndex > 0) excerpt = "..." + excerpt;
            if (endIndex < content.Length) excerpt += "...";

            return excerpt;
        }

        private string GenerateEntityUrl(string entityType, int entityId, Dictionary<string, object> metadata)
        {
            return entityType.ToLowerInvariant() switch
            {
                "page" => metadata.ContainsKey("slug") ? $"/pages/{metadata["slug"]}" : $"/pages/{entityId}",
                "file" => $"/files/{entityId}",
                "user" => $"/users/{entityId}",
                "componenttemplate" => $"/components/{entityId}",
                _ => $"/{entityType.ToLowerInvariant()}/{entityId}"
            };
        }
    }
}