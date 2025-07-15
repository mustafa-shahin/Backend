using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Frontend.Components.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;

namespace Frontend.Components.Files.FileBrowser
{
    public partial class FileBrowser : ComponentBase
    {
        [Parameter] public FileType[]? FileTypes { get; set; }
        [Parameter] public bool AllowMultiSelect { get; set; } = true;
        [Parameter] public bool ShowUpload { get; set; } = false;
        [Parameter] public EventCallback<List<FileDto>> OnFilesSelected { get; set; }

        private List<FileDto> files = new();
        private List<FileDto> selectedFiles = new();
        private bool isLoading = true;
        private string searchTerm = string.Empty;
        private Timer? searchTimer;

        private int currentPage = 1;
        private int pageSize = 24;
        private int totalCount = 0;
        private int totalPages = 0;

        // Upload functionality
        private bool showUploadDialog = false;
        private bool isUploading = false;
        private List<IBrowserFile> selectedUploadFiles = new();
        private Dictionary<string, int> uploadProgress = new();
        private FormDialog? uploadDialog;

        protected override async Task OnInitializedAsync()
        {
            await LoadFiles();
        }

        private async Task LoadFiles()
        {
            try
            {
                isLoading = true;
                StateHasChanged();

                FileType? fileTypeFilter = null;
                if (FileTypes?.Length == 1)
                {
                    fileTypeFilter = FileTypes[0];
                }

                var result = await FileService.GetFilesAsync(
                    pageNumber: currentPage,
                    pageSize: pageSize,
                    search: string.IsNullOrEmpty(searchTerm) ? null : searchTerm,
                    fileType: fileTypeFilter
                );

                files = result.Data?.ToList() ?? new List<FileDto>();
                totalCount = result.TotalCount;
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to load files: {ex.Message}");
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private void OnSearchKeyUp(KeyboardEventArgs e)
        {
            searchTimer?.Dispose();
            searchTimer = new Timer(async _ =>
            {
                currentPage = 1;
                await InvokeAsync(LoadFiles);
            }, null, 500, Timeout.Infinite);
        }

        private async Task GoToPage(int page)
        {
            if (page >= 1 && page <= totalPages)
            {
                currentPage = page;
                await LoadFiles();
            }
        }

        private async Task ToggleFileSelection(FileDto file)
        {
            // Filter by file type if specified
            if (FileTypes != null && FileTypes.Length > 0 && !FileTypes.Contains(file.FileType))
            {
                return;
            }

            if (selectedFiles.Contains(file))
            {
                selectedFiles.Remove(file);
            }
            else
            {
                if (!AllowMultiSelect)
                {
                    selectedFiles.Clear();
                }
                selectedFiles.Add(file);
            }

            await OnFilesSelected.InvokeAsync(selectedFiles.ToList());
            StateHasChanged();
        }

        private async Task ClearSelection()
        {
            selectedFiles.Clear();
            await OnFilesSelected.InvokeAsync(selectedFiles.ToList());
            StateHasChanged();
        }

        private void ShowUploadDialog()
        {
            showUploadDialog = true;
            selectedUploadFiles.Clear();
            uploadProgress.Clear();
            StateHasChanged();
        }

        private string GetFileCardClass(bool isSelected)
        {
            var baseClass = "p-3 rounded-lg border-2 transition-all duration-200";
            if (isSelected)
            {
                return $"{baseClass} border-blue-500 bg-blue-50 dark:bg-blue-900/20";
            }
            return $"{baseClass} border-gray-200 dark:border-gray-700 hover:border-blue-300 dark:hover:border-blue-600 bg-white dark:bg-gray-800";
        }

        private string GetFilePreviewUrl(FileDto file)
        {
            return file.Urls?.Thumbnail ?? file.Urls?.Download ?? FileService.GetThumbnailUrl(file.Id);
        }

        private void CloseUploadDialog()
        {
            showUploadDialog = false;
            selectedUploadFiles.Clear();
            uploadProgress.Clear();
            StateHasChanged();
        }

        private void OnUploadFilesChanged(List<IBrowserFile> files)
        {
            selectedUploadFiles = files;
            StateHasChanged();
        }

        private void OnUploadProgressChanged(Dictionary<string, int> progress)
        {
            uploadProgress = progress;
            StateHasChanged();
        }

        private async Task UploadFiles()
        {
            if (!selectedUploadFiles.Any()) return;

            try
            {
                isUploading = true;
                StateHasChanged();

                var uploadTasks = new List<Task>();

                foreach (var file in selectedUploadFiles)
                {
                    uploadTasks.Add(UploadSingleFile(file));
                }

                await Task.WhenAll(uploadTasks);

                NotificationService.ShowSuccess($"Successfully uploaded {selectedUploadFiles.Count} file(s)");

                // Refresh the files list
                await LoadFiles();

                CloseUploadDialog();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Upload failed: {ex.Message}");
            }
            finally
            {
                isUploading = false;
                StateHasChanged();
            }
        }

        private async Task UploadSingleFile(IBrowserFile file)
        {
            try
            {
                // Initialize progress
                uploadProgress[file.Name] = 0;
                await InvokeAsync(() =>
                {
                    OnUploadProgressChanged(uploadProgress);
                    StateHasChanged();
                });

                // Create a FormFile from IBrowserFile for the upload
                var formFile = new FormFileWrapper(file);

                var uploadDto = new FileUploadDto
                {
                    File = formFile,
                    IsPublic = true,
                    GenerateThumbnail = true,
                    ProcessImmediately = true
                };

                // Simulate progress updates during upload
                var progressTask = Task.Run(async () =>
                {
                    for (int i = 10; i <= 90; i += 10)
                    {
                        await Task.Delay(100);
                        uploadProgress[file.Name] = i;
                        await InvokeAsync(() =>
                        {
                            OnUploadProgressChanged(uploadProgress);
                            StateHasChanged();
                        });
                    }
                });

                var result = await FileService.UploadFileAsync(uploadDto);

                // Complete the progress simulation
                await progressTask;

                if (result?.Success == true && result.File != null)
                {
                    // Update progress to 100%
                    uploadProgress[file.Name] = 100;
                    await InvokeAsync(() =>
                    {
                        OnUploadProgressChanged(uploadProgress);
                        StateHasChanged();
                    });
                }
                else
                {
                    throw new Exception(result?.ErrorMessage ?? "Upload failed");
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to upload {file.Name}: {ex.Message}");
                // Remove from progress on error
                uploadProgress.Remove(file.Name);
                await InvokeAsync(() =>
                {
                    OnUploadProgressChanged(uploadProgress);
                    StateHasChanged();
                });
            }
        }

        public void Dispose()
        {
            searchTimer?.Dispose();
        }

        // FormFile wrapper to convert IBrowserFile to IFormFile
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
                => _browserFile.OpenReadStream(maxAllowedSize: 1024 * 1024 * 100, cancellationToken: cancellationToken).CopyToAsync(target, cancellationToken);
            public Stream OpenReadStream() => _browserFile.OpenReadStream(maxAllowedSize: 1024 * 1024 * 100); // 100MB limit
        }
    }
}
