using Backend.CMS.Infrastructure.Caching.Abstractions;
using Castle.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Infrastructure.Caching.Services
{
    public class CacheStatisticsService : BackgroundService
    {
        private readonly ICacheMonitoringService _monitoringService;
        private readonly ILogger<CacheStatisticsService> _logger;
        private readonly TimeSpan _logInterval;
        private readonly bool _enableLogging;

        public CacheStatisticsService(
            ICacheMonitoringService monitoringService,
            ILogger<CacheStatisticsService> logger,
            IConfiguration configuration)
        {
            _monitoringService = monitoringService;
            _logger = logger;
            _logInterval = TimeSpan.FromMinutes(configuration.GetValue("Cache:StatisticsLogIntervalMinutes", 30));
            _enableLogging = configuration.GetValue("Cache:EnableStatisticsLogging", false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enableLogging)
            {
                _logger.LogInformation("Cache statistics logging is disabled");
                return;
            }

            _logger.LogInformation("Cache statistics service started with interval: {Interval}", _logInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var statistics = await _monitoringService.GetStatisticsAsync();

                    _logger.LogInformation("Cache Statistics - Hits: {Hits}, Misses: {Misses}, Hit Ratio: {HitRatio:P2}, Errors: {Errors}",
                        statistics.HitCount, statistics.MissCount, statistics.HitRatio, statistics.ErrorCount);

                    if (statistics.OperationCounts.Any())
                    {
                        var operations = string.Join(", ", statistics.OperationCounts.Select(kv => $"{kv.Key}: {kv.Value}"));
                        _logger.LogDebug("Cache Operations - {Operations}", operations);
                    }

                    await Task.Delay(_logInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting cache statistics");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait before retry
                }
            }

            _logger.LogInformation("Cache statistics service stopped");
        }
    }
}
