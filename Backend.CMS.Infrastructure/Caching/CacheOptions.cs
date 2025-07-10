using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.CMS.Infrastructure.Caching
{
    /// <summary>
    /// Cache configuration options
    /// </summary>
    public class CacheOptions
    {
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan LongExpiration { get; set; } = TimeSpan.FromHours(6);
        public TimeSpan ShortExpiration { get; set; } = TimeSpan.FromMinutes(5);
        public string KeyPrefix { get; set; } = "cms";
        public string KeySeparator { get; set; } = ":";
        public int MaxKeyLength { get; set; } = 250;
        public bool EnableCompression { get; set; } = false;
        public JsonSerializerOptions? JsonOptions { get; set; }
        public bool EnableDistributedLocking { get; set; } = true;
        public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public int RetryAttempts { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Get default JsonSerializerOptions optimized for caching
        /// </summary>
        public static JsonSerializerOptions GetDefaultJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                MaxDepth = 32,
                IgnoreReadOnlyProperties = true,
                IgnoreReadOnlyFields = true
            };
        }
    }
}