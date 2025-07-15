
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

        // Delegates for handling different image types
        [Parameter] public Func<object, string> GetImageUrlFunc { get; set; } = null!;
        [Parameter] public Func<object, string?> GetImageAltFunc { get; set; } = null!;
        [Parameter] public Func<object, bool> GetIsFeaturedFunc { get; set; } = null!;
        [Parameter] public Func<FileDto, object> CreateImageFromFileFunc { get; set; } = null!;
        [Parameter] public Action<object, string?, string?, bool> UpdateImageFunc { get; set; } = null!;

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
            // Filter to only image files
            tempSelectedFiles = files?.Where(f => f.FileType == Backend.CMS.Domain.Enums.FileType.Image).ToList() ?? new List<FileDto>();

            // If single image mode, only keep the first selected
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

            var newImages = new List<object>();

            foreach (var file in tempSelectedFiles)
            {
                try
                {
                    // Check if already added (for multiple mode)
                    if (AllowMultiple)
                    {
                        // Better duplicate check using file ID comparison
                        bool alreadyExists = false;
                        foreach (var existingImg in SelectedImages)
                        {
                            if (existingImg is CreateCategoryImageDto categoryImg && categoryImg.FileId == file.Id)
                            {
                                alreadyExists = true;
                                break;
                            }
                        }

                        if (!alreadyExists)
                        {
                            var newImage = CreateImageFromFileFunc(file);
                            newImages.Add(newImage);

                            // Cache the file for later use
                            cachedFiles[file.Id] = file;
                        }
                    }
                    else
                    {
                        // Single mode - replace existing
                        SelectedImages.Clear();
                        var newImage = CreateImageFromFileFunc(file);
                        newImages.Add(newImage);
                        cachedFiles[file.Id] = file;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Failed to add image {file.OriginalFileName}: {ex.Message}");
                }
            }

            if (newImages.Any())
            {
                if (AllowMultiple)
                {
                    SelectedImages.AddRange(newImages);
                }
                else
                {
                    SelectedImages = newImages;
                }

                try
                {
                    await SelectedImagesChanged.InvokeAsync(SelectedImages);
                    StateHasChanged(); // Force immediate UI update
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Failed to update images: {ex.Message}");
                }
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
                editingImageCaption = string.Empty; // This would need to be extracted based on your image type
                editingImageIsFeatured = GetIsFeaturedFunc(editingImage);

                // Try to get the cached file
                var imageUrl = GetImageUrlFunc(editingImage);
                var fileId = ExtractFileIdFromUrl(imageUrl);
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
            if (editingImage != null && editingImageIndex >= 0 && editingImageIndex < SelectedImages.Count)
            {
                // If setting as featured and multiple images allowed, remove featured from others
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

                // Update the editing image
                UpdateImageFunc(editingImage, editingImageAlt, editingImageCaption, editingImageIsFeatured);

                await SelectedImagesChanged.InvokeAsync(SelectedImages);
                StateHasChanged(); // Force immediate UI update
                CloseImageEdit();
            }
        }

        private async Task SetAsFeatured(int index)
        {
            if (index >= 0 && index < SelectedImages.Count && AllowFeatured && AllowMultiple)
            {
                // Remove featured from all images
                for (int i = 0; i < SelectedImages.Count; i++)
                {
                    UpdateImageFunc(SelectedImages[i], GetImageAltFunc(SelectedImages[i]), string.Empty, i == index);
                }

                await SelectedImagesChanged.InvokeAsync(SelectedImages);
                StateHasChanged(); // Force immediate UI update
            }
        }

        private async Task RemoveImage(int index)
        {
            if (index >= 0 && index < SelectedImages.Count)
            {
                SelectedImages.RemoveAt(index);
                await SelectedImagesChanged.InvokeAsync(SelectedImages);
                StateHasChanged(); // Force immediate UI update
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

        private int? ExtractFileIdFromUrl(string url)
        {
            // This is a simple implementation - you might need to adjust based on your URL structure
            var parts = url.Split('/');
            foreach (var part in parts)
            {
                if (int.TryParse(part, out var id))
                {
                    return id;
                }
            }
            return null;
        }
    }
}
