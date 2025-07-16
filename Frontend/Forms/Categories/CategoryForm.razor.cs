// Frontend/Forms/Categories/CategoryForm.razor.cs
using Backend.CMS.Application.DTOs;
using Frontend.Components.Common;
using Frontend.Components.Common.ConfirmationDialogComponent;
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
        [Parameter] public CategoryDto? ExistingCategory { get; set; }
        [Parameter] public EventCallback<List<CategoryImageDto>> OnImagesChanged { get; set; }

        // Parent categories for hierarchy
        private List<CategoryTreeDto>? parentCategories;

        // Slug validation
        private bool isValidatingSlug = false;
        private bool? slugValidationResult = null;
        private Timer? slugValidationTimer;
        private string previousSlug = string.Empty;

        // Image management
        private List<CategoryImageDto> categoryImages = new();
        private bool isLoadingImages = false;
        private bool isProcessingImages = false;

        // File browser
        private bool showFileBrowserDialog = false;
        private FormDialog? fileBrowserDialog;
        private List<FileDto> selectedBrowserFiles = new();

        // Image editing
        private bool showImageEditDialog = false;
        private bool isSavingImageEdit = false;
        private FormDialog? imageEditDialog;
        private CategoryImageDto? editingImage = null;
        private string editingImageAlt = string.Empty;
        private string editingImageCaption = string.Empty;
        private int editingImagePosition = 0;
        private bool editingImageIsFeatured = false;

        // Image deletion
        private ConfirmationDialog? deleteImageDialog;
        private CategoryImageDto? imageToDelete = null;

        protected override async Task OnInitializedAsync()
        {
            await LoadParentCategories();
            await LoadCategoryImages();
        }

        protected override async Task OnParametersSetAsync()
        {
            // Reload data when parameters change
            if (IsEditMode && EditingCategoryId.HasValue)
            {
                await LoadCategoryImages();
            }
        }

        #region Category Images Management

        private async Task LoadCategoryImages()
        {
            try
            {
                categoryImages.Clear();

                if (IsEditMode && EditingCategoryId.HasValue)
                {
                    isLoadingImages = true;
                    StateHasChanged();

                    // Load fresh category data from backend to ensure we have latest images
                    var freshCategory = await CategoryService.GetCategoryByIdAsync(EditingCategoryId.Value);
                    if (freshCategory != null)
                    {
                        ExistingCategory = freshCategory;
                        categoryImages = freshCategory.Images?.ToList() ?? new List<CategoryImageDto>();
                    }
                    else if (ExistingCategory != null)
                    {
                        // Fallback to existing category data
                        categoryImages = ExistingCategory.Images?.ToList() ?? new List<CategoryImageDto>();
                    }

                    // Update the model's images collection
                    UpdateModelImagesFromCategoryImages();

                    await OnImagesChanged.InvokeAsync(categoryImages);
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load category images: {ex.Message}");
            }
            finally
            {
                isLoadingImages = false;
                StateHasChanged();
            }
        }

        private void UpdateModelImagesFromCategoryImages()
        {
            Model.Images.Clear();
            foreach (var image in categoryImages)
            {
                Model.Images.Add(new CreateCategoryImageDto
                {
                    FileId = image.FileId,
                    Alt = image.Alt,
                    Caption = image.Caption,
                    Position = image.Position,
                    IsFeatured = image.IsFeatured
                });
            }
        }

        private string GetImageUrl(CategoryImageDto image)
        {
            if (!string.IsNullOrEmpty(image.ThumbnailUrl))
            {
                return image.ThumbnailUrl;
            }
            if (!string.IsNullOrEmpty(image.ImageUrl))
            {
                return image.ImageUrl;
            }
            return FileService.GetThumbnailUrl(image.FileId);
        }

        #endregion

        #region File Browser

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
            if (!selectedBrowserFiles.Any())
            {
                CloseFileBrowser();
                return;
            }

            try
            {
                isProcessingImages = true;
                StateHasChanged();

                var addedCount = 0;

                foreach (var file in selectedBrowserFiles)
                {
                    // Check if image already exists
                    if (categoryImages.Any(img => img.FileId == file.Id))
                        continue;

                    if (IsEditMode && EditingCategoryId.HasValue)
                    {
                        // Add image to existing category via API
                        var createImageDto = new CreateCategoryImageDto
                        {
                            FileId = file.Id,
                            Alt = file.Alt ?? file.OriginalFileName,
                            Position = categoryImages.Count,
                            IsFeatured = false
                        };

                        var newImage = await CategoryService.AddCategoryImageAsync(EditingCategoryId.Value, createImageDto);
                        if (newImage != null)
                        {
                            // Ensure proper URL population
                            if (string.IsNullOrEmpty(newImage.ImageUrl) && string.IsNullOrEmpty(newImage.ThumbnailUrl))
                            {
                                newImage.ThumbnailUrl = FileService.GetThumbnailUrl(newImage.FileId);
                                newImage.ImageUrl = FileService.GetFileUrl(newImage.FileId);
                            }

                            categoryImages.Add(newImage);
                            addedCount++;
                        }
                    }
                    else
                    {
                        // For new categories, add to local collection
                        var newImage = new CategoryImageDto
                        {
                            Id = 0, // Temporary ID for new categories
                            CategoryId = EditingCategoryId ?? 0,
                            FileId = file.Id,
                            Alt = file.Alt ?? file.OriginalFileName,
                            Position = categoryImages.Count,
                            IsFeatured = false,
                            ImageUrl = file.Urls?.Download ?? FileService.GetFileUrl(file.Id),
                            ThumbnailUrl = file.Urls?.Thumbnail ?? FileService.GetThumbnailUrl(file.Id),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        categoryImages.Add(newImage);
                        addedCount++;
                    }
                }

                // Update model and notify parent
                UpdateModelImagesFromCategoryImages();
                await OnImagesChanged.InvokeAsync(categoryImages);

                if (addedCount > 0)
                {
                    NotificationService.ShowSuccess($"Added {addedCount} image(s) to category");
                }

                CloseFileBrowser();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to add images: {ex.Message}");
            }
            finally
            {
                isProcessingImages = false;
                StateHasChanged();
            }
        }

        #endregion

        #region Image Editing

        private void EditImage(CategoryImageDto image)
        {
            editingImage = image;
            editingImageAlt = image.Alt ?? string.Empty;
            editingImageCaption = image.Caption ?? string.Empty;
            editingImagePosition = image.Position;
            editingImageIsFeatured = image.IsFeatured;
            showImageEditDialog = true;
        }

        private void CloseImageEditDialog()
        {
            showImageEditDialog = false;
            editingImage = null;
            editingImageAlt = string.Empty;
            editingImageCaption = string.Empty;
            editingImagePosition = 0;
            editingImageIsFeatured = false;
        }

        private async Task SaveImageChanges()
        {
            if (editingImage == null) return;

            try
            {
                isSavingImageEdit = true;
                StateHasChanged();

                // Validation
                if (string.IsNullOrWhiteSpace(editingImageAlt))
                {
                    NotificationService.ShowError("Alt text is required for accessibility");
                    return;
                }

                if (IsEditMode && EditingCategoryId.HasValue && editingImage.Id > 0)
                {
                    // Update existing image via API
                    var updateDto = new UpdateCategoryImageDto
                    {
                        Id = editingImage.Id,
                        FileId = editingImage.FileId,
                        Alt = editingImageAlt.Trim(),
                        Caption = string.IsNullOrWhiteSpace(editingImageCaption) ? null : editingImageCaption.Trim(),
                        Position = editingImagePosition,
                        IsFeatured = editingImageIsFeatured
                    };

                    var updatedImage = await CategoryService.UpdateCategoryImageAsync(editingImage.Id, updateDto);
                    if (updatedImage != null)
                    {
                        // Ensure proper URL population
                        if (string.IsNullOrEmpty(updatedImage.ImageUrl) && string.IsNullOrEmpty(updatedImage.ThumbnailUrl))
                        {
                            updatedImage.ThumbnailUrl = FileService.GetThumbnailUrl(updatedImage.FileId);
                            updatedImage.ImageUrl = FileService.GetFileUrl(updatedImage.FileId);
                        }

                        // Update local collection
                        var index = categoryImages.FindIndex(img => img.Id == editingImage.Id);
                        if (index >= 0)
                        {
                            categoryImages[index] = updatedImage;
                        }
                    }
                }
                else
                {
                    // Update local image for new categories
                    editingImage.Alt = editingImageAlt.Trim();
                    editingImage.Caption = string.IsNullOrWhiteSpace(editingImageCaption) ? null : editingImageCaption.Trim();
                    editingImage.Position = editingImagePosition;
                    editingImage.IsFeatured = editingImageIsFeatured;
                    editingImage.UpdatedAt = DateTime.UtcNow;
                }

                // Update model and notify parent
                UpdateModelImagesFromCategoryImages();
                await OnImagesChanged.InvokeAsync(categoryImages);

                NotificationService.ShowSuccess("Image updated successfully");
                CloseImageEditDialog();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to update image: {ex.Message}");
            }
            finally
            {
                isSavingImageEdit = false;
                StateHasChanged();
            }
        }

        private async Task ToggleFeatured(CategoryImageDto image)
        {
            try
            {
                // If setting as featured, remove featured status from other images
                if (!image.IsFeatured)
                {
                    foreach (var img in categoryImages.Where(i => i.IsFeatured && i.Id != image.Id))
                    {
                        img.IsFeatured = false;

                        // Update via API if editing existing category
                        if (IsEditMode && EditingCategoryId.HasValue && img.Id > 0)
                        {
                            var updateDto = new UpdateCategoryImageDto
                            {
                                Id = img.Id,
                                FileId = img.FileId,
                                Alt = img.Alt,
                                Caption = img.Caption,
                                Position = img.Position,
                                IsFeatured = false
                            };
                            await CategoryService.UpdateCategoryImageAsync(img.Id, updateDto);
                        }
                    }
                }

                // Toggle the current image's featured status
                image.IsFeatured = !image.IsFeatured;

                // Update via API if editing existing category
                if (IsEditMode && EditingCategoryId.HasValue && image.Id > 0)
                {
                    var updateDto = new UpdateCategoryImageDto
                    {
                        Id = image.Id,
                        FileId = image.FileId,
                        Alt = image.Alt,
                        Caption = image.Caption,
                        Position = image.Position,
                        IsFeatured = image.IsFeatured
                    };

                    var updatedImage = await CategoryService.UpdateCategoryImageAsync(image.Id, updateDto);
                    if (updatedImage != null)
                    {
                        // Ensure proper URL population
                        if (string.IsNullOrEmpty(updatedImage.ImageUrl) && string.IsNullOrEmpty(updatedImage.ThumbnailUrl))
                        {
                            updatedImage.ThumbnailUrl = FileService.GetThumbnailUrl(updatedImage.FileId);
                            updatedImage.ImageUrl = FileService.GetFileUrl(updatedImage.FileId);
                        }

                        // Update local collection
                        var index = categoryImages.FindIndex(img => img.Id == image.Id);
                        if (index >= 0)
                        {
                            categoryImages[index] = updatedImage;
                        }
                    }
                }

                // Update model and notify parent
                UpdateModelImagesFromCategoryImages();
                await OnImagesChanged.InvokeAsync(categoryImages);

                NotificationService.ShowSuccess(image.IsFeatured ? "Image set as featured" : "Featured status removed");
                StateHasChanged();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to update featured status: {ex.Message}");
            }
        }

        #endregion

        #region Image Deletion

        private void ShowDeleteImageConfirmation(CategoryImageDto image)
        {
            imageToDelete = image;
            deleteImageDialog?.Show();
        }

        private async Task DeleteSelectedImage()
        {
            if (imageToDelete == null) return;

            try
            {
                if (IsEditMode && EditingCategoryId.HasValue && imageToDelete.Id > 0)
                {
                    // Delete from backend
                    var success = await CategoryService.DeleteCategoryImageAsync(imageToDelete.Id);
                    if (!success)
                    {
                        NotificationService.ShowError("Failed to delete image");
                        return;
                    }
                }

                // Remove from local collection
                categoryImages.Remove(imageToDelete);

                // Update model and notify parent
                UpdateModelImagesFromCategoryImages();
                await OnImagesChanged.InvokeAsync(categoryImages);

                NotificationService.ShowSuccess("Image removed from category");

                // Reset positions
                await AutoArrangeImages();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to delete image: {ex.Message}");
            }
            finally
            {
                imageToDelete = null;
                StateHasChanged();
            }
        }

        #endregion

        #region Image Arrangement

        private async Task AutoArrangeImages()
        {
            try
            {
                // Reorder images by current position
                var orderedImages = categoryImages.OrderBy(img => img.Position).ToList();

                for (int i = 0; i < orderedImages.Count; i++)
                {
                    orderedImages[i].Position = i;
                }

                categoryImages = orderedImages;

                // Update backend if editing existing category
                if (IsEditMode && EditingCategoryId.HasValue)
                {
                    var imageOrders = categoryImages
                        .Where(img => img.Id > 0)
                        .Select(img => (img.Id, img.Position))
                        .ToList();

                    if (imageOrders.Any())
                    {
                        await CategoryService.ReorderCategoryImagesAsync(EditingCategoryId.Value, imageOrders);
                    }
                }

                // Update model and notify parent
                UpdateModelImagesFromCategoryImages();
                await OnImagesChanged.InvokeAsync(categoryImages);

                StateHasChanged();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to arrange images: {ex.Message}");
            }
        }

        #endregion

        #region Parent Categories and Validation

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

        #endregion

        #region Name and Slug Handling

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

        #region Public Methods for External Control

        /// <summary>
        /// Initialize the form with an existing category for editing
        /// </summary>
        public async Task InitializeForEdit(CategoryDto category)
        {
            ExistingCategory = category;
            IsEditMode = true;
            EditingCategoryId = category.Id;

            // Map category to create model
            Model = new CreateCategoryDto
            {
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                ShortDescription = category.ShortDescription,
                ParentCategoryId = category.ParentCategoryId,
                IsActive = category.IsActive,
                IsVisible = category.IsVisible,
                SortOrder = category.SortOrder,
                MetaTitle = category.MetaTitle,
                MetaDescription = category.MetaDescription,
                MetaKeywords = category.MetaKeywords,
                CustomFields = category.CustomFields ?? new Dictionary<string, object>(),
                Images = new List<CreateCategoryImageDto>()
            };

            await LoadCategoryImages();
            StateHasChanged();
        }

        /// <summary>
        /// Reset the form for creating a new category
        /// </summary>
        public void InitializeForCreate()
        {
            ExistingCategory = null;
            IsEditMode = false;
            EditingCategoryId = null;
            Model = new CreateCategoryDto();
            categoryImages.Clear();
            ValidationErrors.Clear();
            StateHasChanged();
        }

        /// <summary>
        /// Get current images for external access
        /// </summary>
        public List<CategoryImageDto> GetCurrentImages()
        {
            return categoryImages.ToList();
        }

        /// <summary>
        /// Refresh images from backend
        /// </summary>
        public async Task RefreshImages()
        {
            await LoadCategoryImages();
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