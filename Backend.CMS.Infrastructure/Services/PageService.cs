using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.DTOs.Designer;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.IRepositories;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class PageService : IPageService
    {
        private readonly IPageRepository _pageRepository;
        private readonly IRepository<PageComponent> _componentRepository;
        private readonly IRepository<PageVersion> _versionRepository;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;

        public PageService(
            IPageRepository pageRepository,
            IRepository<PageComponent> componentRepository,
            IRepository<PageVersion> versionRepository,
            IUserSessionService userSessionService,
            IMapper mapper)
        {
            _pageRepository = pageRepository;
            _componentRepository = componentRepository;
            _versionRepository = versionRepository;
            _mapper = mapper;
            _userSessionService = userSessionService;
        }

        public async Task<PageDto> GetPageByIdAsync(int pageId)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            var page = await _pageRepository.GetWithComponentsAsync(pageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            return _mapper.Map<PageDto>(page);
        }

        public async Task<PageDto> GetPageBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                throw new ArgumentException("Slug cannot be empty");

            var page = await _pageRepository.GetBySlugAsync(slug.Trim().ToLowerInvariant());
            if (page == null)
                throw new ArgumentException("Page not found");

            return _mapper.Map<PageDto>(page);
        }

        public async Task<List<PageListDto>> GetPagesAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            // Input validation
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var pages = string.IsNullOrWhiteSpace(search)
                ? await _pageRepository.GetPagedAsync(page, pageSize)
                : await _pageRepository.SearchPagesAsync(search.Trim(), page, pageSize);

            return _mapper.Map<List<PageListDto>>(pages);
        }

        public async Task<List<PageDto>> GetPageHierarchyAsync()
        {
            var pages = await _pageRepository.GetPageHierarchyAsync();
            return _mapper.Map<List<PageDto>>(pages);
        }

        public async Task<PageDto> CreatePageAsync(CreatePageDto createPageDto)
        {
            //  Input validation
            if (string.IsNullOrWhiteSpace(createPageDto.Name))
                throw new ArgumentException("Page name is required");

            if (string.IsNullOrWhiteSpace(createPageDto.Title))
                throw new ArgumentException("Page title is required");

            if (string.IsNullOrWhiteSpace(createPageDto.Slug))
                throw new ArgumentException("Page slug is required");

            // Normalize slug
            var normalizedSlug = NormalizeSlug(createPageDto.Slug);

            // Check if slug already exists
            if (await _pageRepository.SlugExistsAsync(normalizedSlug))
                throw new ArgumentException("A page with this slug already exists");

            //  Validate parent page exists if specified
            if (createPageDto.ParentPageId.HasValue)
            {
                var parentPage = await _pageRepository.GetByIdAsync(createPageDto.ParentPageId.Value);
                if (parentPage == null)
                    throw new ArgumentException("Parent page not found");
            }

            var page = _mapper.Map<Page>(createPageDto);
            var currentUserId = _userSessionService.GetCurrentUserId();

            page.Slug = normalizedSlug;
            page.CreatedByUserId = currentUserId;
            page.UpdatedByUserId = currentUserId;
            page.CreatedAt = DateTime.UtcNow;
            page.UpdatedAt = DateTime.UtcNow;

            await _pageRepository.AddAsync(page);
            await _pageRepository.SaveChangesAsync();

            return _mapper.Map<PageDto>(page);
        }

        public async Task<PageDto> UpdatePageAsync(int pageId, UpdatePageDto updatePageDto)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            //  Input validation
            if (string.IsNullOrWhiteSpace(updatePageDto.Name))
                throw new ArgumentException("Page name is required");

            if (string.IsNullOrWhiteSpace(updatePageDto.Title))
                throw new ArgumentException("Page title is required");

            if (string.IsNullOrWhiteSpace(updatePageDto.Slug))
                throw new ArgumentException("Page slug is required");

            var page = await _pageRepository.GetByIdAsync(pageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            //  Normalize slug and check uniqueness
            var normalizedSlug = NormalizeSlug(updatePageDto.Slug);
            if (await _pageRepository.SlugExistsAsync(normalizedSlug, pageId))
                throw new ArgumentException("A page with this slug already exists");

            //  Validate parent page if specified and prevent circular references
            if (updatePageDto.ParentPageId.HasValue)
            {
                if (updatePageDto.ParentPageId == pageId)
                    throw new ArgumentException("A page cannot be its own parent");

                var parentPage = await _pageRepository.GetByIdAsync(updatePageDto.ParentPageId.Value);
                if (parentPage == null)
                    throw new ArgumentException("Parent page not found");

                // Check for circular reference
                if (await WouldCreateCircularReferenceAsync(pageId, updatePageDto.ParentPageId.Value))
                    throw new ArgumentException("Setting this parent would create a circular reference");
            }

            var currentUserId = _userSessionService.GetCurrentUserId();

            _mapper.Map(updatePageDto, page);
            page.Slug = normalizedSlug;
            page.UpdatedByUserId = currentUserId;
            page.UpdatedAt = DateTime.UtcNow;

            _pageRepository.Update(page);
            await _pageRepository.SaveChangesAsync();

            return _mapper.Map<PageDto>(page);
        }

        public async Task<bool> DeletePageAsync(int pageId)
        {
            if (pageId <= 0)
                return false;

            //  Check if page has children
            var childPages = await _pageRepository.GetChildPagesAsync(pageId);
            if (childPages.Any())
                throw new InvalidOperationException("Cannot delete a page that has child pages. Delete or move child pages first.");

            var currentUserId = _userSessionService.GetCurrentUserId();
            return await _pageRepository.SoftDeleteAsync(pageId, currentUserId);
        }

        public async Task<PageDto> SavePageStructureAsync(SavePageStructureDto savePageStructureDto)
        {
            if (savePageStructureDto.PageId <= 0)
                throw new ArgumentException("Invalid page ID");

            if (savePageStructureDto.Components == null)
                throw new ArgumentException("Components cannot be null");

            var page = await _pageRepository.GetWithComponentsAsync(savePageStructureDto.PageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            //  Create a backup version before making changes
            await CreatePageVersionAsync(savePageStructureDto.PageId, "Auto-backup before structure changes");

            // Soft delete existing components
            foreach (var component in page.Components)
            {
                await _componentRepository.SoftDeleteAsync(component, currentUserId);
            }

            //  Validate and add new components with proper hierarchy
            var validatedComponents = ValidateComponentHierarchy(savePageStructureDto.Components);
            var components = await CreateComponentsFromDtosAsync(validatedComponents, savePageStructureDto.PageId, currentUserId);

            foreach (var component in components)
            {
                await _componentRepository.AddAsync(component);
            }

            page.UpdatedAt = DateTime.UtcNow;
            page.UpdatedByUserId = currentUserId;
            _pageRepository.Update(page);
            await _pageRepository.SaveChangesAsync();

            // Return updated page
            var updatedPage = await _pageRepository.GetWithComponentsAsync(savePageStructureDto.PageId);
            return _mapper.Map<PageDto>(updatedPage);
        }

        public async Task<PageDto> PublishPageAsync(int pageId)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            var page = await _pageRepository.GetByIdAsync(pageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            //  Validate page is ready for publishing
            await ValidatePageForPublishingAsync(page);

            page.Status = PageStatus.Published;
            page.PublishedOn = DateTime.UtcNow;
            page.PublishedBy = _userSessionService.GetCurrentUserFullName() ?? "Unknown";
            page.UpdatedAt = DateTime.UtcNow;
            page.UpdatedByUserId = currentUserId;

            _pageRepository.Update(page);
            await _pageRepository.SaveChangesAsync();

            return _mapper.Map<PageDto>(page);
        }

        public async Task<PageDto> UnpublishPageAsync(int pageId)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            var page = await _pageRepository.GetByIdAsync(pageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            page.Status = PageStatus.Draft;
            page.UpdatedAt = DateTime.UtcNow;
            page.UpdatedByUserId = currentUserId;

            _pageRepository.Update(page);
            await _pageRepository.SaveChangesAsync();

            return _mapper.Map<PageDto>(page);
        }

        public async Task<PageDto> DuplicatePageAsync(int pageId, string newName)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New page name is required");

            var originalPage = await _pageRepository.GetWithComponentsAsync(pageId);
            if (originalPage == null)
                throw new ArgumentException("Page not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            //  Generate unique slug
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            await _pageRepository.AddAsync(duplicatedPage);
            await _pageRepository.SaveChangesAsync();

            //  Duplicate components with proper hierarchy mapping
            await DuplicateComponentsAsync(originalPage.Components, duplicatedPage.Id, currentUserId);

            await _componentRepository.SaveChangesAsync();

            var finalPage = await _pageRepository.GetWithComponentsAsync(duplicatedPage.Id);
            return _mapper.Map<PageDto>(finalPage);
        }

        public async Task<List<PageDto>> GetPublishedPagesAsync()
        {
            var pages = await _pageRepository.GetPublishedPagesAsync();
            return _mapper.Map<List<PageDto>>(pages);
        }

        public async Task<List<PageDto>> GetChildPagesAsync(int parentPageId)
        {
            if (parentPageId <= 0)
                throw new ArgumentException("Invalid parent page ID");

            var pages = await _pageRepository.GetChildPagesAsync(parentPageId);
            return _mapper.Map<List<PageDto>>(pages);
        }

        public async Task<bool> ValidateSlugAsync(string slug, int? excludePageId = null)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return false;

            var normalizedSlug = NormalizeSlug(slug);
            return !await _pageRepository.SlugExistsAsync(normalizedSlug, excludePageId);
        }

        public async Task<PageDto> CreatePageVersionAsync(int pageId, string? changeNotes = null)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            var page = await _pageRepository.GetWithComponentsAsync(pageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            // Get the next version number
            var existingVersions = await _versionRepository.FindAsync(v => v.PageId == pageId);
            var nextVersionNumber = existingVersions.Any() ? existingVersions.Max(v => v.VersionNumber) + 1 : 1;

            //  Create a proper snapshot of the current page with better serialization
            var pageSnapshot = CreatePageSnapshot(page);

            var version = new PageVersion
            {
                PageId = pageId,
                VersionNumber = nextVersionNumber,
                Data = JsonSerializer.Serialize(pageSnapshot, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                ChangeNotes = changeNotes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            await _versionRepository.AddAsync(version);
            await _versionRepository.SaveChangesAsync();

            return _mapper.Map<PageDto>(page);
        }

        public async Task<List<PageVersionDto>> GetPageVersionsAsync(int pageId)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            var versions = await _versionRepository.FindAsync(v => v.PageId == pageId);
            return versions.OrderByDescending(v => v.VersionNumber)
                          .Select(v => new PageVersionDto
                          {
                              Id = v.Id,
                              VersionNumber = v.VersionNumber,
                              ChangeNotes = v.ChangeNotes,
                              CreatedAt = v.CreatedAt, 
                          }).ToList();
        }

        public async Task<PageDto> RestorePageVersionAsync(int pageId, int versionId)
        {
            if (pageId <= 0)
                throw new ArgumentException("Invalid page ID");

            if (versionId <= 0)
                throw new ArgumentException("Invalid version ID");

            var page = await _pageRepository.GetWithComponentsAsync(pageId);
            var version = await _versionRepository.GetByIdAsync(versionId);

            if (page == null || version == null || version.PageId != pageId)
                throw new ArgumentException("Page or version not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            try
            {
                // Create a backup before restoration
                await CreatePageVersionAsync(pageId, $"Backup before restoring version {version.VersionNumber}");

                // Properly deserialize and restore the version data
                var versionData = JsonSerializer.Deserialize<PageSnapshotDto>(version.Data, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (versionData != null)
                {
                    await RestorePageFromSnapshotAsync(page, versionData, currentUserId);
                }

                page.UpdatedAt = DateTime.UtcNow;
                page.UpdatedByUserId = currentUserId;
                _pageRepository.Update(page);
                await _pageRepository.SaveChangesAsync();

                var restoredPage = await _pageRepository.GetWithComponentsAsync(pageId);
                return _mapper.Map<PageDto>(restoredPage);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to restore page version: {ex.Message}");
            }
        }

        //  Private helper methods with proper implementation
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

            while (await _pageRepository.SlugExistsAsync(slug))
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

                var parentPage = await _pageRepository.GetByIdAsync(currentParentId.Value);
                currentParentId = parentPage?.ParentPageId;
            }

            return false;
        }

        private async Task ValidatePageForPublishingAsync(Page page)
        {
            // Check if required fields are present
            if (string.IsNullOrWhiteSpace(page.Title))
                throw new InvalidOperationException("Page title is required for publishing");

            if (string.IsNullOrWhiteSpace(page.Slug))
                throw new InvalidOperationException("Page slug is required for publishing");

            // Additional validation can be added here
            await Task.CompletedTask;
        }

        private List<PageComponentDto> ValidateComponentHierarchy(List<PageComponentDto> components)
        {
            //Validate component hierarchy
            var validatedComponents = new List<PageComponentDto>();
            var processedIds = new HashSet<int>();

            // Process root components first (no parent)
            foreach (var component in components.Where(c => c.ParentComponentId == null).OrderBy(c => c.Order))
            {
                validatedComponents.Add(component);
                processedIds.Add(component.Id);

                // Recursively add child components
                AddChildComponents(component, components, validatedComponents, processedIds);
            }

            return validatedComponents;
        }

        private void AddChildComponents(PageComponentDto parent, List<PageComponentDto> allComponents,
            List<PageComponentDto> result, HashSet<int> processedIds)
        {
            var children = allComponents
                .Where(c => c.ParentComponentId == parent.Id && !processedIds.Contains(c.Id))
                .OrderBy(c => c.Order)
                .ToList();

            foreach (var child in children)
            {
                result.Add(child);
                processedIds.Add(child.Id);
                AddChildComponents(child, allComponents, result, processedIds);
            }
        }

        private async Task<List<PageComponent>> CreateComponentsFromDtosAsync(List<PageComponentDto> componentDtos, int pageId, int? currentUserId)
        {
            var components = new List<PageComponent>();
            var idMapping = new Dictionary<int, int>();

            foreach (var dto in componentDtos)
            {
                var component = new PageComponent
                {
                    PageId = pageId,
                    Type = dto.Type,
                    Name = dto.Name,
                    Properties = new Dictionary<string, object>(dto.Properties),
                    Styles = new Dictionary<string, object>(dto.Styles),
                    Content = new Dictionary<string, object>(dto.Content),
                    Order = dto.Order,
                    IsVisible = dto.IsVisible,
                    CssClasses = dto.CssClasses,
                    CustomCss = dto.CustomCss,
                    ResponsiveSettings = new Dictionary<string, object>(dto.ResponsiveSettings),
                    AnimationSettings = new Dictionary<string, object>(dto.AnimationSettings),
                    InteractionSettings = new Dictionary<string, object>(dto.InteractionSettings),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                // Handle parent relationship after all components are created
                if (dto.ParentComponentId.HasValue && idMapping.ContainsKey(dto.ParentComponentId.Value))
                {
                    component.ParentComponentId = idMapping[dto.ParentComponentId.Value];
                }

                components.Add(component);
                // Note: This assumes the DTO ID is meaningful for mapping - may need adjustment
                if (dto.Id > 0)
                {
                    idMapping[dto.Id] = component.Id;
                }
            }

            return components;
        }

        private async Task DuplicateComponentsAsync(ICollection<PageComponent> originalComponents, int newPageId, int? currentUserId)
        {
            var idMapping = new Dictionary<int, int>(); // old ID -> new component for parent mapping

            // First pass: create all components without parent relationships
            foreach (var originalComponent in originalComponents.Where(c => c.ParentComponentId == null).OrderBy(c => c.Order))
            {
                await DuplicateComponentRecursivelyAsync(originalComponent, newPageId, null, currentUserId, idMapping);
            }
        }

        private async Task DuplicateComponentRecursivelyAsync(PageComponent originalComponent, int newPageId,
            int? newParentId, int? currentUserId, Dictionary<int, int> idMapping)
        {
            var duplicatedComponent = new PageComponent
            {
                PageId = newPageId,
                Type = originalComponent.Type,
                Name = originalComponent.Name,
                Properties = new Dictionary<string, object>(originalComponent.Properties),
                Styles = new Dictionary<string, object>(originalComponent.Styles),
                Content = new Dictionary<string, object>(originalComponent.Content),
                Order = originalComponent.Order,
                ParentComponentId = newParentId,
                IsVisible = originalComponent.IsVisible,
                CssClasses = originalComponent.CssClasses,
                CustomCss = originalComponent.CustomCss,
                ResponsiveSettings = new Dictionary<string, object>(originalComponent.ResponsiveSettings),
                AnimationSettings = new Dictionary<string, object>(originalComponent.AnimationSettings),
                InteractionSettings = new Dictionary<string, object>(originalComponent.InteractionSettings),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            await _componentRepository.AddAsync(duplicatedComponent);
            await _componentRepository.SaveChangesAsync(); // Save to get the new ID

            // Map old ID to new ID
            idMapping[originalComponent.Id] = duplicatedComponent.Id;

            // Recursively duplicate child components
            var childComponents = originalComponent.ChildComponents.OrderBy(c => c.Order);
            foreach (var childComponent in childComponents)
            {
                await DuplicateComponentRecursivelyAsync(childComponent, newPageId, duplicatedComponent.Id, currentUserId, idMapping);
            }
        }

        private PageSnapshotDto CreatePageSnapshot(Page page)
        {
            return new PageSnapshotDto
            {
                Name = page.Name,
                Title = page.Title,
                Slug = page.Slug,
                Description = page.Description,
                MetaTitle = page.MetaTitle,
                MetaDescription = page.MetaDescription,
                MetaKeywords = page.MetaKeywords,
                Status = page.Status,
                Template = page.Template,
                Priority = page.Priority,
                ParentPageId = page.ParentPageId,
                RequiresLogin = page.RequiresLogin,
                AdminOnly = page.AdminOnly,
                Components = page.Components.Select(c => new ComponentSnapshotDto
                {
                    Type = c.Type,
                    Name = c.Name,
                    Properties = c.Properties,
                    Styles = c.Styles,
                    Content = c.Content,
                    Order = c.Order,
                    ParentComponentId = c.ParentComponentId,
                    IsVisible = c.IsVisible,
                    CssClasses = c.CssClasses,
                    CustomCss = c.CustomCss,
                    ResponsiveSettings = c.ResponsiveSettings,
                    AnimationSettings = c.AnimationSettings,
                    InteractionSettings = c.InteractionSettings
                }).ToList()
            };
        }

        private async Task RestorePageFromSnapshotAsync(Page page, PageSnapshotDto snapshot, int? currentUserId)
        {
            // Restore page properties
            page.Name = snapshot.Name;
            page.Title = snapshot.Title;
            page.Description = snapshot.Description;
            page.MetaTitle = snapshot.MetaTitle;
            page.MetaDescription = snapshot.MetaDescription;
            page.MetaKeywords = snapshot.MetaKeywords;
            page.Template = snapshot.Template;
            page.Priority = snapshot.Priority;
            page.RequiresLogin = snapshot.RequiresLogin;
            page.AdminOnly = snapshot.AdminOnly;

            // Delete existing components
            foreach (var component in page.Components)
            {
                await _componentRepository.SoftDeleteAsync(component, currentUserId);
            }

            // Restore components
            foreach (var componentSnapshot in snapshot.Components)
            {
                var component = new PageComponent
                {
                    PageId = page.Id,
                    Type = componentSnapshot.Type,
                    Name = componentSnapshot.Name,
                    Properties = new Dictionary<string, object>(componentSnapshot.Properties),
                    Styles = new Dictionary<string, object>(componentSnapshot.Styles),
                    Content = new Dictionary<string, object>(componentSnapshot.Content),
                    Order = componentSnapshot.Order,
                    ParentComponentId = componentSnapshot.ParentComponentId,
                    IsVisible = componentSnapshot.IsVisible,
                    CssClasses = componentSnapshot.CssClasses,
                    CustomCss = componentSnapshot.CustomCss,
                    ResponsiveSettings = new Dictionary<string, object>(componentSnapshot.ResponsiveSettings),
                    AnimationSettings = new Dictionary<string, object>(componentSnapshot.AnimationSettings),
                    InteractionSettings = new Dictionary<string, object>(componentSnapshot.InteractionSettings),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                await _componentRepository.AddAsync(component);
            }
        }
    }
}