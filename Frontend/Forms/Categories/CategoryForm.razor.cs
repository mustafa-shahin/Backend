// Frontend/Forms/Categories/CategoryForm.razor.cs
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Frontend.Components.Common;
using Frontend.Components.Common.GenericCrudPage;
using Frontend.Components.Common.ObjectSelector;
using Frontend.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
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

        // Image management with ObjectSelector
        private ObjectSelector<FileDto>? imageSelector;
        private List<FileDto> selectedImages = new();
        private int? featuredImageId = null;
        private bool isProcessingImages = false;

        // Upload functionality
        private GenericDialog? uploadDialog;
        private bool showUploadDialog = false;
        private bool isUploadingImages = false;
        private List<IBrowserFile> pendingUploadFiles = new();

        // Image preview
        private bool showImagePreviewDialog = false;
        private FileDto? previewingFile = null;

        // Context extraction
        private int? contextEditingId;
        private CategoryDto? contextOriginalCategory;

        // State tracking
        private int stableEntityId = 0;
        private bool hasInitialized = false;
        private readonly SemaphoreSlim initializationSemaphore = new(1, 1);

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
            if (!await initializationSemaphore.WaitAsync(100))
            {
                return;
            }

            try
            {
                await InitializeImages();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error handling parameter changes: {ex.Message}");
            }
            finally
            {
                initializationSemaphore.Release();
            }
        }

        #region Image Management with ObjectSelector

        private async Task InitializeImages()
        {
            try
            {
                isProcessingImages = true;
                await InvokeAsync(StateHasChanged);

                // Reset state
                selectedImages.Clear();
                featuredImageId = null;

                if (IsEditMode && stableEntityId > 0)
                {
                    // Load existing category images from the backend
                    var existingImages = await FileService.GetFilesForEntityAsync("Category", stableEntityId, FileType.Image);
                    selectedImages = existingImages.ToList();

                    // Find featured image from existing category data
                    var existingCategory = ExistingCategory ?? contextOriginalCategory;
                    if (existingCategory?.Images?.Any() == true)
                    {
                        var featuredImage = existingCategory.Images.FirstOrDefault(i => i.IsFeatured);
                        if (featuredImage != null)
                        {
                            var matchingFile = selectedImages.FirstOrDefault(f => f.Id == featuredImage.FileId);
                            if (matchingFile != null)
                            {
                                featuredImageId = matchingFile.Id;
                            }
                        }
                    }

                    // If no featured image set but images exist, set first as featured
                    if (!featuredImageId.HasValue && selectedImages.Any())
                    {
                        featuredImageId = selectedImages.First().Id;
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
                isProcessingImages = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task<FileDto?> GetImageByIdAsync(int imageId)
        {
            try
            {
                var file = await FileService.GetFileByIdAsync(imageId);

                // Ensure it's an image file
                if (file?.FileType == FileType.Image)
                {
                    return file;
                }

                return null;
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load image: {ex.Message}");
                return null;
            }
        }

        private async Task<PaginatedResult<FileDto>> LoadImageFiles(int page, int pageSize, string? searchTerm)
        {
            try
            {
                var searchDto = new FileSearchDto
                {
                    PageNumber = page,
                    PageSize = pageSize,
                    SearchTerm = searchTerm,
                    FileType = FileType.Image,
                    SortBy = "CreatedAt",
                    SortDirection = "Desc"
                };

                return await FileService.SearchFilesAsync(searchDto);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load images: {ex.Message}");
                return PaginatedResult<FileDto>.Empty(page, pageSize);
            }
        }

        private async Task<List<FileDto>> SearchImageFiles(string searchTerm)
        {
            try
            {
                var searchDto = new FileSearchDto
                {
                    PageNumber = 1,
                    PageSize = 50,
                    SearchTerm = searchTerm,
                    FileType = FileType.Image,
                    SortBy = "CreatedAt",
                    SortDirection = "Desc"
                };

                var result = await FileService.SearchFilesAsync(searchDto);
                return result.Data?.ToList() ?? new List<FileDto>();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to search images: {ex.Message}");
                return new List<FileDto>();
            }
        }

        private async Task OnSelectedImagesChanged(List<FileDto> images)
        {
            if (isProcessingImages) return;

            try
            {
                selectedImages = images?.ToList() ?? new List<FileDto>();

                // If featured image is no longer in selection, clear it
                if (featuredImageId.HasValue && !selectedImages.Any(img => img.Id == featuredImageId.Value))
                {
                    featuredImageId = null;
                }

                // If no featured image but images exist, set first as featured
                if (!featuredImageId.HasValue && selectedImages.Any())
                {
                    featuredImageId = selectedImages.First().Id;
                }

                await UpdateModelFromCurrentImages();
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to update selected images: {ex.Message}");
            }
        }

        private async Task OnImageAdded(FileDto image)
        {
            try
            {
                NotificationService.ShowSuccess($"Added image: {image.OriginalFileName}");

                // If this is the first image, make it featured
                if (!featuredImageId.HasValue)
                {
                    featuredImageId = image.Id;
                    await UpdateModelFromCurrentImages();
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error processing added image: {ex.Message}");
            }
        }

        private async Task OnImageRemoved(FileDto image)
        {
            try
            {
                NotificationService.ShowSuccess($"Removed image: {image.OriginalFileName}");

                // If removed image was featured, clear featured status
                if (featuredImageId == image.Id)
                {
                    featuredImageId = selectedImages.FirstOrDefault()?.Id;
                    await UpdateModelFromCurrentImages();
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error processing removed image: {ex.Message}");
            }
        }

        private async Task OnImagePreview(FileDto file)
        {
            previewingFile = file;
            showImagePreviewDialog = true;
            await InvokeAsync(StateHasChanged);
        }

        private async Task UpdateModelFromCurrentImages()
        {
            try
            {
                // Update the model's images collection
                Model.Images = selectedImages.Select((file, index) => new CreateCategoryImageDto
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
                    var categoryImages = selectedImages.Select((file, index) => new CategoryImageDto
                    {
                        Id = 0,
                        CategoryId = GetEffectiveEntityId(),
                        FileId = file.Id,
                        Alt = file.Alt ?? file.OriginalFileName,
                        Caption = file.Description,
                        Position = index,
                        IsFeatured = featuredImageId == file.Id,
                        ImageUrl = GetImagePreviewUrl(file),
                        ThumbnailUrl = GetImageThumbnailUrl(file),
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

        #endregion

        #region Featured Image Management

        private async Task SetFeaturedImage(int imageId)
        {
            try
            {
                if (!selectedImages.Any(img => img.Id == imageId))
                {
                    NotificationService.ShowError("Image not found in selection");
                    return;
                }

                featuredImageId = imageId;
                await UpdateModelFromCurrentImages();

                var featuredImage = selectedImages.First(img => img.Id == imageId);
                NotificationService.ShowSuccess($"Set '{featuredImage.OriginalFileName}' as featured image");

                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to set featured image: {ex.Message}");
            }
        }

        #endregion

        #region Upload Functionality

        private void ShowUploadDialog()
        {
            pendingUploadFiles.Clear();
            showUploadDialog = true;
            StateHasChanged();
        }

        private void CloseUploadDialog()
        {
            showUploadDialog = false;
            pendingUploadFiles.Clear();
            StateHasChanged();
        }

        private async Task OnFileInputChange(InputFileChangeEventArgs e)
        {
            try
            {
                pendingUploadFiles.Clear();

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
                const long maxFileSize = 5 * 1024 * 1024; // 5MB

                foreach (var file in e.GetMultipleFiles(10)) // Max 10 files
                {
                    // Use the extension method for comprehensive validation
                    var validationResult = file.Validate(
                        maxSizeInBytes: maxFileSize,
                        allowedExtensions: allowedExtensions,
                        requireImage: true
                    );

                    if (!validationResult.IsValid)
                    {
                        NotificationService.ShowWarning($"File '{file.Name}': {validationResult.ErrorMessage}");
                        continue;
                    }

                    pendingUploadFiles.Add(file);
                }

                if (pendingUploadFiles.Any())
                {
                    NotificationService.ShowSuccess($"Selected {pendingUploadFiles.Count} valid image(s) for upload");
                }

                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error processing selected files: {ex.Message}");
            }
        }

        private void RemovePendingFile(IBrowserFile file)
        {
            pendingUploadFiles.Remove(file);
            StateHasChanged();
        }

        private async Task UploadImages()
        {
            if (!pendingUploadFiles.Any())
            {
                NotificationService.ShowWarning("No files selected for upload");
                return;
            }

            try
            {
                isUploadingImages = true;
                await InvokeAsync(StateHasChanged);

                var uploadResults = new List<FileDto>();

                foreach (var file in pendingUploadFiles)
                {
                    try
                    {
                        var uploadDto = new FileUploadDto
                        {
                            File = (Microsoft.AspNetCore.Http.IFormFile)file,
                            Description = $"Category image: {file.Name}",
                            Alt = Path.GetFileNameWithoutExtension(file.Name),
                            IsPublic = true,
                            GenerateThumbnail = true,
                            ProcessImmediately = true,
                            EntityType = "Category",
                            EntityId = GetEffectiveEntityId() > 0 ? GetEffectiveEntityId() : null
                        };

                        var result = await FileService.UploadFileAsync(uploadDto);

                        if (result?.Success == true && result.File != null)
                        {
                            uploadResults.Add(result.File);
                        }
                        else
                        {
                            NotificationService.ShowError($"Failed to upload '{file.Name}': {result?.ErrorMessage ?? "Unknown error"}");
                        }
                    }
                    catch (Exception ex)
                    {
                        NotificationService.ShowError($"Failed to upload '{file.Name}': {ex.Message}");
                    }
                }

                if (uploadResults.Any())
                {
                    // Add uploaded files to selection using ObjectSelector's public API
                    foreach (var uploadedFile in uploadResults)
                    {
                        await AddImageToSelection(uploadedFile);
                    }

                    NotificationService.ShowSuccess($"Successfully uploaded {uploadResults.Count} image(s)");
                    CloseUploadDialog();
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error during upload: {ex.Message}");
            }
            finally
            {
                isUploadingImages = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task AddImageToSelection(FileDto file)
        {
            try
            {
                if (imageSelector != null)
                {
                    // Add using ObjectSelector's AddEntityById method
                    await imageSelector.AddEntityById(file.Id);
                }
                else
                {
                    // Fallback: add directly to selection
                    if (!selectedImages.Any(img => img.Id == file.Id))
                    {
                        selectedImages.Add(file);
                        await OnSelectedImagesChanged(selectedImages);
                    }
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to add uploaded image to selection: {ex.Message}");
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

        private string GetImagePreviewUrl(FileDto file)
        {
            return file.Urls?.Download ?? FileService.GetFileUrl(file.Id);
        }

        private string GetImageThumbnailUrl(FileDto file)
        {
            return file.Urls?.Thumbnail ?? FileService.GetThumbnailUrl(file.Id);
        }

        private void CloseImagePreview()
        {
            showImagePreviewDialog = false;
            previewingFile = null;
            StateHasChanged();
        }

        private string FormatFileSize(long bytes)
        {
            return FileService.FormatFileSize(bytes);
        }

        #endregion

        #region Public API

        public List<FileDto> GetSelectedImages()
        {
            return selectedImages.ToList();
        }

        public int? GetFeaturedImageId()
        {
            return featuredImageId;
        }

        public bool HasSelectedImages()
        {
            return selectedImages.Any();
        }

        public async Task AddImageByIdAsync(int imageId)
        {
            try
            {
                var file = await FileService.GetFileByIdAsync(imageId);
                if (file?.FileType == FileType.Image)
                {
                    await AddImageToSelection(file);
                }
                else
                {
                    NotificationService.ShowError("File not found or is not an image");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to add image: {ex.Message}");
            }
        }

        public async Task RefreshImages()
        {
            await InitializeImages();
        }

        public async Task ClearAllImages()
        {
            try
            {
                selectedImages.Clear();
                featuredImageId = null;
                await UpdateModelFromCurrentImages();
                await InvokeAsync(StateHasChanged);
                NotificationService.ShowSuccess("All images cleared");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to clear images: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            slugValidationTimer?.Dispose();
            initializationSemaphore?.Dispose();
        }

        #endregion
    }
}