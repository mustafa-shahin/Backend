using Backend.CMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    /// <summary>
    /// Service to manage scoped DbContext instances and prevent threading issues
    /// </summary>
    public interface IScopedDbContextService
    {
        Task<T> ExecuteWithNewContextAsync<T>(Func<ApplicationDbContext, Task<T>> operation);
        Task ExecuteWithNewContextAsync(Func<ApplicationDbContext, Task> operation);
        Task<T> ExecuteInTransactionAsync<T>(Func<ApplicationDbContext, Task<T>> operation);
        Task ExecuteInTransactionAsync(Func<ApplicationDbContext, Task> operation);
    }

    public class ScopedDbContextService : IScopedDbContextService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScopedDbContextService> _logger;

        public ScopedDbContextService(
            IServiceProvider serviceProvider,
            ILogger<ScopedDbContextService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<T> ExecuteWithNewContextAsync<T>(Func<ApplicationDbContext, Task<T>> operation)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                return await operation(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing operation with new context");
                throw;
            }
        }

        public async Task ExecuteWithNewContextAsync(Func<ApplicationDbContext, Task> operation)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                await operation(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing operation with new context");
                throw;
            }
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<ApplicationDbContext, Task<T>> operation)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var strategy = context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    var result = await operation(context);
                    await transaction.CommitAsync();
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task ExecuteInTransactionAsync(Func<ApplicationDbContext, Task> operation)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    await operation(context);
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
    }
}