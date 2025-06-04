using System.Text.Json;
using System.Net;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Backend.CMS.Audit.Services;
namespace Backend.CMS.API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAuditService auditService)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex, auditService);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception, IAuditService auditService)
        {
            var response = context.Response;
            response.ContentType = "application/json";

            var errorResponse = new ApiErrorResponse
            {
                TraceId = context.TraceIdentifier,
                Timestamp = DateTime.UtcNow,
                Path = context.Request.Path,
                Method = context.Request.Method
            };

            // Log security event for audit
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            switch (exception)
            {
                case ValidationException validationEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "Validation failed";
                    errorResponse.StatusCode = response.StatusCode;
                    errorResponse.Details = string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage));
                    errorResponse.ValidationErrors = validationEx.Errors.GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                    break;

                case UnauthorizedAccessException:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    errorResponse.Message = "Access denied";
                    errorResponse.StatusCode = response.StatusCode;
                    errorResponse.Details = "You don't have permission to access this resource";
                    
                    await auditService.LogSecurityEventAsync("unauthorized_access", 
                        $"Unauthorized access attempt to {context.Request.Path}", userId);
                    break;

                case ArgumentException argEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "Invalid argument";
                    errorResponse.StatusCode = response.StatusCode;
                    errorResponse.Details = argEx.Message;
                    break;

                case InvalidOperationException invalidOpEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "Invalid operation";
                    errorResponse.StatusCode = response.StatusCode;
                    errorResponse.Details = invalidOpEx.Message;
                    break;

                case DbUpdateException dbEx:
                    response.StatusCode = (int)HttpStatusCode.Conflict;
                    errorResponse.Message = "Database operation failed";
                    errorResponse.StatusCode = response.StatusCode;
                    errorResponse.Details = "A database error occurred while processing your request";
                    
                    // Log the actual database error for debugging
                    _logger.LogError(dbEx, "Database update exception: {InnerException}", dbEx.InnerException?.Message);
                    break;

                case TimeoutException:
                    response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    errorResponse.Message = "Request timeout";
                    errorResponse.StatusCode = response.StatusCode;
                    errorResponse.Details = "The request took too long to process";
                    break;

                case NotImplementedException:
                    response.StatusCode = (int)HttpStatusCode.NotImplemented;
                    errorResponse.Message = "Feature not implemented";
                    errorResponse.StatusCode = response.StatusCode;
                    errorResponse.Details = "This feature is not yet implemented";
                    break;

                case KeyNotFoundException:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    errorResponse.Message = "Resource not found";
                    errorResponse.StatusCode = response.StatusCode;
                    errorResponse.Details = "The requested resource was not found";
                    break;

                case OperationCanceledException:
                    response.StatusCode = 499; // Client closed request
                    errorResponse.Message = "Request cancelled";
                    errorResponse.StatusCode = response.StatusCode;
                    errorResponse.Details = "The request was cancelled";
                    break;

                case HttpRequestException httpEx:
                    response.StatusCode = (int)HttpStatusCode.BadGateway;
                    errorResponse.Message = "External service error";
                    errorResponse.StatusCode = response.StatusCode;
                    errorResponse.Details = "An error occurred while communicating with an external service";
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Message = "An internal server error occurred";
                    errorResponse.StatusCode = response.StatusCode;
                    errorResponse.Details = "Please try again later or contact support if the problem persists";
                    
                    // Log critical errors for monitoring
                    await auditService.LogSecurityEventAsync("internal_server_error", 
                        $"Unhandled exception: {exception.GetType().Name}", userId);
                    break;
            }

            // Don't expose sensitive information in production
            if (context.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true)
            {
                errorResponse.StackTrace = exception.StackTrace;
                errorResponse.InnerException = exception.InnerException?.Message;
            }

            var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await response.WriteAsync(jsonResponse);
        }
    }

    public class ApiErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string TraceId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Path { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public Dictionary<string, string[]>? ValidationErrors { get; set; }
        public string? StackTrace { get; set; }
        public string? InnerException { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
