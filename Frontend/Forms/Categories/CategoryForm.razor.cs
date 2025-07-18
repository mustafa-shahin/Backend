using Backend.CMS.Application.DTOs;
using Frontend.Components.Common.GenericCrudPage;
using Microsoft.AspNetCore.Components;
using System.Text;

namespace Frontend.Forms.Categories
{
    public partial class CategoryForm : ComponentBase, IDisposable
    {
        // Form context integration
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

        // Temporary file storage - reactive state
        private List<FileDto> temporaryFiles = new();
        private int? featuredImageId = null;

        // Context extraction
        private int? contextEditingId;
        private CategoryDto? contextOriginalCategory;

        // State tracking
        private int stableEntityId = 0;
        private bool hasInitialized = false;
        private bool isUpdatingImages = false;

        protected override async Task OnInitializedAsync()
        {
            ExtractContextInformation();
            await LoadParentCategories();
            await InitializeImages();
            hasInitialized = true;
        }

        protected override async Task OnParametersSetAsync()
        {
            if (!hasInitialized) return;

            var oldEntityId = stableEntityId;
            ExtractContextInformation();

            if (oldEntityId != stableEntityId)
            {
                await HandleParameterChanges();
            }
        }

        private void ExtractContextInformation()
        {
            if (FormContext is GenericCrudPage<CategoryDto, CategoryDto, CreateCategoryDto, UpdateCategoryDto>.FormContext context)
            {
                contextEditingId = context.EditingEntityId;
                contextOriginalCategory = context.OriginalEntity as CategoryDto;

                if (contextEditingId.HasValue && !EditingCategoryId.HasValue)
                {
                    EditingCategoryId = contextEditingId;
                }

                if (contextOriginalCategory != null && ExistingCategory == null)
                {
                    ExistingCategory = contextOriginalCategory;
                }
            }

            var newEntityId = EditingCategoryId ?? contextEditingId ?? 0;
            stableEntityId = newEntityId;
        }

        private int GetEffectiveEntityId()
        {
            return stableEntityId;
        }

        private async Task HandleParameterChanges()
        {
            try
            {
                await InitializeImages();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error handling parameter changes: {ex.Message}");
            }
        }

        #region Image Management

        private async Task InitializeImages()
        {
            try
            {
                isUpdatingImages = true;

                // Reset state
                temporaryFiles.Clear();
                featuredImageId = null;

                if (IsEditMode && stableEntityId > 0)
                {
                    // Load existing category images
                    var existingImages = await FileService.GetFilesForEntityAsync("Category", stableEntityId, Backend.CMS.Domain.Enums.FileType.Image);
                    temporaryFiles = existingImages.ToList();

                    // Find featured image from existing category data
                    var existingCategory = ExistingCategory ?? contextOriginalCategory;
                    if (existingCategory?.Images?.Any() == true)
                    {
                        var featuredImage = existingCategory.Images.FirstOrDefault(i => i.IsFeatured);
                        if (featuredImage != null)
                        {
                            var matchingFile = temporaryFiles.FirstOrDefault(f => f.Id == featuredImage.FileId);
                            if (matchingFile != null)
                            {
                                featuredImageId = matchingFile.Id;
                            }
                        }
                    }
                }

                // Update model immediately
                await UpdateModelFromCurrentImages();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to initialize images: {ex.Message}");
            }
            finally
            {
                isUpdatingImages = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task OnTemporaryFilesChanged(List<FileDto> files)
        {
            if (isUpdatingImages) return;

            try
            {
                temporaryFiles = files?.ToList() ?? new List<FileDto>();
                await UpdateModelFromCurrentImages();
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to update temporary files: {ex.Message}");
            }
        }

        private async Task OnFeaturedImageChanged(int? fileId)
        {
            if (isUpdatingImages) return;

            try
            {
                featuredImageId = fileId;
                await UpdateModelFromCurrentImages();
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to update featured image: {ex.Message}");
            }
        }

        private async Task UpdateModelFromCurrentImages()
        {
            try
            {
                // Update the model's images collection
                Model.Images = temporaryFiles.Select((file, index) => new CreateCategoryImageDto
                {
                    FileId = file.Id,
                    Alt = file.Alt ?? file.OriginalFileName,
                    Caption = file.Description,
                    Position = index,
                    IsFeatured = featuredImageId == file.Id
                }).ToList();

                // Notify parent components
                if (OnImagesChanged.HasDelegate)
                {
                    var categoryImages = temporaryFiles.Select((file, index) => new CategoryImageDto
                    {
                        Id = 0,
                        CategoryId = GetEffectiveEntityId(),
                        FileId = file.Id,
                        Alt = file.Alt ?? file.OriginalFileName,
                        Caption = file.Description,
                        Position = index,
                        IsFeatured = featuredImageId == file.Id,
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
                if (file.Id > 0)
                {
                    await FileService.DownloadFileAsync(file.Id);
                }
                else
                {
                    NotificationService.ShowInfo($"File {file.OriginalFileName} is in temporary storage. Save the category first to download.");
                }
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

        private string GetImagePreviewUrl(FileDto file)
        {
            if (file.Id > 0)
            {
                return file.Urls?.Download ?? FileService.GetFileUrl(file.Id);
            }
            else
            {
                // For temporary files, use the thumbnail data if available
                return file.Urls?.Thumbnail ?? file.Urls?.Download ?? "#";
            }
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

        #region Public API

        public List<FileDto> GetTemporaryFiles()
        {
            return temporaryFiles.ToList();
        }

        public int? GetFeaturedImageId()
        {
            return featuredImageId;
        }

        public bool HasTemporaryFiles()
        {
            return temporaryFiles.Any(f => f.Id < 0);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            slugValidationTimer?.Dispose();
        }

        #endregion
    }
}