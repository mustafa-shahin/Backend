
using Backend.CMS.Application.DTOs;
using Frontend.Components.Common.GenericCrudPage;
using Microsoft.AspNetCore.Components;
using System.Text;

namespace Frontend.Forms.Categories
{
    public partial class CategoryForm : ComponentBase, IDisposable
    {
        // Form context integration - these will be set by the parent component
        [Parameter] public CreateCategoryDto Model { get; set; } = new();
        [Parameter] public Dictionary<string, string> ValidationErrors { get; set; } = new();
        [Parameter] public bool IsEditMode { get; set; }
        [Parameter] public int? EditingCategoryId { get; set; }
        [Parameter] public CategoryDto? ExistingCategory { get; set; }
        [Parameter] public EventCallback<List<CategoryImageDto>> OnImagesChanged { get; set; }

        // Enhanced context support
        [Parameter] public object? FormContext { get; set; }

        // Component state
        private List<CategoryTreeDto>? parentCategories;
        private bool isValidatingSlug = false;
        private bool? slugValidationResult = null;
        private Timer? slugValidationTimer;
        private string previousSlug = string.Empty;
        private bool showImagePreviewDialog = false;
        private FileDto? previewingFile = null;
        private List<FileDto> currentCategoryImages = new();
        private bool imagesLoaded = false;
        private bool isLoadingImages = false;
        private readonly SemaphoreSlim loadingSemaphore = new(1, 1);

        // Context extraction
        private int? contextEditingId;
        private CategoryDto? contextOriginalCategory;

        // Stable entity ID to prevent FilePicker reloads
        private int stableEntityId = 0;
        private bool hasInitialized = false;

        protected override async Task OnInitializedAsync()
        {
            ExtractContextInformation();
            await LoadParentCategories();
            hasInitialized = true;
        }

        protected override async Task OnParametersSetAsync()
        {
            if (!hasInitialized) return;

            var oldEntityId = stableEntityId;
            ExtractContextInformation();

            // Only handle parameter changes if the entity ID actually changed
            if (oldEntityId != stableEntityId)
            {
                await HandleParameterChanges();
            }
        }

        private void ExtractContextInformation()
        {
            // Extract information from FormContext if available (for GenericCrudPage integration)
            if (FormContext is GenericCrudPage<CategoryDto, CategoryDto, CreateCategoryDto, UpdateCategoryDto>.FormContext context)
            {
                contextEditingId = context.EditingEntityId;
                contextOriginalCategory = context.OriginalEntity as CategoryDto;

                // Override parameters with context information if available
                if (contextEditingId.HasValue && !EditingCategoryId.HasValue)
                {
                    EditingCategoryId = contextEditingId;
                }

                if (contextOriginalCategory != null && ExistingCategory == null)
                {
                    ExistingCategory = contextOriginalCategory;
                }
            }

            // Set stable entity ID
            var newEntityId = EditingCategoryId ?? contextEditingId ?? 0;
            if (stableEntityId != newEntityId)
            {
                stableEntityId = newEntityId;
                // Reset loading state when entity changes
                imagesLoaded = false;
                isLoadingImages = false;
            }
        }

        private int GetEffectiveEntityId()
        {
            return stableEntityId;
        }

