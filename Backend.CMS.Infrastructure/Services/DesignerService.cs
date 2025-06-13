using AutoMapper;
using Backend.CMS.Application.DTOs.Designer;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class DesignerService : IDesignerService
    {
        private readonly IPageRepository _pageRepository;
        private readonly IRepository<PageComponent> _componentRepository;
        private readonly IRepository<PageVersion> _versionRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidationService _cacheInvalidationService; 
        private readonly IMapper _mapper;
        private readonly ILogger<DesignerService> _logger;
        private readonly IDistributedCache _distributedCache;

        public DesignerService(
            IPageRepository pageRepository,
            IRepository<PageComponent> componentRepository,
            IRepository<PageVersion> versionRepository,
            IUserSessionService userSessionService,
            ICacheService cacheService,
            ICacheInvalidationService cacheInvalidationService, 
            IMapper mapper,
            ILogger<DesignerService> logger,
            IDistributedCache distributedCache)
        {
            _pageRepository = pageRepository;
            _componentRepository = componentRepository;
            _versionRepository = versionRepository;
            _userSessionService = userSessionService;
            _cacheService = cacheService;
            _cacheInvalidationService = cacheInvalidationService; // Initialized
            _mapper = mapper;
            _logger = logger;
            _distributedCache = distributedCache;
        }

        #region Page Operations

        public async Task<DesignerPageDto> GetDesignerPageAsync(int pageId)
        {
            var cacheKey = $"designer:page:{pageId}";

            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var page = await _pageRepository.GetWithComponentsAsync(pageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                return await MapToDesignerPageDto(page);
            }) ?? throw new ArgumentException("Page not found");
        }

        public async Task<DesignerPageDto> SaveDesignerPageAsync(SaveDesignerPageDto saveDto)
        {
            var page = await _pageRepository.GetWithComponentsAsync(saveDto.PageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            // Create version if requested
            if (saveDto.CreateVersion)
            {
                await CreateVersionInternalAsync(page, saveDto.ChangeDescription);
            }

            // Update page properties
            page.UpdatedAt = DateTime.UtcNow;
            page.UpdatedByUserId = currentUserId;

            // Clear existing components and rebuild
            var existingComponents = await _componentRepository.FindAsync(c => c.PageId == saveDto.PageId);
            foreach (var component in existingComponents)
            {
                await _componentRepository.SoftDeleteAsync(component, currentUserId);
            }

            // Create new components from the saved data
            await CreateComponentsFromDtos(saveDto.Components, saveDto.PageId, currentUserId);

            await _pageRepository.SaveChangesAsync();

            // Clear cache
            await _cacheService.RemoveAsync($"designer:page:{saveDto.PageId}");
            await _cacheInvalidationService.InvalidatePageCacheAsync(saveDto.PageId); // Corrected service call

            _logger.LogInformation("Designer page saved: PageId={PageId}, ComponentCount={ComponentCount}",
                saveDto.PageId, saveDto.Components.Count);

            return await GetDesignerPageAsync(saveDto.PageId);
        }

        public async Task<bool> DeleteDesignerPageAsync(int pageId)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();
            var result = await _pageRepository.SoftDeleteAsync(pageId, currentUserId);

            if (result)
            {
                await _cacheService.RemoveAsync($"designer:page:{pageId}");
                await _cacheInvalidationService.InvalidatePageCacheAsync(pageId); 
            }

            return result;
        }

        #endregion

        #region Component Operations

        public async Task<DesignerComponentDto> CreateComponentAsync(CreateComponentDto createDto)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();

            // Generate unique component key if not provided
            if (string.IsNullOrEmpty(createDto.ComponentKey))
            {
                createDto.ComponentKey = GenerateComponentKey();
            }

            // Get default properties for the component type
            var defaultData = GetDefaultComponentData(createDto.Type);

            var component = new PageComponent
            {
                PageId = createDto.PageId,
                Type = createDto.Type,
                Name = createDto.Name,
                ComponentKey = createDto.ComponentKey,
                GridColumn = createDto.GridColumn,
                GridColumnSpan = createDto.GridColumnSpan,
                GridRow = createDto.GridRow,
                GridRowSpan = createDto.GridRowSpan,
                Order = createDto.Order,
                ParentComponentId = createDto.ParentComponentId,
                Properties = MergeWithDefaults(createDto.Properties, defaultData.Properties),
                Styles = MergeWithDefaults(createDto.Styles, defaultData.Styles),
                Content = MergeWithDefaults(createDto.Content, defaultData.Content),
                Settings = MergeWithDefaults(createDto.Settings, defaultData.Settings),
                IsVisible = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            await _componentRepository.AddAsync(component);
            await _componentRepository.SaveChangesAsync();

            // Clear cache
            await _cacheService.RemoveAsync($"designer:page:{createDto.PageId}");

            _logger.LogInformation("Component created: ComponentId={ComponentId}, Type={Type}, PageId={PageId}",
                component.Id, component.Type, createDto.PageId);

            return await MapToDesignerComponentDto(component);
        }

        public async Task<DesignerComponentDto> UpdateComponentAsync(UpdateComponentDto updateDto)
        {
            var component = await _componentRepository.GetByIdAsync(updateDto.ComponentId);
            if (component == null)
                throw new ArgumentException("Component not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            // Update only provided properties
            if (!string.IsNullOrEmpty(updateDto.Name))
                component.Name = updateDto.Name;

            if (updateDto.GridColumn.HasValue)
                component.GridColumn = updateDto.GridColumn.Value;

            if (updateDto.GridColumnSpan.HasValue)
                component.GridColumnSpan = updateDto.GridColumnSpan.Value;

            if (updateDto.GridRow.HasValue)
                component.GridRow = updateDto.GridRow.Value;

            if (updateDto.GridRowSpan.HasValue)
                component.GridRowSpan = updateDto.GridRowSpan.Value;

            if (updateDto.Properties != null)
                component.Properties = updateDto.Properties;

            if (updateDto.Styles != null)
                component.Styles = updateDto.Styles;

            if (updateDto.Content != null)
                component.Content = updateDto.Content;

            if (updateDto.Settings != null)
                component.Settings = updateDto.Settings;

            if (updateDto.ResponsiveSettings != null)
                component.ResponsiveSettings = updateDto.ResponsiveSettings;

            if (updateDto.AnimationSettings != null)
                component.AnimationSettings = updateDto.AnimationSettings;

            if (updateDto.InteractionSettings != null)
                component.InteractionSettings = updateDto.InteractionSettings;

            if (updateDto.IsVisible.HasValue)
                component.IsVisible = updateDto.IsVisible.Value;

            if (updateDto.IsLocked.HasValue)
                component.IsLocked = updateDto.IsLocked.Value;

            if (updateDto.CssClasses != null)
                component.CssClasses = updateDto.CssClasses;

            if (updateDto.CustomCss != null)
                component.CustomCss = updateDto.CustomCss;

            component.UpdatedAt = DateTime.UtcNow;
            component.UpdatedByUserId = currentUserId;

            _componentRepository.Update(component);
            await _componentRepository.SaveChangesAsync();

            // Clear cache
            await _cacheService.RemoveAsync($"designer:page:{component.PageId}");

            _logger.LogInformation("Component updated: ComponentId={ComponentId}, Key={ComponentKey}",
                updateDto.ComponentId, updateDto.ComponentKey);

            return await MapToDesignerComponentDto(component);
        }

        public async Task<DesignerComponentDto> DuplicateComponentAsync(DuplicateComponentDto duplicateDto)
        {
            var originalComponent = await _componentRepository.GetByIdAsync(duplicateDto.ComponentId);
            if (originalComponent == null)
                throw new ArgumentException("Component not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            var duplicatedComponent = new PageComponent
            {
                PageId = originalComponent.PageId,
                Type = originalComponent.Type,
                Name = duplicateDto.NewName ?? $"Copy of {originalComponent.Name}",
                ComponentKey = GenerateComponentKey(),
                GridColumn = duplicateDto.GridColumn,
                GridRow = duplicateDto.GridRow,
                GridColumnSpan = originalComponent.GridColumnSpan,
                GridRowSpan = originalComponent.GridRowSpan,
                Order = duplicateDto.Order,
                ParentComponentId = duplicateDto.ParentComponentId,
                Properties = new Dictionary<string, object>(originalComponent.Properties),
                Styles = new Dictionary<string, object>(originalComponent.Styles),
                Content = new Dictionary<string, object>(originalComponent.Content),
                Settings = new Dictionary<string, object>(originalComponent.Settings),
                ResponsiveSettings = new Dictionary<string, object>(originalComponent.ResponsiveSettings),
                AnimationSettings = new Dictionary<string, object>(originalComponent.AnimationSettings),
                InteractionSettings = new Dictionary<string, object>(originalComponent.InteractionSettings),
                IsVisible = originalComponent.IsVisible,
                IsLocked = false, // Unlock duplicated component
                CssClasses = originalComponent.CssClasses,
                CustomCss = originalComponent.CustomCss,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            await _componentRepository.AddAsync(duplicatedComponent);
            await _componentRepository.SaveChangesAsync();

            // Clear cache
            await _cacheService.RemoveAsync($"designer:page:{originalComponent.PageId}");

            _logger.LogInformation("Component duplicated: OriginalId={OriginalId}, NewId={NewId}",
                duplicateDto.ComponentId, duplicatedComponent.Id);

            return await MapToDesignerComponentDto(duplicatedComponent);
        }

        public async Task<bool> DeleteComponentAsync(int componentId, string componentKey)
        {
            var component = await _componentRepository.GetByIdAsync(componentId);
            if (component == null)
                return false;

            var currentUserId = _userSessionService.GetCurrentUserId();

            // Delete child components recursively
            var childComponents = await _componentRepository.FindAsync(c => c.ParentComponentId == componentId);
            foreach (var child in childComponents)
            {
                await _componentRepository.SoftDeleteAsync(child, currentUserId);
            }

            var result = await _componentRepository.SoftDeleteAsync(componentId, currentUserId);

            if (result)
            {
                await _cacheService.RemoveAsync($"designer:page:{component.PageId}");
                _logger.LogInformation("Component deleted: ComponentId={ComponentId}, Key={ComponentKey}",
                    componentId, componentKey);
            }

            return result;
        }

        public async Task<DesignerComponentDto> MoveComponentAsync(MoveComponentDto moveDto)
        {
            var component = await _componentRepository.GetByIdAsync(moveDto.ComponentId);
            if (component == null)
                throw new ArgumentException("Component not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            component.ParentComponentId = moveDto.NewParentComponentId;
            component.GridColumn = moveDto.NewGridColumn;
            component.GridRow = moveDto.NewGridRow;
            component.Order = moveDto.NewOrder;
            component.UpdatedAt = DateTime.UtcNow;
            component.UpdatedByUserId = currentUserId;

            _componentRepository.Update(component);
            await _componentRepository.SaveChangesAsync();

            // Clear cache
            await _cacheService.RemoveAsync($"designer:page:{component.PageId}");

            _logger.LogInformation("Component moved: ComponentId={ComponentId}, NewPosition=({Column},{Row})",
                moveDto.ComponentId, moveDto.NewGridColumn, moveDto.NewGridRow);

            return await MapToDesignerComponentDto(component);
        }

        public async Task<List<DesignerComponentDto>> ReorderComponentsAsync(int pageId, List<ComponentOrderDto> componentOrders)
        {
            var components = await _componentRepository.FindAsync(c => c.PageId == pageId);
            var currentUserId = _userSessionService.GetCurrentUserId();

            foreach (var orderDto in componentOrders)
            {
                var component = components.FirstOrDefault(c => c.ComponentKey == orderDto.ComponentKey);
                if (component != null)
                {
                    component.Order = orderDto.Order;
                    component.GridColumn = orderDto.GridColumn;
                    component.GridRow = orderDto.GridRow;
                    component.UpdatedAt = DateTime.UtcNow;
                    component.UpdatedByUserId = currentUserId;
                    _componentRepository.Update(component);
                }
            }

            await _componentRepository.SaveChangesAsync();
            await _cacheService.RemoveAsync($"designer:page:{pageId}");

            var updatedComponents = await _componentRepository.FindAsync(c => c.PageId == pageId);
            return await MapToDesignerComponentDtos(updatedComponents);
        }

        #endregion

        #region Component Library

        public async Task<ComponentLibraryDto> GetComponentLibraryAsync()
        {
            var cacheKey = "designer:component-library";

            return await _cacheService.GetAsync(cacheKey, async () =>
            {
                var library = new ComponentLibraryDto();

                // Define available component types by category
                var categories = new Dictionary<string, List<ComponentType>>
                {
                    ["Layout"] = new() { ComponentType.Container, ComponentType.Grid },
                    ["Content"] = new() { ComponentType.Text, ComponentType.Image },
                    ["Interactive"] = new() { ComponentType.Button, ComponentType.Form },
                    ["Media"] = new() { ComponentType.Video, ComponentType.Gallery },
                    ["Navigation"] = new() { ComponentType.Navigation, ComponentType.Header, ComponentType.Footer }
                };

                foreach (var category in categories)
                {
                    var componentCategory = new ComponentCategoryDto
                    {
                        Name = category.Key.ToLowerInvariant(),
                        DisplayName = category.Key,
                        Icon = GetCategoryIcon(category.Key),
                        Components = category.Value.Select(type => new AvailableComponentDto
                        {
                            Type = type,
                            Name = type.ToString().ToLowerInvariant(),
                            DisplayName = GetComponentDisplayName(type),
                            Description = GetComponentDescription(type),
                            Icon = GetComponentIcon(type),
                            Category = category.Key.ToLowerInvariant(),
                            DefaultProperties = GetDefaultComponentData(type).Properties,
                            DefaultStyles = GetDefaultComponentData(type).Styles,
                            DefaultSettings = GetDefaultComponentData(type).Settings,
                            AllowChildren = ComponentAllowsChildren(type),
                            DefaultColumnSpan = GetDefaultColumnSpan(type)
                        }).ToList()
                    };

                    library.Categories.Add(componentCategory);
                }

                return library;
            }) ?? new ComponentLibraryDto();
        }

        #endregion

        #region Preview and Publishing

        public async Task<DesignerPreviewDto> GeneratePreviewAsync(int pageId, Dictionary<string, object>? settings = null)
        {
            var page = await _pageRepository.GetWithComponentsAsync(pageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            var previewToken = Guid.NewGuid().ToString("N");
            var expiresAt = DateTime.UtcNow.AddHours(24);

            var previewData = new
            {
                PageId = pageId,
                Page = await MapToDesignerPageDto(page),
                Settings = settings ?? new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt
            };

            var cacheKey = $"preview:{previewToken}";
            await _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(previewData),
                new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt });

            return new DesignerPreviewDto
            {
                PageId = pageId,
                PreviewUrl = $"/preview/{previewToken}",
                PreviewToken = previewToken,
                ExpiresAt = expiresAt,
                Settings = settings ?? new Dictionary<string, object>()
            };
        }

        public async Task<string> GetPreviewContentAsync(string previewToken)
        {
            var cacheKey = $"preview:{previewToken}";
            var previewDataJson = await _distributedCache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(previewDataJson))
                throw new ArgumentException("Preview not found or expired");

            return previewDataJson;
        }

        public async Task<DesignerPageDto> PublishPageAsync(PublishPageDto publishDto)
        {
            var page = await _pageRepository.GetWithComponentsAsync(publishDto.PageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            if (publishDto.CreateVersion)
            {
                await CreateVersionInternalAsync(page, publishDto.PublishMessage ?? "Published");
            }

            page.Status = PageStatus.Published;
            page.PublishedOn = publishDto.ScheduledAt ?? DateTime.UtcNow;
            page.PublishedBy = _userSessionService.GetCurrentUserFullName() ?? "System";
            page.UpdatedAt = DateTime.UtcNow;
            page.UpdatedByUserId = currentUserId;

            _pageRepository.Update(page);
            await _pageRepository.SaveChangesAsync();

            await _cacheService.RemoveAsync($"designer:page:{publishDto.PageId}");
            await _cacheInvalidationService.InvalidatePageCacheAsync(publishDto.PageId); // Corrected service call

            _logger.LogInformation("Page published: PageId={PageId}", publishDto.PageId);

            return await GetDesignerPageAsync(publishDto.PageId);
        }

        public async Task<DesignerPageDto> UnpublishPageAsync(int pageId)
        {
            var page = await _pageRepository.GetByIdAsync(pageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            page.Status = PageStatus.Draft;
            page.PublishedOn = null;
            page.PublishedBy = null;
            page.UpdatedAt = DateTime.UtcNow;
            page.UpdatedByUserId = currentUserId;

            _pageRepository.Update(page);
            await _pageRepository.SaveChangesAsync();

            await _cacheService.RemoveAsync($"designer:page:{pageId}");
            await _cacheInvalidationService.InvalidatePageCacheAsync(pageId);

            return await GetDesignerPageAsync(pageId);
        }

        #endregion

        #region Versioning

        public async Task<DesignerPageDto> CreateVersionAsync(int pageId, string? changeNotes = null)
        {
            var page = await _pageRepository.GetWithComponentsAsync(pageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            await CreateVersionInternalAsync(page, changeNotes);
            return await GetDesignerPageAsync(pageId);
        }

        public async Task<List<Application.DTOs.Designer.PageVersionDto>> GetPageVersionsAsync(int pageId)
        {
            var versions = await _versionRepository.FindAsync(v => v.PageId == pageId);
            return versions.OrderByDescending(v => v.CreatedAt)
                          .Select(v => new Application.DTOs.Designer.PageVersionDto
                          {
                              Id = v.Id,
                              VersionNumber = v.VersionNumber,
                              ChangeNotes = v.ChangeNotes,
                              CreatedAt = v.CreatedAt
                          }).ToList();
        }

        public async Task<DesignerPageDto> RestoreVersionAsync(int pageId, int versionId)
        {
            var version = await _versionRepository.GetByIdAsync(versionId);
            if (version == null || version.PageId != pageId)
                throw new ArgumentException("Version not found");

            var page = await _pageRepository.GetWithComponentsAsync(pageId);
            if (page == null)
                throw new ArgumentException("Page not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            // Create backup before restore
            await CreateVersionInternalAsync(page, "Backup before restore");

            // TODO: Implement version restoration logic based on your version data structure

            page.UpdatedAt = DateTime.UtcNow;
            page.UpdatedByUserId = currentUserId;

            _pageRepository.Update(page);
            await _pageRepository.SaveChangesAsync();

            await _cacheService.RemoveAsync($"designer:page:{pageId}");

            return await GetDesignerPageAsync(pageId);
        }

        #endregion

        #region Designer State

        public async Task<DesignerStateDto> GetDesignerStateAsync(int pageId)
        {
            var userId = _userSessionService.GetCurrentUserId();
            var cacheKey = $"designer:state:{pageId}:{userId}";

            var state = await _cacheService.GetAsync<DesignerStateDto>(cacheKey);
            return state ?? new DesignerStateDto { PageId = pageId };
        }

        public async Task<DesignerStateDto> SaveDesignerStateAsync(DesignerStateDto stateDto)
        {
            var userId = _userSessionService.GetCurrentUserId();
            var cacheKey = $"designer:state:{stateDto.PageId}:{userId}";

            await _cacheService.SetAsync(cacheKey, stateDto, TimeSpan.FromHours(24));
            return stateDto;
        }

        public async Task ClearDesignerStateAsync(int pageId)
        {
            var userId = _userSessionService.GetCurrentUserId();
            var cacheKey = $"designer:state:{pageId}:{userId}";
            await _cacheService.RemoveAsync(cacheKey);
        }

        #endregion

        #region Private Helper Methods

        private async Task<DesignerPageDto> MapToDesignerPageDto(Page page)
        {
            var designerPage = new DesignerPageDto
            {
                Id = page.Id,
                Name = page.Name,
                Title = page.Title,
                Slug = page.Slug,
                Description = page.Description,
                Status = page.Status,
                CreatedAt = page.CreatedAt,
                UpdatedAt = page.UpdatedAt,
                PublishedAt = page.PublishedOn,
                Layout = new DesignerPageLayoutDto()
            };

            // Map components with hierarchy
            var allComponents = page.Components.Where(c => !c.IsDeleted).ToList();
            var rootComponents = allComponents.Where(c => c.ParentComponentId == null)
                                             .OrderBy(c => c.GridRow)
                                             .ThenBy(c => c.GridColumn)
                                             .ThenBy(c => c.Order);

            designerPage.Components = new List<DesignerComponentDto>();
            foreach (var component in rootComponents)
            {
                var designerComponent = await MapToDesignerComponentDto(component);
                await LoadChildComponents(designerComponent, allComponents);
                designerPage.Components.Add(designerComponent);
            }

            return designerPage;
        }

        private async Task<DesignerComponentDto> MapToDesignerComponentDto(PageComponent component)
        {
            return new DesignerComponentDto
            {
                Id = component.Id,
                ComponentKey = component.ComponentKey,
                Type = component.Type,
                Name = component.Name,
                GridColumn = component.GridColumn,
                GridColumnSpan = component.GridColumnSpan,
                GridRow = component.GridRow,
                GridRowSpan = component.GridRowSpan,
                Order = component.Order,
                ParentComponentId = component.ParentComponentId,
                ParentComponentKey = component.ParentComponent?.ComponentKey,
                Properties = component.Properties,
                Styles = component.Styles,
                Content = component.Content,
                Settings = component.Settings,
                IsVisible = component.IsVisible,
                IsLocked = component.IsLocked,
                CssClasses = component.CssClasses,
                CustomCss = component.CustomCss,
                ResponsiveSettings = component.ResponsiveSettings,
                AnimationSettings = component.AnimationSettings,
                InteractionSettings = component.InteractionSettings,
                Children = new List<DesignerComponentDto>()
            };
        }

        private async Task LoadChildComponents(DesignerComponentDto parent, List<PageComponent> allComponents)
        {
            var children = allComponents.Where(c => c.ParentComponentId == parent.Id)
                                      .OrderBy(c => c.GridRow)
                                      .ThenBy(c => c.GridColumn)
                                      .ThenBy(c => c.Order);

            foreach (var child in children)
            {
                var designerChild = await MapToDesignerComponentDto(child);
                await LoadChildComponents(designerChild, allComponents);
                parent.Children.Add(designerChild);
            }
        }

        private async Task<List<DesignerComponentDto>> MapToDesignerComponentDtos(IEnumerable<PageComponent> components)
        {
            var result = new List<DesignerComponentDto>();
            foreach (var component in components)
            {
                result.Add(await MapToDesignerComponentDto(component));
            }
            return result;
        }

        private async Task CreateComponentsFromDtos(List<DesignerComponentDto> components, int pageId, int? currentUserId)
        {
            var componentMap = new Dictionary<string, int>();

            // First pass: create all components
            foreach (var dto in components.OrderBy(c => c.GridRow).ThenBy(c => c.GridColumn).ThenBy(c => c.Order))
            {
                var component = new PageComponent
                {
                    PageId = pageId,
                    Type = dto.Type,
                    Name = dto.Name,
                    ComponentKey = dto.ComponentKey,
                    GridColumn = dto.GridColumn,
                    GridColumnSpan = dto.GridColumnSpan,
                    GridRow = dto.GridRow,
                    GridRowSpan = dto.GridRowSpan,
                    Order = dto.Order,
                    Properties = dto.Properties,
                    Styles = dto.Styles,
                    Content = dto.Content,
                    Settings = dto.Settings,
                    ResponsiveSettings = dto.ResponsiveSettings,
                    AnimationSettings = dto.AnimationSettings,
                    InteractionSettings = dto.InteractionSettings,
                    IsVisible = dto.IsVisible,
                    IsLocked = dto.IsLocked,
                    CssClasses = dto.CssClasses,
                    CustomCss = dto.CustomCss,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                await _componentRepository.AddAsync(component);
                await _componentRepository.SaveChangesAsync();

                componentMap[dto.ComponentKey] = component.Id;
            }

            // Second pass: set parent relationships
            foreach (var dto in components.Where(c => !string.IsNullOrEmpty(c.ParentComponentKey)))
            {
                if (componentMap.TryGetValue(dto.ComponentKey, out var componentId) &&
                    componentMap.TryGetValue(dto.ParentComponentKey!, out var parentId))
                {
                    var component = await _componentRepository.GetByIdAsync(componentId);
                    if (component != null)
                    {
                        component.ParentComponentId = parentId;
                        component.UpdatedAt = DateTime.UtcNow;
                        component.UpdatedByUserId = currentUserId;
                        _componentRepository.Update(component);
                    }
                }
            }

            await _componentRepository.SaveChangesAsync();
        }

        private async Task CreateVersionInternalAsync(Page page, string? changeNotes)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();
            var existingVersions = await _versionRepository.FindAsync(v => v.PageId == page.Id);
            var versionNumber = existingVersions.Any() ? existingVersions.Max(v => v.VersionNumber) + 1 : 1;

            var designerPage = await MapToDesignerPageDto(page);
            var versionData = JsonSerializer.Serialize(designerPage);

            var version = new PageVersion
            {
                PageId = page.Id,
                VersionNumber = versionNumber,
                Data = versionData,
                ChangeNotes = changeNotes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            await _versionRepository.AddAsync(version);
            await _versionRepository.SaveChangesAsync();
        }

        private string GenerateComponentKey()
        {
            return $"comp_{Guid.NewGuid():N}";
        }

        private (Dictionary<string, object> Properties, Dictionary<string, object> Styles, Dictionary<string, object> Content, Dictionary<string, object> Settings) GetDefaultComponentData(ComponentType type)
        {
            return type switch
            {
                ComponentType.Text => (
                    new Dictionary<string, object> { { "text", "Enter your text here..." } },
                    new Dictionary<string, object> { { "fontSize", "16px" }, { "color", "#000000" } },
                    new Dictionary<string, object>(),
                    new Dictionary<string, object> { { "allowFormatting", true } }
                ),
                ComponentType.Image => (
                    new Dictionary<string, object> { { "src", "" }, { "alt", "Image" } },
                    new Dictionary<string, object> { { "width", "100%" }, { "height", "auto" } },
                    new Dictionary<string, object>(),
                    new Dictionary<string, object> { { "allowResize", true } }
                ),
                ComponentType.Button => (
                    new Dictionary<string, object> { { "text", "Click Me" }, { "variant", "primary" } },
                    new Dictionary<string, object> { { "padding", "12px 24px" }, { "borderRadius", "4px" } },
                    new Dictionary<string, object>(),
                    new Dictionary<string, object> { { "type", "button" } }
                ),
                ComponentType.Container => (
                    new Dictionary<string, object> { { "maxWidth", "1200px" } },
                    new Dictionary<string, object> { { "padding", "20px" } },
                    new Dictionary<string, object>(),
                    new Dictionary<string, object> { { "allowChildren", true } }
                ),
                _ => (
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>()
                )
            };
        }

        private Dictionary<string, object> MergeWithDefaults(Dictionary<string, object> provided, Dictionary<string, object> defaults)
        {
            var result = new Dictionary<string, object>(defaults);
            foreach (var kvp in provided)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        private string GetComponentDisplayName(ComponentType type) => type switch
        {
            ComponentType.Text => "Text Block",
            ComponentType.Image => "Image",
            ComponentType.Button => "Button",
            ComponentType.Container => "Container",
            ComponentType.Grid => "Grid Layout",
            ComponentType.Card => "Card",
            ComponentType.List => "List",
            ComponentType.Form => "Form",
            ComponentType.Video => "Video",
            ComponentType.Map => "Map",
            ComponentType.Gallery => "Image Gallery",
            ComponentType.Slider => "Slider",
            ComponentType.Navigation => "Navigation Menu",
            ComponentType.Footer => "Footer",
            ComponentType.Header => "Header",
            ComponentType.Sidebar => "Sidebar",
            _ => type.ToString()
        };

        private string GetComponentDescription(ComponentType type) => type switch
        {
            ComponentType.Text => "Add text content with formatting options",
            ComponentType.Image => "Display images with customizable styling",
            ComponentType.Button => "Interactive button with click actions",
            ComponentType.Container => "Layout container for organizing content",
            ComponentType.Grid => "Grid layout for structured content arrangement",
            _ => $"A {type.ToString().ToLowerInvariant()} component"
        };

        private string GetComponentIcon(ComponentType type) => type switch
        {
            ComponentType.Text => "type",
            ComponentType.Image => "image",
            ComponentType.Button => "hand-pointer",
            ComponentType.Container => "square",
            ComponentType.Grid => "grid",
            ComponentType.Card => "credit-card",
            ComponentType.List => "list",
            ComponentType.Form => "file-text",
            ComponentType.Video => "video",
            ComponentType.Gallery => "images",
            ComponentType.Navigation => "menu",
            _ => "square"
        };

        private string GetCategoryIcon(string category) => category.ToLowerInvariant() switch
        {
            "layout" => "layout",
            "content" => "file-text",
            "interactive" => "mouse-pointer",
            "media" => "image",
            "navigation" => "navigation",
            _ => "folder"
        };

        private bool ComponentAllowsChildren(ComponentType type) => type switch
        {
            ComponentType.Container => true,
            ComponentType.Grid => true,
            ComponentType.Card => true,
            ComponentType.Form => true,
            ComponentType.Header => true,
            ComponentType.Footer => true,
            ComponentType.Sidebar => true,
            _ => false
        };

        private int GetDefaultColumnSpan(ComponentType type) => type switch
        {
            ComponentType.Button => 3,
            ComponentType.Image => 6,
            ComponentType.Text => 12,
            ComponentType.Container => 12,
            ComponentType.Grid => 12,
            _ => 12
        };

        #endregion
    }
}