namespace Backend.CMS.Infrastructure.Caching
{
    public static class CacheKeys
    {
        public const string USER_PREFIX = "user";
        public const string PAGE_PREFIX = "page";
        public const string COMPONENT_PREFIX = "component";
        public const string COMPANY_PREFIX = "company";
        public const string LOCATION_PREFIX = "location";
        public const string PERMISSION_PREFIX = "permission";
        public const string FILE_PREFIX = "file";
        public const string FOLDER_PREFIX = "folder";
        public const string CATEGORY_PREFIX = "category";
        public const string PRODUCT_PREFIX = "product";
        public const string PRODUCT_VARIANT_PREFIX = "variant";


        #region User Cache Keys
        public static string UserById(int userId) => $"{USER_PREFIX}:id:{userId}";
        public static string UserByEmail(string email) => $"{USER_PREFIX}:email:{email}";
        public static string UserByUsername(string username) => $"{USER_PREFIX}:username:{username}";
        public static string UserPermissions(int userId) => $"{USER_PREFIX}:permissions:{userId}";
        public static string UsersPattern => $"{USER_PREFIX}:*";
        public static string UserSessions(int userId) => $"user:sessions:{userId}";

        #endregion

        #region Page Cache Keys

        public static string PageById(int pageId) => $"{PAGE_PREFIX}:id:{pageId}";
        public static string PageBySlug(string slug) => $"{PAGE_PREFIX}:slug:{slug}";
        public static string PublishedPages => $"{PAGE_PREFIX}:published";
        public static string PageHierarchy => $"{PAGE_PREFIX}:hierarchy";
        public static string PagesByParent(int parentId) => $"{PAGE_PREFIX}:parent:{parentId}";
        public static string PagesPattern => $"{PAGE_PREFIX}:*";
        public static string PageWithComponents(int pageId) => $"page:components:{pageId}";
        public static string PageVersions(int pageId) => $"page:versions:{pageId}";
        public static string PagesSearch(string searchTerm, int page, int pageSize) =>
            $"pages:search:{searchTerm?.ToLowerInvariant()}:{page}:{pageSize}";

        #endregion

        #region Designer Cache Keys

        public static string DesignerPage(int pageId) => $"designer:page:{pageId}";
        public static string DesignerState(int pageId, int userId) => $"designer:state:{pageId}:{userId}";
        public static string PreviewToken(string token) => $"preview:{token}";
        public static string DesignerPattern => "designer:*";

        #endregion

        #region Component Cache Keys
        public static string ComponentTemplate(int templateId) => $"{COMPONENT_PREFIX}:template:{templateId}";
        public static string ComponentTemplates => $"{COMPONENT_PREFIX}:templates";
        public static string ComponentLibrary => $"{COMPONENT_PREFIX}:library";
        public static string ComponentsByType(string type) => $"{COMPONENT_PREFIX}:type:{type}";
        public static string ComponentsByCategory(string category) => $"{COMPONENT_PREFIX}:category:{category}";
        public static string ComponentsPattern => $"{COMPONENT_PREFIX}:*";

        #endregion

        #region Company Cache Keys
        public static string Company => $"{COMPANY_PREFIX}:main";
        public static string CompanyPattern => $"{COMPANY_PREFIX}:*";
        #endregion

        #region Location Cache Keys
        public static string LocationById(int locationId) => $"{LOCATION_PREFIX}:id:{locationId}";
        public static string MainLocation => $"{LOCATION_PREFIX}:main";
        public static string LocationsByCompany(int companyId) => $"{LOCATION_PREFIX}:company:{companyId}";
        public static string LocationsPattern => $"{LOCATION_PREFIX}:*";

        #endregion

        #region File Cache Keys
        public static string FileById(int fileId) => $"{FILE_PREFIX}:id:{fileId}";
        public static string FilesByFolder(int folderId) => $"{FILE_PREFIX}:folder:{folderId}";
        public static string RecentFiles => $"{FILE_PREFIX}:recent";
        public static string FileStatistics => $"{FILE_PREFIX}:statistics";
        public static string FilesPattern => $"{FILE_PREFIX}:*";

        #endregion

