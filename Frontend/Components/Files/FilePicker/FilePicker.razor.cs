using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Frontend.Components.Common;
using Frontend.Components.Common.ConfirmationDialogComponent;
using Frontend.Components.Files.FilePreviewComponent;
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
        private List<FileDto> temporaryFiles = new();
        private int? featuredFileId = null;
        private bool isLoading = false;
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

        // Parameter change tracking
        private string? lastEntityType;
        private int lastEntityId;
        private bool hasInitialized = false;
        private readonly SemaphoreSlim loadingSemaphore = new(1, 1);

        // Temporary storage tracking
        private int nextTemporaryId = -1;

        // State tracking for immediate updates
        private bool isInternalUpdate = false;

        protected override async Task OnInitializedAsync()
        {
            lastEntityType = EntityType;
            lastEntityId = EntityId;

            // Initialize from parameters
            InitializeFromParameters();

            hasInitialized = true;

            // Load files from backend if entity exists
            if (!string.IsNullOrEmpty(EntityType) && EntityId > 0)
            {
                await LoadFilesAsync();
            }
            else
            {
                // Use temporary storage mode
                files = temporaryFiles.ToList();
                isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        protected override async Task OnParametersSetAsync()
        {
            if (!hasInitialized) return;

            // Skip if this is an internal update to prevent loops
            if (isInternalUpdate)
            {
                isInternalUpdate = false;
                return;
            }

            var parametersChanged = false;

            // Handle temporary files parameter changes
            if (AllowTemporaryStorage && !AreFileListsEqual(TemporaryFiles, temporaryFiles))
            {
                temporaryFiles = TemporaryFiles?.ToList() ?? new List<FileDto>();
                files = temporaryFiles.ToList();
                parametersChanged = true;
            }

            // Handle featured file parameter changes
            if (FeaturedFileId != featuredFileId)
            {
                featuredFileId = FeaturedFileId;
                parametersChanged = true;
            }

            // Handle entity parameter changes
            if (EntityType != lastEntityType || EntityId != lastEntityId)
            {
                lastEntityType = EntityType;
                lastEntityId = EntityId;

                if (!string.IsNullOrEmpty(EntityType) && EntityId > 0)
                {
                    await LoadFilesAsync();
                }
                else if (AllowTemporaryStorage)
                {
                    files = temporaryFiles.ToList();
                    isLoading = false;
                }
                parametersChanged = true;
            }

            if (parametersChanged)
            {
                await InvokeAsync(StateHasChanged);
            }
        }

        private void InitializeFromParameters()
        {
            // Initialize temporary files if provided
            if (TemporaryFiles?.Any() == true)
            {
                temporaryFiles = TemporaryFiles.ToList();
            }

            // Initialize featured file
            featuredFileId = FeaturedFileId;

            // Set initial files state
            files = temporaryFiles.ToList();
        }

        private bool AreFileListsEqual(List<FileDto>? list1, List<FileDto> list2)
        {
            if (list1 == null && list2.Count == 0) return true;
            if (list1 == null || list1.Count != list2.Count) return false;

            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i].Id != list2[i].Id ||
                    list1[i].OriginalFileName != list2[i].OriginalFileName)
                {
                    return false;
                }
            }
            return true;
        }

        #region File Loading and Management

        private async Task LoadFilesAsync()
        {
            if (!await loadingSemaphore.WaitAsync(100))
            {
                return;
            }

            try
            {
                isLoading = true;
                await InvokeAsync(StateHasChanged);

                if (string.IsNullOrWhiteSpace(EntityType) || EntityId <= 0)
                {
                    files = temporaryFiles.ToList();
                    isLoading = false;
                    await InvokeAsync(StateHasChanged);
                    return;
                }

                var entityFiles = await FileService.GetFilesForEntityAsync(EntityType, EntityId);

                if (AllowedFileTypes?.Any() == true)
                {
                    entityFiles = entityFiles.Where(f => AllowedFileTypes.Contains(f.FileType)).ToList();
                }

                if (AllowedExtensions?.Any() == true)
                {
                    entityFiles = entityFiles.Where(f =>
                        AllowedExtensions.Any(ext =>
                            f.FileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase))).ToList();
                }

                files = entityFiles;
                await NotifyFilesChanged();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load files: {ex.Message}");
                files.Clear();
            }
            finally
            {
                isLoading = false;
                loadingSemaphore.Release();
                await InvokeAsync(StateHasChanged);
            }
        }

        #endregion

        #region Temporary File Management

        private async Task AddTemporaryFile(FileDto file)
        {
            // Assign temporary ID if not set
            if (file.Id == 0)
            {
                file.Id = nextTemporaryId--;
            }

            // Check file count limit
            if (temporaryFiles.Count >= MaxFiles)
            {
                NotificationService.ShowError($"Cannot add more than {MaxFiles} files.");
                return;
            }

            // Check for duplicates by filename
            if (temporaryFiles.Any(f => f.OriginalFileName == file.OriginalFileName))
            {
                NotificationService.ShowError($"File '{file.OriginalFileName}' already exists.");
                return;
            }

            temporaryFiles.Add(file);
            files = temporaryFiles.ToList();

            // Automatically set first image as featured if none is set
            if (AllowFeaturedSelection && file.IsImage && !featuredFileId.HasValue)
            {
                featuredFileId = file.Id;
                await NotifyFeaturedFileChanged();
            }

            await NotifyTemporaryFilesChanged();
            await NotifyFilesChanged();
            await InvokeAsync(StateHasChanged);
        }

        private async Task RemoveTemporaryFile(FileDto file)
        {
            temporaryFiles.RemoveAll(f => f.Id == file.Id);
            files = temporaryFiles.ToList();

            // Clear featured if this was the featured file
            if (featuredFileId == file.Id)
            {
                featuredFileId = null;
                await NotifyFeaturedFileChanged();
            }

            await NotifyTemporaryFilesChanged();
            await NotifyFilesChanged();
            await InvokeAsync(StateHasChanged);
        }

        private async Task ClearTemporaryFiles()
        {
            temporaryFiles.Clear();
            files.Clear();
            featuredFileId = null;

            await NotifyTemporaryFilesChanged();
            await NotifyFilesChanged();
            await NotifyFeaturedFileChanged();
            await InvokeAsync(StateHasChanged);
        }

        #endregion

        #region Event Notifications

        private async Task NotifyFilesChanged()
        {
            isInternalUpdate = true;
            if (OnFilesChanged.HasDelegate)
            {
                await OnFilesChanged.InvokeAsync(files.ToList());
            }
        }

        private async Task NotifyTemporaryFilesChanged()
        {
            isInternalUpdate = true;
            if (OnTemporaryFilesChanged.HasDelegate)
            {
                await OnTemporaryFilesChanged.InvokeAsync(temporaryFiles.ToList());
            }
        }

        private async Task NotifyFeaturedFileChanged()
        {
            isInternalUpdate = true;
            if (OnFeaturedFileChanged.HasDelegate)
            {
                await OnFeaturedFileChanged.InvokeAsync(featuredFileId);
            }
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
            var selectedFiles = e.GetMultipleFiles(MaxFiles).ToList();

            if (!selectedFiles.Any())
                return;

            // Check file count limit
            if (files.Count + selectedFiles.Count > MaxFiles)
            {
                NotificationService.ShowError($"Cannot upload more than {MaxFiles} files. Currently {files.Count} files uploaded.");
                return;
            }

            try
            {
                isUploading = true;
                uploadProgress.Clear();
                await InvokeAsync(StateHasChanged);

                if (AllowTemporaryStorage && (string.IsNullOrEmpty(EntityType) || EntityId <= 0))
                {
                    // Handle temporary storage
                    await ProcessTemporaryFiles(selectedFiles);
                }
                else
                {
                    // Handle direct upload to entity
                    await ProcessDirectUpload(selectedFiles);
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
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task ProcessTemporaryFiles(List<IBrowserFile> selectedFiles)
        {
            var processedFiles = new List<FileDto>();

            foreach (var file in selectedFiles)
            {
                if (!ValidateFile(file))
                    continue;

                try
                {
                    uploadProgress[file.Name] = 0;
                    await InvokeAsync(StateHasChanged);

                    // Create temporary file DTO
                    var tempFile = await CreateTemporaryFileDto(file);

                    // Simulate progress
                    for (int i = 20; i <= 100; i += 20)
                    {
                        uploadProgress[file.Name] = i;
                        await InvokeAsync(StateHasChanged);
                        await Task.Delay(50);
                    }

                    processedFiles.Add(tempFile);
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Failed to process {file.Name}: {ex.Message}");
                    uploadProgress.Remove(file.Name);
                    await InvokeAsync(StateHasChanged);
                }
            }

            // Add processed files to temporary storage
            foreach (var file in processedFiles)
            {
                await AddTemporaryFile(file);
            }

            if (processedFiles.Any())
            {
                NotificationService.ShowSuccess($"Added {processedFiles.Count} file(s)");
            }
        }

        private async Task ProcessDirectUpload(List<IBrowserFile> selectedFiles)
        {
            var uploadTasks = new List<Task>();

            foreach (var file in selectedFiles)
            {
                if (!ValidateFile(file))
                    continue;

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

        private async Task<FileDto> CreateTemporaryFileDto(IBrowserFile file)
        {
            var tempFile = new FileDto
            {
                Id = nextTemporaryId--,
                OriginalFileName = file.Name,
                ContentType = file.ContentType,
                FileSize = file.Size,
                FileSizeFormatted = FileService.FormatFileSize(file.Size),
                FileExtension = Path.GetExtension(file.Name).ToLower(),
                FileType = DetermineFileType(file.ContentType),
                IsPublic = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Urls = new FileUrlsDto(),
                HasThumbnail = false,
                CanPreview = false
            };

            // Set file type name
            tempFile.FileTypeName = tempFile.FileType.ToString();

            // Generate thumbnail for images
            if (tempFile.IsImage && ShowThumbnails)
            {
                try
                {
                    var thumbnailData = await GenerateThumbnailFromFile(file);
                    if (thumbnailData != null)
                    {
                        tempFile.Urls.Thumbnail = thumbnailData;
                        tempFile.Urls.Download = thumbnailData;
                        tempFile.HasThumbnail = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to generate thumbnail: {ex.Message}");
                }
            }

            return tempFile;
        }

        private async Task<string?> GenerateThumbnailFromFile(IBrowserFile file)
        {
            try
            {
                const int maxSize = 2 * 1024 * 1024; // 2MB limit for preview
                using var stream = file.OpenReadStream(maxAllowedSize: Math.Min(file.Size, maxSize));
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);

                var bytes = memoryStream.ToArray();
                var base64 = Convert.ToBase64String(bytes);
                return $"data:{file.ContentType};base64,{base64}";
            }
            catch
            {
                return null;
            }
        }

        private FileType DetermineFileType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return FileType.Other;

            if (contentType.StartsWith("image/"))
                return FileType.Image;
            if (contentType.StartsWith("video/"))
                return FileType.Video;
            if (contentType.StartsWith("audio/"))
                return FileType.Audio;
            if (contentType.Contains("pdf") || contentType.Contains("document") || contentType.Contains("text"))
                return FileType.Document;
            if (contentType.Contains("zip") || contentType.Contains("compressed"))
                return FileType.Archive;

            return FileType.Other;
        }

        private bool ValidateFile(IBrowserFile file)
        {
            // Validate file size
            if (file.Size > MaxFileSize)
            {
                NotificationService.ShowError($"File {file.Name} is too large. Maximum size is {FileService.FormatFileSize(MaxFileSize)}.");
                return false;
            }

            // Validate file type
            if (AllowedExtensions?.Any() == true)
            {
                var extension = Path.GetExtension(file.Name);
                if (!AllowedExtensions.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase)))
                {
                    NotificationService.ShowError($"File type {extension} is not allowed.");
                    return false;
                }
            }

            return true;
        }

        private async Task UploadSingleFile(IBrowserFile file)
        {
            try
            {
                uploadProgress[file.Name] = 0;
                await InvokeAsync(StateHasChanged);

                var formFile = new FormFileWrapper(file);
                var uploadDto = new FileUploadDto
                {
                    File = formFile,
                    EntityType = EntityType,
                    EntityId = EntityId,
                    IsPublic = false,
                    GenerateThumbnail = ShowThumbnails,
                    ProcessImmediately = true
                };

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

        #region Featured File Management

        private async Task ToggleFeaturedFile(FileDto file)
        {
            if (!AllowFeaturedSelection)
                return;

            if (featuredFileId == file.Id)
            {
                // Remove featured
                featuredFileId = null;
            }
            else
            {
                // Set as featured
                featuredFileId = file.Id;
            }

            await NotifyFeaturedFileChanged();
            await InvokeAsync(StateHasChanged);
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

            try
            {
                isProcessing = true;
                await InvokeAsync(StateHasChanged);

                if (AllowTemporaryStorage && (string.IsNullOrEmpty(EntityType) || EntityId <= 0))
                {
                    // Add to temporary storage
                    var addedCount = 0;
                    foreach (var file in selectedBrowserFiles)
                    {
                        if (temporaryFiles.Any(f => f.Id == file.Id))
                            continue;

                        if (temporaryFiles.Count >= MaxFiles)
                        {
                            NotificationService.ShowError($"Cannot add more files. Maximum {MaxFiles} files allowed.");
                            break;
                        }

                        await AddTemporaryFile(file);
                        addedCount++;
                    }

                    if (addedCount > 0)
                    {
                        NotificationService.ShowSuccess($"Added {addedCount} file(s)");
                    }
                }
                else
                {
                    // Add to entity (existing logic)
                    var addedCount = 0;
                    foreach (var file in selectedBrowserFiles)
                    {
                        if (files.Any(f => f.Id == file.Id))
                            continue;

                        if (files.Count >= MaxFiles)
                        {
                            NotificationService.ShowError($"Cannot add more files. Maximum {MaxFiles} files allowed.");
                            break;
                        }

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
                await InvokeAsync(StateHasChanged);
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
                else if (file.Id > 0)
                {
                    await FileService.DownloadFileAsync(file.Id);
                }
                else
                {
                    NotificationService.ShowInfo($"File {file.OriginalFileName} is in temporary storage and cannot be downloaded until saved.");
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
                await InvokeAsync(StateHasChanged);

                if (AllowTemporaryStorage && fileToRemove.Id < 0)
                {
                    // Remove from temporary storage
                    await RemoveTemporaryFile(fileToRemove);
                    NotificationService.ShowSuccess($"Removed {fileToRemove.OriginalFileName}");
                }
                else
                {
                    // Remove from entity
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
                        await NotifyFilesChanged();
                        NotificationService.ShowSuccess($"Removed {fileToRemove.OriginalFileName}");
                    }
                    else
                    {
                        NotificationService.ShowError("Failed to remove file association");
                    }
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
                await InvokeAsync(StateHasChanged);
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
                await InvokeAsync(StateHasChanged);

                if (AllowTemporaryStorage && (string.IsNullOrEmpty(EntityType) || EntityId <= 0))
                {
                    // Clear temporary storage
                    await ClearTemporaryFiles();
                    NotificationService.ShowSuccess("Cleared all files");
                }
                else
                {
                    // Clear entity files
                    var result = await FileService.DeleteFilesForEntityAsync(EntityType, EntityId);

                    if (result.IsCompleteSuccess || result.IsPartialSuccess)
                    {
                        files.Clear();
                        await NotifyFilesChanged();
                        NotificationService.ShowSuccess($"Cleared {result.SuccessCount} file(s)");
                    }
                    else
                    {
                        NotificationService.ShowError("Failed to clear files");
                    }
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to clear files: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                await InvokeAsync(StateHasChanged);
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

        private string GetFileCardClasses(FileDto file)
        {
            var baseClass = "border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden bg-white dark:bg-gray-800 shadow-sm hover:shadow-md transition-shadow duration-200";

            if (featuredFileId == file.Id)
            {
                baseClass += " ring-2 ring-yellow-400 dark:ring-yellow-500";
            }

            return baseClass;
        }

        private string GetFileListItemClasses(FileDto file)
        {
            var baseClass = "bg-gray-50 dark:bg-gray-700";

            if (featuredFileId == file.Id)
            {
                baseClass = "bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800";
            }

            return baseClass;
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

        public List<FileDto> GetFiles()
        {
            return files.ToList();
        }

        public int GetFileCount()
        {
            return files.Count;
        }

        public bool IsMaxFilesReached()
        {
            return files.Count >= MaxFiles;
        }

        public async Task Refresh()
        {
            await LoadFilesAsync();
        }

        public async Task Clear()
        {
            await ClearAllFiles();
        }

        public int? GetFeaturedFileId()
        {
            return featuredFileId;
        }

        public List<FileDto> GetTemporaryFiles()
        {
            return temporaryFiles.ToList();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            loadingSemaphore?.Dispose();
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