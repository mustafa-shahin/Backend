// Frontend/Components/Files/FilePicker/FilePicker.razor.cs
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Frontend.Components.Common;
using Frontend.Components.Common.ConfirmationDialogComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

namespace Frontend.Components.Files.FilePicker
{
    public partial class FilePicker : ComponentBase, IDisposable
    {
        // Private fields
        private List<FileDto> files = new();
        private bool isLoading = true;
        private bool isUploading = false;
        private bool isProcessing = false;
        private bool showFileCount = true;
        private Dictionary<string, int> uploadProgress = new();

        // File input reference
        private InputFile? fileInput;

        // Dialog references
        private FormDialog? fileBrowserDialog;
        private ConfirmationDialog? removeFileDialog;
        private ConfirmationDialog? clearAllDialog;

        // File browser state
        private bool showFileBrowserDialog = false;
        private List<FileDto> selectedBrowserFiles = new();

        // File removal state
        private FileDto? fileToRemove = null;

        // Debounce timer for entity changes
        private Timer? entityChangeTimer;

        protected override async Task OnInitializedAsync()
        {
            await LoadFilesAsync();
        }

        protected override async Task OnParametersSetAsync()
        {
            // Debounce entity changes to avoid rapid reloads
            entityChangeTimer?.Dispose();
            entityChangeTimer = new Timer(async _ =>
            {
                await InvokeAsync(LoadFilesAsync);
            }, null, 300, Timeout.Infinite);
        }

        #region File Loading and Management

        private async Task LoadFilesAsync()
        {
            if (string.IsNullOrWhiteSpace(EntityType) || EntityId <= 0)
            {
                files.Clear();
                isLoading = false;
                StateHasChanged();
                return;
            }

            try
            {
                isLoading = true;
                StateHasChanged();

                // Get files for the specific entity
                var entityFiles = await FileService.GetFilesForEntityAsync(EntityType, EntityId);

                // Filter by allowed file types if specified
                if (AllowedFileTypes?.Any() == true)
                {
                    entityFiles = entityFiles.Where(f => AllowedFileTypes.Contains(f.FileType)).ToList();
                }

                // Filter by allowed extensions if specified
                if (AllowedExtensions?.Any() == true)
                {
                    entityFiles = entityFiles.Where(f =>
                        AllowedExtensions.Any(ext =>
                            f.FileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase))).ToList();
                }

                files = entityFiles;
                await OnFilesChanged.InvokeAsync(files);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load files: {ex.Message}");
                files.Clear();
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task RefreshFiles()
        {
            await LoadFilesAsync();
        }

        #endregion

        #region File Upload

        private async Task TriggerFileDialog()
        {
            try
            {
                if (fileInput?.Element != null)
                {
                    await JSRuntime.InvokeVoidAsync("triggerFileInput", fileInput.Element);
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to open file dialog: {ex.Message}");
            }
        }

        private async Task OnFilesSelected(InputFileChangeEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EntityType) || EntityId <= 0)
            {
                NotificationService.ShowError("Please save the item before uploading files.");
                return;
            }

            var selectedFiles = e.GetMultipleFiles(MaxFiles).ToList();

            if (!selectedFiles.Any())
                return;

            // Validate file count
            if (files.Count + selectedFiles.Count > MaxFiles)
            {
                NotificationService.ShowError($"Cannot upload more than {MaxFiles} files. Currently {files.Count} files uploaded.");
                return;
            }

            try
            {
                isUploading = true;
                uploadProgress.Clear();
                StateHasChanged();

                var uploadTasks = new List<Task>();

                foreach (var file in selectedFiles)
                {
                    // Validate file size
                    if (file.Size > MaxFileSize)
                    {
                        NotificationService.ShowError($"File {file.Name} is too large. Maximum size is {FileService.FormatFileSize(MaxFileSize)}.");
                        continue;
                    }

                    // Validate file type
                    if (AllowedExtensions?.Any() == true)
                    {
                        var extension = Path.GetExtension(file.Name);
                        if (!AllowedExtensions.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase)))
                        {
                            NotificationService.ShowError($"File type {extension} is not allowed.");
                            continue;
                        }
                    }

                    uploadTasks.Add(UploadSingleFile(file));
                }

                await Task.WhenAll(uploadTasks);

                var successCount = uploadTasks.Count;
                if (successCount > 0)
                {
                    NotificationService.ShowSuccess($"Successfully uploaded {successCount} file(s)");
                    await LoadFilesAsync();
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Upload failed: {ex.Message}");
            }
            finally
            {
                isUploading = false;
                uploadProgress.Clear();
                StateHasChanged();
            }
        }