        #region Folder Cache Keys
        public static string FolderById(int folderId) => $"{FOLDER_PREFIX}:id:{folderId}";
        public static string FolderTree => $"{FOLDER_PREFIX}:tree";
        public static string FoldersByParent(int parentId) => $"{FOLDER_PREFIX}:parent:{parentId}";
        public static string FoldersPattern => $"{FOLDER_PREFIX}:*";

        #endregion

        #region Search Cache Keys

        public static string SearchResults(string query, string entityTypes, bool publicOnly, int page, int pageSize) =>
            $"search:{query?.ToLowerInvariant()}:{entityTypes}:{publicOnly}:{page}:{pageSize}";
        public static string SearchSuggestions(string query, int maxSuggestions) =>
            $"suggestions:{query?.ToLowerInvariant()}:{maxSuggestions}";
        public static string IndexingStatus => "search:indexing:status";
        public static string SearchPattern => "search:*";

        #endregion

        #region Category Cache Keys
        public static string CategoryById(int categoryId) => $"{CATEGORY_PREFIX}:id:{categoryId}";
        public static string CategoryBySlug(string slug) => $"{CATEGORY_PREFIX}:slug:{slug}";
        public static string AllCategories => $"{CATEGORY_PREFIX}:all";
        public static string CategoryTree => $"{CATEGORY_PREFIX}:tree";
        public static string RootCategories => $"{CATEGORY_PREFIX}:root";
        public static string SubCategories(int parentId) => $"{CATEGORY_PREFIX}:sub:{parentId}";
        public static string CategoriesPattern => $"{CATEGORY_PREFIX}:*";
        #endregion

        #region Product Cache Keys 
        public static string ProductById(int id) => $"product:id:{id}";
        public static string ProductBySlug(string slug) => $"product:slug:{slug}";
        public static string ProductsList(int page, int pageSize) => $"products:list:page:{page}:size:{pageSize}";
        public static string ProductsByCategory(int categoryId, int page, int pageSize) => $"products:category:{categoryId}:page:{page}:size:{pageSize}";
        public static string FeaturedProducts(int count) => $"products:featured:count:{count}";
        public static string RelatedProducts(int productId, int count) => $"products:related:{productId}:count:{count}";
        public static string RecentProducts(int count) => $"products:recent:count:{count}";
        public static string ProductStatistics => "products:statistics";
        public static string ProductPriceRange => "products:price-range";
        public static string ProductVendors => "products:vendors";
        public static string ProductTags => "products:tags";
        public const string ProductsPattern = "products:*";

        #endregion

        #region Permission Cache Keys
        public static string AllPermissions => $"{PERMISSION_PREFIX}:all";
        public static string RolePermissions(string role) => $"{PERMISSION_PREFIX}:role:{role}";
        public static string PermissionsPattern => $"{PERMISSION_PREFIX}:*";
        #endregion

        #region Session Cache Keys

        public static string SessionById(string sessionId) => $"session:id:{sessionId}";
        public static string SessionByToken(string token) => $"session:token:{token}";
        public static string SessionsPattern => "session:*";

        #endregion

        #region ProductVariant Cache Keys

        public static string ProductVariantById(int id) => $"product-variant:id:{id}";
        public static string ProductVariantBySKU(string sku) => $"product-variant:sku:{sku}";
        public static string ProductVariantsByProduct(int productId) => $"product-variants:product:{productId}";
        public static string ProductDefaultVariant(int productId) => $"product-variant:default:{productId}";
        public static string ProductVariantsPattern(int productId) => $"product-variants:product:{productId}:*";

        #endregion

        #region Generic Helper Methods

        /// <summary>
        /// Generate a cache key with custom prefix and identifiers
        /// </summary>
        public static string Custom(string prefix, params object[] identifiers)
        {
            var keyParts = new List<string> { prefix };
            keyParts.AddRange(identifiers.Select(id => id?.ToString() ?? "null"));
            return string.Join(":", keyParts);
        }

        /// <summary>
        /// Generate a pattern for batch operations
        /// </summary>
        public static string Pattern(string prefix) => $"{prefix}:*";

        #endregion
    }
}