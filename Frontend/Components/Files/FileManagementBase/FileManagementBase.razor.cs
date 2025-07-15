using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Frontend.Components.Common;
using Frontend.Components.Common.ConfirmationDialogComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.JSInterop;
using Frontend.Components.Files.FilePreviewComponent;
namespace Frontend.Components.Files.FileManagementBase
{
    public partial class FileManagementBase : ComponentBase
    {
    // Parameters that must be set by the consuming page
        [Parameter, EditorRequired] public FileType? TargetFileType { get; set; }
        [Parameter, EditorRequired] public string FileTypeName { get; set; } = string.Empty;
        [Parameter, EditorRequired] public string PageTitle { get; set; } = string.Empty;
        [Parameter, EditorRequired] public string PageDescription { get; set; } = string.Empty;

        // Optional parameters
        [Parameter] public Func<string, bool>? FileTypeValidator { get; set; }
        [Parameter] public int DefaultPageSize { get; set; } = 24;

        // ViewMode enum
        public enum ViewMode { Grid, List }

        private string DialogTitle => $"Edit {FileTypeName}";
        private string DialogDescription => $"Update {FileTypeName.ToLower()} information and metadata";

        private string UploadDialogTitle => $"Upload {FileTypeName}";
        private string UploadDialogDescription => $"Upload new {FileTypeName.ToLower()} files to your library";

        private string DeleteDialogTitle => $"Delete {FileTypeName}";
        private string DeleteDialogMessage => $"Are you sure you want to delete this {FileTypeName.ToLower()}? This action cannot be undone.";

        // State management classes
        public class LoadingState
        {
            public bool IsLoading { get; set; } = true;
            public bool IsSaving { get; set; } = false;
            public bool IsUploading { get; set; } = false;
            public bool IsDeleting { get; set; } = false;
        }

        public class SearchState
        {
            public string SearchTerm { get; set; } = string.Empty;
            public Timer? SearchTimer { get; set; }
        }

        public class PaginationState
        {
            public int CurrentPage { get; set; } = 1;
            public int PageSize { get; set; } = 24;
            public int TotalPages { get; set; } = 0;
        }

        public class DialogState
        {
            public bool ShowFileDialog { get; set; } = false;
            public bool ShowUploadDialog { get; set; } = false;
            public bool ShowDeleteDialog { get; set; } = false;
        }

        public class UploadState
        {
            public List<IBrowserFile> SelectedFiles { get; set; } = new();
            public Dictionary<string, int> UploadProgress { get; set; } = new();
        }

        // State instances
        private LoadingState loadingState = new();
        private SearchState searchState = new();
        private PaginationState paginationState = new();
        private DialogState dialogState = new();
        private UploadState uploadState = new();

        // Data
        private PaginatedResult<FileDto> files = new();
        private ViewMode currentViewMode = ViewMode.Grid;
        private FileDto? selectedFile = null;

        // Form models and validation
        private UpdateFileDto fileModel = new();
        private Dictionary<string, string> fileValidationErrors = new();

        // Component references
        private FormDialog? fileDialog;
        private GenericDialog? uploadDialog;
        private ConfirmationDialog? deleteFileDialog;
        private FileUpload? fileUpload;
        private FilePreview? filePreview;

        // Performance optimization
        private readonly SemaphoreSlim loadingSemaphore = new(1, 1);
        private bool _isParametersValid = false;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                // Validate required parameters
                ValidateRequiredParameters();

