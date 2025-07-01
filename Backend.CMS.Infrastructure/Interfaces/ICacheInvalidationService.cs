using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    /// <summary>
    /// Enhanced service for comprehensive cache invalidation operations
    /// </summary>
    public interface ICacheInvalidationService
    {
        /// <summary>
        /// Invalidate all cache entries related to a specific user
        /// </summary>
        Task InvalidateUserCacheAsync(int userId);

        /// <summary>
        /// Invalidate all cache entries related to a specific page
        /// </summary>
        Task InvalidatePageCacheAsync(int pageId);

        /// <summary>
        /// Invalidate all company-related cache entries
        /// </summary>
        Task InvalidateCompanyCacheAsync();

        /// <summary>
        /// Invalidate location-related cache entries
        /// </summary>
        Task InvalidateLocationCacheAsync(int? locationId = null);

        /// <summary>
        /// Invalidate file-related cache entries
        /// </summary>
        Task InvalidateFileCacheAsync(int? fileId = null);

        /// <summary>
        /// Invalidate folder-related cache entries
        /// </summary>
        Task InvalidateFolderCacheAsync(int? folderId = null);

        /// <summary>
        /// Invalidate product-related cache entries
        /// </summary>
        Task InvalidateProductCacheAsync(int? productId = null);

        /// <summary>
        /// Invalidate product variant cache entries
        /// </summary>
        Task InvalidateProductVariantCacheAsync(int? variantId = null, int? productId = null);

        /// <summary>
        /// Invalidate category-related cache entries
        /// </summary>
        Task InvalidateCategoryCacheAsync(int? categoryId = null);

        /// <summary>
        /// Invalidate permission-related cache entries
        /// </summary>
        Task InvalidatePermissionCacheAsync(string? role = null);

        /// <summary>
        /// Invalidate session-related cache entries
        /// </summary>
        Task InvalidateSessionCacheAsync(string? sessionId = null, int? userId = null);

        /// <summary>
        /// Invalidate search-related cache entries
        /// </summary>
        Task InvalidateSearchCacheAsync(string? searchTerm = null);

        /// <summary>
        /// Invalidate component-related cache entries
        /// </summary>
        Task InvalidateComponentCacheAsync(int? componentId = null, int? pageId = null);

        /// <summary>
        /// Invalidate all cache entries (use with caution)
        /// </summary>
        Task InvalidateAllCacheAsync();

        /// <summary>
        /// Warmup cache with commonly accessed data
        /// </summary>
        Task WarmupCacheAsync();

        /// <summary>
        /// Get cache statistics and metrics
        /// </summary>
        Task<Dictionary<string, object>> GetCacheStatisticsAsync();

        /// <summary>
        /// Get all cache keys matching a pattern
        /// </summary>
        Task<List<string>> GetCacheKeysAsync(string pattern = "*");

        /// <summary>
        /// Invalidate multiple entity types in a single operation
        /// </summary>
        Task InvalidateMultipleAsync(params CacheInvalidationRequest[] requests);

        /// <summary>
        /// Invalidate related entities based on entity relationships
        /// </summary>
        Task InvalidateRelatedEntitiesAsync(CacheInvalidationContext context);
    }

    /// <summary>
    /// Request for cache invalidation
    /// </summary>
    public class CacheInvalidationRequest
    {
        public CacheEntityType EntityType { get; set; }
        public int? EntityId { get; set; }
        public string? EntityKey { get; set; }
        public bool InvalidateRelated { get; set; } = true;
        public Dictionary<string, object>? AdditionalContext { get; set; }
    }

    /// <summary>
    /// Context for cache invalidation operations
    /// </summary>
    public class CacheInvalidationContext
    {
        public CacheEntityType EntityType { get; set; }
        public int? EntityId { get; set; }
        public string? EntityKey { get; set; }
        public CacheOperationType OperationType { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public List<CacheInvalidationRequest>? RelatedInvalidations { get; set; }
    }

    /// <summary>
    /// Types of entities that can be cached
    /// </summary>
    public enum CacheEntityType
    {
        User,
        Page,
        File,
        Folder,
        Company,
        Location,
        Product,
        ProductVariant,
        Category,
        Permission,
        Session,
        Component,
        Search,
        All
    }

    /// <summary>
    /// Types of cache operations
    /// </summary>
    public enum CacheOperationType
    {
        Create,
        Update,
        Delete,
        Move,
        Copy,
        BulkUpdate,
        BulkDelete,
        StatusChange,
        Relationship
    }
}