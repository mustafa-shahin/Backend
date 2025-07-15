using Backend.CMS.Application.DTOs;
using Microsoft.AspNetCore.Components;
using System.Text;

namespace Frontend.Forms.Categories
{
    public partial class CategoryForm : ComponentBase
    {

    [Parameter] public CreateCategoryDto Model { get; set; } = new();
        [Parameter] public Dictionary<string, string> ValidationErrors { get; set; } = new();
        [Parameter] public bool IsEditMode { get; set; }

        private List<CategoryTreeDto>? parentCategories;
        private bool isValidatingSlug = false;
        private bool? slugValidationResult = null;
        private Timer? slugValidationTimer;
        private string previousSlug = string.Empty;

        // Image picker support
        private List<object> categoryImageObjects = new();

        protected override void OnParametersSet()
        {
            // Convert CreateCategoryImageDto to objects for the generic ImagePicker
            categoryImageObjects = Model.Images.Cast<object>().ToList();
        }

        protected override async Task OnInitializedAsync()
        {
            await LoadParentCategories();
        }

        private async Task LoadParentCategories()
        {
            try
            {
                parentCategories = await CategoryService.GetCategoryTreeAsync();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load parent categories: {ex.Message}");
            }
        }

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

            // Reset validation state
            slugValidationResult = null;

            // Debounce slug validation
            slugValidationTimer?.Dispose();
            slugValidationTimer = new Timer(async _ => await ValidateSlugAsync(), null, 500, Timeout.Infinite);

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

                var isValid = await CategoryService.ValidateSlugAsync(Model.Slug, IsEditMode ? null : null);
                slugValidationResult = isValid;
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

        // ImagePicker helper methods
        private async Task OnCategoryImagesChanged(List<object> images)
        {
            categoryImageObjects = images;
            Model.Images = images.Cast<CreateCategoryImageDto>().ToList();

            // Update positions to ensure proper ordering
            for (int i = 0; i < Model.Images.Count; i++)
            {
                Model.Images[i].Position = i;
            }

            StateHasChanged();
            await Task.CompletedTask; // Ensure this is async-compatible
        }

        private string GetCategoryImageUrl(object image)
        {
            if (image is CreateCategoryImageDto categoryImage)
            {
                // Return a URL based on FileId - you might need to adjust this based on your file service
                return FileService.GetThumbnailUrl(categoryImage.FileId);
            }
            return string.Empty;
        }

        private string? GetCategoryImageAlt(object image)
        {
            if (image is CreateCategoryImageDto categoryImage)
            {
                return categoryImage.Alt;
            }
            return null;
        }

        private bool GetCategoryImageIsFeatured(object image)
        {
            if (image is CreateCategoryImageDto categoryImage)
            {
                return categoryImage.IsFeatured;
            }
            return false;
        }

        private object CreateCategoryImageFromFile(FileDto file)
        {
            return new CreateCategoryImageDto
            {
                FileId = file.Id,
                Position = Model.Images.Count,
                IsFeatured = !Model.Images.Any() // First image is featured by default
            };
        }

        private void UpdateCategoryImage(object image, string? alt, string? caption, bool isFeatured)
        {
            if (image is CreateCategoryImageDto categoryImage)
            {
                categoryImage.Alt = alt;
                categoryImage.Caption = caption;
                categoryImage.IsFeatured = isFeatured;
            }
        }

        public void Dispose()
        {
            slugValidationTimer?.Dispose();
        }
    }
}

