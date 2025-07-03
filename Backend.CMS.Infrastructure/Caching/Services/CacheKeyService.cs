using Backend.CMS.Infrastructure.Caching.Interfaces;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Caching.Services
{
    /// <summary>
    /// Service for generating and managing cache keys with consistent patterns
    /// </summary>
    public class CacheKeyService : ICacheKeyService
    {
        private readonly CacheOptions _options;
        private readonly string _separator;

        public CacheKeyService(IOptions<CacheOptions> options)
        {
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _separator = _options.KeySeparator;
        }

        public string GetEntityKey<T>(object entityId, string? suffix = null)
        {
            var entityName = GetEntityName<T>();
            var key = $"{entityName}{_separator}id{_separator}{entityId}";

            if (!string.IsNullOrWhiteSpace(suffix))
            {
                key += $"{_separator}{suffix}";
            }

            return key;
        }

        public string GetCollectionKey<T>(string operation, params object[] parameters)
        {
            var entityName = GetEntityName<T>();
            var key = $"{entityName}{_separator}list{_separator}{operation}";

            if (parameters?.Length > 0)
            {
                var paramString = string.Join(_separator, parameters.Where(p => p != null).Select(p => p.ToString()));
                if (!string.IsNullOrEmpty(paramString))
                {
                    key += $"{_separator}{paramString}";
                }
            }

            return key;
        }

        public string GetQueryKey<T>(string operation, object queryParameters)
        {
            var entityName = GetEntityName<T>();
            var queryHash = GenerateQueryHash(queryParameters);

            return $"{entityName}{_separator}query{_separator}{operation}{_separator}{queryHash}";
        }

        public string GetCustomKey(string prefix, params object[] parts)
        {
            var keyParts = new List<string> { prefix };
            keyParts.AddRange(parts.Where(p => p != null).Select(p => p.ToString()!));

            return string.Join(_separator, keyParts);
        }

        public string GetEntityPattern<T>()
        {
            var entityName = GetEntityName<T>();
            return $"{entityName}{_separator}*";
        }

        public bool IsValidKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            // Check for invalid characters that might cause issues in Redis
            var invalidChars = new[] { ' ', '\t', '\r', '\n', '\0' };
            return !key.Any(c => invalidChars.Contains(c));
        }

        private string GetEntityName<T>()
        {
            return typeof(T).Name.ToLowerInvariant();
        }

        private string GenerateQueryHash(object queryParameters)
        {
            if (queryParameters == null)
                return "null";

            try
            {
                // Serialize to JSON for consistent hashing
                var json = JsonSerializer.Serialize(queryParameters, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                // Generate SHA256 hash
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
                return Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-").TrimEnd('=')[..12]; // Take first 12 chars
            }
            catch
            {
                // Fallback to string representation
                return queryParameters.ToString()?.GetHashCode().ToString("x") ?? "unknown";
            }
        }
    }

    /// <summary>
    /// Centralized cache keys for all entities - provides consistency and discoverability
    /// </summary>
    public static class CacheKeys
    {
        private const string SEPARATOR = ":";

        #region User Cache Keys
        public static string UserById(int userId) => $"user{SEPARATOR}id{SEPARATOR}{userId}";
        public static string UserByEmail(string email) => $"user{SEPARATOR}email{SEPARATOR}{email.ToLowerInvariant()}";
        public static string UserByUsername(string username) => $"user{SEPARATOR}username{SEPARATOR}{username.ToLowerInvariant()}";
        public static string UserPermissions(int userId) => $"user{SEPARATOR}permissions{SEPARATOR}{userId}";
        public static string UserSessions(int userId) => $"user{SEPARATOR}sessions{SEPARATOR}{userId}";
        public static string UserList(int page, int pageSize) => $"user{SEPARATOR}list{SEPARATOR}page{SEPARATOR}{page}{SEPARATOR}size{SEPARATOR}{pageSize}";
        public static string UserSearch(string searchTerm, int page, int pageSize) => $"user{SEPARATOR}search{SEPARATOR}{GetSearchHash(searchTerm)}{SEPARATOR}{page}{SEPARATOR}{pageSize}";
        public static string UserPattern => "user:*";
        #endregion

        #region Page Cache Keys
        public static string PageById(int pageId) => $"page{SEPARATOR}id{SEPARATOR}{pageId}";
        public static string PageBySlug(string slug) => $"page{SEPARATOR}slug{SEPARATOR}{slug.ToLowerInvariant()}";
        public static string PublishedPages => $"page{SEPARATOR}list{SEPARATOR}published";
        public static string PageHierarchy => $"page{SEPARATOR}hierarchy";
        public static string PagesByParent(int parentId) => $"page{SEPARATOR}parent{SEPARATOR}{parentId}";
        public static string PageWithComponents(int pageId) => $"page{SEPARATOR}components{SEPARATOR}{pageId}";
        public static string PageVersions(int pageId) => $"page{SEPARATOR}versions{SEPARATOR}{pageId}";
        public static string PageList(int page, int pageSize) => $"page{SEPARATOR}list{SEPARATOR}page{SEPARATOR}{page}{SEPARATOR}size{SEPARATOR}{pageSize}";
        public static string PageSearch(string searchTerm, int page, int pageSize) => $"page{SEPARATOR}search{SEPARATOR}{GetSearchHash(searchTerm)}{SEPARATOR}{page}{SEPARATOR}{pageSize}";
        public static string PagePattern => "page:*";
        #endregion

        #region File Cache Keys
        public static string FileById(int fileId) => $"file{SEPARATOR}id{SEPARATOR}{fileId}";
        public static string FileContent(int fileId) => $"file{SEPARATOR}content{SEPARATOR}{fileId}";
        public static string FileThumbnail(int fileId) => $"file{SEPARATOR}thumbnail{SEPARATOR}{fileId}";
        public static string FilesByFolder(int? folderId) => $"file{SEPARATOR}folder{SEPARATOR}{folderId ?? 0}";
        public static string FilesByType(string fileType) => $"file{SEPARATOR}type{SEPARATOR}{fileType.ToLowerInvariant()}";
        public static string RecentFiles(int count) => $"file{SEPARATOR}recent{SEPARATOR}{count}";
        public static string FileStatistics => $"file{SEPARATOR}statistics";
        public static string FileList(int page, int pageSize) => $"file{SEPARATOR}list{SEPARATOR}page{SEPARATOR}{page}{SEPARATOR}size{SEPARATOR}{pageSize}";
        public static string FileSearch(string searchHash) => $"file{SEPARATOR}search{SEPARATOR}{searchHash}";
        public static string FilePattern => "file:*";
        #endregion

        #region Folder Cache Keys
        public static string FolderById(int folderId) => $"folder{SEPARATOR}id{SEPARATOR}{folderId}";
        public static string FolderTree => $"folder{SEPARATOR}tree";
        public static string FoldersByParent(int? parentId) => $"folder{SEPARATOR}parent{SEPARATOR}{parentId ?? 0}";
        public static string FolderPattern => "folder:*";
        #endregion

        #region Product Cache Keys
        public static string ProductById(int productId) => $"product{SEPARATOR}id{SEPARATOR}{productId}";
        public static string ProductBySlug(string slug) => $"product{SEPARATOR}slug{SEPARATOR}{slug.ToLowerInvariant()}";
        public static string ProductBySku(string sku) => $"product{SEPARATOR}sku{SEPARATOR}{sku.ToLowerInvariant()}";
        public static string ProductsByCategory(int categoryId, int page, int pageSize) => $"product{SEPARATOR}category{SEPARATOR}{categoryId}{SEPARATOR}page{SEPARATOR}{page}{SEPARATOR}size{SEPARATOR}{pageSize}";
        public static string FeaturedProducts(int count) => $"product{SEPARATOR}featured{SEPARATOR}{count}";
        public static string RecentProducts(int count) => $"product{SEPARATOR}recent{SEPARATOR}{count}";
        public static string RelatedProducts(int productId, int count) => $"product{SEPARATOR}related{SEPARATOR}{productId}{SEPARATOR}{count}";
        public static string ProductStatistics => $"product{SEPARATOR}statistics";
        public static string ProductPriceRange => $"product{SEPARATOR}price_range";
        public static string ProductVendors => $"product{SEPARATOR}vendors";
        public static string ProductTags => $"product{SEPARATOR}tags";
        public static string ProductList(int page, int pageSize) => $"product{SEPARATOR}list{SEPARATOR}page{SEPARATOR}{page}{SEPARATOR}size{SEPARATOR}{pageSize}";
        public static string ProductSearch(string searchHash) => $"product{SEPARATOR}search{SEPARATOR}{searchHash}";
        public static string ProductPattern => "product:*";
        #endregion

        #region Category Cache Keys
        public static string CategoryById(int categoryId) => $"category{SEPARATOR}id{SEPARATOR}{categoryId}";
        public static string CategoryBySlug(string slug) => $"category{SEPARATOR}slug{SEPARATOR}{slug.ToLowerInvariant()}";
        public static string AllCategories => $"category{SEPARATOR}all";
        public static string CategoryTree => $"category{SEPARATOR}tree";
        public static string RootCategories => $"category{SEPARATOR}root";
        public static string SubCategories(int parentId) => $"category{SEPARATOR}sub{SEPARATOR}{parentId}";
        public static string CategoryWithProducts(int categoryId) => $"category{SEPARATOR}products{SEPARATOR}{categoryId}";
        public static string CategoryList(int page, int pageSize) => $"category{SEPARATOR}list{SEPARATOR}page{SEPARATOR}{page}{SEPARATOR}size{SEPARATOR}{pageSize}";
        public static string CategorySearch(string searchTerm, int page, int pageSize) => $"category{SEPARATOR}search{SEPARATOR}{GetSearchHash(searchTerm)}{SEPARATOR}{page}{SEPARATOR}{pageSize}";
        public static string CategoryPattern => "category:*";
        #endregion

        #region Company & Location Cache Keys
        public static string CompanyMain => $"company{SEPARATOR}main";
        public static string CompanyById(int companyId) => $"company{SEPARATOR}id{SEPARATOR}{companyId}";
        public static string LocationById(int locationId) => $"location{SEPARATOR}id{SEPARATOR}{locationId}";
        public static string MainLocation => $"location{SEPARATOR}main";
        public static string LocationsByCompany(int companyId) => $"location{SEPARATOR}company{SEPARATOR}{companyId}";
        public static string CompanyPattern => "company:*";
        public static string LocationPattern => "location:*";
        #endregion

        #region Permission & Session Cache Keys
        public static string AllPermissions => $"permission{SEPARATOR}all";
        public static string RolePermissions(string role) => $"permission{SEPARATOR}role{SEPARATOR}{role.ToLowerInvariant()}";
        public static string SessionById(string sessionId) => $"session{SEPARATOR}id{SEPARATOR}{sessionId}";
        public static string SessionByToken(string token) => $"session{SEPARATOR}token{SEPARATOR}{GetTokenHash(token)}";
        public static string PermissionPattern => "permission:*";
        public static string SessionPattern => "session:*";
        #endregion

        #region Search Cache Keys
        public static string SearchResults(string query, string entityTypes, bool publicOnly, int page, int pageSize) =>
            $"search{SEPARATOR}results{SEPARATOR}{GetSearchHash(query)}{SEPARATOR}{entityTypes}{SEPARATOR}{publicOnly}{SEPARATOR}{page}{SEPARATOR}{pageSize}";
        public static string SearchSuggestions(string query, int maxSuggestions) =>
            $"search{SEPARATOR}suggestions{SEPARATOR}{GetSearchHash(query)}{SEPARATOR}{maxSuggestions}";
        public static string IndexingStatus => $"search{SEPARATOR}indexing{SEPARATOR}status";
        public static string SearchPattern => "search:*";
        #endregion

        #region Designer & Preview Cache Keys
        public static string DesignerPage(int pageId) => $"designer{SEPARATOR}page{SEPARATOR}{pageId}";
        public static string DesignerState(int pageId, int userId) => $"designer{SEPARATOR}state{SEPARATOR}{pageId}{SEPARATOR}{userId}";
        public static string PreviewToken(string token) => $"preview{SEPARATOR}{GetTokenHash(token)}";
        public static string DesignerPattern => "designer:*";
        public static string PreviewPattern => "preview:*";
        #endregion

        #region Helper Methods
        private static string GetSearchHash(string? searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return "empty";

            // Simple hash for search terms
            var normalized = searchTerm.ToLowerInvariant().Trim();
            return normalized.Length <= 20 ? normalized : $"hash_{normalized.GetHashCode():x}";
        }

        private static string GetTokenHash(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return "empty";

            // Use last 8 characters for token identification (safe for logs)
            return token.Length <= 8 ? token : token[^8..];
        }

        /// <summary>
        /// Generate cache key with custom parameters
        /// </summary>
        public static string Custom(string prefix, params object[] identifiers)
        {
            var keyParts = new List<string> { prefix };
            keyParts.AddRange(identifiers.Where(id => id != null).Select(id => id.ToString()!));
            return string.Join(SEPARATOR, keyParts);
        }

        /// <summary>
        /// Generate pattern for bulk operations
        /// </summary>
        public static string Pattern(string prefix) => $"{prefix}:*";

        /// <summary>
        /// Get all available cache key patterns for monitoring
        /// </summary>
        public static IEnumerable<string> GetAllPatterns()
        {
            return new[]
            {
                UserPattern,
                PagePattern,
                FilePattern,
                FolderPattern,
                ProductPattern,
                CategoryPattern,
                CompanyPattern,
                LocationPattern,
                PermissionPattern,
                SessionPattern,
                SearchPattern,
                DesignerPattern,
                PreviewPattern
            };
        }

        /// <summary>
        /// Get entity-specific patterns for invalidation
        /// </summary>
        public static Dictionary<string, string> GetEntityPatterns()
        {
            return new Dictionary<string, string>
            {
                ["User"] = UserPattern,
                ["Page"] = PagePattern,
                ["File"] = FilePattern,
                ["Folder"] = FolderPattern,
                ["Product"] = ProductPattern,
                ["Category"] = CategoryPattern,
                ["Company"] = CompanyPattern,
                ["Location"] = LocationPattern,
                ["Permission"] = PermissionPattern,
                ["Session"] = SessionPattern
            };
        }
        #endregion
    }

    /// <summary>
    /// Cache extensions for easier usage in services
    /// </summary>
    public static class CacheExtensions
    {
        /// <summary>
        /// Get entity by ID with automatic key generation
        /// </summary>
        public static Task<T?> GetEntityAsync<T>(this ICacheService cache, ICacheKeyService keyService, object entityId, CancellationToken cancellationToken = default) where T : class
        {
            var key = keyService.GetEntityKey<T>(entityId);
            return cache.GetAsync<T>(key, cancellationToken);
        }

        /// <summary>
        /// Set entity by ID with automatic key generation
        /// </summary>
        public static Task SetEntityAsync<T>(this ICacheService cache, ICacheKeyService keyService, object entityId, T entity, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            var key = keyService.GetEntityKey<T>(entityId);
            return cache.SetAsync(key, entity, expiration, cancellationToken);
        }

        /// <summary>
        /// Remove entity by ID with automatic key generation
        /// </summary>
        public static Task RemoveEntityAsync<T>(this ICacheService cache, ICacheKeyService keyService, object entityId, CancellationToken cancellationToken = default)
        {
            var key = keyService.GetEntityKey<T>(entityId);
            return cache.RemoveAsync(key, cancellationToken);
        }

        /// <summary>
        /// Get or add entity with automatic key generation
        /// </summary>
        public static Task<T?> GetOrAddEntityAsync<T>(this ICacheService cache, ICacheKeyService keyService, object entityId, Func<Task<T?>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            var key = keyService.GetEntityKey<T>(entityId);
            return cache.GetOrAddAsync(key, factory, expiration, cancellationToken);
        }

        /// <summary>
        /// Get collection with automatic key generation
        /// </summary>
        public static Task<T?> GetCollectionAsync<T>(this ICacheService cache, ICacheKeyService keyService, string operation, object[] parameters, CancellationToken cancellationToken = default) where T : class
        {
            var key = keyService.GetCollectionKey<T>(operation, parameters);
            return cache.GetAsync<T>(key, cancellationToken);
        }

        /// <summary>
        /// Get or add collection with automatic key generation
        /// </summary>
        public static Task<T?> GetOrAddCollectionAsync<T>(this ICacheService cache, ICacheKeyService keyService, string operation, object[] parameters, Func<Task<T?>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            var key = keyService.GetCollectionKey<T>(operation, parameters);
            return cache.GetOrAddAsync(key, factory, expiration, cancellationToken);
        }
    }
}