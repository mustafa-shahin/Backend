using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class DesignerService : IDesignerService
    {
        private readonly IPageRepository _pageRepository;
        private readonly IRepository<PageVersion> _versionRepository;
        private readonly ICacheService _cacheService;
        private readonly IPageContentValidationService _contentValidationService;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;
        private readonly ILogger<DesignerService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public DesignerService(
            IPageRepository pageRepository,
            IRepository<PageVersion> versionRepository,
            ICacheService cacheService,
            IPageContentValidationService contentValidationService,
            IMapper mapper,
            IUserSessionService userSessionService,
            ILogger<DesignerService> logger)
        {
            _pageRepository = pageRepository ?? throw new ArgumentNullException(nameof(pageRepository));
            _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _contentValidationService = contentValidationService ?? throw new ArgumentNullException(nameof(contentValidationService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<DesignerPageDto> GetDesignerPageAsync(int pageId)
        {
            try
            {
                var cacheKey = CacheKeys.DesignerPage(pageId);
                return await _cacheService.GetAsync(cacheKey, async () =>
                {
                    var page = await _pageRepository.GetByIdAsync(pageId);
                    if (page == null)
                        throw new ArgumentException("Page not found");

                    var designerPage = _mapper.Map<DesignerPageDto>(page);

                    // Get current version number
                    var versions = await _versionRepository.FindAsync(v => v.PageId == pageId);
                    designerPage.CurrentVersion = versions.Any() ? versions.Max(v => v.VersionNumber) : 0;

                    return designerPage;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting designer page {PageId}", pageId);
                throw;
            }
        }

        public async Task<DesignerPageDto> SaveDesignerPageAsync(SaveDesignerPageDto saveDto)
        {
            try
            {
                var page = await _pageRepository.GetByIdAsync(saveDto.PageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Validate the content and layout before saving
                await ValidatePageDataAsync(saveDto);

                // Always create version when saving (unless it's an auto-save)
                if (saveDto.CreateVersion && !saveDto.AutoSave)
                {
                    await CreateVersionAsync(saveDto.PageId, saveDto.ChangeDescription ?? "Designer changes");
                }

                // Update page content - the frontend sends the complete structure
                page.Content = saveDto.Content ?? new Dictionary<string, object>();
                page.Layout = saveDto.Layout ?? new Dictionary<string, object>();
                page.Settings = saveDto.Settings ?? new Dictionary<string, object>();
                page.Styles = saveDto.Styles ?? new Dictionary<string, object>();
                page.UpdatedAt = DateTime.UtcNow;
                page.UpdatedByUserId = currentUserId;

                _pageRepository.Update(page);
                await _pageRepository.SaveChangesAsync();

                // Clear cache
                await InvalidatePageCacheAsync(saveDto.PageId);

                _logger.LogInformation("Saved designer page {PageId} by user {UserId}", saveDto.PageId, currentUserId);

                return await GetDesignerPageAsync(saveDto.PageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving designer page {PageId}", saveDto.PageId);
                throw;
            }
        }

        public async Task<bool> DeleteDesignerPageAsync(int pageId)
        {
            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var success = await _pageRepository.SoftDeleteAsync(pageId, currentUserId);

                if (success)
                {
                    await InvalidatePageCacheAsync(pageId);
                    _logger.LogInformation("Deleted designer page {PageId} by user {UserId}", pageId, currentUserId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting designer page {PageId}", pageId);
                return false;
            }
        }

        public async Task<DesignerPreviewDto> GeneratePreviewAsync(int pageId, Dictionary<string, object>? settings = null)
        {
            try
            {
                var page = await _pageRepository.GetByIdAsync(pageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                var previewToken = GeneratePreviewToken();
                var expiresAt = DateTime.UtcNow.AddHours(2);

                var previewData = new
                {
                    PageId = pageId,
                    Content = page.Content,
                    Layout = page.Layout,
                    Settings = settings ?? page.Settings,
                    Styles = page.Styles,
                    PageInfo = new
                    {
                        page.Name,
                        page.Title,
                        page.Slug,
                        page.Description
                    },
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                };

                var cacheKey = CacheKeys.PreviewToken(previewToken);
                await _cacheService.SetAsync(cacheKey, previewData, TimeSpan.FromHours(2));

                return new DesignerPreviewDto
                {
                    PageId = pageId,
                    PreviewUrl = $"/api/designer/preview/{previewToken}",
                    PreviewToken = previewToken,
                    ExpiresAt = expiresAt,
                    Settings = settings ?? new Dictionary<string, object>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating preview for page {PageId}", pageId);
                throw;
            }
        }

        public async Task<string> GetPreviewContentAsync(string previewToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(previewToken))
                    throw new ArgumentException("Preview token is required");

                var cacheKey = CacheKeys.PreviewToken(previewToken);
                var previewData = await _cacheService.GetAsync<object>(cacheKey);

                if (previewData == null)
                    throw new ArgumentException("Preview not found or expired");

                return JsonSerializer.Serialize(previewData, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting preview content for token {PreviewToken}", previewToken);
                throw;
            }
        }

        public async Task<DesignerPageDto> PublishPageAsync(PublishPageDto publishDto)
        {
            try
            {
                var page = await _pageRepository.GetByIdAsync(publishDto.PageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                if (publishDto.CreateVersion)
                {
                    var version = await CreateVersionAsync(publishDto.PageId,
                        publishDto.PublishMessage ?? "Published version");

                    // Mark version as published
                    version.IsPublished = true;
                    version.PublishedAt = publishDto.ScheduledAt ?? DateTime.UtcNow;
                    _versionRepository.Update(version);
                }

                page.Status = PageStatus.Published;
                page.PublishedOn = publishDto.ScheduledAt ?? DateTime.UtcNow;
                page.PublishedBy = _userSessionService.GetCurrentUserFullName() ?? "Unknown";
                page.UpdatedAt = DateTime.UtcNow;
                page.UpdatedByUserId = currentUserId;

                _pageRepository.Update(page);
                await _pageRepository.SaveChangesAsync();

                await InvalidatePageCacheAsync(publishDto.PageId);

                _logger.LogInformation("Published page {PageId} by user {UserId}", publishDto.PageId, currentUserId);

                return await GetDesignerPageAsync(publishDto.PageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing page {PageId}", publishDto.PageId);
                throw;
            }
        }

        public async Task<DesignerPageDto> UnpublishPageAsync(int pageId)
        {
            try
            {
                var page = await _pageRepository.GetByIdAsync(pageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                page.Status = PageStatus.Draft;
                page.UpdatedAt = DateTime.UtcNow;
                page.UpdatedByUserId = currentUserId;

                _pageRepository.Update(page);
                await _pageRepository.SaveChangesAsync();

                await InvalidatePageCacheAsync(pageId);

                _logger.LogInformation("Unpublished page {PageId} by user {UserId}", pageId, currentUserId);

                return await GetDesignerPageAsync(pageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpublishing page {PageId}", pageId);
                throw;
            }
        }

        public async Task<PageVersion> CreateVersionAsync(int pageId, string? changeNotes = null)
        {
            try
            {
                var page = await _pageRepository.GetByIdAsync(pageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                var currentUserId = _userSessionService.GetCurrentUserId();
                var existingVersions = await _versionRepository.FindAsync(v => v.PageId == pageId);
                var nextVersionNumber = existingVersions.Any() ? existingVersions.Max(v => v.VersionNumber) + 1 : 1;

                var pageSnapshot = CreatePageSnapshot(page);
                var version = new PageVersion
                {
                    PageId = pageId,
                    VersionNumber = nextVersionNumber,
                    ChangeNotes = changeNotes,
                    PageSnapshot = pageSnapshot,
                    Metadata = new Dictionary<string, object>
                    {
                        { "createdBy", _userSessionService.GetCurrentUserFullName() ?? "Unknown" },
                        { "userAgent", GetUserAgent() },
                        { "ipAddress", GetClientIpAddress() }
                    },
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                await _versionRepository.AddAsync(version);
                await _versionRepository.SaveChangesAsync();

                _logger.LogInformation("Created version {VersionNumber} for page {PageId}", nextVersionNumber, pageId);

                return version;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating version for page {PageId}", pageId);
                throw;
            }
        }

        public async Task<List<PageVersionDto>> GetPageVersionsAsync(int pageId)
        {
            try
            {
                var versions = await _versionRepository.FindAsync(v => v.PageId == pageId);
                return versions.OrderByDescending(v => v.VersionNumber)
                              .Select(v => _mapper.Map<PageVersionDto>(v))
                              .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting versions for page {PageId}", pageId);
                throw;
            }
        }

        public async Task<DesignerPageDto> RestoreVersionAsync(int pageId, int versionId)
        {
            try
            {
                var page = await _pageRepository.GetByIdAsync(pageId);
                var version = await _versionRepository.GetByIdAsync(versionId);

                if (page == null || version == null || version.PageId != pageId)
                    throw new ArgumentException("Page or version not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Create backup before restoration
                await CreateVersionAsync(pageId, $"Backup before restoring version {version.VersionNumber}");

                // Restore from version snapshot
                RestorePageFromSnapshot(page, version.PageSnapshot, currentUserId);

                page.UpdatedAt = DateTime.UtcNow;
                page.UpdatedByUserId = currentUserId;
                _pageRepository.Update(page);
                await _pageRepository.SaveChangesAsync();

                await InvalidatePageCacheAsync(pageId);

                _logger.LogInformation("Restored page {PageId} to version {VersionNumber}", pageId, version.VersionNumber);

                return await GetDesignerPageAsync(pageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring version {VersionId} for page {PageId}", versionId, pageId);
                throw;
            }
        }

        public async Task<DesignerStateDto> GetDesignerStateAsync(int pageId)
        {
            try
            {
                var userId = _userSessionService.GetCurrentUserId();
                if (!userId.HasValue)
                    return new DesignerStateDto { PageId = pageId };

                var cacheKey = CacheKeys.DesignerState(pageId, userId.Value);
                var state = await _cacheService.GetAsync<DesignerStateDto>(cacheKey);

                return state ?? new DesignerStateDto { PageId = pageId };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting designer state for page {PageId}", pageId);
                return new DesignerStateDto { PageId = pageId };
            }
        }

        public async Task<DesignerStateDto> SaveDesignerStateAsync(DesignerStateDto stateDto)
        {
            try
            {
                var userId = _userSessionService.GetCurrentUserId();
                if (!userId.HasValue)
                    return stateDto;

                stateDto.LastModified = DateTime.UtcNow;
                var cacheKey = CacheKeys.DesignerState(stateDto.PageId, userId.Value);

                await _cacheService.SetAsync(cacheKey, stateDto, TimeSpan.FromDays(7));
                return stateDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving designer state for page {PageId}", stateDto.PageId);
                throw;
            }
        }

        public async Task ClearDesignerStateAsync(int pageId)
        {
            try
            {
                var userId = _userSessionService.GetCurrentUserId();
                if (!userId.HasValue) return;

                var cacheKey = CacheKeys.DesignerState(pageId, userId.Value);
                await _cacheService.RemoveAsync(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing designer state for page {PageId}", pageId);
            }
        }

        // Private helper methods
        private async Task ValidatePageDataAsync(SaveDesignerPageDto saveDto)
        {
            var errors = new List<string>();

            // Validate content
            if (saveDto.Content.Any())
            {
                var contentErrors = _contentValidationService.GetValidationErrors(saveDto.Content, "content");
                errors.AddRange(contentErrors);
            }

            // Validate layout
            if (saveDto.Layout.Any())
            {
                var layoutErrors = _contentValidationService.GetValidationErrors(saveDto.Layout, "layout");
                errors.AddRange(layoutErrors);
            }

            // Validate settings
            if (saveDto.Settings.Any())
            {
                var settingsErrors = _contentValidationService.GetValidationErrors(saveDto.Settings, "settings");
                errors.AddRange(settingsErrors);
            }

            // Validate styles
            if (saveDto.Styles.Any())
            {
                var stylesErrors = _contentValidationService.GetValidationErrors(saveDto.Styles, "styles");
                errors.AddRange(stylesErrors);
            }

            if (errors.Any())
            {
                var errorMessage = $"Page validation failed: {string.Join("; ", errors)}";
                _logger.LogWarning("Page validation failed for page {PageId}: {Errors}", saveDto.PageId, errorMessage);
                throw new ArgumentException(errorMessage);
            }
        }

        private Dictionary<string, object> CreatePageSnapshot(Page page)
        {
            return new Dictionary<string, object>
            {
                { "pageInfo", new {
                    page.Name,
                    page.Title,
                    page.Slug,
                    page.Description,
                    page.MetaTitle,
                    page.MetaDescription,
                    page.MetaKeywords,
                    page.Status,
                    page.Template,
                    page.Priority,
                    page.ParentPageId,
                    page.RequiresLogin,
                    page.AdminOnly
                }},
                { "content", page.Content },
                { "layout", page.Layout },
                { "settings", page.Settings },
                { "styles", page.Styles },
                { "snapshotDate", DateTime.UtcNow }
            };
        }

        private void RestorePageFromSnapshot(Page page, Dictionary<string, object> snapshot, int? currentUserId)
        {
            if (snapshot.TryGetValue("pageInfo", out var pageInfoObj) && pageInfoObj is JsonElement pageInfo)
            {
                if (pageInfo.TryGetProperty("name", out var name)) page.Name = name.GetString() ?? page.Name;
                if (pageInfo.TryGetProperty("title", out var title)) page.Title = title.GetString() ?? page.Title;
                if (pageInfo.TryGetProperty("description", out var desc)) page.Description = desc.GetString();
                if (pageInfo.TryGetProperty("metaTitle", out var metaTitle)) page.MetaTitle = metaTitle.GetString();
                if (pageInfo.TryGetProperty("metaDescription", out var metaDesc)) page.MetaDescription = metaDesc.GetString();
                if (pageInfo.TryGetProperty("metaKeywords", out var metaKeys)) page.MetaKeywords = metaKeys.GetString();
                if (pageInfo.TryGetProperty("template", out var template)) page.Template = template.GetString();
                if (pageInfo.TryGetProperty("priority", out var priority)) page.Priority = priority.GetInt32();
                if (pageInfo.TryGetProperty("requiresLogin", out var reqLogin)) page.RequiresLogin = reqLogin.GetBoolean();
                if (pageInfo.TryGetProperty("adminOnly", out var adminOnly)) page.AdminOnly = adminOnly.GetBoolean();
            }

            if (snapshot.TryGetValue("content", out var contentObj))
                page.Content = ParseDictionary(contentObj) ?? new Dictionary<string, object>();

            if (snapshot.TryGetValue("layout", out var layoutObj))
                page.Layout = ParseDictionary(layoutObj) ?? new Dictionary<string, object>();

            if (snapshot.TryGetValue("settings", out var settingsObj))
                page.Settings = ParseDictionary(settingsObj) ?? new Dictionary<string, object>();

            if (snapshot.TryGetValue("styles", out var stylesObj))
                page.Styles = ParseDictionary(stylesObj) ?? new Dictionary<string, object>();

            page.UpdatedByUserId = currentUserId;
        }

        private Dictionary<string, object>? ParseDictionary(object obj)
        {
            if (obj is Dictionary<string, object> dict)
                return dict;

            if (obj is JsonElement element && element.ValueKind == JsonValueKind.Object)
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
            }

            return null;
        }

        private async Task InvalidatePageCacheAsync(int pageId)
        {
            var keysToRemove = new[]
            {
                CacheKeys.DesignerPage(pageId),
                CacheKeys.PageById(pageId),
                CacheKeys.PublishedPages,
                CacheKeys.PageHierarchy
            };

            await _cacheService.RemoveAsync(keysToRemove);
            await _cacheService.RemoveByPatternAsync($"preview:*");
        }

        private static string GeneratePreviewToken()
        {
            return $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        }

        private string GetUserAgent()
        {
            return "Designer";
        }

        private string GetClientIpAddress()
        {
            return "127.0.0.1";
        }
    }
}