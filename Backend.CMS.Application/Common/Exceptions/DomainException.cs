namespace Backend.CMS.Application.Common.Exceptions
{
    public class DomainException : Exception
    {
        public string Code { get; }
        public Dictionary<string, object> Metadata { get; }

        public DomainException(string message, string code = "DOMAIN_ERROR") : base(message)
        {
            Code = code;
            Metadata = new Dictionary<string, object>();
        }

        public DomainException(string message, string code, Exception innerException) : base(message, innerException)
        {
            Code = code;
            Metadata = new Dictionary<string, object>();
        }

        public DomainException(string message, string code, Dictionary<string, object> metadata) : base(message)
        {
            Code = code;
            Metadata = metadata ?? new Dictionary<string, object>();
        }
    }

    public class BusinessRuleViolationException : DomainException
    {
        public BusinessRuleViolationException(string message) : base(message, "BUSINESS_RULE_VIOLATION")
        {
        }

        public BusinessRuleViolationException(string message, Dictionary<string, object> metadata)
            : base(message, "BUSINESS_RULE_VIOLATION", metadata)
        {
        }
    }

    public class ConcurrencyException : DomainException
    {
        public ConcurrencyException(string message) : base(message, "CONCURRENCY_CONFLICT")
        {
        }

        public ConcurrencyException(string entityName, object entityId)
            : base($"The {entityName} with ID {entityId} has been modified by another user", "CONCURRENCY_CONFLICT")
        {
            Metadata["EntityName"] = entityName;
            Metadata["EntityId"] = entityId;
        }
    }

    public class TenantMismatchException : DomainException
    {
        public TenantMismatchException(string userTenant, string resourceTenant)
            : base($"User tenant '{userTenant}' does not match resource tenant '{resourceTenant}'", "TENANT_MISMATCH")
        {
            Metadata["UserTenant"] = userTenant;
            Metadata["ResourceTenant"] = resourceTenant;
        }
    }

    public class PermissionDeniedException : DomainException
    {
        public PermissionDeniedException(string resource, string action)
            : base($"Permission denied for {action} on {resource}", "PERMISSION_DENIED")
        {
            Metadata["Resource"] = resource;
            Metadata["Action"] = action;
        }
    }

    public class ResourceNotFoundException : DomainException
    {
        public ResourceNotFoundException(string resourceType, object resourceId)
            : base($"{resourceType} with ID '{resourceId}' was not found", "RESOURCE_NOT_FOUND")
        {
            Metadata["ResourceType"] = resourceType;
            Metadata["ResourceId"] = resourceId;
        }
    }

    public class ValidationException : DomainException
    {
        public List<ValidationError> Errors { get; }

        public ValidationException(string message, List<ValidationError> errors)
            : base(message, "VALIDATION_FAILED")
        {
            Errors = errors ?? new List<ValidationError>();
        }

        public ValidationException(List<ValidationError> errors)
            : base("One or more validation errors occurred", "VALIDATION_FAILED")
        {
            Errors = errors ?? new List<ValidationError>();
        }
    }

    public class ValidationError
    {
        public string PropertyName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public object? AttemptedValue { get; set; }
        public string? ErrorCode { get; set; }
    }
}