        private async Task HandleParameterChanges()
        {
            if (!await loadingSemaphore.WaitAsync(100))
            {
                return; // Skip if already loading
            }

            try
            {
                if (IsEditMode && stableEntityId > 0 && !imagesLoaded && !isLoadingImages)
                {
                    await LoadCategoryImagesFromFiles(stableEntityId);
                }
                else if (!IsEditMode && imagesLoaded)
                {
                    // Clear images when switching from edit to create mode
                    currentCategoryImages.Clear();
                    imagesLoaded = false;
                    isLoadingImages = false;
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error handling parameter changes: {ex.Message}");
            }
            finally
            {
                loadingSemaphore.Release();
            }
        }

        #region Image Management

        private async Task LoadCategoryImagesFromFiles(int categoryId)
        {
            if (isLoadingImages || imagesLoaded) return;

            try
            {
                isLoadingImages = true;
                await InvokeAsync(StateHasChanged);

                var categoryFiles = await FileService.GetFilesForEntityAsync("Category", categoryId, Backend.CMS.Domain.Enums.FileType.Image);
                currentCategoryImages = categoryFiles?.ToList() ?? new List<FileDto>();

                // Update model without triggering external events during initial load
                await UpdateModelFromCurrentImages(suppressEvents: true);

                imagesLoaded = true;
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load category images: {ex.Message}");
                currentCategoryImages.Clear();
                imagesLoaded = false;
            }
            finally
            {
                isLoadingImages = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task OnCategoryImagesChanged(List<FileDto> files)
        {
            if (isLoadingImages) return; // Ignore events during initial loading

            try
            {
                currentCategoryImages = files?.ToList() ?? new List<FileDto>();
                await UpdateModelFromCurrentImages(suppressEvents: false);
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to update category images: {ex.Message}");
            }
        }

        private async Task UpdateModelFromCurrentImages(bool suppressEvents = false)
        {
            try
            {
                Model.Images = currentCategoryImages.Select((file, index) => new CreateCategoryImageDto
                {
                    FileId = file.Id,
                    Alt = file.Alt ?? file.OriginalFileName,
                    Caption = file.Description,
                    Position = index,
                    IsFeatured = index == 0
                }).ToList();

                // Only invoke external events if not suppressing them (i.e., not during initial load)
                if (!suppressEvents && OnImagesChanged.HasDelegate)
                {
                    var categoryImages = currentCategoryImages.Select((file, index) => new CategoryImageDto
                    {
                        Id = 0,
                        CategoryId = GetEffectiveEntityId(),
                        FileId = file.Id,
                        Alt = file.Alt ?? file.OriginalFileName,
                        Caption = file.Description,
                        Position = index,
                        IsFeatured = index == 0,
                        ImageUrl = file.Urls?.Download ?? FileService.GetFileUrl(file.Id),
                        ThumbnailUrl = file.Urls?.Thumbnail ?? FileService.GetThumbnailUrl(file.Id),
                        CreatedAt = file.CreatedAt,
                        UpdatedAt = file.UpdatedAt
                    }).ToList();

                    await OnImagesChanged.InvokeAsync(categoryImages);
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to update model from images: {ex.Message}");
            }
        }

        private async Task OnImagePreview(FileDto file)
        {
            previewingFile = file;
            showImagePreviewDialog = true;
            await InvokeAsync(StateHasChanged);
        }

        private async Task OnImageDownload(FileDto file)
        {
            try
            {
                await FileService.DownloadFileAsync(file.Id);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to download image: {ex.Message}");
            }
        }

        private void CloseImagePreview()
        {
            showImagePreviewDialog = false;
            previewingFile = null;
            StateHasChanged();
        }

        #endregion

        #region Parent Categories and Validation

        private async Task LoadParentCategories()
        {
            try
            {
                parentCategories = await CategoryService.GetCategoryTreeAsync();

                if (IsEditMode && stableEntityId > 0)
                {
                    parentCategories = FilterParentCategories(parentCategories, stableEntityId);
                }

                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load parent categories: {ex.Message}");
            }
        }

        private List<CategoryTreeDto> FilterParentCategories(List<CategoryTreeDto> categories, int categoryToExclude)
        {
            var filtered = new List<CategoryTreeDto>();

            foreach (var category in categories)
            {
                if (category.Id != categoryToExclude && !IsDescendantOf(category, categoryToExclude))
                {
                    var filteredCategory = new CategoryTreeDto
                    {
                        Id = category.Id,
                        Name = category.Name,
                        Slug = category.Slug,
                        ParentCategoryId = category.ParentCategoryId,
                        IsActive = category.IsActive,
                        IsVisible = category.IsVisible,
                        SortOrder = category.SortOrder,
                        Level = category.Level,
                        Path = category.Path,
                        ProductCount = category.ProductCount,
                        TotalDescendants = category.TotalDescendants,
                        CreatedAt = category.CreatedAt,
                        UpdatedAt = category.UpdatedAt,
                        Children = FilterParentCategories(category.Children, categoryToExclude)
                    };
                    filtered.Add(filteredCategory);
                }
            }

            return filtered;
        }

        private bool IsDescendantOf(CategoryTreeDto category, int ancestorId)
        {
            foreach (var child in category.Children)
            {
                if (child.Id == ancestorId || IsDescendantOf(child, ancestorId))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Name and Slug Handling

        private void OnNameChanged(ChangeEventArgs e)
        {
            var name = e.Value?.ToString() ?? string.Empty;
            Model.Name = name;

            var generatedSlug = GenerateSlugFromName(name);

            if (string.IsNullOrEmpty(Model.Slug) || Model.Slug == previousSlug)
            {
                Model.Slug = generatedSlug;
                OnSlugChanged();
            }

            previousSlug = generatedSlug;
            StateHasChanged();
        }

        private void OnSlugChanged(ChangeEventArgs? e = null)
        {
            if (e != null)
            {
                Model.Slug = e.Value?.ToString() ?? string.Empty;
            }

            slugValidationResult = null;
            slugValidationTimer?.Dispose();

            if (!string.IsNullOrWhiteSpace(Model.Slug))
            {
                slugValidationTimer = new Timer(async _ => await ValidateSlugAsync(), null, 500, Timeout.Infinite);
            }

            StateHasChanged();
        }

        private async Task ValidateSlugAsync()
        {
            if (string.IsNullOrWhiteSpace(Model.Slug)) return;

            try
            {
                isValidatingSlug = true;
                await InvokeAsync(StateHasChanged);

                var excludeId = stableEntityId > 0 ? stableEntityId : (int?)null;
                var isValid = await CategoryService.ValidateSlugAsync(Model.Slug, excludeId);
                slugValidationResult = isValid;

                if (!isValid)
                {
                    ValidationErrors["Slug"] = "This slug is already in use. Please choose a different one.";
                }
                else
                {
                    ValidationErrors.Remove("Slug");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to validate slug: {ex.Message}");
                slugValidationResult = false;
            }
            finally
            {
                isValidatingSlug = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private string GenerateSlugFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            return name.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("_", "-")
                .Where(c => char.IsLetterOrDigit(c) || c == '-')
                .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c))
                .ToString()
                .Trim('-');
        }

        #endregion

        #region Helper Methods

        private string GetValidationClass(string fieldName)
        {
            return ValidationErrors.ContainsKey(fieldName) ? "border-red-500 dark:border-red-400" : string.Empty;
        }

        private string GetCategoryDisplayName(CategoryTreeDto category)
        {
            var prefix = new string('—', category.Level * 2);
            return $"{prefix} {category.Name}";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            slugValidationTimer?.Dispose();
            loadingSemaphore?.Dispose();
        }

        #endregion
    }
}