using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Frontend.Components.Common.ConfirmationDialogComponent
{
    public partial class ConfirmationDialog : ComponentBase
    {
        [Parameter] public string Title { get; set; } = "Confirm Action";
        [Parameter] public string Message { get; set; } = "Are you sure you want to proceed?";
        [Parameter] public string ConfirmText { get; set; } = "Confirm";
        [Parameter] public string CancelText { get; set; } = "Cancel";
        [Parameter] public string ProcessingText { get; set; } = "Processing...";
        [Parameter] public string? ConfirmIcon { get; set; }
        [Parameter] public string ConfirmClass { get; set; } = "";
        [Parameter] public string Type { get; set; } = "warning"; // warning, danger, info, success
        [Parameter] public bool CloseOnBackdrop { get; set; } = true;
        [Parameter] public bool IsVisible { get; set; }
        [Parameter] public bool IsProcessing { get; set; }
        [Parameter] public EventCallback OnConfirm { get; set; }
        [Parameter] public EventCallback OnCancel { get; set; }

        // State management
        private bool _isDisposing = false;
        private DotNetObjectReference<ConfirmationDialog>? _objRef;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _objRef = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("addEscapeKeyListener", _objRef);
            }
        }

        private async Task HandleConfirm()
        {
            if (IsProcessing) return;

            try
            {
                IsProcessing = true;
                StateHasChanged();

                await OnConfirm.InvokeAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in confirmation action: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsVisible = false;
                StateHasChanged();
            }
        }

        private async Task HandleCancel()
        {
            if (IsProcessing) return;

            IsVisible = false;
            StateHasChanged();

            if (OnCancel.HasDelegate)
            {
                await OnCancel.InvokeAsync();
            }
        }

        private async Task HandleBackdropClick()
        {
            if (CloseOnBackdrop && !IsProcessing)
            {
                await HandleCancel();
            }
        }

        [JSInvokable]
        public async Task HandleEscapeKey()
        {
            if (IsVisible && !IsProcessing)
            {
                await HandleCancel();
            }
        }

        private string GetIconClass()
        {
            return Type.ToLower() switch
            {
                "danger" => "fas fa-exclamation-triangle",
                "success" => "fas fa-check",
                "info" => "fas fa-info",
                _ => "fas fa-exclamation-triangle"
            };
        }

        private string GetIconBackgroundClass()
        {
            return Type.ToLower() switch
            {
                "danger" => "bg-red-500",
                "success" => "bg-green-500",
                "info" => "bg-blue-500",
                _ => "bg-yellow-500"
            };
        }

        private string GetConfirmButtonClass()
        {
            if (!string.IsNullOrEmpty(ConfirmClass))
            {
                return ConfirmClass;
            }

            return Type.ToLower() switch
            {
                "danger" => StyleService.GetButtonClass("danger", "medium"),
                "success" => StyleService.GetButtonClass("success", "medium"),
                "info" => StyleService.GetButtonClass("primary", "medium"),
                _ => StyleService.GetButtonClass("warning", "medium")
            };
        }

        public void Show()
        {
            IsVisible = true;
            IsProcessing = false;
            StateHasChanged();
        }

        public void Hide()
        {
            IsVisible = false;
            IsProcessing = false;
            StateHasChanged();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_isDisposing)
            {
                _isDisposing = true;

                try
                {
                    await JSRuntime.InvokeVoidAsync("removeEscapeKeyListener");
                    _objRef?.Dispose();
                }
                catch (JSDisconnectedException)
                {
                    // Browser has disconnected, ignore
                }
                catch (TaskCanceledException)
                {
                    // Operation was cancelled, ignore
                }
            }
        }
    }
}
