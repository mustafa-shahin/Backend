using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Backend.CMS.Infrastructure.Data;

namespace Backend.CMS.BackgroundServices.Jobs
{
    public class DatabaseCleanupJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseCleanupJob> _logger;
        private readonly TimeSpan _period = TimeSpan.FromHours(24); // Run daily

        public DatabaseCleanupJob(IServiceProvider serviceProvider, ILogger<DatabaseCleanupJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupAsync();
                    await Task.Delay(_period, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during database cleanup");
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // Wait before retry
                }
            }
        }

        private async Task PerformCleanupAsync()
        {
            _logger.LogInformation("Starting database cleanup job");

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

            var cutoffDate = DateTime.UtcNow.AddDays(-90); // Keep 90 days of audit logs

            // Clean old audit logs
            var oldAuditLogs = await context.Set<Backend.CMS.Audit.Entities.AuditLog>()
                .Where(a => a.CreatedOn < cutoffDate)
                .ToListAsync();

            if (oldAuditLogs.Any())
            {
                context.Set<Backend.CMS.Audit.Entities.AuditLog>().RemoveRange(oldAuditLogs);
                await context.SaveChangesAsync();
                _logger.LogInformation("Removed {Count} old audit log entries", oldAuditLogs.Count);
            }

            // Clean old login attempts
            var oldLoginAttempts = await context.Set<Backend.CMS.Audit.Entities.LoginAttempt>()
                .Where(l => l.AttemptedAt < cutoffDate)
                .ToListAsync();

            if (oldLoginAttempts.Any())
            {
                context.Set<Backend.CMS.Audit.Entities.LoginAttempt>().RemoveRange(oldLoginAttempts);
                await context.SaveChangesAsync();
                _logger.LogInformation("Removed {Count} old login attempt records", oldLoginAttempts.Count);
            }

            // Clean soft-deleted pages older than 30 days
            var softDeleteCutoff = DateTime.UtcNow.AddDays(-30);
            var softDeletedPages = await context.Pages
                .Where(p => p.IsDeleted && p.DeletedOn < softDeleteCutoff)
                .ToListAsync();

            if (softDeletedPages.Any())
            {
                context.Pages.RemoveRange(softDeletedPages);
                await context.SaveChangesAsync();
                _logger.LogInformation("Permanently removed {Count} soft-deleted pages", softDeletedPages.Count);
            }

            _logger.LogInformation("Database cleanup job completed successfully");
        }
    }
}