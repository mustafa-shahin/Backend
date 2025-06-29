using Frontend.Enums;

namespace Frontend.Helpers
{
    public class NotificationMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int DurationMs { get; set; } = 5000;
        public bool AutoClose { get; set; } = true;
    }
}