                paginationState.PageSize = DefaultPageSize;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to initialize page: {ex.Message}");
                throw; // Re-throw to prevent component from rendering in invalid state
            }
        }

        protected override async Task OnParametersSetAsync()
        {
            // Only validate if not already validated and parameters are set
            if (!_isParametersValid && IsParametersReady())
            {
                try
                {
                    ValidateRequiredParameters();
                    _isParametersValid = true;
                }
                catch (Exception ex)
                {
                    await JSRuntime.InvokeVoidAsync("console.error", $"Parameter validation failed: {ex.Message}");
                    throw;
                }
            }
        }

        private bool IsParametersReady()
        {
            return TargetFileType.HasValue &&
                   !string.IsNullOrEmpty(FileTypeName) &&
                   !string.IsNullOrEmpty(PageTitle) &&
                   !string.IsNullOrEmpty(PageDescription);
        }

        private void ValidateRequiredParameters()
        {
            if (!TargetFileType.HasValue)
            {
                throw new InvalidOperationException("TargetFileType parameter is required");
            }

            if (string.IsNullOrEmpty(FileTypeName))
            {
                throw new InvalidOperationException("FileTypeName parameter is required");
            }

            if (string.IsNullOrEmpty(PageTitle))
            {
                throw new InvalidOperationException("PageTitle parameter is required");
            }

            if (string.IsNullOrEmpty(PageDescription))
            {
                throw new InvalidOperationException("PageDescription parameter is required");
            }
        }

        private async Task LoadDataAsync()
        {
            if (!await loadingSemaphore.WaitAsync(100))
            {
                return; // Skip if already loading
            }

            try
            {
                loadingState.IsLoading = true;
                StateHasChanged();

                var searchTerm = string.IsNullOrEmpty(searchState.SearchTerm) ? null : searchState.SearchTerm;

                files = await FileService.GetFilesAsync(
                    paginationState.CurrentPage,
                    paginationState.PageSize,
                    null, // folderId - not handling folder navigation in this base component
                    searchTerm,
                    TargetFileType
                );

                // Update pagination state
                paginationState.TotalPages = files.TotalPages;

                // Validate pagination integrity
                if (paginationState.CurrentPage > paginationState.TotalPages && paginationState.TotalPages > 0)
                {
                    paginationState.CurrentPage = 1;
                    await LoadDataAsync(); // Reload with corrected page
                    return;
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load {FileTypeName.ToLower()}: {ex.Message}");
                files = new PaginatedResult<FileDto>();
            }
            finally
            {
                loadingState.IsLoading = false;
                loadingSemaphore.Release();
                StateHasChanged();
            }
        }

        private async Task ForceRefreshData()
        {
            try
            {
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to refresh data: {ex.Message}");
            }
        }

        private void OnSearchKeyUp(KeyboardEventArgs e)
        {
            searchState.SearchTimer?.Dispose();
            searchState.SearchTimer = new Timer(async _ =>
            {
                paginationState.CurrentPage = 1;
                await InvokeAsync(LoadDataAsync);
            }, null, 300, Timeout.Infinite);
        }

        private void ClearSearch()
        {
            searchState.SearchTerm = string.Empty;
            paginationState.CurrentPage = 1;
            _ = LoadDataAsync();
        }

        private async Task OnPageChanged(int page)
        {
            if (page == paginationState.CurrentPage || loadingState.IsLoading)
                return;

            paginationState.CurrentPage = page;
            await LoadDataAsync();
        }

        private async Task RefreshData()
        {
            await LoadDataAsync();
        }

        private void SetViewMode(ViewMode mode)
        {
            currentViewMode = mode;
            StateHasChanged();
        }

        // File Management Operations
        private async Task ShowEditFileDialog(FileDto file)
        {
            try
            {
                // Close any open dialogs first
                CloseAllDialogs();

                selectedFile = file;
                fileModel = new UpdateFileDto
                {
                    Description = file.Description,
                    Alt = file.Alt,
                    IsPublic = file.IsPublic,
                    FolderId = file.FolderId,
                    Tags = file.Tags ?? new Dictionary<string, object>()
                };
                fileValidationErrors.Clear();
                dialogState.ShowFileDialog = true;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to open edit dialog: {ex.Message}");
            }
        }

        private void CloseFileDialog()
        {
            dialogState.ShowFileDialog = false;
            fileModel = new UpdateFileDto();
            selectedFile = null;
            fileValidationErrors.Clear();
            StateHasChanged();
        }

        private async Task SaveFile()
        {
            if (loadingState.IsSaving || selectedFile == null) return;

            try
            {
                loadingState.IsSaving = true;
                StateHasChanged();

                await FileService.UpdateFileAsync(selectedFile.Id, fileModel);
                NotificationService.ShowSuccess($"{FileTypeName} updated successfully");

                CloseFileDialog();
                await ForceRefreshData();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to save {FileTypeName.ToLower()}: {ex.Message}");
            }
            finally
            {
                loadingState.IsSaving = false;
                StateHasChanged();
            }
        }

        private void ShowDeleteFileConfirmation(FileDto file)
        {
            try
            {
                // Close any open dialogs first
                CloseAllDialogs();

                // Set the selected file
                selectedFile = file;

                // Reset deletion state
                loadingState.IsDeleting = false;

                // Show the confirmation dialog
                dialogState.ShowDeleteDialog = true;



                StateHasChanged();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to show delete confirmation: {ex.Message}");
            }
        }

        private async Task DeleteFile()
        {
            if (selectedFile == null || loadingState.IsDeleting) return;

            try
            {
                loadingState.IsDeleting = true;
                StateHasChanged();

                var success = await FileService.DeleteFileAsync(selectedFile.Id);

                if (success)
                {
                    NotificationService.ShowSuccess($"{FileTypeName} deleted successfully");

                    // Close dialog and clear state immediately
                    CancelDelete();

                    // Refresh data
                    await ForceRefreshData();
                }
                else
                {
                    NotificationService.ShowError($"Failed to delete {FileTypeName.ToLower()}");
                    loadingState.IsDeleting = false;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to delete {FileTypeName.ToLower()}: {ex.Message}");
                loadingState.IsDeleting = false;
                StateHasChanged();
            }
        }

        private void CancelDelete()
        {
            try
            {
                dialogState.ShowDeleteDialog = false;
                selectedFile = null;
                loadingState.IsDeleting = false;

                // Force the dialog to close
                deleteFileDialog?.Hide();

                StateHasChanged();
            }
            catch (Exception ex)
            {
                // Log but don't show error to user for cancel operation
                Console.WriteLine($"Error canceling delete: {ex.Message}");
            }
        }

        private async Task PreviewFile(FileDto file)
        {
            try
            {
                filePreview?.ShowPreview(file);
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
                await FileService.DownloadFileAsync(file.Id);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to download {FileTypeName.ToLower()}: {ex.Message}");
            }
        }

        // Upload Management
        private void ShowUploadDialog()
        {
            try
            {
                // Close any open dialogs first
                CloseAllDialogs();

                uploadState.SelectedFiles.Clear();
                uploadState.UploadProgress.Clear();
                dialogState.ShowUploadDialog = true;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to show upload dialog: {ex.Message}");
            }
        }

        private void CloseUploadDialog()
        {
            dialogState.ShowUploadDialog = false;
            uploadState.SelectedFiles.Clear();
            uploadState.UploadProgress.Clear();
            fileUpload?.ClearFiles();
            StateHasChanged();
        }

        private async Task UploadFiles()
        {
            if (!uploadState.SelectedFiles.Any() || loadingState.IsUploading) return;

            var validFiles = uploadState.SelectedFiles;
            if (FileTypeValidator != null)
            {
                validFiles = uploadState.SelectedFiles.Where(f => FileTypeValidator(f.ContentType)).ToList();
            }
            else
            {
                validFiles = uploadState.SelectedFiles.Where(f => IsValidFileType(f.ContentType)).ToList();
            }

            if (!validFiles.Any())
            {
                NotificationService.ShowWarning($"Please select at least one valid {FileTypeName.ToLower()} file");
                return;
            }

            try
            {
                loadingState.IsUploading = true;
                StateHasChanged();

                var uploadTasks = validFiles.Select(async file =>
                {
                    try
                    {
                        using var memoryStream = new MemoryStream();
                        var maxSize = StyleService.GetMaxFileSize(TargetFileType.Value);
                        await file.OpenReadStream(maxAllowedSize: maxSize).CopyToAsync(memoryStream);
                        memoryStream.Position = 0;

                        var formFile = new FormFile(memoryStream, 0, memoryStream.Length, file.Name, file.Name)
                        {
                            Headers = new HeaderDictionary(),
                            ContentType = file.ContentType
                        };

                        var uploadDto = new FileUploadDto
                        {
                            File = formFile,
                            FolderId = null, // Not handling folder upload in base component
                            IsPublic = false,
                            GenerateThumbnail = TargetFileType == FileType.Image
                        };

                        var result = await FileService.UploadFileAsync(uploadDto);
                        return result?.File;
                    }
                    catch (Exception ex)
                    {
                        NotificationService.ShowError($"Failed to upload {file.Name}: {ex.Message}");
                        return null;
                    }
                });

                var results = await Task.WhenAll(uploadTasks);
                var successCount = results.Count(r => r != null);

                if (successCount > 0)
                {
                    NotificationService.ShowSuccess($"Successfully uploaded {successCount} {FileTypeName.ToLower()}(s)");
                    CloseUploadDialog();
                    await ForceRefreshData();
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to upload {FileTypeName.ToLower()}: {ex.Message}");
            }
            finally
            {
                loadingState.IsUploading = false;
                StateHasChanged();
            }
        }

        private Task OnFilesChanged(List<IBrowserFile> files)
        {
            uploadState.SelectedFiles = files;
            StateHasChanged();
            return Task.CompletedTask;
        }

        private Task OnUploadProgressChanged(Dictionary<string, int> progress)
        {
            uploadState.UploadProgress = progress;
            StateHasChanged();
            return Task.CompletedTask;
        }

        // Helper methods for file type validation and styling
        private bool IsValidFileType(string contentType)
        {
            return TargetFileType switch
            {
                FileType.Image => contentType.StartsWith("image/"),
                FileType.Video => contentType.StartsWith("video/"),
                FileType.Audio => contentType.StartsWith("audio/"),
                FileType.Document => IsDocumentType(contentType),
                _ => true
            };
        }

        private bool IsDocumentType(string contentType)
        {
            return contentType.Contains("pdf") ||
                   contentType.Contains("word") ||
                   contentType.Contains("excel") ||
                   contentType.Contains("powerpoint") ||
                   contentType.Contains("text") ||
                   contentType.Contains("document");
        }

        private void CloseAllDialogs()
        {
            dialogState.ShowFileDialog = false;
            dialogState.ShowUploadDialog = false;
            dialogState.ShowDeleteDialog = false;
            selectedFile = null;
            loadingState.IsDeleting = false;
            loadingState.IsSaving = false;
            loadingState.IsUploading = false;
        }

        public void Dispose()
        {
            searchState.SearchTimer?.Dispose();
            loadingSemaphore?.Dispose();
        }
    }
}

