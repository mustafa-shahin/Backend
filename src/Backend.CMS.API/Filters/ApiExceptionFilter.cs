using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Backend.CMS.API.Middleware;
using FluentValidation;

namespace Backend.CMS.API.Filters
{
    public class ApiExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<ApiExceptionFilter> _logger;
        private readonly IWebHostEnvironment _environment;

        public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public void OnException(ExceptionContext context)
        {
            _logger.LogError(context.Exception, "Exception caught by ApiExceptionFilter");

            var errorResponse = new ApiErrorResponse
            {
                TraceId = context.HttpContext.TraceIdentifier,
                Timestamp = DateTime.UtcNow,
                Path = context.HttpContext.Request.Path,
                Method = context.HttpContext.Request.Method
            };

            var result = context.Exception switch
            {
                ValidationException validationEx => HandleValidationException(validationEx, errorResponse),
                ArgumentException argEx => HandleArgumentException(argEx, errorResponse),
                UnauthorizedAccessException => HandleUnauthorizedException(errorResponse),
                KeyNotFoundException => HandleNotFoundException(errorResponse),
                _ => HandleGenericException(context.Exception, errorResponse)
            };

            context.Result = result;
            context.ExceptionHandled = true;
        }

        private IActionResult HandleValidationException(ValidationException ex, ApiErrorResponse errorResponse)
        {
            errorResponse.Message = "Validation failed";
            errorResponse.StatusCode = 400;
            errorResponse.Details = "One or more validation errors occurred";
            errorResponse.ValidationErrors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return new BadRequestObjectResult(errorResponse);
        }

        private IActionResult HandleArgumentException(ArgumentException ex, ApiErrorResponse errorResponse)
        {
            errorResponse.Message = "Invalid argument";
            errorResponse.StatusCode = 400;
            errorResponse.Details = ex.Message;

            return new BadRequestObjectResult(errorResponse);
        }

        private IActionResult HandleUnauthorizedException(ApiErrorResponse errorResponse)
        {
            errorResponse.Message = "Access denied";
            errorResponse.StatusCode = 401;
            errorResponse.Details = "You don't have permission to access this resource";

            return new UnauthorizedObjectResult(errorResponse);
        }

        private IActionResult HandleNotFoundException(ApiErrorResponse errorResponse)
        {
            errorResponse.Message = "Resource not found";
            errorResponse.StatusCode = 404;
            errorResponse.Details = "The requested resource was not found";

            return new NotFoundObjectResult(errorResponse);
        }

        private IActionResult HandleGenericException(Exception ex, ApiErrorResponse errorResponse)
        {
            errorResponse.Message = "An internal server error occurred";
            errorResponse.StatusCode = 500;
            errorResponse.Details = _environment.IsDevelopment()
                ? ex.Message
                : "Please try again later or contact support if the problem persists";

            if (_environment.IsDevelopment())
            {
                errorResponse.StackTrace = ex.StackTrace;
                errorResponse.InnerException = ex.InnerException?.Message;
            }

            return new ObjectResult(errorResponse)
            {
                StatusCode = 500
            };
        }
    }
}