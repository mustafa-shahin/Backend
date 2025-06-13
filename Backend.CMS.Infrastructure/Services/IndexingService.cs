using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace Backend.CMS.Infrastructure.Services
{
    public class IndexingService : IIndexingService
    {
        private readonly IRepository<SearchIndex> _searchIndexRepository;
        private readonly IRepository<IndexingJob> _indexingJobRepository;
        private readonly IPageRepository _pageRepository;
        private readonly IRepository<FileEntity> _fileRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRepository<ComponentTemplate> _componentTemplateRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly ILogger<IndexingService> _logger;

        public IndexingService(
            IRepository<SearchIndex> searchIndexRepository,
            IRepository<IndexingJob> indexingJobRepository,
            IPageRepository pageRepository,
            IRepository<FileEntity> fileRepository,
            IUserRepository userRepository,
            IRepository<ComponentTemplate> componentTemplateRepository,
            IUserSessionService userSessionService,
            ILogger<IndexingService> logger)
        {
            _searchIndexRepository = searchIndexRepository;
            _indexingJobRepository = indexingJobRepository;
            _pageRepository = pageRepository;
            _fileRepository = fileRepository;
            _userRepository = userRepository;
            _componentTemplateRepository = componentTemplateRepository;
            _userSessionService = userSessionService;
            _logger = logger;
        }

        public async Task<bool> IndexPagesAsync(IEnumerable<int>? pageIds = null)
        {
            try
            {
                var pages = pageIds?.Any() == true
                    ? await _pageRepository.FindAsync(p => pageIds.Contains(p.Id))
                    : await _pageRepository.GetAllAsync();

                foreach (var page in pages)
                {
                    await IndexPageAsync(page);
                }

                await _searchIndexRepository.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing pages");
                return false;
            }
        }

        public async Task<bool> IndexFilesAsync(IEnumerable<int>? fileIds = null)
        {
            try
            {
                var files = fileIds?.Any() == true
                    ? await _fileRepository.FindAsync(f => fileIds.Contains(f.Id))
                    : await _fileRepository.GetAllAsync();

                foreach (var file in files)
                {
                    await IndexFileAsync(file);
                }

                await _searchIndexRepository.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing files");
                return false;
            }
        }

        public async Task<bool> IndexUsersAsync(IEnumerable<int>? userIds = null)
        {
            try
            {
                var users = userIds?.Any() == true
                    ? await _userRepository.FindAsync(u => userIds.Contains(u.Id))
                    : await _userRepository.GetAllAsync();

                foreach (var user in users)
                {
                    await IndexUserAsync(user);
                }

                await _searchIndexRepository.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing users");
                return false;
            }
        }

        public async Task<bool> IndexComponentTemplatesAsync(IEnumerable<int>? templateIds = null)
        {
            try
            {
                var templates = templateIds?.Any() == true
                    ? await _componentTemplateRepository.FindAsync(t => templateIds.Contains(t.Id))
                    : await _componentTemplateRepository.GetAllAsync();

                foreach (var template in templates)
                {
                    await IndexComponentTemplateAsync(template);
                }

                await _searchIndexRepository.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing component templates");
                return false;
            }
        }

        public async Task<bool> RemoveFromIndexAsync(string entityType, int entityId)
        {
            try
            {
                var existingIndex = await _searchIndexRepository.FirstOrDefaultAsync(
                    si => si.EntityType == entityType && si.EntityId == entityId);

                if (existingIndex != null)
                {
                    await _searchIndexRepository.SoftDeleteAsync(existingIndex, _userSessionService.GetCurrentUserId());
                    await _searchIndexRepository.SaveChangesAsync();
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
            var job = await CreateIndexingJobAsync("Full", new Dictionary<string, object>
            {
                { "description", "Full reindex of all entities" }
            });

            try
            {
                await UpdateIndexingJobAsync(job.Id, "Running");

                // Clear existing index
                var existingIndexes = await _searchIndexRepository.GetAllAsync();
                foreach (var index in existingIndexes)
                {
                    await _searchIndexRepository.SoftDeleteAsync(index, _userSessionService.GetCurrentUserId());
                }
                await _searchIndexRepository.SaveChangesAsync();

                var totalEntities = 0;
                var processedEntities = 0;

                // Count total entities
                var pageCount = await _pageRepository.CountAsync();
                var fileCount = await _fileRepository.CountAsync();
                var userCount = await _userRepository.CountAsync();
                var templateCount = await _componentTemplateRepository.CountAsync();
                totalEntities = pageCount + fileCount + userCount + templateCount;

                await UpdateIndexingJobAsync(job.Id, "Running", totalEntities: totalEntities);

                // Index pages
                if (await IndexPagesAsync())
                {
                    processedEntities += pageCount;
                    await UpdateIndexingJobAsync(job.Id, "Running", processedEntities);
                }

                // Index files
                if (await IndexFilesAsync())
                {
                    processedEntities += fileCount;
                    await UpdateIndexingJobAsync(job.Id, "Running", processedEntities);
                }

                // Index users
                if (await IndexUsersAsync())
                {
                    processedEntities += userCount;
                    await UpdateIndexingJobAsync(job.Id, "Running", processedEntities);
                }

                // Index component templates
                if (await IndexComponentTemplatesAsync())
                {
                    processedEntities += templateCount;
                    await UpdateIndexingJobAsync(job.Id, "Running", processedEntities);
                }

                await CompleteIndexingJobAsync(job.Id, true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during full reindex");
                await CompleteIndexingJobAsync(job.Id, false, ex.Message);
                return false;
            }
        }

        public async Task<bool> IncrementalIndexAsync(DateTime? since = null)
        {
            var sinceDate = since ?? DateTime.UtcNow.AddHours(-1); // Default to last hour
            var job = await CreateIndexingJobAsync("Incremental", new Dictionary<string, object>
            {
                { "since", sinceDate },
                { "description", $"Incremental index since {sinceDate}" }
            });

            try
            {
                await UpdateIndexingJobAsync(job.Id, "Running");

                var processedEntities = 0;

                // Index updated pages
                var updatedPages = await _pageRepository.FindAsync(p => p.UpdatedAt >= sinceDate);
                foreach (var page in updatedPages)
                {
                    await IndexPageAsync(page);
                    processedEntities++;
                }

                // Index updated files
                var updatedFiles = await _fileRepository.FindAsync(f => f.UpdatedAt >= sinceDate);
                foreach (var file in updatedFiles)
                {
                    await IndexFileAsync(file);
                    processedEntities++;
                }

                // Index updated users
                var updatedUsers = await _userRepository.FindAsync(u => u.UpdatedAt >= sinceDate);
                foreach (var user in updatedUsers)
                {
                    await IndexUserAsync(user);
                    processedEntities++;
                }

                // Index updated component templates
                var updatedTemplates = await _componentTemplateRepository.FindAsync(t => t.UpdatedAt >= sinceDate);
                foreach (var template in updatedTemplates)
                {
                    await IndexComponentTemplateAsync(template);
                    processedEntities++;
                }

                await _searchIndexRepository.SaveChangesAsync();
                await CompleteIndexingJobAsync(job.Id, true);
                return true;
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
                JobType = jobType,
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
            return job;
        }

        public async Task<bool> UpdateIndexingJobAsync(int jobId, string status, int? processedEntities = null, int? failedEntities = null, string? errorMessage = null, int? totalEntities = null)
        {
            try
            {
                var job = await _indexingJobRepository.GetByIdAsync(jobId);
                if (job == null) return false;

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
                if (job == null) return false;

                var currentUserId = _userSessionService.GetCurrentUserId();
                job.Status = success ? "Completed" : "Failed";
                job.CompletedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(errorMessage)) job.ErrorMessage = errorMessage;
                job.UpdatedAt = DateTime.UtcNow;
                job.UpdatedByUserId = currentUserId;

                _indexingJobRepository.Update(job);
                await _indexingJobRepository.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing indexing job {JobId}", jobId);
                return false;
            }
        }

        // Private helper methods for indexing specific entities
        private async Task IndexPageAsync(Page page)
        {
            var content = ExtractPageContent(page);
            var searchVector = GenerateSearchVector(page.Title, content, page.MetaKeywords, page.Description);

            var searchIndex = await GetOrCreateSearchIndexAsync("Page", page.Id);
            searchIndex.Title = page.Title;
            searchIndex.Content = content;
            searchIndex.SearchVector = searchVector;
            searchIndex.IsPublic = page.Status == Domain.Enums.PageStatus.Published && !page.RequiresLogin;
            searchIndex.LastIndexedAt = DateTime.UtcNow;
            searchIndex.Metadata = new Dictionary<string, object>
            {
                { "slug", page.Slug },
                { "status", page.Status.ToString() },
                { "requiresLogin", page.RequiresLogin },
                { "adminOnly", page.AdminOnly },
                { "parentPageId", page.ParentPageId ?? 0 }
            };

            if (searchIndex.Id == 0)
            {
                await _searchIndexRepository.AddAsync(searchIndex);
            }
            else
            {
                _searchIndexRepository.Update(searchIndex);
            }
        }

        private async Task IndexFileAsync(FileEntity file)
        {
            var content = $"{file.OriginalFileName} {file.Description} {file.Alt}".Trim();
            var searchVector = GenerateSearchVector(file.OriginalFileName, content);

            var searchIndex = await GetOrCreateSearchIndexAsync("File", file.Id);
            searchIndex.Title = file.OriginalFileName;
            searchIndex.Content = content;
            searchIndex.SearchVector = searchVector;
            searchIndex.IsPublic = file.IsPublic;
            searchIndex.LastIndexedAt = DateTime.UtcNow;
            searchIndex.Metadata = new Dictionary<string, object>
            {
                { "fileType", file.FileType.ToString() },
                { "contentType", file.ContentType },
                { "fileSize", file.FileSize },
                { "folderId", file.FolderId ?? 0 }
            };

            if (searchIndex.Id == 0)
            {
                await _searchIndexRepository.AddAsync(searchIndex);
            }
            else
            {
                _searchIndexRepository.Update(searchIndex);
            }
        }

        private async Task IndexUserAsync(User user)
        {
            var content = $"{user.FirstName} {user.LastName} {user.Username} {user.Email}".Trim();
            var searchVector = GenerateSearchVector(user.FullName, content);

            var searchIndex = await GetOrCreateSearchIndexAsync("User", user.Id);
            searchIndex.Title = user.FullName;
            searchIndex.Content = content;
            searchIndex.SearchVector = searchVector;
            searchIndex.IsPublic = false; // Users are typically not public
            searchIndex.LastIndexedAt = DateTime.UtcNow;
            searchIndex.Metadata = new Dictionary<string, object>
            {
                { "role", user.Role.ToString() },
                { "isActive", user.IsActive },
                { "isLocked", user.IsLocked }
            };

            if (searchIndex.Id == 0)
            {
                await _searchIndexRepository.AddAsync(searchIndex);
            }
            else
            {
                _searchIndexRepository.Update(searchIndex);
            }
        }

        private async Task IndexComponentTemplateAsync(ComponentTemplate template)
        {
            var content = $"{template.DisplayName} {template.Description} {template.Category} {template.Tags}".Trim();
            var searchVector = GenerateSearchVector(template.DisplayName, content);

            var searchIndex = await GetOrCreateSearchIndexAsync("ComponentTemplate", template.Id);
            searchIndex.Title = template.DisplayName;
            searchIndex.Content = content;
            searchIndex.SearchVector = searchVector;
            searchIndex.IsPublic = true;
            searchIndex.LastIndexedAt = DateTime.UtcNow;
            searchIndex.Metadata = new Dictionary<string, object>
            {
                { "type", template.Type.ToString() },
                { "category", template.Category ?? "" },
                { "isSystemTemplate", template.IsSystemTemplate },
                { "isActive", template.IsActive }
            };

            if (searchIndex.Id == 0)
            {
                await _searchIndexRepository.AddAsync(searchIndex);
            }
            else
            {
                _searchIndexRepository.Update(searchIndex);
            }
        }

        private async Task<SearchIndex> GetOrCreateSearchIndexAsync(string entityType, int entityId)
        {
            var existing = await _searchIndexRepository.FirstOrDefaultAsync(
                si => si.EntityType == entityType && si.EntityId == entityId);

            if (existing != null)
                return existing;

            var currentUserId = _userSessionService.GetCurrentUserId();
            return new SearchIndex
            {
                EntityType = entityType,
                EntityId = entityId,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };
        }

        private string ExtractPageContent(Page page)
        {
            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine(page.Name);
            contentBuilder.AppendLine(page.Title);
            contentBuilder.AppendLine(page.Description);

            // Extract content from page components
            foreach (var component in page.Components.Where(c => !c.IsDeleted))
            {
                ExtractComponentContent(component, contentBuilder);
            }

            return CleanText(contentBuilder.ToString());
        }

        private void ExtractComponentContent(PageComponent component, StringBuilder contentBuilder)
        {
            contentBuilder.AppendLine(component.Name);

            // Extract text from properties
            foreach (var prop in component.Properties)
            {
                if (prop.Value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
                {
                    contentBuilder.AppendLine(stringValue);
                }
            }

            // Extract text from content
            foreach (var content in component.Content)
            {
                if (content.Value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
                {
                    contentBuilder.AppendLine(stringValue);
                }
            }

            // Recursively extract from child components
            foreach (var child in component.ChildComponents.Where(c => !c.IsDeleted))
            {
                ExtractComponentContent(child, contentBuilder);
            }
        }

        private string GenerateSearchVector(params string[] texts)
        {
            var cleanedTexts = texts.Where(t => !string.IsNullOrWhiteSpace(t))
                                   .Select(CleanText)
                                   .Where(t => !string.IsNullOrWhiteSpace(t));

            return string.Join(" ", cleanedTexts).Trim();
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove HTML tags
            text = Regex.Replace(text, "<.*?>", " ");

            // Remove extra whitespace
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }
    }
}