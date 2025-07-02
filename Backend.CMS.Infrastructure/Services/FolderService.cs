using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class FolderService : IFolderService, IDisposable
    {
        private readonly FolderService _baseFolderService;
        private readonly ICacheService _cacheService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly ILogger<FolderService> _logger;
        private readonly IConfiguration _configuration;

        // Semaphore management for concurrent operations
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _folderSemaphores;
        private readonly Timer _semaphoreCleanupTimer;
        private readonly Lock _semaphoreLock = new();

        // Cache TTL configurations
        private readonly TimeSpan _folderMetadataCacheTTL;
        private readonly TimeSpan _folderListCacheTTL;
        private readonly TimeSpan _folderTreeCacheTTL;
        private readonly TimeSpan _folderSearchCacheTTL;
        private readonly TimeSpan _folderStatisticsCacheTTL;
        private readonly TimeSpan _folderValidationCacheTTL;
        private readonly TimeSpan _breadcrumbsCacheTTL;

        // Performance settings
        private readonly bool _enableAggressiveCaching;
        private readonly int _maxConcurrentOperations;

        private bool _disposed = false;

        public FolderService(
            FolderService baseFolderService,
            ICacheService cacheService,
            ICacheKeyService cacheKeyService,
            ICacheInvalidationService cacheInvalidationService,
            ILogger<FolderService> logger,
            IConfiguration configuration)
        {
            _baseFolderService = baseFolderService ?? throw new ArgumentNullException(nameof(baseFolderService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _cacheKeyService = cacheKeyService ?? throw new ArgumentNullException(nameof(cacheKeyService));
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Load cache TTL configurations
            _folderMetadataCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FolderStorage:CacheTTL:MetadataMinutes", 30));
            _folderListCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FolderStorage:CacheTTL:ListMinutes", 15));
            _folderTreeCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FolderStorage:CacheTTL:TreeMinutes", 20));
            _folderSearchCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FolderStorage:CacheTTL:SearchMinutes", 10));
            _folderStatisticsCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FolderStorage:CacheTTL:StatisticsMinutes", 30));
            _folderValidationCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FolderStorage:CacheTTL:ValidationMinutes", 5));
            _breadcrumbsCacheTTL = TimeSpan.FromMinutes(_configuration.GetValue("FolderStorage:CacheTTL:BreadcrumbsMinutes", 15));

            // Load performance settings
            _enableAggressiveCaching = _configuration.GetValue("FolderStorage:Performance:EnableAggressiveCaching", true);
            _maxConcurrentOperations = _configuration.GetValue("FolderStorage:Performance:MaxConcurrentOperations", Environment.ProcessorCount);

            // Initialize semaphores and cleanup
            _operationSemaphore = new SemaphoreSlim(_maxConcurrentOperations, _maxConcurrentOperations);
            _folderSemaphores = new ConcurrentDictionary<int, SemaphoreSlim>();

            // Cleanup unused semaphores every 10 minutes
            _semaphoreCleanupTimer = new Timer(CleanupUnusedSemaphores, null,
                TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

            _logger.LogInformation("EnterpriseCachedFolderService initialized with {MaxOperations} max concurrent operations",
                _maxConcurrentOperations);
        }

        #region Create/Update/Delete Operations (Invalidate cache)

        public async Task<FolderDto> CreateFolderAsync(CreateFolderDto createDto)
        {
            var result = await _baseFolderService.CreateFolderAsync(createDto);
            await InvalidateFolderRelatedCacheAsync(result.Id, result.ParentFolderId);
            await InvalidateGlobalFolderCacheAsync();
            return result;
        }

        public async Task<FolderDto> UpdateFolderAsync(int folderId, UpdateFolderDto updateDto)
        {
            // Get original folder info for cache invalidation
            FolderDto? originalFolder = null;
            try
            {
                originalFolder = await GetFolderByIdAsync(folderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get original folder info before update: {FolderId}", folderId);
            }

            var result = await _baseFolderService.UpdateFolderAsync(folderId, updateDto);

            await Task.WhenAll(
                InvalidateFolderRelatedCacheAsync(folderId, originalFolder?.ParentFolderId),
                InvalidateFolderRelatedCacheAsync(folderId, result.ParentFolderId),
                InvalidateGlobalFolderCacheAsync(),
                InvalidateFolderValidationCacheAsync()
            );

            return result;
        }

        public async Task<bool> DeleteFolderAsync(int folderId, bool deleteFiles = false)
        {
            // Get folder info before deletion
            FolderDto? folderInfo = null;
            try
            {
                folderInfo = await GetFolderByIdAsync(folderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get folder info before deletion: {FolderId}", folderId);
            }

            var result = await _baseFolderService.DeleteFolderAsync(folderId, deleteFiles);
            if (result)
            {
                await Task.WhenAll(
                    InvalidateFolderRelatedCacheAsync(folderId, folderInfo?.ParentFolderId),
                    InvalidateGlobalFolderCacheAsync(),
                    InvalidateFolderValidationCacheAsync()
                );
            }
            return result;
        }

        public async Task<FolderDto> MoveFolderAsync(MoveFolderDto moveDto)
        {
            // Get original folder info
            FolderDto? originalFolder = null;
            try
            {
                originalFolder = await GetFolderByIdAsync(moveDto.FolderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get original folder info before move: {FolderId}", moveDto.FolderId);
            }

            var result = await _baseFolderService.MoveFolderAsync(moveDto);

            // Invalidate cache for both old and new parent folders
            await Task.WhenAll(
                InvalidateFolderRelatedCacheAsync(moveDto.FolderId, originalFolder?.ParentFolderId),
                InvalidateFolderRelatedCacheAsync(moveDto.FolderId, moveDto.NewParentFolderId),
                InvalidateGlobalFolderCacheAsync(),
                InvalidateFolderValidationCacheAsync()
            );

            return result;
        }

        public async Task<bool> RenameFolderAsync(int folderId, string newName)
        {
            var result = await _baseFolderService.RenameFolderAsync(folderId, newName);
            if (result)
            {
                await Task.WhenAll(
                    InvalidateFolderMetadataCacheAsync(folderId),
                    InvalidateGlobalFolderCacheAsync(),
                    InvalidateFolderValidationCacheAsync()
                );
            }
            return result;
        }

        public async Task<FolderDto> CopyFolderAsync(int folderId, int? destinationFolderId, string? newName = null)
        {
            var result = await _baseFolderService.CopyFolderAsync(folderId, destinationFolderId, newName);
            await Task.WhenAll(
                InvalidateFolderRelatedCacheAsync(result.Id, destinationFolderId),
                InvalidateGlobalFolderCacheAsync()
            );
            return result;
        }

        #endregion

        #region Cached Read Operations

        public async Task<FolderDto> GetFolderByIdAsync(int folderId)
        {
            if (folderId <= 0)
                throw new ArgumentException("Folder ID must be greater than 0", nameof(folderId));

            var cacheKey = _cacheKeyService.GetEntityKey<FolderDto>(folderId);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for folder metadata: {FolderId}", folderId);
                return await _baseFolderService.GetFolderByIdAsync(folderId);
            }, _folderMetadataCacheTTL) ?? throw new InvalidOperationException($"Folder with ID {folderId} could not be retrieved.");
        }

        public async Task<List<FolderDto>> GetFoldersAsync(int? parentFolderId = null)
        {
            var cacheKey = _cacheKeyService.GetCollectionKey<FolderDto>("by_parent", parentFolderId ?? 0);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for folder list: parent {ParentFolderId}", parentFolderId);
                var folders = await _baseFolderService.GetFoldersAsync(parentFolderId);

                // Cache individual folder metadata for future single folder requests
                if (_enableAggressiveCaching && folders.Any())
                {
                    await CacheFolderMetadataListAsync(folders);
                }

                return folders;
            }, _folderListCacheTTL) ?? new List<FolderDto>();
        }

        public async Task<FolderTreeDto> GetFolderTreeAsync(int? rootFolderId = null)
        {
            var cacheKey = _cacheKeyService.GetCollectionKey<FolderTreeDto>("tree", rootFolderId ?? 0);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for folder tree: root {RootFolderId}", rootFolderId);
                return await _baseFolderService.GetFolderTreeAsync(rootFolderId);
            }, _folderTreeCacheTTL) ?? new FolderTreeDto();
        }

        public async Task<List<FolderDto>> SearchFoldersAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<FolderDto>();

            var searchHash = GenerateSearchHash(searchTerm);
            var cacheKey = _cacheKeyService.GetQueryKey<FolderDto>("search", new { Hash = searchHash });

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for folder search: {SearchTerm}", searchTerm);
                var folders = await _baseFolderService.SearchFoldersAsync(searchTerm);

                // Cache individual folder metadata
                if (_enableAggressiveCaching && folders.Any())
                {
                    await CacheFolderMetadataListAsync(folders, TimeSpan.FromMinutes(5));
                }

                return folders;
            }, _folderSearchCacheTTL) ?? new List<FolderDto>();
        }

        public async Task<string> GetFolderPathAsync(int folderId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("folder_path", folderId);

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var path = await _baseFolderService.GetFolderPathAsync(folderId);
                return new { Path = path };
            }, _folderMetadataCacheTTL);

            return result?.Path ?? string.Empty;
        }

        public async Task<List<FolderDto>> GetFolderBreadcrumbsAsync(int folderId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("folder_breadcrumbs", folderId);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for folder breadcrumbs: {FolderId}", folderId);
                return await _baseFolderService.GetFolderBreadcrumbsAsync(folderId);
            }, _breadcrumbsCacheTTL) ?? new List<FolderDto>();
        }

        public async Task<FolderDto?> GetFolderByPathAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var cacheKey = _cacheKeyService.GetCustomKey("folder_by_path", path.ToLowerInvariant());

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for folder by path: {Path}", path);
                return await _baseFolderService.GetFolderByPathAsync(path);
            }, _folderMetadataCacheTTL);
        }

        public async Task<Dictionary<string, object>> GetFolderStatisticsAsync(int folderId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("folder_statistics", folderId);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for folder statistics: {FolderId}", folderId);
                return await _baseFolderService.GetFolderStatisticsAsync(folderId);
            }, _folderStatisticsCacheTTL) ?? new Dictionary<string, object>();
        }

        #endregion

        #region Cached Validation Operations

        public async Task<bool> FolderExistsAsync(int folderId)
        {
            if (folderId <= 0)
                return false;

            var cacheKey = _cacheKeyService.GetCustomKey("folder_exists", folderId);

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var exists = await _baseFolderService.FolderExistsAsync(folderId);
                return new { Exists = exists };
            }, _folderValidationCacheTTL);

            var exists = result?.Exists ?? false;
            _logger.LogDebug("Folder existence check for {FolderId}: {Exists}", folderId, exists);

            return exists;
        }

        public async Task<bool> ValidateFolderNameAsync(string name, int? parentFolderId = null, int? excludeFolderId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var cacheKey = _cacheKeyService.GetCustomKey("folder_name_validation",
                name.ToLowerInvariant(), parentFolderId ?? 0, excludeFolderId ?? 0);

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var isValid = await _baseFolderService.ValidateFolderNameAsync(name, parentFolderId, excludeFolderId);
                return new { IsValid = isValid };
            }, _folderValidationCacheTTL);

            var isValid = result?.IsValid ?? false;
            _logger.LogDebug("Folder name validation for '{Name}' in parent {ParentId}: {IsValid}",
                name, parentFolderId, isValid);

            return isValid;
        }

        public async Task<bool> IsSubFolderOfAsync(int childFolderId, int parentFolderId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("folder_hierarchy_check", childFolderId, parentFolderId);

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var isSubFolder = await _baseFolderService.IsSubFolderOfAsync(childFolderId, parentFolderId);
                return new { IsSubFolder = isSubFolder };
            }, _folderValidationCacheTTL);

            return result?.IsSubFolder ?? false;
        }

        #endregion

        #region System Folder Operations (Cached)

        public async Task<FolderDto> GetOrCreateSystemFolderAsync(FolderType folderType)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("system_folder", folderType.ToString());

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for system folder: {FolderType}", folderType);
                return await _baseFolderService.GetOrCreateSystemFolderAsync(folderType);
            }, _folderMetadataCacheTTL) ?? throw new InvalidOperationException($"System folder {folderType} could not be created.");
        }

        public async Task<FolderDto> GetUserAvatarFolderAsync(int userId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("user_avatar_folder", userId);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for user avatar folder: {UserId}", userId);
                return await _baseFolderService.GetUserAvatarFolderAsync(userId);
            }, _folderMetadataCacheTTL) ?? throw new InvalidOperationException($"User avatar folder for user {userId} could not be created.");
        }

        public async Task<FolderDto> GetCompanyAssetsFolderAsync()
        {
            var cacheKey = _cacheKeyService.GetCustomKey("company_assets_folder");

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for company assets folder");
                return await _baseFolderService.GetCompanyAssetsFolderAsync();
            }, _folderMetadataCacheTTL) ?? throw new InvalidOperationException("Company assets folder could not be created.");
        }

        #endregion

        #region Cache Helper Methods

        private async Task CacheFolderMetadataListAsync(IEnumerable<FolderDto> folders, TimeSpan? customTtl = null)
        {
            if (!_enableAggressiveCaching || !folders.Any())
                return;

            var ttl = customTtl ?? _folderMetadataCacheTTL;
            var cachingTasks = folders.Select(async folder =>
            {
                try
                {
                    var cacheKey = _cacheKeyService.GetEntityKey<FolderDto>(folder.Id);
                    await _cacheService.SetAsync(cacheKey, folder, ttl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache metadata for folder {FolderId}", folder.Id);
                }
            });

            await Task.WhenAll(cachingTasks);
            _logger.LogDebug("Cached metadata for {Count} folders", folders.Count());
        }

        private string GenerateSearchHash(string searchTerm)
        {
            var normalizedTerm = searchTerm.ToLowerInvariant().Trim();
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalizedTerm));
            return Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-").TrimEnd('=')[..12];
        }

        private async Task InvalidateFolderRelatedCacheAsync(int folderId, int? parentFolderId)
        {
            try
            {
                var invalidationTasks = new List<Task>
                {
                    InvalidateFolderMetadataCacheAsync(folderId),
                    InvalidateFolderHierarchyCacheAsync(folderId),
                    InvalidateGlobalFolderCacheAsync()
                };

                if (parentFolderId.HasValue)
                {
                    invalidationTasks.Add(InvalidateParentFolderCacheAsync(parentFolderId.Value));
                }

                await Task.WhenAll(invalidationTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache for folder {FolderId}", folderId);
            }
        }

        private async Task InvalidateFolderMetadataCacheAsync(int folderId)
        {
            try
            {
                var cacheKeys = new[]
                {
                    _cacheKeyService.GetEntityKey<FolderDto>(folderId),
                    _cacheKeyService.GetCustomKey("folder_path", folderId),
                    _cacheKeyService.GetCustomKey("folder_breadcrumbs", folderId),
                    _cacheKeyService.GetCustomKey("folder_statistics", folderId),
                    _cacheKeyService.GetCustomKey("folder_exists", folderId)
                };

                await _cacheService.RemoveAsync(cacheKeys);
                _logger.LogDebug("Invalidated metadata cache for folder {FolderId}", folderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate metadata cache for folder {FolderId}", folderId);
            }
        }

        private async Task InvalidateFolderHierarchyCacheAsync(int folderId)
        {
            try
            {
                // Invalidate hierarchy-related cache patterns
                await Task.WhenAll(
                    _cacheService.RemoveByPatternAsync($"folder:*:hierarchy_check:*:{folderId}"),
                    _cacheService.RemoveByPatternAsync($"folder:*:hierarchy_check:{folderId}:*"),
                    _cacheService.RemoveByPatternAsync("folder:*:breadcrumbs:*"),
                    _cacheService.RemoveByPatternAsync("folder:*:tree:*")
                );

                _logger.LogDebug("Invalidated hierarchy cache for folder {FolderId}", folderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate hierarchy cache for folder {FolderId}", folderId);
            }
        }

        private async Task InvalidateParentFolderCacheAsync(int parentFolderId)
        {
            try
            {
                // Invalidate parent folder's children list
                await Task.WhenAll(
                    _cacheService.RemoveAsync(_cacheKeyService.GetCollectionKey<FolderDto>("by_parent", parentFolderId)),
                    _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("folder_statistics", parentFolderId)),
                    InvalidateFolderMetadataCacheAsync(parentFolderId)
                );

                _logger.LogDebug("Invalidated parent folder cache: {ParentFolderId}", parentFolderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate parent folder cache: {ParentFolderId}", parentFolderId);
            }
        }

        private async Task InvalidateGlobalFolderCacheAsync()
        {
            try
            {
                var patterns = new[]
                {
                    "folder:list:*",
                    "folder:tree:*",
                    "folder:query:search:*"
                };

                var invalidationTasks = patterns.Select(pattern => _cacheService.RemoveByPatternAsync(pattern));
                await Task.WhenAll(invalidationTasks);

                _logger.LogDebug("Invalidated global folder cache");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate global folder cache");
            }
        }

        private async Task InvalidateFolderValidationCacheAsync()
        {
            try
            {
                await Task.WhenAll(
                    _cacheService.RemoveByPatternAsync("folder:*:name_validation:*"),
                    _cacheService.RemoveByPatternAsync("folder:*:hierarchy_check:*")
                );

                _logger.LogDebug("Invalidated folder validation cache");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate folder validation cache");
            }
        }

        private SemaphoreSlim GetOrCreateFolderSemaphore(int folderId)
        {
            return _folderSemaphores.GetOrAdd(folderId, _ => new SemaphoreSlim(1, 1));
        }

        private void CleanupUnusedSemaphores(object? state)
        {
            lock (_semaphoreLock)
            {
                try
                {
                    var keysToRemove = new List<int>();

                    foreach (var kvp in _folderSemaphores)
                    {
                        var semaphore = kvp.Value;
                        // If semaphore is not being used, remove it
                        if (semaphore.CurrentCount == 1) // No one is waiting
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }

                    foreach (var key in keysToRemove.Take(100)) // Limit cleanup
                    {
                        if (_folderSemaphores.TryRemove(key, out var semaphore))
                        {
                            semaphore.Dispose();
                        }
                    }

                    if (keysToRemove.Count > 0)
                    {
                        _logger.LogDebug("Cleaned up {Count} unused folder semaphores",
                            Math.Min(keysToRemove.Count, 100));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during semaphore cleanup");
                }
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _semaphoreCleanupTimer?.Dispose();
                _operationSemaphore?.Dispose();

                // Dispose all folder semaphores
                foreach (var semaphore in _folderSemaphores.Values)
                {
                    semaphore.Dispose();
                }
                _folderSemaphores.Clear();

                // Dispose base service if it implements IDisposable
                if (_baseFolderService is IDisposable disposableService)
                {
                    disposableService.Dispose();
                }

                _disposed = true;
            }
        }

        #endregion
    }
}