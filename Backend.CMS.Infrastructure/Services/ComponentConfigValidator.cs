using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.CMS.Infrastructure.Services
{
    public class ComponentConfigValidator : IComponentConfigValidator
    {
        private readonly IRepository<ComponentTemplate> _templateRepository;
        private readonly ILogger<ComponentConfigValidator> _logger;

        // Define built-in validation schemas for system components
        private static readonly Dictionary<ComponentType, Dictionary<string, object>> BuiltInSchemas = new()
        {
            [ComponentType.Text] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["required"] = new[] { "text" },
                ["properties"] = new Dictionary<string, object>
                {
                    ["text"] = new Dictionary<string, object> { ["type"] = "string", ["minLength"] = 1 },
                    ["textColor"] = new Dictionary<string, object> { ["type"] = "string", ["pattern"] = "^#[0-9A-Fa-f]{6}$" },
                    ["fontSize"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["fontWeight"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "normal", "bold", "lighter", "bolder" } },
                    ["textAlign"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "left", "center", "right", "justify" } },
                    ["backgroundColor"] = new Dictionary<string, object> { ["type"] = "string", ["pattern"] = "^#[0-9A-Fa-f]{6}$" },
                    ["margin"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["padding"] = new Dictionary<string, object> { ["type"] = "string" }
                }
            },
            [ComponentType.Image] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["required"] = new[] { "src" },
                ["properties"] = new Dictionary<string, object>
                {
                    ["src"] = new Dictionary<string, object> { ["type"] = "string", ["minLength"] = 1 },
                    ["alt"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["width"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["height"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["objectFit"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "cover", "contain", "fill", "scale-down" } },
                    ["borderRadius"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["margin"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["padding"] = new Dictionary<string, object> { ["type"] = "string" }
                }
            },
            [ComponentType.Button] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["required"] = new[] { "text" },
                ["properties"] = new Dictionary<string, object>
                {
                    ["text"] = new Dictionary<string, object> { ["type"] = "string", ["minLength"] = 1 },
                    ["href"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["target"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "_self", "_blank", "_parent", "_top" } },
                    ["variant"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "primary", "secondary", "outline", "ghost" } },
                    ["size"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "small", "medium", "large" } },
                    ["backgroundColor"] = new Dictionary<string, object> { ["type"] = "string", ["pattern"] = "^#[0-9A-Fa-f]{6}$" },
                    ["textColor"] = new Dictionary<string, object> { ["type"] = "string", ["pattern"] = "^#[0-9A-Fa-f]{6}$" },
                    ["borderRadius"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["margin"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["padding"] = new Dictionary<string, object> { ["type"] = "string" }
                }
            },
            [ComponentType.Container] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["maxWidth"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["centered"] = new Dictionary<string, object> { ["type"] = "boolean" },
                    ["backgroundColor"] = new Dictionary<string, object> { ["type"] = "string", ["pattern"] = "^#[0-9A-Fa-f]{6}$" },
                    ["borderRadius"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["border"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["margin"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["padding"] = new Dictionary<string, object> { ["type"] = "string" }
                }
            }
        };

        private static readonly Dictionary<ComponentType, Dictionary<string, object>> DefaultConfigs = new()
        {
            [ComponentType.Text] = new Dictionary<string, object>
            {
                ["text"] = "Enter your text here...",
                ["textColor"] = "#000000",
                ["fontSize"] = "16px",
                ["fontWeight"] = "normal",
                ["textAlign"] = "left",
                ["margin"] = "0",
                ["padding"] = "16px"
            },
            [ComponentType.Image] = new Dictionary<string, object>
            {
                ["src"] = "",
                ["alt"] = "Image",
                ["width"] = "100%",
                ["height"] = "auto",
                ["objectFit"] = "cover"
            },
            [ComponentType.Button] = new Dictionary<string, object>
            {
                ["text"] = "Click Me",
                ["href"] = "",
                ["target"] = "_self",
                ["variant"] = "primary",
                ["size"] = "medium",
                ["backgroundColor"] = "#007bff",
                ["textColor"] = "#ffffff",
                ["borderRadius"] = "4px",
                ["padding"] = "12px 24px"
            },
            [ComponentType.Container] = new Dictionary<string, object>
            {
                ["maxWidth"] = "1200px",
                ["centered"] = true,
                ["margin"] = "0 auto",
                ["padding"] = "20px"
            }
        };

        public ComponentConfigValidator(
            IRepository<ComponentTemplate> templateRepository,
            ILogger<ComponentConfigValidator> logger)
        {
            _templateRepository = templateRepository;
            _logger = logger;
        }

        public async Task<ComponentConfigValidationResult> ValidateAsync(ComponentType type, Dictionary<string, object> config)
        {
            var result = new ComponentConfigValidationResult();

            try
            {
                // Get schema for validation
                var schema = await GetConfigSchemaAsync(type);

                // Sanitize config first
                result.SanitizedConfig = await SanitizeConfigAsync(type, config);

                // Validate against schema
                var validationErrors = ValidateAgainstSchema(schema, result.SanitizedConfig);
                result.Errors.AddRange(validationErrors);

                // Additional business logic validation
                var businessValidationErrors = ValidateBusinessLogic(type, result.SanitizedConfig);
                result.Errors.AddRange(businessValidationErrors);

                result.IsValid = !result.Errors.Any();

                _logger.LogDebug("Validated {ComponentType} config with {ErrorCount} errors", type, result.Errors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating {ComponentType} config", type);
                result.Errors.Add($"Validation error: {ex.Message}");
                result.IsValid = false;
            }

            return result;
        }

        public async Task<Dictionary<string, object>> GetDefaultConfigAsync(ComponentType type)
        {
            try
            {
                // First try to get from database template
                var templates = await _templateRepository.FindAsync(t => t.Type == type && t.IsSystemTemplate);
                var template = templates.FirstOrDefault();

                if (template?.DefaultConfig != null && template.DefaultConfig.Any())
                {
                    return new Dictionary<string, object>(template.DefaultConfig);
                }

                // Fall back to built-in defaults
                if (DefaultConfigs.TryGetValue(type, out var defaultConfig))
                {
                    return new Dictionary<string, object>(defaultConfig);
                }

                return new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default config for {ComponentType}", type);
                return new Dictionary<string, object>();
            }
        }

        public async Task<Dictionary<string, object>> GetConfigSchemaAsync(ComponentType type)
        {
            try
            {
                // First try to get from database template
                var templates = await _templateRepository.FindAsync(t => t.Type == type && t.IsSystemTemplate);
                var template = templates.FirstOrDefault();

                if (template?.ConfigSchema != null && template.ConfigSchema.Any())
                {
                    return new Dictionary<string, object>(template.ConfigSchema);
                }

                // Fall back to built-in schema
                if (BuiltInSchemas.TryGetValue(type, out var schema))
                {
                    return new Dictionary<string, object>(schema);
                }

                return new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting config schema for {ComponentType}", type);
                return new Dictionary<string, object>();
            }
        }

        public async Task<Dictionary<string, object>> SanitizeConfigAsync(ComponentType type, Dictionary<string, object> config)
        {
            var sanitized = new Dictionary<string, object>();

            try
            {
                var schema = await GetConfigSchemaAsync(type);
                var properties = GetSchemaProperties(schema);

                foreach (var kvp in config)
                {
                    var key = kvp.Key;
                    var value = kvp.Value;

                    // Skip null values
                    if (value == null) continue;

                    // Sanitize based on property schema if available
                    if (properties.TryGetValue(key, out var propertySchema))
                    {
                        sanitized[key] = SanitizeValue(value, propertySchema);
                    }
                    else
                    {
                        // Basic sanitization for unknown properties
                        sanitized[key] = SanitizeUnknownValue(value);
                    }
                }

                return sanitized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sanitizing {ComponentType} config", type);
                return config; // Return original config if sanitization fails
            }
        }

        private List<string> ValidateAgainstSchema(Dictionary<string, object> schema, Dictionary<string, object> config)
        {
            var errors = new List<string>();

            try
            {
                // Check required properties
                if (schema.TryGetValue("required", out var requiredObj) && requiredObj is JsonElement requiredElement)
                {
                    if (requiredElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in requiredElement.EnumerateArray())
                        {
                            var requiredProp = element.GetString();
                            if (!string.IsNullOrEmpty(requiredProp) && !config.ContainsKey(requiredProp))
                            {
                                errors.Add($"Required property '{requiredProp}' is missing");
                            }
                        }
                    }
                }

                // Validate individual properties
                var properties = GetSchemaProperties(schema);
                foreach (var kvp in config)
                {
                    if (properties.TryGetValue(kvp.Key, out var propertySchema))
                    {
                        var propErrors = ValidateProperty(kvp.Key, kvp.Value, propertySchema);
                        errors.AddRange(propErrors);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Schema validation error: {ex.Message}");
            }

            return errors;
        }

        private List<string> ValidateBusinessLogic(ComponentType type, Dictionary<string, object> config)
        {
            var errors = new List<string>();

            switch (type)
            {
                case ComponentType.Button:
                    if (config.TryGetValue("href", out var href) && !string.IsNullOrEmpty(href?.ToString()))
                    {
                        if (!IsValidUrl(href.ToString()!))
                        {
                            errors.Add("Button href must be a valid URL or relative path");
                        }
                    }
                    break;

                case ComponentType.Image:
                    if (config.TryGetValue("src", out var src) && !string.IsNullOrEmpty(src?.ToString()))
                    {
                        if (!IsValidImageUrl(src.ToString()!))
                        {
                            errors.Add("Image src must be a valid image URL");
                        }
                    }
                    break;
            }

            return errors;
        }

        private Dictionary<string, Dictionary<string, object>> GetSchemaProperties(Dictionary<string, object> schema)
        {
            var properties = new Dictionary<string, Dictionary<string, object>>();

            if (schema.TryGetValue("properties", out var propertiesObj))
            {
                try
                {
                    if (propertiesObj is JsonElement propertiesElement)
                    {
                        foreach (var property in propertiesElement.EnumerateObject())
                        {
                            var propertySchema = JsonSerializer.Deserialize<Dictionary<string, object>>(property.Value.GetRawText());
                            if (propertySchema != null)
                            {
                                properties[property.Name] = propertySchema;
                            }
                        }
                    }
                    else if (propertiesObj is Dictionary<string, object> propertiesDict)
                    {
                        foreach (var kvp in propertiesDict)
                        {
                            if (kvp.Value is Dictionary<string, object> propertySchema)
                            {
                                properties[kvp.Key] = propertySchema;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing schema properties");
                }
            }

            return properties;
        }

        private List<string> ValidateProperty(string propertyName, object value, Dictionary<string, object> propertySchema)
        {
            var errors = new List<string>();

            try
            {
                // Type validation
                if (propertySchema.TryGetValue("type", out var typeObj))
                {
                    var expectedType = typeObj?.ToString();
                    if (!IsValidType(value, expectedType))
                    {
                        errors.Add($"Property '{propertyName}' must be of type {expectedType}");
                    }
                }

                // Pattern validation for strings
                if (propertySchema.TryGetValue("pattern", out var patternObj) && value is string stringValue)
                {
                    var pattern = patternObj?.ToString();
                    if (!string.IsNullOrEmpty(pattern) && !Regex.IsMatch(stringValue, pattern))
                    {
                        errors.Add($"Property '{propertyName}' does not match required pattern");
                    }
                }

                // Enum validation
                if (propertySchema.TryGetValue("enum", out var enumObj))
                {
                    var validValues = GetEnumValues(enumObj);
                    if (validValues.Any() && !validValues.Contains(value?.ToString()))
                    {
                        errors.Add($"Property '{propertyName}' must be one of: {string.Join(", ", validValues)}");
                    }
                }

                // String length validation
                if (value is string str)
                {
                    if (propertySchema.TryGetValue("minLength", out var minLengthObj) && int.TryParse(minLengthObj?.ToString(), out var minLength))
                    {
                        if (str.Length < minLength)
                        {
                            errors.Add($"Property '{propertyName}' must be at least {minLength} characters");
                        }
                    }

                    if (propertySchema.TryGetValue("maxLength", out var maxLengthObj) && int.TryParse(maxLengthObj?.ToString(), out var maxLength))
                    {
                        if (str.Length > maxLength)
                        {
                            errors.Add($"Property '{propertyName}' must be no more than {maxLength} characters");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error validating property '{propertyName}': {ex.Message}");
            }

            return errors;
        }

        private object SanitizeValue(object value, Dictionary<string, object> propertySchema)
        {
            try
            {
                if (value is string stringValue)
                {
                    // HTML sanitization for text content
                    if (propertySchema.TryGetValue("allowHtml", out var allowHtmlObj) &&
                        bool.TryParse(allowHtmlObj?.ToString(), out var allowHtml) && allowHtml)
                    {
                        return SanitizeHtml(stringValue);
                    }
                    else
                    {
                        // Escape HTML for safety
                        return System.Net.WebUtility.HtmlEncode(stringValue);
                    }
                }

                return value;
            }
            catch
            {
                return value;
            }
        }

        private object SanitizeUnknownValue(object value)
        {
            if (value is string stringValue)
            {
                // Basic HTML encoding for unknown string values
                return System.Net.WebUtility.HtmlEncode(stringValue);
            }

            return value;
        }

        private bool IsValidType(object value, string? expectedType)
        {
            if (string.IsNullOrEmpty(expectedType)) return true;

            return expectedType.ToLower() switch
            {
                "string" => value is string,
                "number" => value is int or long or float or double or decimal,
                "integer" => value is int or long,
                "boolean" => value is bool,
                "object" => value is Dictionary<string, object> or JsonElement,
                "array" => value is Array or List<object>,
                _ => true
            };
        }

        private List<string> GetEnumValues(object enumObj)
        {
            var values = new List<string>();

            try
            {
                if (enumObj is JsonElement enumElement && enumElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in enumElement.EnumerateArray())
                    {
                        var value = element.GetString();
                        if (!string.IsNullOrEmpty(value))
                        {
                            values.Add(value);
                        }
                    }
                }
                else if (enumObj is string[] stringArray)
                {
                    values.AddRange(stringArray);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing enum values");
            }

            return values;
        }

        private bool IsValidUrl(string url)
        {
            // Allow relative URLs
            if (url.StartsWith("/") || url.StartsWith("./") || url.StartsWith("../"))
                return true;

            // Allow anchor links
            if (!url.StartsWith("#"))
                // Validate absolute URLs
                return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

            return true;
        }

        private bool IsValidImageUrl(string url)
        {
            if (!IsValidUrl(url)) return false;

            // Check for common image extensions
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".svg", ".webp", ".bmp" };
            var lowerUrl = url.ToLower();

            return imageExtensions.Any(ext => lowerUrl.Contains(ext)) ||
                   lowerUrl.Contains("image") ||
                   url.StartsWith("data:image/") ||
                   url.StartsWith("/api/files/"); // Internal file URLs
        }

        private string SanitizeHtml(string html)
        {
            // Basic HTML sanitization - remove script tags and other dangerous elements
            var sanitized = html;

            // Remove script tags
            sanitized = Regex.Replace(sanitized, @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", "", RegexOptions.IgnoreCase);

            // Remove dangerous attributes
            sanitized = Regex.Replace(sanitized, @"\s(on\w+)\s*=\s*[""']?[^""'>]*[""']?", "", RegexOptions.IgnoreCase);

            // Remove javascript: URLs
            sanitized = Regex.Replace(sanitized, @"javascript\s*:", "", RegexOptions.IgnoreCase);

            return sanitized;
        }
    }
}