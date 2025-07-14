using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class PageService : IPageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;
        private readonly ILogger<PageService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public PageService(
            IUnitOfWork unitOfWork,
            IUserSessionService userSessionService,
            IMapper mapper,
            ILogger<PageService> logger)
        {
            _unitOfWork = unitOfWork;
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

        public async Task<PageDto> GetPageByIdAsync(int pageId)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID", nameof(pageId));

            try
            {
                var page = await _unitOfWork.Pages.GetByIdAsync(pageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                return _mapper.Map<PageDto>(page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting page {PageId}", pageId);
                throw;
            }
        }

        public async Task<PageDto> GetPageBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                throw new ArgumentException("Slug cannot be empty", nameof(slug));

            try
            {
                var normalizedSlug = NormalizeSlug(slug);
                // For public access, only get published pages
                var page = await _unitOfWork.Pages.GetPublishedBySlugAsync(normalizedSlug);
                return page == null ? throw new ArgumentException("Page not found") : _mapper.Map<PageDto>(page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting page by slug {Slug}", slug);
                throw;
            }
        }

        public async Task<List<PageListDto>> GetPagesAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            try
            {
                var searchTerm = search?.Trim() ?? "";

                var pages = string.IsNullOrWhiteSpace(searchTerm)
                    ? await _unitOfWork.Pages.GetPagedAsync(page, pageSize)
                    : await _unitOfWork.Pages.SearchPagesAsync(searchTerm, page, pageSize);

                var pageList = pages.ToList();
                var pageDtos = _mapper.Map<List<PageListDto>>(pageList);

                var pageIds = pageList.Select(p => p.Id).ToList();
                var allVersions = await _unitOfWork.GetRepository<PageVersion>().FindAsync(v => pageIds.Contains(v.PageId));
                var versionGroups = allVersions.GroupBy(v => v.PageId).ToDictionary(g => g.Key, g => g.ToList());

                foreach (var pageDto in pageDtos)
                {
                    if (versionGroups.TryGetValue(pageDto.Id, out var versions))
                    {
                        pageDto.VersionCount = versions.Count;
                        pageDto.CurrentVersion = versions.Count != 0 ? versions.Max(v => v.VersionNumber) : 0;
                    }
                }

                return pageDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pages list");
                throw;
            }
        }
        public async Task<PaginatedResult<PageListDto>> GetPagesPagedAsync(PageSearchDto searchDto)
        {
            try
            {
                searchDto.PageNumber = Math.Max(1, searchDto.PageNumber);
                searchDto.PageSize = Math.Clamp(searchDto.PageSize, 1, 100);

                // Build predicate for filtering
                Expression<Func<Page, bool>>? predicate = null;
                if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm) ||
                    searchDto.Status.HasValue ||
                    searchDto.ParentPageId.HasValue ||
                    searchDto.RequiresLogin.HasValue ||
                    searchDto.AdminOnly.HasValue ||
                    searchDto.CreatedFrom.HasValue ||
                    searchDto.CreatedTo.HasValue ||
                    searchDto.UpdatedFrom.HasValue ||
                    searchDto.UpdatedTo.HasValue ||
                    !string.IsNullOrWhiteSpace(searchDto.Template) ||
                    searchDto.IsPublished.HasValue)
                {
                    predicate = p =>
                        (string.IsNullOrWhiteSpace(searchDto.SearchTerm) ||
                         p.Name.Contains(searchDto.SearchTerm) ||
                         p.Title.Contains(searchDto.SearchTerm) ||
                         p.Description != null && p.Description.Contains(searchDto.SearchTerm) ||
                         p.Slug.Contains(searchDto.SearchTerm)) &&
                        (!searchDto.Status.HasValue || p.Status == searchDto.Status.Value) &&
                        (!searchDto.ParentPageId.HasValue || p.ParentPageId == searchDto.ParentPageId.Value) &&
                        (!searchDto.RequiresLogin.HasValue || p.RequiresLogin == searchDto.RequiresLogin.Value) &&
                        (!searchDto.AdminOnly.HasValue || p.AdminOnly == searchDto.AdminOnly.Value) &&
                        (!searchDto.CreatedFrom.HasValue || p.CreatedAt >= searchDto.CreatedFrom.Value) &&
                        (!searchDto.CreatedTo.HasValue || p.CreatedAt <= searchDto.CreatedTo.Value) &&
                        (!searchDto.UpdatedFrom.HasValue || p.UpdatedAt >= searchDto.UpdatedFrom.Value) &&
                        (!searchDto.UpdatedTo.HasValue || p.UpdatedAt <= searchDto.UpdatedTo.Value) &&
                        (string.IsNullOrWhiteSpace(searchDto.Template) || p.Template == searchDto.Template) &&
                        (!searchDto.IsPublished.HasValue ||
                         (searchDto.IsPublished.Value && p.Status == PageStatus.Published) ||
                         (!searchDto.IsPublished.Value && p.Status != PageStatus.Published));
                }

                // Build ordering
                Func<IQueryable<Page>, IOrderedQueryable<Page>>? orderBy = null;
                orderBy = searchDto.SortBy.ToLowerInvariant() switch
                {
                    "name" => searchDto.SortDirection.ToLowerInvariant() == "asc"
                        ? q => q.OrderBy(p => p.Name)
                        : q => q.OrderByDescending(p => p.Name),
                    "title" => searchDto.SortDirection.ToLowerInvariant() == "asc"
                        ? q => q.OrderBy(p => p.Title)
                        : q => q.OrderByDescending(p => p.Title),
                    "status" => searchDto.SortDirection.ToLowerInvariant() == "asc"
                        ? q => q.OrderBy(p => p.Status)
                        : q => q.OrderByDescending(p => p.Status),
                    "createdat" => searchDto.SortDirection.ToLowerInvariant() == "asc"
                        ? q => q.OrderBy(p => p.CreatedAt)
                        : q => q.OrderByDescending(p => p.CreatedAt),
                    "publishedon" => searchDto.SortDirection.ToLowerInvariant() == "asc"
                        ? q => q.OrderBy(p => p.PublishedOn)
                        : q => q.OrderByDescending(p => p.PublishedOn),
                    _ => searchDto.SortDirection.ToLowerInvariant() == "asc"
                        ? q => q.OrderBy(p => p.UpdatedAt)
                        : q => q.OrderByDescending(p => p.UpdatedAt)
                };

                var pagedResult = await _unitOfWork.Pages.GetPagedResultAsync(
                    searchDto.PageNumber,
                    searchDto.PageSize,
                    predicate,
                    orderBy);

                var pageListDtos = _mapper.Map<List<PageListDto>>(pagedResult.Data);

                // Enhance with version information efficiently
                if (pageListDtos.Count != 0)
                {
                    var pageIds = pageListDtos.Select(p => p.Id).ToList();
                    var allVersions = await _unitOfWork.GetRepository<PageVersion>().FindAsync(v => pageIds.Contains(v.PageId));
                    var versionGroups = allVersions.GroupBy(v => v.PageId).ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var pageDto in pageListDtos)
                    {
                        if (versionGroups.TryGetValue(pageDto.Id, out var versions))
                        {
                            pageDto.VersionCount = versions.Count;
                            pageDto.CurrentVersion = versions.Count != 0 ? versions.Max(v => v.VersionNumber) : 0;
                        }
                    }
                }

                return new PaginatedResult<PageListDto>(
                    pageListDtos,
                    pagedResult.PageNumber,
                    pagedResult.PageSize,
                    pagedResult.TotalCount);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged pages with search criteria");
                throw;
            }
        }
        public async Task<PaginatedResult<PageListDto>> SearchPagesPagedAsync(PageSearchDto searchDto)
        {
            return await GetPagesPagedAsync(searchDto);
        }
        public async Task<List<PageDto>> GetPageHierarchyAsync()
        {
            try
            {
                var pages = await _unitOfWork.Pages.GetPageHierarchyAsync();
                return _mapper.Map<List<PageDto>>(pages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting page hierarchy");
                throw;
            }
        }

        public async Task<List<PageDto>> GetPublishedPagesAsync()
        {
            try
            {
                var pages = await _unitOfWork.Pages.GetPublishedPagesAsync();
                return _mapper.Map<List<PageDto>>(pages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting published pages");
                throw;
            }
        }

        public async Task<PageDto> CreatePageAsync(CreatePageDto createPageDto)
        {
            ValidateCreatePageDto(createPageDto);

            try
            {
                var normalizedSlug = NormalizeSlug(createPageDto.Slug);

                if (await _unitOfWork.Pages.SlugExistsAsync(normalizedSlug))
                    throw new ArgumentException("A page with this slug already exists");

                if (createPageDto.ParentPageId.HasValue)
                {
                    var parentPage = await _unitOfWork.Pages.GetByIdAsync(createPageDto.ParentPageId.Value) ?? throw new ArgumentException("Parent page not found");
                }

                var page = _mapper.Map<Page>(createPageDto);
                var currentUserId = _userSessionService.GetCurrentUserId();

                page.Slug = normalizedSlug;
                page.CreatedByUserId = currentUserId;
                page.UpdatedByUserId = currentUserId;
                page.CreatedAt = DateTime.UtcNow;
                page.UpdatedAt = DateTime.UtcNow;

                // Validate content
                ValidatePageContent(page);

                await _unitOfWork.Pages.AddAsync(page);
                await _unitOfWork.Pages.SaveChangesAsync();

                // Create initial version
                await CreatePageVersionAsync(page.Id, "Initial page creation");

                _logger.LogInformation("Created page {PageId} with slug {Slug}", page.Id, page.Slug);

                return _mapper.Map<PageDto>(page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating page");
                throw;
            }
        }

        public async Task<PageDto> UpdatePageAsync(int pageId, UpdatePageDto updatePageDto)
        {
            ValidateUpdatePageDto(pageId, updatePageDto);

            try
            {
                var page = await _unitOfWork.Pages.GetByIdAsync(pageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                var normalizedSlug = NormalizeSlug(updatePageDto.Slug);
                if (await _unitOfWork.Pages.SlugExistsAsync(normalizedSlug, pageId))
                    throw new ArgumentException("A page with this slug already exists");

                if (updatePageDto.ParentPageId.HasValue)
                {
                    if (updatePageDto.ParentPageId == pageId)
                        throw new ArgumentException("A page cannot be its own parent");

                    var parentPage = await _unitOfWork.Pages.GetByIdAsync(updatePageDto.ParentPageId.Value);
                    if (parentPage == null)
                        throw new ArgumentException("Parent page not found");

                    if (await WouldCreateCircularReferenceAsync(pageId, updatePageDto.ParentPageId.Value))
                        throw new ArgumentException("Setting this parent would create a circular reference");
                }

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Create version before updating
                await CreatePageVersionAsync(pageId, "Page updated");

                // Update page properties
                _mapper.Map(updatePageDto, page);
                page.Slug = normalizedSlug;
                page.UpdatedByUserId = currentUserId;
                page.UpdatedAt = DateTime.UtcNow;

                // Validate content
                ValidatePageContent(page);

                _unitOfWork.Pages.Update(page);
                await _unitOfWork.Pages.SaveChangesAsync();

                _logger.LogInformation("Updated page {PageId}", pageId);

                return _mapper.Map<PageDto>(page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating page {PageId}", pageId);
                throw;
            }
        }

        public async Task<PageDto> SavePageStructureAsync(SavePageStructureDto savePageStructureDto)
        {
            if (savePageStructureDto.PageId <= 0)
                throw new ArgumentException("Invalid page ID");

            try
            {
                var page = await _unitOfWork.Pages.GetByIdAsync(savePageStructureDto.PageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Create version if requested
                if (savePageStructureDto.CreateVersion)
                {
                    await CreatePageVersionAsync(savePageStructureDto.PageId,
                        savePageStructureDto.ChangeDescription ?? "Structure updated");
                }

                // Update page structure
                page.Content = savePageStructureDto.Content ?? [];
                page.Layout = savePageStructureDto.Layout ?? [];
                page.Settings = savePageStructureDto.Settings ?? [];
                page.Styles = savePageStructureDto.Styles ?? [];
                page.UpdatedAt = DateTime.UtcNow;
                page.UpdatedByUserId = currentUserId;

                // Validate content
                ValidatePageContent(page);

                _unitOfWork.Pages.Update(page);
                await _unitOfWork.Pages.SaveChangesAsync();

                _logger.LogInformation("Saved page structure for {PageId}", savePageStructureDto.PageId);

                return _mapper.Map<PageDto>(page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving page structure for {PageId}", savePageStructureDto.PageId);
                throw;
            }
        }

        public async Task<bool> DeletePageAsync(int pageId)
        {
            if (pageId <= 0)
                return false;

            try
            {
                var childPages = await _unitOfWork.Pages.GetChildPagesAsync(pageId);
                if (childPages.Any())
                    throw new InvalidOperationException("Cannot delete a page that has child pages. Delete or move child pages first.");

                var currentUserId = _userSessionService.GetCurrentUserId();
                var success = await _unitOfWork.Pages.SoftDeleteAsync(pageId, currentUserId);

                if (success)
                {
                    _logger.LogInformation("Deleted page {PageId}", pageId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting page {PageId}", pageId);
                throw;
            }
        }

        public async Task<PageDto> PublishPageAsync(int pageId)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            try
            {
                var page = await _unitOfWork.Pages.GetByIdAsync(pageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                await ValidatePageForPublishingAsync(page);

                // Create published version
                var version = await CreatePageVersionAsync(pageId, "Page published");
                version.IsPublished = true;
                version.PublishedAt = DateTime.UtcNow;
                _unitOfWork.GetRepository<PageVersion>().Update(version);

                page.Status = PageStatus.Published;
                page.PublishedOn = DateTime.UtcNow;
                page.PublishedBy = _userSessionService.GetCurrentUserFullName() ?? "Unknown";
                page.UpdatedAt = DateTime.UtcNow;
                page.UpdatedByUserId = currentUserId;

                _unitOfWork.Pages.Update(page);
                await _unitOfWork.Pages.SaveChangesAsync();

                _logger.LogInformation("Published page {PageId}", pageId);

                return _mapper.Map<PageDto>(page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing page {PageId}", pageId);
                throw;
            }
        }

        public async Task<PageDto> UnpublishPageAsync(int pageId)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            try
            {
                var page = await _unitOfWork.Pages.GetByIdAsync(pageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Create version before unpublishing
                await CreatePageVersionAsync(pageId, "Page unpublished");

                page.Status = PageStatus.Draft;
                page.UpdatedAt = DateTime.UtcNow;
                page.UpdatedByUserId = currentUserId;

                _unitOfWork.Pages.Update(page);
                await _unitOfWork.Pages.SaveChangesAsync();

                _logger.LogInformation("Unpublished page {PageId}", pageId);

                return _mapper.Map<PageDto>(page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpublishing page {PageId}", pageId);
                throw;
            }
        }

        public async Task<PageDto> DuplicatePageAsync(int pageId, string newName)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New page name is required");

            try
            {
                var originalPage = await _unitOfWork.Pages.GetByIdAsync(pageId);
                if (originalPage == null)
                    throw new ArgumentException("Page not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                var baseSlug = NormalizeSlug(newName);
                var uniqueSlug = await GenerateUniqueSlugAsync(baseSlug);

                var duplicatedPage = new Page
                {
                    Name = newName.Trim(),
                    Title = originalPage.Title + " (Copy)",
                    Slug = uniqueSlug,
                    Description = originalPage.Description,
                    MetaTitle = originalPage.MetaTitle,
                    MetaDescription = originalPage.MetaDescription,
                    MetaKeywords = originalPage.MetaKeywords,
                    Status = PageStatus.Draft,
                    Template = originalPage.Template,
                    Priority = originalPage.Priority,
                    ParentPageId = originalPage.ParentPageId,
                    RequiresLogin = originalPage.RequiresLogin,
                    AdminOnly = originalPage.AdminOnly,
                    Content = DeepCopyDictionary(originalPage.Content),
                    Layout = DeepCopyDictionary(originalPage.Layout),
                    Settings = DeepCopyDictionary(originalPage.Settings),
                    Styles = DeepCopyDictionary(originalPage.Styles),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                // Validate content
                ValidatePageContent(duplicatedPage);

                await _unitOfWork.Pages.AddAsync(duplicatedPage);
                await _unitOfWork.Pages.SaveChangesAsync();

                // Create initial version for duplicated page
                await CreatePageVersionAsync(duplicatedPage.Id, $"Duplicated from page {originalPage.Name}");

                _logger.LogInformation("Duplicated page {OriginalPageId} to new page {NewPageId}", pageId, duplicatedPage.Id);

                return _mapper.Map<PageDto>(duplicatedPage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error duplicating page {PageId}", pageId);
                throw;
            }
        }

        public async Task<List<PageDto>> GetChildPagesAsync(int parentPageId)
        {
            if (parentPageId <= 0)
                throw new ArgumentException("Invalid parent page ID");

            try
            {
                var pages = await _unitOfWork.Pages.GetChildPagesAsync(parentPageId);
                return _mapper.Map<List<PageDto>>(pages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting child pages for parent {ParentPageId}", parentPageId);
                throw;
            }
        }

        public async Task<bool> ValidateSlugAsync(string slug, int? excludePageId = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return false;

            try
            {
                var normalizedSlug = NormalizeSlug(slug);
                return !await _unitOfWork.Pages.SlugExistsAsync(normalizedSlug, excludePageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating slug {Slug}", slug);
                return false;
            }
        }

        public async Task<PageVersion> CreatePageVersionAsync(int pageId, string? changeNotes = null)
        {
            try
            {
                var page = await _unitOfWork.Pages.GetByIdAsync(pageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                var existingVersions = await _unitOfWork.GetRepository<PageVersion>().FindAsync(v => v.PageId == pageId);
                var nextVersionNumber = existingVersions.Any() ? existingVersions.Max(v => v.VersionNumber) + 1 : 1;

                var pageSnapshot = new Dictionary<string, object>
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

                var version = new PageVersion
                {
                    PageId = pageId,
                    VersionNumber = nextVersionNumber,
                    ChangeNotes = changeNotes,
                    PageSnapshot = pageSnapshot,
                    Metadata = new Dictionary<string, object>
                    {
                        { "createdBy", _userSessionService.GetCurrentUserFullName() ?? "Unknown" }
                    },
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                await _unitOfWork.GetRepository<PageVersion>().AddAsync(version);
                await _unitOfWork.GetRepository<PageVersion>().SaveChangesAsync();

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
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            try
            {
                var versions = await _unitOfWork.GetRepository<PageVersion>().FindAsync(v => v.PageId == pageId);
                return [.. versions.OrderByDescending(v => v.VersionNumber).Select(v => _mapper.Map<PageVersionDto>(v))];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting versions for page {PageId}", pageId);
                throw;
            }
        }

        public async Task<PageDto> RestorePageVersionAsync(int pageId, int versionId)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            if (versionId <= 0)
                throw new ArgumentException("Invalid version ID");

            try
            {
                var page = await _unitOfWork.Pages.GetByIdAsync(pageId);
                var version = await _unitOfWork.GetRepository<PageVersion>().GetByIdAsync(versionId);

                if (page == null || version == null || version.PageId != pageId)
                    throw new ArgumentException("Page or version not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Create backup before restoration
                await CreatePageVersionAsync(pageId, $"Backup before restoring version {version.VersionNumber}");

                // Restore from version snapshot
                RestorePageFromSnapshot(page, version.PageSnapshot, currentUserId);

                page.UpdatedAt = DateTime.UtcNow;
                page.UpdatedByUserId = currentUserId;

                // Validate content after restoration
                ValidatePageContent(page);

                _unitOfWork.Pages.Update(page);
                await _unitOfWork.Pages.SaveChangesAsync();

                _logger.LogInformation("Restored page {PageId} to version {VersionNumber}", pageId, version.VersionNumber);

                return _mapper.Map<PageDto>(page);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring version {VersionId} for page {PageId}", versionId, pageId);
                throw;
            }
        }

        // Private helper methods
        private static void ValidateCreatePageDto(CreatePageDto createPageDto)
        {
            if (string.IsNullOrWhiteSpace(createPageDto.Name))
                throw new ArgumentException("Page name is required");

            if (string.IsNullOrWhiteSpace(createPageDto.Title))
                throw new ArgumentException("Page title is required");

            if (string.IsNullOrWhiteSpace(createPageDto.Slug))
                throw new ArgumentException("Page slug is required");
        }

        private static void ValidateUpdatePageDto(int pageId, UpdatePageDto updatePageDto)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            if (string.IsNullOrWhiteSpace(updatePageDto.Name))
                throw new ArgumentException("Page name is required");

            if (string.IsNullOrWhiteSpace(updatePageDto.Title))
                throw new ArgumentException("Page title is required");

            if (string.IsNullOrWhiteSpace(updatePageDto.Slug))
                throw new ArgumentException("Page slug is required");
        }

        private void ValidatePageContent(Page page)
        {
            try
            {
                // Validate that JSON content is serializable
                if (page.Content.Count != 0)
                {
                    JsonSerializer.Serialize(page.Content, _jsonOptions);
                }

                if (page.Layout.Count != 0)
                {
                    JsonSerializer.Serialize(page.Layout, _jsonOptions);
                }

                if (page.Settings.Count != 0)
                {
                    JsonSerializer.Serialize(page.Settings, _jsonOptions);
                }

                if (page.Styles.Count != 0)
                {
                    JsonSerializer.Serialize(page.Styles, _jsonOptions);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid JSON content in page {PageId}", page.Id);
                throw new ArgumentException("Page content contains invalid JSON structure");
            }
        }

        private static string NormalizeSlug(string slug)
        {
            return slug.Trim().ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("_", "-")
                .Replace("--", "-");
        }

        private async Task<string> GenerateUniqueSlugAsync(string baseSlug)
        {
            var slug = baseSlug;
            var counter = 1;

            while (await _unitOfWork.Pages.SlugExistsAsync(slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }

        private async Task<bool> WouldCreateCircularReferenceAsync(int pageId, int? parentPageId)
        {
            var currentParentId = parentPageId;

            while (currentParentId.HasValue)
            {
                if (currentParentId == pageId)
                    return true;

                var parentPage = await _unitOfWork.Pages.GetByIdAsync(currentParentId.Value);
                currentParentId = parentPage?.ParentPageId;
            }

            return false;
        }

        private async Task ValidatePageForPublishingAsync(Page page)
        {
            if (string.IsNullOrWhiteSpace(page.Title))
                throw new InvalidOperationException("Page title is required for publishing");

            if (string.IsNullOrWhiteSpace(page.Slug))
                throw new InvalidOperationException("Page slug is required for publishing");

            await Task.CompletedTask;
        }

        private void RestorePageFromSnapshot(Page page, Dictionary<string, object> snapshot, int? currentUserId)
        {
            if (snapshot.TryGetValue("pageInfo", out var pageInfoObj))
            {
                var pageInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(pageInfoObj.ToString() ?? "{}");
                if (pageInfo != null)
                {
                    if (pageInfo.TryGetValue("name", out var name)) page.Name = name?.ToString() ?? page.Name;
                    if (pageInfo.TryGetValue("title", out var title)) page.Title = title?.ToString() ?? page.Title;
                    if (pageInfo.TryGetValue("description", out var desc)) page.Description = desc?.ToString();
                    if (pageInfo.TryGetValue("metaTitle", out var metaTitle)) page.MetaTitle = metaTitle?.ToString();
                    if (pageInfo.TryGetValue("metaDescription", out var metaDesc)) page.MetaDescription = metaDesc?.ToString();
                    if (pageInfo.TryGetValue("metaKeywords", out var metaKeys)) page.MetaKeywords = metaKeys?.ToString();
                    if (pageInfo.TryGetValue("template", out var template)) page.Template = template?.ToString();
                    if (pageInfo.TryGetValue("priority", out var priority) && int.TryParse(priority?.ToString(), out var p)) page.Priority = p;
                    if (pageInfo.TryGetValue("requiresLogin", out var reqLogin) && bool.TryParse(reqLogin?.ToString(), out var rl)) page.RequiresLogin = rl;
                    if (pageInfo.TryGetValue("adminOnly", out var adminOnly) && bool.TryParse(adminOnly?.ToString(), out var ao)) page.AdminOnly = ao;
                }
            }

            if (snapshot.TryGetValue("content", out var contentObj))
                page.Content = ParseDictionary(contentObj) ?? [];

            if (snapshot.TryGetValue("layout", out var layoutObj))
                page.Layout = ParseDictionary(layoutObj) ?? [];

            if (snapshot.TryGetValue("settings", out var settingsObj))
                page.Settings = ParseDictionary(settingsObj) ?? [];

            if (snapshot.TryGetValue("styles", out var stylesObj))
                page.Styles = ParseDictionary(stylesObj) ?? [];

            page.UpdatedByUserId = currentUserId;
        }

        private Dictionary<string, object>? ParseDictionary(object obj)
        {
            try
            {
                if (obj is Dictionary<string, object> dict)
                    return dict;

                if (obj is JsonElement element && element.ValueKind == JsonValueKind.Object)
                {
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
                }

                var jsonString = obj?.ToString();
                if (!string.IsNullOrEmpty(jsonString))
                {
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse dictionary from object");
            }

            return null;
        }

        private Dictionary<string, object> DeepCopyDictionary(Dictionary<string, object> original)
        {
            try
            {
                var json = JsonSerializer.Serialize(original, _jsonOptions);
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deep copy dictionary, returning empty dictionary");
                return [];
            }
        }
    }
}