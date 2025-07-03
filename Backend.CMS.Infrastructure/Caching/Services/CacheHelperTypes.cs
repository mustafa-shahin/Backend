namespace Backend.CMS.Infrastructure.Services
{
    /// <summary>
    /// Helper wrapper classes for primitive types since ICacheService only works with reference types (where T : class)
    /// </summary>

    /// <summary>
    /// Wrapper for int values to enable caching of primitive counts
    /// </summary>
    public class CountWrapper
    {
        public int Value { get; set; }

        public CountWrapper() { }

        public CountWrapper(int value)
        {
            Value = value;
        }

        public static implicit operator int(CountWrapper wrapper) => wrapper?.Value ?? 0;
        public static implicit operator CountWrapper(int value) => new(value);
    }

    /// <summary>
    /// Wrapper for decimal price ranges
    /// </summary>
    public class PriceRange
    {
        public decimal Min { get; set; }
        public decimal Max { get; set; }

        public PriceRange() { }

        public PriceRange(decimal min, decimal max)
        {
            Min = min;
            Max = max;
        }
    }

    /// <summary>
    /// Wrapper for boolean values to enable caching of primitive booleans
    /// </summary>
    public class BoolWrapper
    {
        public bool Value { get; set; }

        public BoolWrapper() { }

        public BoolWrapper(bool value)
        {
            Value = value;
        }

        public static implicit operator bool(BoolWrapper wrapper) => wrapper?.Value ?? false;
        public static implicit operator BoolWrapper(bool value) => new(value);
    }

    /// <summary>
    /// Wrapper for string values that might be null
    /// </summary>
    public class StringWrapper
    {
        public string? Value { get; set; }

        public StringWrapper() { }

        public StringWrapper(string? value)
        {
            Value = value;
        }

        public static implicit operator string?(StringWrapper wrapper) => wrapper?.Value;
        public static implicit operator StringWrapper(string? value) => new(value);
    }

    /// <summary>
    /// Wrapper for DateTime values to enable caching of primitive dates
    /// </summary>
    public class DateTimeWrapper
    {
        public DateTime Value { get; set; }

        public DateTimeWrapper() { }

        public DateTimeWrapper(DateTime value)
        {
            Value = value;
        }

        public static implicit operator DateTime(DateTimeWrapper wrapper) => wrapper?.Value ?? DateTime.MinValue;
        public static implicit operator DateTimeWrapper(DateTime value) => new(value);
    }

    /// <summary>
    /// Generic wrapper for any value type
    /// </summary>
    /// <typeparam name="T">The value type to wrap</typeparam>
    public class ValueWrapper<T> where T : struct
    {
        public T Value { get; set; }

        public ValueWrapper() { }

        public ValueWrapper(T value)
        {
            Value = value;
        }

        public static implicit operator T(ValueWrapper<T> wrapper) => wrapper != null ? wrapper.Value : default;
        public static implicit operator ValueWrapper<T>(T value) => new(value);
    }

    /// <summary>
    /// Cache-friendly statistics container
    /// </summary>
    public class CacheableStatistics
    {
        public Dictionary<string, object> Data { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        public T? GetValue<T>(string key)
        {
            if (Data.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return default;
        }

        public void SetValue<T>(string key, T value)
        {
            if (value != null)
                Data[key] = value;
        }
    }

    /// <summary>
    /// Cache-friendly configuration container
    /// </summary>
    public class CacheableConfiguration
    {
        public Dictionary<string, string> Settings { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string Version { get; set; } = "1.0";

        public string? GetSetting(string key) => Settings.TryGetValue(key, out var value) ? value : null;
        public void SetSetting(string key, string value) => Settings[key] = value;
    }

    /// <summary>
    /// Cache time constants for consistent expiration management across services
    /// </summary>
    public static class CacheExpiration
    {
        public static readonly TimeSpan VeryShort = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan Short = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan Medium = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan Default = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan Long = TimeSpan.FromHours(2);
        public static readonly TimeSpan VeryLong = TimeSpan.FromHours(6);
        public static readonly TimeSpan Daily = TimeSpan.FromHours(24);

        /// <summary>
        /// Get appropriate cache expiration based on data volatility
        /// </summary>
        public static TimeSpan ForDataType(CacheDataVolatility volatility)
        {
            return volatility switch
            {
                CacheDataVolatility.RealTime => VeryShort,
                CacheDataVolatility.HighlyVolatile => Short,
                CacheDataVolatility.Volatile => Medium,
                CacheDataVolatility.SemiStatic => Default,
                CacheDataVolatility.Static => Long,
                CacheDataVolatility.Configuration => VeryLong,
                CacheDataVolatility.Reference => Daily,
                _ => Default
            };
        }
    }

    /// <summary>
    /// Data volatility levels for cache expiration determination
    /// </summary>
    public enum CacheDataVolatility
    {
        RealTime,        // User sessions, real-time counters
        HighlyVolatile,  // Search results, temporary calculations
        Volatile,        // Product stock, user activity
        SemiStatic,      // Product listings, user profiles
        Static,          // Product details, page content
        Configuration,   // Settings, permissions, categories
        Reference        // Static content, system data
    }
}