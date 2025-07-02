using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Backend.CMS.Infrastructure.Caching
{
    public class CacheConfigurationBuilder
    {
        private readonly CacheOptions _options = new();

        public CacheConfigurationBuilder WithDefaultExpiration(TimeSpan expiration)
        {
            _options.DefaultExpiration = expiration;
            return this;
        }

        public CacheConfigurationBuilder WithLongExpiration(TimeSpan expiration)
        {
            _options.LongExpiration = expiration;
            return this;
        }

        public CacheConfigurationBuilder WithShortExpiration(TimeSpan expiration)
        {
            _options.ShortExpiration = expiration;
            return this;
        }

        public CacheConfigurationBuilder WithKeyPrefix(string prefix)
        {
            _options.KeyPrefix = prefix;
            return this;
        }

        public CacheConfigurationBuilder WithKeySeparator(string separator)
        {
            _options.KeySeparator = separator;
            return this;
        }

        public CacheConfigurationBuilder WithMaxKeyLength(int maxLength)
        {
            _options.MaxKeyLength = maxLength;
            return this;
        }

        public CacheConfigurationBuilder WithCompression(bool enabled = true)
        {
            _options.EnableCompression = enabled;
            return this;
        }

        public CacheConfigurationBuilder WithDistributedLocking(bool enabled = true)
        {
            _options.EnableDistributedLocking = enabled;
            return this;
        }

        public CacheConfigurationBuilder WithLockTimeout(TimeSpan timeout)
        {
            _options.LockTimeout = timeout;
            return this;
        }

        public CacheConfigurationBuilder WithRetryPolicy(int attempts, TimeSpan delay)
        {
            _options.RetryAttempts = attempts;
            _options.RetryDelay = delay;
            return this;
        }

        public CacheConfigurationBuilder WithJsonOptions(JsonSerializerOptions jsonOptions)
        {
            _options.JsonOptions = jsonOptions;
            return this;
        }

        public CacheOptions Build() => _options;
    }
}
