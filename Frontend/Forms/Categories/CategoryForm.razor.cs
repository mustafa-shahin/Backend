using Backend.CMS.Application.DTOs;
using Frontend.Components.Common;
using Microsoft.AspNetCore.Components;
using System.Text;

namespace Frontend.Forms.Categories
{
    public partial class CategoryForm : ComponentBase, IDisposable
    {
        [Parameter] public CreateCategoryDto Model { get; set; } = new();
        [Parameter] public Dictionary<string, string> ValidationErrors { get; set; } = new();
        [Parameter] public bool IsEditMode { get; set; }
        [Parameter] public int? EditingCategoryId { get; set; }
        [Parameter] public EventCallback<List<FileDto>> OnImagesChanged { get; set; }

        private List<CategoryTreeDto>? parentCategories;
        private bool isValidatingSlug = false;
        private bool? slugValidationResult = null;
        private Timer? slugValidationTimer;
        private string previousSlug = string.Empty;

        private List<FileDto> categoryFiles = new();
        private bool showFileBrowserDialog = false;
        private FormDialog? fileBrowserDialog;
        private List<FileDto> selectedBrowserFiles = new();
        protected override async Task OnInitializedAsync()
        {
            await LoadParentCategories();

            if (IsEditMode && EditingCategoryId.HasValue)
            {
                await LoadCategoryFiles();
            }
        }

        protected override async Task OnParametersSetAsync()
        {
            // Reload files when EditingCategoryId changes
            if (IsEditMode && EditingCategoryId.HasValue)
            {
                await LoadCategoryFiles();
            }
        }

        private async Task LoadCategoryFiles()
        {
            try
            {
                if (EditingCategoryId.HasValue && EditingCategoryId.Value > 0)
                {
                    categoryFiles = await FileService.GetFilesForEntityAsync("Category", EditingCategoryId.Value);
                    await OnImagesChanged.InvokeAsync(categoryFiles);
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load category files: {ex.Message}");
            }
        }
        private async Task LoadParentCategories()
        {
            try
            {
                parentCategories = await CategoryService.GetCategoryTreeAsync();

                // Filter out the current category and its descendants if editing
                if (IsEditMode && EditingCategoryId.HasValue)
                {
                    parentCategories = FilterParentCategories(parentCategories, EditingCategoryId.Value);
                }

                StateHasChanged();
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

        private void OnNameChanged(ChangeEventArgs e)
        {
            var name = e.Value?.ToString() ?? string.Empty;
            Model.Name = name;

            // Auto-generate slug if it's empty or matches the previous auto-generated slug
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

            // Reset validation state
            slugValidationResult = null;

            // Cancel previous validation timer
            slugValidationTimer?.Dispose();

            // Start new validation timer (debounce)
            if (!string.IsNullOrWhiteSpace(Model.Slug))
            {
                slugValidationTimer = new Timer(async _ => await ValidateSlugAsync(), null, 500, Timeout.Infinite);
            }

            StateHasChanged();
        }

        private async Task ValidateSlugAsync()
        {
            if (string.IsNullOrWhiteSpace(Model.Slug))
            {
                return;
            }

            try
            {
                isValidatingSlug = true;
                await InvokeAsync(StateHasChanged);

                var isValid = await CategoryService.ValidateSlugAsync(Model.Slug, EditingCategoryId);
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
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            return name.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("_", "-")
                .Where(c => char.IsLetterOrDigit(c) || c == '-')
                .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c))
                .ToString()
                .Trim('-');
        }

        private string GetValidationClass(string fieldName)
        {
            return ValidationErrors.ContainsKey(fieldName) ? "border-red-500 dark:border-red-400" : string.Empty;
        }

        private string GetCategoryDisplayName(CategoryTreeDto category)
        {
            var prefix = new string('—', category.Level * 2);
            return $"{prefix} {category.Name}";
        }
        private async Task RemoveImage(FileDto file)
        {
            categoryFiles.Remove(file);
            var image = Model.Images.FirstOrDefault(i => i.FileId == file.Id);
            if (image != null)
            {
                Model.Images.Remove(image);
            }
            await OnImagesChanged.InvokeAsync(categoryFiles);
            StateHasChanged();
        }

        private void OpenFileBrowser()
        {
            showFileBrowserDialog = true;
            selectedBrowserFiles.Clear();
        }

        private void CloseFileBrowser()
        {
            showFileBrowserDialog = false;
            selectedBrowserFiles.Clear();
        }

        private void OnFileBrowserSelectionChanged(List<FileDto> files)
        {
            selectedBrowserFiles = files;
        }

        private async Task AddSelectedFiles()
        {
            foreach (var file in selectedBrowserFiles)
            {
                if (categoryFiles.All(f => f.Id != file.Id))
                {
                    categoryFiles.Add(file);
                    Model.Images.Add(new CreateCategoryImageDto
                    {
                        FileId = file.Id,
                        Position = Model.Images.Count
                    });
                }
            }

            await OnImagesChanged.InvokeAsync(categoryFiles);
            CloseFileBrowser();
            StateHasChanged();
        }

        public void Dispose()
        {
            slugValidationTimer?.Dispose();
        }
    }
}