using Frontend.Enums;
using Frontend.Helpers;
using Frontend.Interfaces;

namespace Frontend.Services
{
    public class NotificationService : INotificationService
    {
        public event Action<NotificationMessage>? OnNotificationAdded;
        public event Action<string>? OnNotificationRemoved;

        public void ShowSuccess(string message, string title = "Success", int durationMs = 5000)
        {
            var notification = new NotificationMessage
            {
                Title = title,
                Message = message,
                Type = NotificationType.Success,
                DurationMs = durationMs
            };

            OnNotificationAdded?.Invoke(notification);
        }

        public void ShowError(string message, string title = "Error", int durationMs = 8000)
        {
            var notification = new NotificationMessage
            {
                Title = title,
                Message = message,
                Type = NotificationType.Error,
                DurationMs = durationMs
            };

            OnNotificationAdded?.Invoke(notification);
        }

        public void ShowWarning(string message, string title = "Warning", int durationMs = 6000)
        {
            var notification = new NotificationMessage
            {
                Title = title,
                Message = message,
                Type = NotificationType.Warning,
                DurationMs = durationMs
            };

            OnNotificationAdded?.Invoke(notification);
        }

        public void ShowInfo(string message, string title = "Info", int durationMs = 5000)
        {
            var notification = new NotificationMessage
            {
                Title = title,
                Message = message,
                Type = NotificationType.Info,
                DurationMs = durationMs
            };

            OnNotificationAdded?.Invoke(notification);
        }

        public void Remove(string id)
        {
            OnNotificationRemoved?.Invoke(id);
        }

        public void Clear()
        {
            OnNotificationRemoved?.Invoke("*");
        }
    }
}
