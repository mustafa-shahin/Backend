using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Infrastructure.Caching.Interfaces
{
    /// <summary>
    /// Service for generating and managing cache keys
    /// </summary>
    public interface ICacheKeyService
    {
        /// <summary>
        /// Generate entity-specific cache key
        /// </summary>
        string GetEntityKey<T>(object entityId, string? suffix = null);

        /// <summary>
        /// Generate collection cache key for entity type
        /// </summary>
        string GetCollectionKey<T>(string operation, params object[] parameters);

        /// <summary>
        /// Generate search/query cache key
        /// </summary>
        string GetQueryKey<T>(string operation, object queryParameters);

        /// <summary>
        /// Generate custom cache key with prefix
        /// </summary>
        string GetCustomKey(string prefix, params object[] parts);

        /// <summary>
        /// Get pattern for entity type (for bulk operations)
        /// </summary>
        string GetEntityPattern<T>();

        /// <summary>
        /// Validate cache key format
        /// </summary>
        bool IsValidKey(string key);
    }
}
