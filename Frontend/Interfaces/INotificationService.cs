using Frontend.Helpers;

namespace Frontend.Interfaces
{
    public interface INotificationService
    {
        event Action<NotificationMessage>? OnNotificationAdded;
        event Action<string>? OnNotificationRemoved;

        void ShowSuccess(string message, string title = "Success", int durationMs = 5000);
        void ShowError(string message, string title = "Error", int durationMs = 8000);
        void ShowWarning(string message, string title = "Warning", int durationMs = 6000);
        void ShowInfo(string message, string title = "Info", int durationMs = 5000);
        void Remove(string id);
        void Clear();
    }
}
