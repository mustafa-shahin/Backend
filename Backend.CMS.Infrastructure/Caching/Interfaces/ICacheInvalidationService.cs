using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Infrastructure.Caching.Interfaces
{
    /// <summary>
    /// Cache invalidation service for managing cache lifecycles
    /// </summary>
    public interface ICacheInvalidationService
    {
        /// <summary>
        /// Invalidate cache for a specific entity
        /// </summary>
        Task InvalidateEntityAsync<T>(object entityId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidate cache for multiple entities of the same type
        /// </summary>
        Task InvalidateEntitiesAsync<T>(IEnumerable<object> entityIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidate all cache entries for an entity type
        /// </summary>
        Task InvalidateEntityTypeAsync<T>(CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidate cache using custom pattern
        /// </summary>
        Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidate related entities (for complex relationships)
        /// </summary>
        Task InvalidateRelatedAsync<T>(object entityId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clear all cache entries (use with caution)
        /// </summary>
        Task ClearAllAsync(CancellationToken cancellationToken = default);
    }
}
