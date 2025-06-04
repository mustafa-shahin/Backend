using MediatR;
using Microsoft.Extensions.Logging;
using Backend.CMS.Audit.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Backend.CMS.Application.Common.Behaviors
{
    public class AuditingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<AuditingBehavior<TRequest, TResponse>> _logger;
        private readonly IAuditService _auditService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditingBehavior(
            ILogger<AuditingBehavior<TRequest, TResponse>> logger,
            IAuditService auditService,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _auditService = auditService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var userId = GetCurrentUserId();

            // Log the request
            _logger.LogInformation("Processing {RequestName} for user {UserId}", requestName, userId);

            try
            {
                var response = await next();

                // Audit successful operations
                if (ShouldAuditRequest(requestName))
                {
                    await AuditRequestAsync(requestName, request, response, userId, true);
                }

                return response;
            }
            catch (Exception ex)
            {
                // Audit failed operations
                if (ShouldAuditRequest(requestName))
                {
                    await AuditRequestAsync(requestName, request, default(TResponse), userId, false, ex);
                }

                throw;
            }
        }

        private async Task AuditRequestAsync(string requestName, TRequest request, TResponse? response, string? userId, bool success, Exception? exception = null)
        {
            try
            {
                var details = new
                {
                    RequestType = requestName,
                    Success = success,
                    Request = GetSafeRequestData(request),
                    Response = success ? GetSafeResponseData(response) : null,
                    Error = exception?.Message,
                    Timestamp = DateTime.UtcNow
                };

                await _auditService.LogUserActionAsync(
                    userId ?? "anonymous",
                    $"{requestName}_{(success ? "Success" : "Failed")}",
                    JsonSerializer.Serialize(details));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to audit request {RequestName}", requestName);
            }
        }

        private string? GetCurrentUserId()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        private bool ShouldAuditRequest(string requestName)
        {
            // Audit commands but not queries for performance
            return requestName.EndsWith("Command") ||
                   requestName.Contains("Delete") ||
                   requestName.Contains("Update") ||
                   requestName.Contains("Create");
        }

        private object GetSafeRequestData(TRequest request)
        {
            try
            {
                // Remove sensitive data before auditing
                var requestData = JsonSerializer.Serialize(request, GetJsonOptions());
                // In a real implementation, you would sanitize passwords, tokens, etc.
                return JsonSerializer.Deserialize<object>(requestData, GetJsonOptions()) ?? new object();
            }
            catch (Exception)
            {
                // If serialization fails, return a safe representation
                return new { Type = typeof(TRequest).Name, Error = "Serialization failed" };
            }
        }

        private object? GetSafeResponseData(TResponse? response)
        {
            if (response == null) return null;

            try
            {
                // Remove sensitive data before auditing
                var responseData = JsonSerializer.Serialize(response, GetJsonOptions());
                return JsonSerializer.Deserialize<object>(responseData, GetJsonOptions());
            }
            catch (Exception)
            {
                // If serialization fails, return a safe representation
                return new { Type = typeof(TResponse).Name, Error = "Serialization failed" };
            }
        }

        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }
    }
}