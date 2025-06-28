using Microsoft.AspNetCore.Components;

namespace Backend.CMS.Web.Services
{
    public enum DialogSize
    {
        Small,
        Medium,
        Large,
        ExtraLarge,
        FullScreen
    }

    public class DialogOptions
    {
        public string Title { get; set; } = string.Empty;
        public DialogSize Size { get; set; } = DialogSize.Medium;
        public bool CloseOnBackdropClick { get; set; } = true;
        public bool ShowCloseButton { get; set; } = true;
        public bool ShowHeader { get; set; } = true;
        public bool ShowFooter { get; set; } = true;
        public string? CssClass { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
    }

    public class DialogReference
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Type ComponentType { get; set; } = default!;
        public DialogOptions Options { get; set; } = new();
        public TaskCompletionSource<object?> Result { get; set; } = new();
    }

    public interface IDialogService
    {
        event Action? DialogsChanged;

        Task<T?> ShowAsync<T>(Type componentType, string title, DialogOptions? options = null);
        Task<T?> ShowAsync<T>(Type componentType, DialogOptions options);
        Task ShowAsync(Type componentType, string title, DialogOptions? options = null);
        Task ShowAsync(Type componentType, DialogOptions options);

        Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel");
        Task ShowAlertAsync(string title, string message, string okText = "OK");

        Task CloseAsync(string dialogId, object? result = null);
        Task CloseAllAsync();

        List<DialogReference> GetActiveDialogs();
    }

    public class DialogService : IDialogService
    {
        private readonly List<DialogReference> _dialogs = new();
        private readonly ILogger<DialogService> _logger;

        public event Action? DialogsChanged;

        public DialogService(ILogger<DialogService> logger)
        {
            _logger = logger;
        }

        public async Task<T?> ShowAsync<T>(Type componentType, string title, DialogOptions? options = null)
        {
            options ??= new DialogOptions();
            options.Title = title;

            return await ShowAsync<T>(componentType, options);
        }

        public async Task<T?> ShowAsync<T>(Type componentType, DialogOptions options)
        {
            var dialogRef = new DialogReference
            {
                ComponentType = componentType,
                Options = options
            };

            _dialogs.Add(dialogRef);
            DialogsChanged?.Invoke();

            _logger.LogDebug("Dialog opened: {Title} ({Id})", options.Title, dialogRef.Id);

            var result = await dialogRef.Result.Task;
            return result is T typedResult ? typedResult : default;
        }

        public async Task ShowAsync(Type componentType, string title, DialogOptions? options = null)
        {
            await ShowAsync<object>(componentType, title, options);
        }

        public async Task ShowAsync(Type componentType, DialogOptions options)
        {
            await ShowAsync<object>(componentType, options);
        }

        public async Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel")
        {
            var options = new DialogOptions
            {
                Title = title,
                Size = DialogSize.Small,
                Parameters = new Dictionary<string, object>
                {
                    ["Message"] = message,
                    ["ConfirmText"] = confirmText,
                    ["CancelText"] = cancelText
                }
            };

            var result = await ShowAsync<bool>(typeof(Components.Shared.ConfirmDialog), options);
            return result;
        }

        public async Task ShowAlertAsync(string title, string message, string okText = "OK")
        {
            var options = new DialogOptions
            {
                Title = title,
                Size = DialogSize.Small,
                Parameters = new Dictionary<string, object>
                {
                    ["Message"] = message,
                    ["OkText"] = okText
                }
            };

            await ShowAsync(typeof(Components.Shared.AlertDialog), options);
        }

        public async Task CloseAsync(string dialogId, object? result = null)
        {
            try
            {
                var dialog = _dialogs.FirstOrDefault(d => d.Id == dialogId);
                if (dialog != null)
                {
                    _dialogs.Remove(dialog);
                    dialog.Result.SetResult(result);
                    DialogsChanged?.Invoke();

                    _logger.LogDebug("Dialog closed: {Id}", dialogId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing dialog: {DialogId}", dialogId);
            }

            await Task.CompletedTask;
        }

        public async Task CloseAllAsync()
        {
            try
            {
                var dialogIds = _dialogs.Select(d => d.Id).ToList();

                foreach (var dialog in _dialogs.ToList())
                {
                    dialog.Result.SetResult(null);
                }

                _dialogs.Clear();
                DialogsChanged?.Invoke();

                _logger.LogDebug("All dialogs closed ({Count})", dialogIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing all dialogs");
            }

            await Task.CompletedTask;
        }

        public List<DialogReference> GetActiveDialogs()
        {
            return _dialogs.ToList();
        }
    }
}