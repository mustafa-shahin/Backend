using MediatR;
using Microsoft.Extensions.Logging;
using Backend.CMS.Application.Common.Exceptions;
using FluentValidation;

namespace Backend.CMS.Application.Common.Behaviors
{
    public class ErrorHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<ErrorHandlingBehavior<TRequest, TResponse>> _logger;

        public ErrorHandlingBehavior(ILogger<ErrorHandlingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                return await next();
            }
            catch (FluentValidation.ValidationException ex)
            {
                _logger.LogWarning("Validation failed for {RequestType}: {Errors}",
                    typeof(TRequest).Name,
                    string.Join(", ", ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

                var errors = ex.Errors.Select(e => new ValidationError
                {
                    PropertyName = e.PropertyName,
                    ErrorMessage = e.ErrorMessage,
                    AttemptedValue = e.AttemptedValue,
                    ErrorCode = e.ErrorCode
                }).ToList();

                throw new Backend.CMS.Application.Common.Exceptions.ValidationException(errors);
            }
            catch (DomainException ex)
            {
                _logger.LogWarning("Domain exception in {RequestType}: {Message} (Code: {Code})",
                    typeof(TRequest).Name, ex.Message, ex.Code);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in {RequestType}", typeof(TRequest).Name);
                throw;
            }
        }
    }
}
