using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Infrastructure.Logging
{
    public static class LoggerExtensions
    {
        public static void LogUserAction(this ILogger logger, string userId, string action, object? data = null)
        {
            logger.LogInformation("User {UserId} performed {Action} with data {@Data}",
                userId, action, data);
        }

        public static void LogSecurityEvent(this ILogger logger, string eventType, string details, string? userId = null)
        {
            logger.LogWarning("Security event {EventType}: {Details} for user {UserId}",
                eventType, details, userId);
        }
    }
}
