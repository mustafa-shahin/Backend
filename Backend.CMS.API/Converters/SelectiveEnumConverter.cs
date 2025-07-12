using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.CMS.Domain.Enums;

namespace Backend.CMS.API.Converters
{
    /// <summary>
    /// Custom JSON converter that handles enum serialization based on enum type.
    /// UserRole enums are serialized as numbers, other enums as camelCase strings.
    /// </summary>
    public class SelectiveEnumConverter : JsonConverterFactory
    {
        private static readonly HashSet<Type> NumericEnums =
        [
            typeof(UserRole)
        ];

        private static readonly HashSet<Type> StringEnums =
        [
            typeof(PageStatus),
            typeof(ComponentType),
            typeof(DeploymentStatus),
            typeof(SyncStatus),
            typeof(ConflictResolutionStrategy),
            typeof(JobStatus),
            typeof(JobType),
            typeof(JobPriority),
            typeof(ProposalStatus),
            typeof(FileType),
            typeof(FolderType),
            typeof(FileAccessType),
            typeof(ProductStatus),
            typeof(ProductType)
        ];

        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsEnum ||
                   (Nullable.GetUnderlyingType(typeToConvert)?.IsEnum == true);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            // Determine if the type is nullable
            bool isNullable = Nullable.GetUnderlyingType(typeToConvert) != null;
            Type enumType = isNullable ? Nullable.GetUnderlyingType(typeToConvert)! : typeToConvert;

            if (NumericEnums.Contains(enumType))
            {
                var innerConverterType = typeof(NumericEnumConverter<>).MakeGenericType(enumType);
                var innerConverter = (JsonConverter)Activator.CreateInstance(innerConverterType)!;

                if (isNullable)
                {
                    return (JsonConverter)Activator.CreateInstance(
                        typeof(NullableEnumConverter<>).MakeGenericType(enumType), innerConverter)!;
                }
                return innerConverter;
            }

            if (StringEnums.Contains(enumType))
            {
                var innerConverterType = typeof(StringEnumConverter<>).MakeGenericType(enumType);
                var innerConverter = (JsonConverter)Activator.CreateInstance(innerConverterType)!;

                if (isNullable)
                {
                    return (JsonConverter)Activator.CreateInstance(
                        typeof(NullableEnumConverter<>).MakeGenericType(enumType), innerConverter)!;
                }
                return innerConverter;
            }

            // Default to string conversion for unknown enums
            {
                var innerConverterType = typeof(StringEnumConverter<>).MakeGenericType(enumType);
                var innerConverter = (JsonConverter)Activator.CreateInstance(innerConverterType)!;

                if (isNullable)
                {
                    return (JsonConverter)Activator.CreateInstance(
                        typeof(NullableEnumConverter<>).MakeGenericType(enumType), innerConverter)!;
                }
                return innerConverter;
            }
        }
    }

    /// <summary>
    /// Converts enums to/from numeric values
    /// </summary>
    /// <typeparam name="T">The enum type</typeparam>
    public class NumericEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                var value = reader.GetInt32();
                return (T)Enum.ToObject(typeof(T), value);
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                if (int.TryParse(stringValue, out var intValue))
                {
                    return (T)Enum.ToObject(typeof(T), intValue);
                }

                if (Enum.TryParse<T>(stringValue, true, out var enumValue))
                {
                    return enumValue;
                }
            }

            throw new JsonException($"Unable to convert \"{reader.GetString()}\" to enum {typeof(T).Name}");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(Convert.ToInt32(value));
        }
    }

    /// <summary>
    /// Converts enums to/from camelCase string values
    /// </summary>
    /// <typeparam name="T">The enum type</typeparam>
    public class StringEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                if (!string.IsNullOrEmpty(stringValue))
                {
                    // Try exact match first
                    if (Enum.TryParse<T>(stringValue, true, out var exactMatch))
                    {
                        return exactMatch;
                    }

                    // Try converting from camelCase to PascalCase
                    var pascalCase = char.ToUpper(stringValue[0]) + stringValue.Substring(1);
                    if (Enum.TryParse<T>(pascalCase, true, out var pascalMatch))
                    {
                        return pascalMatch;
                    }
                }
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                var value = reader.GetInt32();
                return (T)Enum.ToObject(typeof(T), value);
            }

            throw new JsonException($"Unable to convert \"{reader.GetString()}\" to enum {typeof(T).Name}");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var enumString = value.ToString();
            var camelCase = char.ToLower(enumString[0]) + enumString.Substring(1);
            writer.WriteStringValue(camelCase);
        }
    }

    /// <summary>
    /// Handles nullable enum types
    /// </summary>
    /// <typeparam name="T">The non-nullable enum type</typeparam>
    public class NullableEnumConverter<T> : JsonConverter<T?> where T : struct, Enum
    {
        private readonly JsonConverter<T> _innerConverter;

        public NullableEnumConverter(JsonConverter<T> innerConverter)
        {
            _innerConverter = innerConverter;
        }

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            // Pass the original options to the inner converter
            return _innerConverter.Read(ref reader, typeof(T), options);
        }

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                _innerConverter.Write(writer, value.Value, options);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}