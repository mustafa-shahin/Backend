using Backend.CMS.Application.Components;
using Backend.CMS.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class PageContentValidationService : IPageContentValidationService
    {
        private readonly ILogger<PageContentValidationService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public PageContentValidationService(ILogger<PageContentValidationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                PropertyNameCaseInsensitive = true
            };
        }

        public bool ValidatePageContent(Dictionary<string, object> content)
        {
            try
            {
                var errors = GetValidationErrors(content, "content");
                return errors.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating page content");
                return false;
            }
        }

        public bool ValidatePageLayout(Dictionary<string, object> layout)
        {
            try
            {
                var errors = GetValidationErrors(layout, "layout");
                return !errors.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating page layout");
                return false;
            }
        }

        public List<string> GetValidationErrors(Dictionary<string, object> data, string section)
        {
            var errors = new List<string>();

            try
            {
                if (section == "layout")
                {
                    ValidateLayoutStructure(data, errors);
                }
                else if (section == "content")
                {
                    ValidateContentStructure(data, errors);
                }

                // Validate that the data can be serialized to JSON
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                if (string.IsNullOrEmpty(json))
                {
                    errors.Add($"{section} cannot be serialized to JSON");
                }
            }
            catch (JsonException ex)
            {
                errors.Add($"JSON serialization error in {section}: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating {Section}", section);
                errors.Add($"Validation error in {section}: {ex.Message}");
            }

            return errors;
        }

        public bool TryDeserializeComponent(Dictionary<string, object> componentData, out BaseComponent? component)
        {
            component = null;

            try
            {
                if (!componentData.TryGetValue("type", out var typeObj) || typeObj?.ToString() is not string type)
                {
                    return false;
                }

                var json = JsonSerializer.Serialize(componentData, _jsonOptions);

                // Try to deserialize based on component type
                component = type switch
                {
                    nameof(ButtonComponent) => JsonSerializer.Deserialize<ButtonComponent>(json, _jsonOptions),
                    // Add more component types here as they are created
                    _ => null
                };

                return component != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deserializing component of type {Type}",
                    componentData.TryGetValue("type", out var t) ? t?.ToString() : "unknown");
                return false;
            }
        }

        public bool TryDeserializeBlock(Dictionary<string, object> blockData, out Block? block)
        {
            block = null;

            try
            {
                var json = JsonSerializer.Serialize(blockData, _jsonOptions);
                block = JsonSerializer.Deserialize<Block>(json, _jsonOptions);
                return block != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deserializing block");
                return false;
            }
        }

        private void ValidateLayoutStructure(Dictionary<string, object> layout, List<string> errors)
        {
            // Check if blocks exist and are valid
            if (layout.TryGetValue("blocks", out var blocksObj))
            {
                try
                {
                    var json = JsonSerializer.Serialize(blocksObj, _jsonOptions);
                    var blocks = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json, _jsonOptions);

                    if (blocks != null)
                    {
                        ValidateBlocks(blocks, errors);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Invalid blocks structure: {ex.Message}");
                }
            }

            // Validate global styles if present
            if (layout.TryGetValue("globalStyles", out var globalStylesObj) && globalStylesObj != null)
            {
                try
                {
                    JsonSerializer.Serialize(globalStylesObj, _jsonOptions);
                }
                catch (Exception ex)
                {
                    errors.Add($"Invalid global styles: {ex.Message}");
                }
            }

            // Validate layout settings if present
            if (layout.TryGetValue("layoutSettings", out var layoutSettingsObj) && layoutSettingsObj != null)
            {
                try
                {
                    JsonSerializer.Serialize(layoutSettingsObj, _jsonOptions);
                }
                catch (Exception ex)
                {
                    errors.Add($"Invalid layout settings: {ex.Message}");
                }
            }
        }

        private void ValidateBlocks(List<Dictionary<string, object>> blocks, List<string> errors)
        {
            var blockIds = new HashSet<string>();

            foreach (var blockData in blocks)
            {
                // Validate block has required fields
                if (!blockData.TryGetValue("id", out var idObj) || string.IsNullOrWhiteSpace(idObj?.ToString()))
                {
                    errors.Add("Block is missing required 'id' field");
                    continue;
                }

                var blockId = idObj.ToString()!;

                // Check for duplicate block IDs
                if (!blockIds.Add(blockId))
                {
                    errors.Add($"Duplicate block ID: {blockId}");
                }

                // Validate columns if present
                if (blockData.TryGetValue("columns", out var columnsObj))
                {
                    if (int.TryParse(columnsObj?.ToString(), out var columns))
                    {
                        if (columns < 1 || columns > 12)
                        {
                            errors.Add($"Block {blockId}: columns must be between 1 and 12");
                        }
                    }
                    else if (columnsObj != null)
                    {
                        errors.Add($"Block {blockId}: columns must be a number");
                    }
                }

                // Validate priority if present
                if (blockData.TryGetValue("priority", out var priorityObj))
                {
                    if (int.TryParse(priorityObj?.ToString(), out var priority))
                    {
                        if (priority < 0)
                        {
                            errors.Add($"Block {blockId}: priority cannot be negative");
                        }
                    }
                    else if (priorityObj != null)
                    {
                        errors.Add($"Block {blockId}: priority must be a number");
                    }
                }

                // Validate components if present
                if (blockData.TryGetValue("components", out var componentsObj))
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(componentsObj, _jsonOptions);
                        var components = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json, _jsonOptions);

                        if (components != null)
                        {
                            ValidateComponents(components, blockId, errors);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Block {blockId}: invalid components structure - {ex.Message}");
                    }
                }
            }
        }

        private void ValidateComponents(List<Dictionary<string, object>> components, string blockId, List<string> errors)
        {
            var componentIds = new HashSet<string>();

            foreach (var componentData in components)
            {
                // Validate component has required fields
                if (!componentData.TryGetValue("id", out var idObj) || string.IsNullOrWhiteSpace(idObj?.ToString()))
                {
                    errors.Add($"Block {blockId}: component is missing required 'id' field");
                    continue;
                }

                var componentId = idObj.ToString()!;

                // Check for duplicate component IDs within block
                if (!componentIds.Add(componentId))
                {
                    errors.Add($"Block {blockId}: duplicate component ID {componentId}");
                }

                // Validate component has type
                if (!componentData.TryGetValue("type", out var typeObj) || string.IsNullOrWhiteSpace(typeObj?.ToString()))
                {
                    errors.Add($"Block {blockId}, Component {componentId}: missing required 'type' field");
                }

                // Try to deserialize the component to validate its structure
                if (!TryDeserializeComponent(componentData, out var component))
                {
                    errors.Add($"Block {blockId}, Component {componentId}: invalid component structure");
                }
            }
        }

        private static void ValidateContentStructure(Dictionary<string, object> content, List<string> errors)
        {    // TODO
            // Basic content validation - can be extended based on specific content requirements
            // For now  just ensure it is serializable (already checked in GetValidationErrors)

            // Add specific content validation rules here as needed
        }
    }
}