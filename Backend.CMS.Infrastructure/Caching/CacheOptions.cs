using System.Text.Json;

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
    }
}