        private async Task UploadSingleFile(IBrowserFile file)
        {
            try
            {
                // Initialize progress
                uploadProgress[file.Name] = 0;
                await InvokeAsync(StateHasChanged);

                var formFile = new FormFileWrapper(file);
                var uploadDto = new FileUploadDto
                {
                    File = formFile,
                    EntityType = EntityType,
                    EntityId = EntityId,
                    IsPublic = false, // Default to private for entity files
                    GenerateThumbnail = ShowThumbnails,
                    ProcessImmediately = true
                };

                // Simulate progress updates
                var progressTask = Task.Run(async () =>
                {
                    for (int i = 10; i <= 90; i += 10)
                    {
                        await Task.Delay(100);
                        uploadProgress[file.Name] = i;
                        await InvokeAsync(StateHasChanged);
                    }
                });

                var result = await FileService.UploadFileForEntityAsync(uploadDto);

                await progressTask;

                if (result?.Success == true && result.File != null)
                {
                    uploadProgress[file.Name] = 100;
                    await InvokeAsync(StateHasChanged);

                    // Small delay to show completion
                    await Task.Delay(200);
                }
                else
                {
                    throw new Exception(result?.ErrorMessage ?? "Upload failed");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to upload {file.Name}: {ex.Message}");
                uploadProgress.Remove(file.Name);
                await InvokeAsync(StateHasChanged);
            }
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

        private void OnFileBrowserSelectionChanged(List<FileDto> selectedFiles)
        {
            selectedBrowserFiles = selectedFiles;
        }

        private async Task AddSelectedBrowserFiles()
        {
            if (!selectedBrowserFiles.Any())
            {
                CloseFileBrowser();
                return;
            }

            if (string.IsNullOrWhiteSpace(EntityType) || EntityId <= 0)
            {
                NotificationService.ShowError("Please save the item before adding files.");
                CloseFileBrowser();
                return;
            }

            try
            {
                isProcessing = true;
                StateHasChanged();

                var addedCount = 0;

                foreach (var file in selectedBrowserFiles)
                {
                    // Check if file already exists
                    if (files.Any(f => f.Id == file.Id))
                        continue;

                    // Validate file count
                    if (files.Count + addedCount >= MaxFiles)
                    {
                        NotificationService.ShowError($"Cannot add more files. Maximum {MaxFiles} files allowed.");
                        break;
                    }

                    // For entity-specific files, we need to create a link between the file and entity
                    // This could be done via file tags or a separate entity-file relationship table
                    // For now, we'll update the file with entity information via tags
                    var updateDto = new UpdateFileDto
                    {
                        Description = file.Description,
                        Alt = file.Alt,
                        IsPublic = file.IsPublic,
                        Tags = file.Tags ?? new Dictionary<string, object>()
                    };

                    updateDto.Tags["EntityType"] = EntityType;
                    updateDto.Tags["EntityId"] = EntityId.ToString();

                    var updatedFile = await FileService.UpdateFileAsync(file.Id, updateDto);
                    if (updatedFile != null)
                    {
                        addedCount++;
                    }
                }

                if (addedCount > 0)
                {
                    NotificationService.ShowSuccess($"Added {addedCount} file(s)");
                    await LoadFilesAsync();
                }

                CloseFileBrowser();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to add files: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                StateHasChanged();
            }
        }

        #endregion

        #region File Actions

        private async Task PreviewFile(FileDto file)
        {
            try
            {
                if (OnFilePreview.HasDelegate)
                {
                    await OnFilePreview.InvokeAsync(file);
                }
                else
                {
                    // Default preview action - could open a preview dialog
                    NotificationService.ShowInfo($"Preview for {file.OriginalFileName}");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to preview file: {ex.Message}");
            }
        }

        private async Task DownloadFile(FileDto file)
        {
            try
            {
                if (OnFileDownload.HasDelegate)
                {
                    await OnFileDownload.InvokeAsync(file);
                }
                else
                {
                    await FileService.DownloadFileAsync(file.Id);
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to download file: {ex.Message}");
            }
        }

        private void ShowRemoveConfirmation(FileDto file)
        {
            fileToRemove = file;
            removeFileDialog?.Show();
        }

        private async Task RemoveSelectedFile()
        {
            if (fileToRemove == null) return;

            try
            {
                isProcessing = true;
                StateHasChanged();

                // Remove entity association by clearing entity tags
                var updateDto = new UpdateFileDto
                {
                    Description = fileToRemove.Description,
                    Alt = fileToRemove.Alt,
                    IsPublic = fileToRemove.IsPublic,
                    Tags = fileToRemove.Tags?.Where(t =>
                        t.Key != "EntityType" && t.Key != "EntityId")
                        .ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, object>()
                };

                var updatedFile = await FileService.UpdateFileAsync(fileToRemove.Id, updateDto);
                if (updatedFile != null)
                {
                    files.Remove(fileToRemove);
                    await OnFilesChanged.InvokeAsync(files);
                    NotificationService.ShowSuccess($"Removed {fileToRemove.OriginalFileName}");
                }
                else
                {
                    NotificationService.ShowError("Failed to remove file association");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to remove file: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                fileToRemove = null;
                StateHasChanged();
            }
        }

        private void ShowClearConfirmation()
        {
            clearAllDialog?.Show();
        }

        private async Task ClearAllFiles()
        {
            try
            {
                isProcessing = true;
                StateHasChanged();

                var result = await FileService.DeleteFilesForEntityAsync(EntityType, EntityId);

                if (result.IsCompleteSuccess || result.IsPartialSuccess)
                {
                    files.Clear();
                    await OnFilesChanged.InvokeAsync(files);
                    NotificationService.ShowSuccess($"Cleared {result.SuccessCount} file(s)");
                }
                else
                {
                    NotificationService.ShowError("Failed to clear files");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to clear files: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                StateHasChanged();
            }
        }

        #endregion

        #region Helper Methods

        private string GetAcceptAttribute()
        {
            if (AllowedExtensions?.Any() == true)
            {
                return string.Join(",", AllowedExtensions);
            }

            if (AllowedFileTypes?.Any() == true)
            {
                var mimeTypes = new List<string>();

                foreach (var fileType in AllowedFileTypes)
                {
                    switch (fileType)
                    {
                        case FileType.Image:
                            mimeTypes.Add("image/*");
                            break;
                        case FileType.Video:
                            mimeTypes.Add("video/*");
                            break;
                        case FileType.Audio:
                            mimeTypes.Add("audio/*");
                            break;
                        case FileType.Document:
                            mimeTypes.AddRange(new[] { ".pdf", ".doc", ".docx", ".txt", ".rtf" });
                            break;
                    }
                }

                return string.Join(",", mimeTypes.Distinct());
            }

            return "*/*";
        }

        private string GetGridClasses()
        {
            return GridSize.ToLower() switch
            {
                "small" => "grid-cols-2 sm:grid-cols-4 md:grid-cols-6 lg:grid-cols-8",
                "medium" => "grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6",
                "large" => "grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4",
                _ => "grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6"
            };
        }

        private string GetFileCardClasses()
        {
            return "border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden bg-white dark:bg-gray-800 shadow-sm hover:shadow-md transition-shadow duration-200";
        }

        private string GetFilePreviewClasses()
        {
            return GridSize.ToLower() switch
            {
                "small" => "aspect-square relative",
                "medium" => "aspect-square relative",
                "large" => "aspect-video relative",
                _ => "aspect-square relative"
            };
        }

        private string GetFilePreviewUrl(FileDto file)
        {
            return file.Urls?.Thumbnail ?? file.Urls?.Download ?? FileService.GetThumbnailUrl(file.Id);
        }

        private string GetFileTypeBadgeClass(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200",
                FileType.Video => "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200",
                FileType.Audio => "bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200",
                FileType.Document => "bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200",
                FileType.Archive => "bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200",
                _ => "bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200"
            };
        }

        private string GetFileIconBackgroundClass(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => "bg-green-500",
                FileType.Video => "bg-blue-500",
                FileType.Audio => "bg-purple-500",
                FileType.Document => "bg-orange-500",
                FileType.Archive => "bg-gray-500",
                _ => "bg-gray-500"
            };
        }

        #endregion

        #region Public API Methods

        /// <summary>
        /// Get current files
        /// </summary>
        public List<FileDto> GetFiles()
        {
            return files.ToList();
        }

        /// <summary>
        /// Get file count
        /// </summary>
        public int GetFileCount()
        {
            return files.Count;
        }

        /// <summary>
        /// Check if max files reached
        /// </summary>
        public bool IsMaxFilesReached()
        {
            return files.Count >= MaxFiles;
        }

        /// <summary>
        /// Refresh files from backend
        /// </summary>
        public async Task Refresh()
        {
            await LoadFilesAsync();
        }

        /// <summary>
        /// Clear all files
        /// </summary>
        public async Task Clear()
        {
            await ClearAllFiles();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            entityChangeTimer?.Dispose();
        }

        #endregion

        #region FormFile Wrapper

        private class FormFileWrapper : IFormFile
        {
            private readonly IBrowserFile _browserFile;

            public FormFileWrapper(IBrowserFile browserFile)
            {
                _browserFile = browserFile;
            }

            public string ContentType => _browserFile.ContentType;
            public string ContentDisposition => "";
            public IHeaderDictionary Headers => new Microsoft.AspNetCore.Http.HeaderDictionary();
            public long Length => _browserFile.Size;
            public string Name => _browserFile.Name;
            public string FileName => _browserFile.Name;

            public void CopyTo(Stream target) => throw new NotImplementedException();

            public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
                => _browserFile.OpenReadStream(maxAllowedSize: 1024 * 1024 * 100, cancellationToken: cancellationToken)
                    .CopyToAsync(target, cancellationToken);

            public Stream OpenReadStream() => _browserFile.OpenReadStream(maxAllowedSize: 1024 * 1024 * 100);
        }

        #endregion
    }
}