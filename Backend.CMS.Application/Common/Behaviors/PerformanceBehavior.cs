using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Backend.CMS.Application.Common.Behaviors
{
    public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
        private readonly Stopwatch _timer;

        public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
            _timer = new Stopwatch();
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _timer.Start();

            var response = await next();

            _timer.Stop();

            var elapsedMilliseconds = _timer.ElapsedMilliseconds;

            if (elapsedMilliseconds > 500) // Log slow requests
            {
                var requestName = typeof(TRequest).Name;
                _logger.LogWarning("Slow request detected: {RequestName} took {ElapsedMilliseconds}ms",
                    requestName, elapsedMilliseconds);
            }
            else
            {
                _logger.LogDebug("{RequestName} completed in {ElapsedMilliseconds}ms",
                    typeof(TRequest).Name, elapsedMilliseconds);
            }

            return response;
        }
    }
}