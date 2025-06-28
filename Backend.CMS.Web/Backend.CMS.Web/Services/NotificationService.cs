namespace Backend.CMS.Web.Services
{
    public enum NotificationType
    {
        Success,
        Error,
        Warning,
        Info
    }

    public class NotificationMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int DurationMs { get; set; } = 5000;
        public bool AutoClose { get; set; } = true;
        public string? ActionText { get; set; }
        public Func<Task>? ActionCallback { get; set; }
    }

    public interface INotificationService
    {
        event Action<NotificationMessage>? NotificationAdded;
        event Action<string>? NotificationRemoved;

        Task ShowSuccessAsync(string message, string? title = null, int? durationMs = null);
        Task ShowErrorAsync(string message, string? title = null, int? durationMs = null);
        Task ShowWarningAsync(string message, string? title = null, int? durationMs = null);
        Task ShowInfoAsync(string message, string? title = null, int? durationMs = null);

        Task ShowNotificationAsync(NotificationMessage notification);
        Task RemoveNotificationAsync(string id);
        Task ClearAllNotificationsAsync();

        List<NotificationMessage> GetActiveNotifications();
    }

    public class NotificationService : INotificationService
    {
        private readonly List<NotificationMessage> _notifications = new();
        private readonly ILogger<NotificationService> _logger;
        private readonly Timer _cleanupTimer;

        public event Action<NotificationMessage>? NotificationAdded;
        public event Action<string>? NotificationRemoved;

        public NotificationService(ILogger<NotificationService> logger)
        {
            _logger = logger;

            // Clean up expired notifications every 30 seconds
            _cleanupTimer = new Timer(CleanupExpiredNotifications, null,
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public async Task ShowSuccessAsync(string message, string? title = null, int? durationMs = null)
        {
            var notification = new NotificationMessage
            {
                Title = title ?? "Success",
                Message = message,
                Type = NotificationType.Success,
                DurationMs = durationMs ?? 5000
            };

            await ShowNotificationAsync(notification);
        }

        public async Task ShowErrorAsync(string message, string? title = null, int? durationMs = null)
        {
            var notification = new NotificationMessage
            {
                Title = title ?? "Error",
                Message = message,
                Type = NotificationType.Error,
                DurationMs = durationMs ?? 8000,
                AutoClose = durationMs.HasValue // Don't auto-close errors unless specified
            };

            await ShowNotificationAsync(notification);
        }

        public async Task ShowWarningAsync(string message, string? title = null, int? durationMs = null)
        {
            var notification = new NotificationMessage
            {
                Title = title ?? "Warning",
                Message = message,
                Type = NotificationType.Warning,
                DurationMs = durationMs ?? 7000
            };

            await ShowNotificationAsync(notification);
        }

        public async Task ShowInfoAsync(string message, string? title = null, int? durationMs = null)
        {
            var notification = new NotificationMessage
            {
                Title = title ?? "Information",
                Message = message,
                Type = NotificationType.Info,
                DurationMs = durationMs ?? 5000
            };

            await ShowNotificationAsync(notification);
        }

        public async Task ShowNotificationAsync(NotificationMessage notification)
        {
            try
            {
                _notifications.Add(notification);
                NotificationAdded?.Invoke(notification);

                _logger.LogDebug("Notification added: {Type} - {Message}",
                    notification.Type, notification.Message);

                // Auto-remove notification after duration if enabled
                if (notification.AutoClose && notification.DurationMs > 0)
                {
                    _ = Task.Delay(notification.DurationMs).ContinueWith(async _ =>
                    {
                        await RemoveNotificationAsync(notification.Id);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing notification");
            }

            await Task.CompletedTask;
        }

        public async Task RemoveNotificationAsync(string id)
        {
            try
            {
                var notification = _notifications.FirstOrDefault(n => n.Id == id);
                if (notification != null)
                {
                    _notifications.Remove(notification);
                    NotificationRemoved?.Invoke(id);

                    _logger.LogDebug("Notification removed: {Id}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing notification: {Id}", id);
            }

            await Task.CompletedTask;
        }

        public async Task ClearAllNotificationsAsync()
        {
            try
            {
                var notificationIds = _notifications.Select(n => n.Id).ToList();
                _notifications.Clear();

                foreach (var id in notificationIds)
                {
                    NotificationRemoved?.Invoke(id);
                }

                _logger.LogDebug("All notifications cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all notifications");
            }

            await Task.CompletedTask;
        }

        public List<NotificationMessage> GetActiveNotifications()
        {
            return _notifications.ToList();
        }

        private void CleanupExpiredNotifications(object? state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-5); // Remove notifications older than 5 minutes
                var expiredNotifications = _notifications
                    .Where(n => n.CreatedAt < cutoffTime)
                    .ToList();

                foreach (var notification in expiredNotifications)
                {
                    _notifications.Remove(notification);
                    NotificationRemoved?.Invoke(notification.Id);
                }

                if (expiredNotifications.Any())
                {
                    _logger.LogDebug("Cleaned up {Count} expired notifications", expiredNotifications.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during notification cleanup");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }
}