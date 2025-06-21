using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.DTOs.Designer;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class DesignerService : IDesignerService
    {
        private readonly IPageRepository _pageRepository;
        private readonly IRepository<PageComponent> _componentRepository;
        private readonly IRepository<PageVersion> _versionRepository;
        private readonly IRepository<ComponentTemplate> _templateRepository;
        private readonly IComponentConfigValidator _configValidator;
        private readonly ICacheService _cacheService;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;
        private readonly ILogger<DesignerService> _logger;

        public DesignerService(
            IPageRepository pageRepository,
            IRepository<PageComponent> componentRepository,
            IRepository<PageVersion> versionRepository,
            IRepository<ComponentTemplate> templateRepository,
            IComponentConfigValidator configValidator,
            ICacheService cacheService,
            IMapper mapper,
            IUserSessionService userSessionService,
            ILogger<DesignerService> logger)
        {
            _pageRepository = pageRepository;
            _componentRepository = componentRepository;
            _versionRepository = versionRepository;
            _templateRepository = templateRepository;
            _configValidator = configValidator;
            _cacheService = cacheService;
            _mapper = mapper;
            _userSessionService = userSessionService;
            _logger = logger;
        }

        public async Task<DesignerPageDto> GetDesignerPageAsync(int pageId)
        {
            try
            {
                var cacheKey = $"designer:page:{pageId}";
                return await _cacheService.GetAsync(cacheKey, async () =>
                {
                    var page = await _pageRepository.GetWithComponentsAsync(pageId);
                    if (page == null)
                        throw new ArgumentException("Page not found");

                    var designerPage = _mapper.Map<DesignerPageDto>(page);
                    designerPage.Components = await BuildComponentHierarchyAsync(page.Components.Where(c => !c.IsDeleted).ToList());

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
                var page = await _pageRepository.GetWithComponentsAsync(saveDto.PageId);
                if (page == null)
                    throw new ArgumentException("Page not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Create version if requested
                if (saveDto.CreateVersion)
                {
                    await CreateVersionAsync(saveDto.PageId, saveDto.ChangeDescription);
                }

                // Delete existing components
                foreach (var component in page.Components)
                {
                    await _componentRepository.SoftDeleteAsync(component, currentUserId);
                }

                // Create new components
                var components = await CreateComponentsFromDesignerDtosAsync(saveDto.Components, saveDto.PageId, currentUserId);
                foreach (var component in components)
                {
                    await _componentRepository.AddAsync(component);
                }

                page.UpdatedAt = DateTime.UtcNow;
                page.UpdatedByUserId = currentUserId;
                _pageRepository.Update(page);
                await _pageRepository.SaveChangesAsync();

                // Clear cache
                await _cacheService.RemoveAsync($"designer:page:{saveDto.PageId}");

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
                    await _cacheService.RemoveAsync($"designer:page:{pageId}");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting designer page {PageId}", pageId);
                return false;
            }
        }

        public async Task<DesignerComponentDto> CreateComponentAsync(CreateComponentDto createDto)
        {
            try
            {
                // Validate config
                var validationResult = await _configValidator.ValidateAsync(createDto.Type, createDto.Config);
                if (!validationResult.IsValid)
                {
                    throw new ArgumentException($"Invalid configuration: {string.Join(", ", validationResult.Errors)}");
                }

                var currentUserId = _userSessionService.GetCurrentUserId();
                var component = new PageComponent
                {
                    PageId = createDto.PageId,
                    Type = createDto.Type,
                    Name = createDto.Name,
                    ComponentKey = createDto.ComponentKey,
                    Config = validationResult.SanitizedConfig,
                    GridColumn = createDto.GridColumn,
                    GridColumnSpan = createDto.GridColumnSpan,
                    GridRow = createDto.GridRow,
                    GridRowSpan = createDto.GridRowSpan,
                    Order = createDto.Order,
                    ParentComponentId = createDto.ParentComponentId,
                    IsVisible = true,
                    IsLocked = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                await _componentRepository.AddAsync(component);
                await _componentRepository.SaveChangesAsync();

                // Clear cache
                await _cacheService.RemoveAsync($"designer:page:{createDto.PageId}");

                return _mapper.Map<DesignerComponentDto>(component);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating component for page {PageId}", createDto.PageId);
                throw;
            }
        }

        public async Task<DesignerComponentDto> UpdateComponentAsync(UpdateComponentDto updateDto)
        {
            try
            {
                var component = await _componentRepository.GetByIdAsync(updateDto.ComponentId);
                if (component == null)
                    throw new ArgumentException("Component not found");

                // Validate config if provided
                if (updateDto.Config != null)
                {
                    var validationResult = await _configValidator.ValidateAsync(component.Type, updateDto.Config);
                    if (!validationResult.IsValid)
                    {
                        throw new ArgumentException($"Invalid configuration: {string.Join(", ", validationResult.Errors)}");
                    }
                    component.Config = validationResult.SanitizedConfig;
                }

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Update properties
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

                return _mapper.Map<DesignerComponentDto>(component);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating component {ComponentId}", updateDto.ComponentId);
                throw;
            }
        }

        public async Task<DesignerComponentDto> DuplicateComponentAsync(DuplicateComponentDto duplicateDto)
        {
            try
            {
                var originalComponent = await _componentRepository.GetByIdAsync(duplicateDto.ComponentId);
                if (originalComponent == null)
                    throw new ArgumentException("Component not found");

                var currentUserId = _userSessionService.GetCurrentUserId();
                var duplicatedComponent = new PageComponent
                {
                    PageId = originalComponent.PageId,
                    Type = originalComponent.Type,
                    Name = duplicateDto.NewName ?? $"{originalComponent.Name} (Copy)",
                    ComponentKey = Guid.NewGuid().ToString(),
                    Config = new Dictionary<string, object>(originalComponent.Config),
                    GridColumn = duplicateDto.GridColumn,
                    GridRow = duplicateDto.GridRow,
                    GridColumnSpan = originalComponent.GridColumnSpan,
                    GridRowSpan = originalComponent.GridRowSpan,
                    Order = duplicateDto.Order,
                    ParentComponentId = duplicateDto.ParentComponentId,
                    IsVisible = originalComponent.IsVisible,
                    IsLocked = false,
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

                return _mapper.Map<DesignerComponentDto>(duplicatedComponent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error duplicating component {ComponentId}", duplicateDto.ComponentId);
                throw;
            }
        }

        public async Task<bool> DeleteComponentAsync(int componentId, string componentKey)
        {
            try
            {
                var component = await _componentRepository.GetByIdAsync(componentId);
                if (component == null || component.ComponentKey != componentKey)
                    return false;

                var currentUserId = _userSessionService.GetCurrentUserId();
                var pageId = component.PageId;

                // Recursively delete child components
                await DeleteComponentRecursivelyAsync(component, currentUserId);

                // Clear cache
                await _cacheService.RemoveAsync($"designer:page:{pageId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting component {ComponentId}", componentId);
                return false;
            }
        }

        public async Task<DesignerComponentDto> MoveComponentAsync(MoveComponentDto moveDto)
        {
            try
            {
                var component = await _componentRepository.GetByIdAsync(moveDto.ComponentId);
                if (component == null || component.ComponentKey != moveDto.ComponentKey)
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

                return _mapper.Map<DesignerComponentDto>(component);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving component {ComponentId}", moveDto.ComponentId);
                throw;
            }
        }

        public async Task<List<DesignerComponentDto>> ReorderComponentsAsync(int pageId, List<ComponentOrderDto> componentOrders)
        {
            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var components = new List<DesignerComponentDto>();

                foreach (var order in componentOrders)
                {
                    var component = await _componentRepository.FirstOrDefaultAsync(c =>
                        c.ComponentKey == order.ComponentKey && c.PageId == pageId);

                    if (component != null)
                    {
                        component.Order = order.Order;
                        component.GridColumn = order.GridColumn;
                        component.GridRow = order.GridRow;
                        component.UpdatedAt = DateTime.UtcNow;
                        component.UpdatedByUserId = currentUserId;

                        _componentRepository.Update(component);
                        components.Add(_mapper.Map<DesignerComponentDto>(component));
                    }
                }

                await _componentRepository.SaveChangesAsync();

                // Clear cache
                await _cacheService.RemoveAsync($"designer:page:{pageId}");

                return components;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering components for page {PageId}", pageId);
                throw;
            }
        }

        public async Task<ComponentLibraryDto> GetComponentLibraryAsync()
        {
            try
            {
                return await _cacheService.GetAsync("designer:component-library", async () =>
                {
                    var templates = await _templateRepository.FindAsync(t => t.IsActive);
                    var groupedTemplates = templates.GroupBy(t => t.Category ?? "Other");

                    var library = new ComponentLibraryDto
                    {
                        Categories = groupedTemplates.Select(g => new ComponentCategoryDto
                        {
                            Name = g.Key,
                            DisplayName = g.Key,
                            Components = g.OrderBy(t => t.SortOrder).Select(t => new AvailableComponentDto
                            {
                                Type = t.Type,
                                Name = t.Name,
                                DisplayName = t.DisplayName,
                                Description = t.Description,
                                Icon = t.Icon,
                                Category = t.Category ?? "Other",
                                DefaultConfig = t.DefaultConfig,
                                AllowChildren = IsContainerType(t.Type),
                                DefaultColumnSpan = GetDefaultColumnSpan(t.Type)
                            }).ToList()
                        }).ToList()
                    };

                    return library;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting component library");
                throw;
            }
        }

        public async Task<DesignerPreviewDto> GeneratePreviewAsync(int pageId, Dictionary<string, object>? settings = null)
        {
            try
            {
                var previewToken = Guid.NewGuid().ToString();
                var expiresAt = DateTime.UtcNow.AddHours(1);

                // Store preview data in cache
                var previewData = new
                {
                    PageId = pageId,
                    Settings = settings ?? new Dictionary<string, object>(),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                };

                await _cacheService.SetAsync($"preview:{previewToken}", previewData, TimeSpan.FromHours(1));

                return new DesignerPreviewDto
                {
                    PageId = pageId,
                    PreviewUrl = $"/preview/{previewToken}",
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
                var previewData = await _cacheService.GetAsync<dynamic>($"preview:{previewToken}");
                if (previewData == null)
                    throw new ArgumentException("Preview not found or expired");

                // Get page data and return as JSON
                var page = await GetDesignerPageAsync((int)previewData.PageId);
                return JsonSerializer.Serialize(page, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });
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
                    await CreateVersionAsync(publishDto.PageId, publishDto.PublishMessage);
                }

                page.Status = PageStatus.Published;
                page.PublishedOn = publishDto.ScheduledAt ?? DateTime.UtcNow;
                page.PublishedBy = _userSessionService.GetCurrentUserFullName() ?? "Unknown";
                page.UpdatedAt = DateTime.UtcNow;
                page.UpdatedByUserId = currentUserId;

                _pageRepository.Update(page);
                await _pageRepository.SaveChangesAsync();

                // Clear cache
                await _cacheService.RemoveAsync($"designer:page:{publishDto.PageId}");

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

                // Clear cache
                await _cacheService.RemoveAsync($"designer:page:{pageId}");

                return await GetDesignerPageAsync(pageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpublishing page {PageId}", pageId);
                throw;
            }
        }

        public async Task<DesignerPageDto> CreateVersionAsync(int pageId, string? changeNotes = null)
        {
            try
            {
                var page = await _pageRepository.GetWithComponentsAsync(pageId);
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

                return await GetDesignerPageAsync(pageId);
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
                              .Select(v => new PageVersionDto
                              {
                                  Id = v.Id,
                                  VersionNumber = v.VersionNumber,
                                  ChangeNotes = v.ChangeNotes,
                                  CreatedAt = v.CreatedAt,
                                  CreatedByUserId = v.CreatedByUserId
                              }).ToList();
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
                var page = await _pageRepository.GetWithComponentsAsync(pageId);
                var version = await _versionRepository.GetByIdAsync(versionId);

                if (page == null || version == null || version.PageId != pageId)
                    throw new ArgumentException("Page or version not found");

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Create backup before restoration
                await CreateVersionAsync(pageId, $"Backup before restoring version {version.VersionNumber}");

                // Restore from version data
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

                // Clear cache
                await _cacheService.RemoveAsync($"designer:page:{pageId}");

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
                var cacheKey = $"designer:state:{pageId}:{userId}";

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
                var cacheKey = $"designer:state:{stateDto.PageId}:{userId}";

                await _cacheService.SetAsync(cacheKey, stateDto, TimeSpan.FromHours(24));
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
                var cacheKey = $"designer:state:{pageId}:{userId}";

                await _cacheService.RemoveAsync(cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing designer state for page {PageId}", pageId);
            }
        }

        // Private helper methods
        private async Task<List<DesignerComponentDto>> BuildComponentHierarchyAsync(List<PageComponent> components)
        {
            var componentDtos = components.Select(c => _mapper.Map<DesignerComponentDto>(c)).ToList();
            var rootComponents = new List<DesignerComponentDto>();

            // Build hierarchy
            foreach (var component in componentDtos.Where(c => c.ParentComponentId == null).OrderBy(c => c.Order))
            {
                component.Children = BuildChildComponents(component, componentDtos);
                rootComponents.Add(component);
            }

            return rootComponents;
        }

        private List<DesignerComponentDto> BuildChildComponents(DesignerComponentDto parent, List<DesignerComponentDto> allComponents)
        {
            var children = allComponents
                .Where(c => c.ParentComponentId == parent.Id)
                .OrderBy(c => c.Order)
                .ToList();

            foreach (var child in children)
            {
                child.Children = BuildChildComponents(child, allComponents);
            }

            return children;
        }

        private async Task<List<PageComponent>> CreateComponentsFromDesignerDtosAsync(List<DesignerComponentDto> componentDtos, int pageId, int? currentUserId)
        {
            var components = new List<PageComponent>();

            foreach (var dto in componentDtos)
            {
                var component = await CreateComponentFromDesignerDtoAsync(dto, pageId, currentUserId);
                components.Add(component);

                // Recursively create child components
                foreach (var childDto in dto.Children)
                {
                    var childComponent = await CreateComponentFromDesignerDtoAsync(childDto, pageId, currentUserId);
                    childComponent.ParentComponentId = component.Id;
                    components.Add(childComponent);
                }
            }

            return components;
        }

        private async Task<PageComponent> CreateComponentFromDesignerDtoAsync(DesignerComponentDto dto, int pageId, int? currentUserId)
        {
            var validationResult = await _configValidator.ValidateAsync(dto.Type, dto.Config);

            return new PageComponent
            {
                PageId = pageId,
                Type = dto.Type,
                Name = dto.Name,
                ComponentKey = dto.ComponentKey,
                Config = validationResult.IsValid ? validationResult.SanitizedConfig : dto.Config,
                GridColumn = dto.GridColumn,
                GridColumnSpan = dto.GridColumnSpan,
                GridRow = dto.GridRow,
                GridRowSpan = dto.GridRowSpan,
                Order = dto.Order,
                ParentComponentId = dto.ParentComponentId,
                IsVisible = dto.IsVisible,
                IsLocked = dto.IsLocked,
                CssClasses = dto.CssClasses,
                CustomCss = dto.CustomCss,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };
        }

        private async Task DeleteComponentRecursivelyAsync(PageComponent component, int? currentUserId)
        {
            // Delete child components first
            var childComponents = await _componentRepository.FindAsync(c => c.ParentComponentId == component.Id);
            foreach (var child in childComponents)
            {
                await DeleteComponentRecursivelyAsync(child, currentUserId);
            }

            // Delete the component itself
            await _componentRepository.SoftDeleteAsync(component, currentUserId);
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
                    ComponentKey = c.ComponentKey,
                    Config = c.Config,
                    GridColumn = c.GridColumn,
                    GridColumnSpan = c.GridColumnSpan,
                    GridRow = c.GridRow,
                    GridRowSpan = c.GridRowSpan,
                    Order = c.Order,
                    ParentComponentId = c.ParentComponentId,
                    IsVisible = c.IsVisible,
                    CssClasses = c.CssClasses,
                    CustomCss = c.CustomCss
                }).ToList()
            };
        }

        private async Task RestorePageFromSnapshotAsync(Page page, PageSnapshotDto snapshot, int? currentUserId)
        {
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
                var validationResult = await _configValidator.ValidateAsync(componentSnapshot.Type, componentSnapshot.Config);

                var component = new PageComponent
                {
                    PageId = page.Id,
                    Type = componentSnapshot.Type,
                    Name = componentSnapshot.Name,
                    ComponentKey = componentSnapshot.ComponentKey,
                    Config = validationResult.IsValid ? validationResult.SanitizedConfig : componentSnapshot.Config,
                    GridColumn = componentSnapshot.GridColumn,
                    GridColumnSpan = componentSnapshot.GridColumnSpan,
                    GridRow = componentSnapshot.GridRow,
                    GridRowSpan = componentSnapshot.GridRowSpan,
                    Order = componentSnapshot.Order,
                    ParentComponentId = componentSnapshot.ParentComponentId,
                    IsVisible = componentSnapshot.IsVisible,
                    CssClasses = componentSnapshot.CssClasses,
                    CustomCss = componentSnapshot.CustomCss,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                await _componentRepository.AddAsync(component);
            }
        }

        private static bool IsContainerType(ComponentType type)
        {
            return type == ComponentType.Container || type == ComponentType.Grid;
        }

        private static int GetDefaultColumnSpan(ComponentType type)
        {
            return type switch
            {
                ComponentType.Container => 12,
                ComponentType.Grid => 12,
                ComponentType.Header => 12,
                ComponentType.Footer => 12,
                ComponentType.Navigation => 12,
                _ => 6
            };
        }
    }
}