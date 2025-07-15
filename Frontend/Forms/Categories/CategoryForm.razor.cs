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
        [Parameter] public int? EditingCategoryId { get; set; }

        private List<CategoryTreeDto>? parentCategories;
        private bool isValidatingSlug = false;
        private bool? slugValidationResult = null;
        private Timer? slugValidationTimer;
        private string previousSlug = string.Empty;

        private List<object> categoryImageObjects = new();

        protected override void OnParametersSet()
        {
            categoryImageObjects = Model.Images.Cast<object>().ToList();
        }

        protected override async Task OnInitializedAsync()
        {
            await LoadParentCategories();
        }

        public void LoadExistingImages(List<CategoryImageDto> existingImages)
        {
            Model.Images = existingImages.Select(img => new CreateCategoryImageDto
            {
                FileId = img.FileId,
                Alt = img.Alt,
                Caption = img.Caption,
                Position = img.Position,
                IsFeatured = img.IsFeatured
            }).ToList();

            categoryImageObjects = Model.Images.Cast<object>().ToList();
            StateHasChanged();
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

            slugValidationResult = null;
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

                var isValid = await CategoryService.ValidateSlugAsync(Model.Slug, EditingCategoryId);
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

        private async Task OnCategoryImagesChanged(List<object> images)
        {
            categoryImageObjects = images;
            Model.Images = images.Cast<CreateCategoryImageDto>().ToList();

            for (int i = 0; i < Model.Images.Count; i++)
            {
                Model.Images[i].Position = i;
            }

            StateHasChanged();
            await Task.CompletedTask;
        }

        private string GetCategoryImageUrl(object image)
        {
            if (image is CreateCategoryImageDto categoryImage)
            {
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

        private int? GetCategoryImageId(object image)
        {
            if (image is CategoryImageDto categoryImage)
            {
                return categoryImage.Id;
            }
            return null;
        }

        private object CreateCategoryImageFromFile(FileDto file)
        {
            return new CreateCategoryImageDto
            {
                FileId = file.Id,
                Position = Model.Images.Count,
                IsFeatured = !Model.Images.Any()
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

        private async Task<object?> AddCategoryImageApi(int categoryId, CreateCategoryImageDto createImageDto)
        {
            try
            {
                var addedImage = await CategoryService.AddCategoryImageAsync(categoryId, createImageDto);
                if (addedImage != null)
                {
                    return new CreateCategoryImageDto
                    {
                        FileId = addedImage.FileId,
                        Alt = addedImage.Alt,
                        Caption = addedImage.Caption,
                        Position = addedImage.Position,
                        IsFeatured = addedImage.IsFeatured
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to add image: {ex.Message}");
                return null;
            }
        }

        private async Task<object?> UpdateCategoryImageApi(int imageId, UpdateCategoryImageDto updateImageDto)
        {
            try
            {
                var updatedImage = await CategoryService.UpdateCategoryImageAsync(imageId, updateImageDto);
                if (updatedImage != null)
                {
                    return new CreateCategoryImageDto
                    {
                        FileId = updatedImage.FileId,
                        Alt = updatedImage.Alt,
                        Caption = updatedImage.Caption,
                        Position = updatedImage.Position,
                        IsFeatured = updatedImage.IsFeatured
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to update image: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> DeleteCategoryImageApi(int imageId)
        {
            try
            {
                return await CategoryService.DeleteCategoryImageAsync(imageId);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to delete image: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            slugValidationTimer?.Dispose();
        }
    }
}