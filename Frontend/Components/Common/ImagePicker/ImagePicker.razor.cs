using Backend.CMS.Application.DTOs;
using Frontend.Components.Files;
using Frontend.Components.Files.FileBrowser;
using Microsoft.AspNetCore.Components;

namespace Frontend.Components.Common.ImagePicker
{
    public partial class ImagePicker : ComponentBase
    {
        [Parameter] public List<object> SelectedImages { get; set; } = new();
        [Parameter] public EventCallback<List<object>> SelectedImagesChanged { get; set; }
        [Parameter] public bool AllowMultiple { get; set; } = true;
        [Parameter] public bool AllowFeatured { get; set; } = true;
        [Parameter] public bool AllowUpload { get; set; } = true;
        [Parameter] public string EntityName { get; set; } = "item";
        [Parameter] public int? EntityId { get; set; } 

        // Delegates for handling different image types
        [Parameter] public Func<object, string> GetImageUrlFunc { get; set; } = null!;
        [Parameter] public Func<object, string?> GetImageAltFunc { get; set; } = null!;
        [Parameter] public Func<object, bool> GetIsFeaturedFunc { get; set; } = null!;
        [Parameter] public Func<object, int?> GetImageIdFunc { get; set; } = null!;
        [Parameter] public Func<FileDto, object> CreateImageFromFileFunc { get; set; } = null!;
        [Parameter] public Action<object, string?, string?, bool> UpdateImageFunc { get; set; } = null!;

        // Backend API delegates - these will make immediate calls
        [Parameter] public Func<int, CreateCategoryImageDto, Task<object?>>? AddImageApiFunc { get; set; }
        [Parameter] public Func<int, UpdateCategoryImageDto, Task<object?>>? UpdateImageApiFunc { get; set; }
        [Parameter] public Func<int, Task<bool>>? DeleteImageApiFunc { get; set; }

        private FormDialog? filePickerDialog;
        private FormDialog? imageEditDialog;
        private FileBrowser? fileBrowser;

        private bool showFilePicker = false;
        private bool showImageEdit = false;
        private object? editingImage = null;
        private int editingImageIndex = -1;
        private FileDto? editingImageFile = null;
        private string editingImageAlt = string.Empty;
        private string editingImageCaption = string.Empty;
        private bool editingImageIsFeatured = false;

        private List<FileDto> tempSelectedFiles = new();
        private Dictionary<int, FileDto> cachedFiles = new();

        private void ShowFilePicker()
        {
            tempSelectedFiles.Clear();
            showFilePicker = true;
            StateHasChanged();
        }

        private void CloseFilePicker()
        {
            showFilePicker = false;
            tempSelectedFiles.Clear();
            StateHasChanged();
        }

        private void OnFilesSelected(List<FileDto> files)
        {
            tempSelectedFiles = files?.Where(f => f.FileType == Backend.CMS.Domain.Enums.FileType.Image).ToList() ?? new List<FileDto>();

            if (!AllowMultiple && tempSelectedFiles.Count > 1)
            {
                tempSelectedFiles = tempSelectedFiles.Take(1).ToList();
            }

            StateHasChanged();
        }

        private void RemoveTempFile(FileDto file)
        {
            tempSelectedFiles.Remove(file);
            StateHasChanged();
        }

        private async Task AddSelectedFiles()
        {
            if (!tempSelectedFiles.Any())
            {
                CloseFilePicker();
                return;
            }

            try
            {
                foreach (var file in tempSelectedFiles)
                {
                    var newImageDto = new CreateCategoryImageDto
                    {
                        FileId = file.Id,
                        Position = SelectedImages.Count,
                        IsFeatured = !SelectedImages.Any() && AllowFeatured
                    };

                    if (EntityId.HasValue && AddImageApiFunc != null)
                    {
                        var addedImage = await AddImageApiFunc(EntityId.Value, newImageDto);
                        if (addedImage != null)
                        {
                            SelectedImages.Add(addedImage);
                            cachedFiles[file.Id] = file;
                        }
                    }
                    else
                    {
                        var newImage = CreateImageFromFileFunc(file);
                        SelectedImages.Add(newImage);
                        cachedFiles[file.Id] = file;
                    }
                }

                await SelectedImagesChanged.InvokeAsync(SelectedImages);
                StateHasChanged();
                NotificationService.ShowSuccess($"{tempSelectedFiles.Count} image(s) added successfully");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to add images: {ex.Message}");
            }

            CloseFilePicker();
        }

        private void EditImage(int index)
        {
            if (index >= 0 && index < SelectedImages.Count)
            {
                editingImageIndex = index;
                editingImage = SelectedImages[index];
                editingImageAlt = GetImageAltFunc(editingImage) ?? string.Empty;
                editingImageCaption = string.Empty;
                editingImageIsFeatured = GetIsFeaturedFunc(editingImage);

                var fileId = ExtractFileIdFromImageObject(editingImage);
                if (fileId.HasValue && cachedFiles.ContainsKey(fileId.Value))
                {
                    editingImageFile = cachedFiles[fileId.Value];
                }

                showImageEdit = true;
                StateHasChanged();
            }
        }

        private void CloseImageEdit()
        {
            showImageEdit = false;
            editingImage = null;
            editingImageFile = null;
            editingImageIndex = -1;
            editingImageAlt = string.Empty;
            editingImageCaption = string.Empty;
            editingImageIsFeatured = false;
            StateHasChanged();
        }

        private async Task SaveImageEdit()
        {
            if (editingImage == null || editingImageIndex < 0 || editingImageIndex >= SelectedImages.Count)
                return;

            try
            {
                // If setting as featured, remove featured from others
                if (AllowFeatured && AllowMultiple && editingImageIsFeatured)
                {
                    for (int i = 0; i < SelectedImages.Count; i++)
                    {
                        if (i != editingImageIndex)
                        {
                            UpdateImageFunc(SelectedImages[i], GetImageAltFunc(SelectedImages[i]), string.Empty, false);
                        }
                    }
                }

                // Update local image
                UpdateImageFunc(editingImage, editingImageAlt, editingImageCaption, editingImageIsFeatured);

                var imageId = GetImageIdFunc?.Invoke(editingImage);
                if (imageId.HasValue && imageId.Value > 0 && UpdateImageApiFunc != null)
                {
                    var updateDto = new UpdateCategoryImageDto
                    {
                        Id = imageId.Value,
                        FileId = ExtractFileIdFromImageObject(editingImage) ?? 0,
                        Alt = editingImageAlt,
                        Caption = editingImageCaption,
                        Position = editingImageIndex,
                        IsFeatured = editingImageIsFeatured
                    };

                    var updatedImage = await UpdateImageApiFunc(imageId.Value, updateDto);
                    if (updatedImage != null)
                    {
                        SelectedImages[editingImageIndex] = updatedImage;
                        NotificationService.ShowSuccess("Image updated successfully");
                    }
                }

                await SelectedImagesChanged.InvokeAsync(SelectedImages);
                StateHasChanged();
                CloseImageEdit();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to update image: {ex.Message}");
            }
        }

        private async Task SetAsFeatured(int index)
        {
            if (index < 0 || index >= SelectedImages.Count || !AllowFeatured || !AllowMultiple)
                return;

            try
            {
                // Remove featured from all images
                for (int i = 0; i < SelectedImages.Count; i++)
                {
                    UpdateImageFunc(SelectedImages[i], GetImageAltFunc(SelectedImages[i]), string.Empty, i == index);
                }

                await SelectedImagesChanged.InvokeAsync(SelectedImages);
                StateHasChanged();
                NotificationService.ShowSuccess("Featured image updated");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to set featured image: {ex.Message}");
            }
        }

        private async Task RemoveImage(int index)
        {
            if (index < 0 || index >= SelectedImages.Count)
                return;

            try
            {
                var imageToRemove = SelectedImages[index];
                var imageId = GetImageIdFunc?.Invoke(imageToRemove);

                if (imageId.HasValue && imageId.Value > 0 && DeleteImageApiFunc != null)
                {
                    var success = await DeleteImageApiFunc(imageId.Value);
                    if (!success)
                    {
                        NotificationService.ShowError("Failed to delete image from server");
                        return;
                    }
                }

                // Remove from local list
                SelectedImages.RemoveAt(index);
                await SelectedImagesChanged.InvokeAsync(SelectedImages);
                StateHasChanged();
                NotificationService.ShowSuccess("Image removed successfully");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to remove image: {ex.Message}");
            }
        }

        private string GetImageUrl(object image)
        {
            return GetImageUrlFunc?.Invoke(image) ?? string.Empty;
        }

        private string GetImageAlt(object image)
        {
            return GetImageAltFunc?.Invoke(image) ?? "Image";
        }

        private bool IsFeatured(int index)
        {
            if (!AllowFeatured || index < 0 || index >= SelectedImages.Count)
                return false;

            return GetIsFeaturedFunc?.Invoke(SelectedImages[index]) ?? false;
        }

        private string GetSelectionTitle()
        {
            if (AllowMultiple)
            {
                return $"Selected Images ({SelectedImages.Count})";
            }
            return "Selected Image";
        }

        private string GetPickerDescription()
        {
            if (AllowMultiple)
            {
                return "Select multiple images from existing files or upload new images";
            }
            return "Select from existing files or upload a new image";
        }

        private string GetFilePreviewUrl(FileDto file)
        {
            return file.Urls?.Thumbnail ?? file.Urls?.Download ?? FileService.GetThumbnailUrl(file.Id);
        }

        private int? ExtractFileIdFromImageObject(object image)
        {
            var fileIdProperty = image.GetType().GetProperty("FileId");
            if (fileIdProperty != null && fileIdProperty.PropertyType == typeof(int))
            {
                return (int?)fileIdProperty.GetValue(image);
            }
            return null;
        }
    }
}