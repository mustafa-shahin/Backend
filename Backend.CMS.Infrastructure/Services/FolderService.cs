using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace Backend.CMS.Infrastructure.Services
{
    public class FolderService : IFolderService, IDisposable
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserSessionService _userSessionService;
        private readonly ICacheService _cacheService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly IMapper _mapper;
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

        // Pagination settings
        private readonly int _defaultPageSize;
        private readonly int _maxPageSize;

        private bool _disposed = false;

        public FolderService(
            IUnitOfWork unitOfWork,
            IUserSessionService userSessionService,
            ICacheService cacheService,
            ICacheKeyService cacheKeyService,
            ICacheInvalidationService cacheInvalidationService,
            IMapper mapper,
            ILogger<FolderService> logger,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _cacheKeyService = cacheKeyService ?? throw new ArgumentNullException(nameof(cacheKeyService));
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
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

            // Load pagination settings
            _defaultPageSize = _configuration.GetValue("Pagination:DefaultPageSize", 10);
            _maxPageSize = _configuration.GetValue("Pagination:MaxPageSize", 100);

            // Initialize semaphores and cleanup
            _operationSemaphore = new SemaphoreSlim(_maxConcurrentOperations, _maxConcurrentOperations);
            _folderSemaphores = new ConcurrentDictionary<int, SemaphoreSlim>();

            // Cleanup unused semaphores every 10 minutes
            _semaphoreCleanupTimer = new Timer(CleanupUnusedSemaphores, null,
                TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        }

        #region Paginated Operations (Core Service Logic)

        public async Task<PagedResult<FolderDto>> GetFoldersPagedAsync(int? parentFolderId = null, int pageNumber = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize <= 0 ? _defaultPageSize : pageSize, 1, _maxPageSize);

            var cacheKey = _cacheKeyService.GetCollectionKey<FolderDto>("paged", parentFolderId ?? 0, pageNumber, pageSize);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for paginated folders: parent {ParentFolderId}, page {PageNumber}, size {PageSize}",
                    parentFolderId, pageNumber, pageSize);

                await _operationSemaphore.WaitAsync();
                try
                {
                    // Get all folders for the parent (this will be optimized with repository-level pagination in production)
                    var allFolders = parentFolderId.HasValue
                        ? await _unitOfWork.Folders.GetSubFoldersAsync(parentFolderId.Value)
                        : await _unitOfWork.Folders.GetRootFoldersAsync();

                    var totalCount = allFolders.Count();

                    // Apply server-side pagination
                    var folders = allFolders
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    // Map to DTOs with enhanced metadata
                    var folderDtos = await MapFoldersToDto(folders);

                    // Create paginated result
                    var result = new PagedResult<FolderDto>(folderDtos, pageNumber, pageSize, totalCount);

                    // Cache individual folder metadata for future single folder requests
                    if (_enableAggressiveCaching && folderDtos.Any())
                    {
                        await CacheFolderMetadataListAsync(folderDtos);
                    }

                    return result;
                }
                finally
                {
                    _operationSemaphore.Release();
                }
            }, _folderListCacheTTL) ?? PagedResult<FolderDto>.Empty(pageNumber, pageSize);
        }

        public async Task<PagedResult<FolderDto>> SearchFoldersPagedAsync(string searchTerm, int pageNumber = 1, int pageSize = 10)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return PagedResult<FolderDto>.Empty(pageNumber, pageSize);

            // Validate and normalize pagination parameters
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize <= 0 ? _defaultPageSize : pageSize, 1, _maxPageSize);

            var searchHash = GenerateSearchHash(searchTerm);
            var cacheKey = _cacheKeyService.GetQueryKey<FolderDto>("search_paged", new { Hash = searchHash, PageNumber = pageNumber, PageSize = pageSize });

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for paginated folder search: {SearchTerm}, page {PageNumber}, size {PageSize}",
                    searchTerm, pageNumber, pageSize);

                await _operationSemaphore.WaitAsync();
                try
                {
                    // Get all matching folders
                    var allFolders = await _unitOfWork.Folders.SearchFoldersByNameAsync(searchTerm);
                    var totalCount = allFolders.Count();

                    // Apply server-side pagination
                    var folders = allFolders
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    // Map to DTOs
                    var folderDtos = await MapFoldersToDto(folders);

                    // Create paginated result
                    var result = new PagedResult<FolderDto>(folderDtos, pageNumber, pageSize, totalCount);

                    // Cache individual folder metadata
                    if (_enableAggressiveCaching && folderDtos.Any())
                    {
                        await CacheFolderMetadataListAsync(folderDtos, TimeSpan.FromMinutes(5));
                    }

                    return result;
                }
                finally
                {
                    _operationSemaphore.Release();
                }
            }, _folderSearchCacheTTL) ?? PagedResult<FolderDto>.Empty(pageNumber, pageSize);
        }

        #endregion

        #region Create/Update/Delete Operations (Invalidate cache)

        public async Task<FolderDto> CreateFolderAsync(CreateFolderDto createDto)
        {
            if (createDto == null)
                throw new ArgumentNullException(nameof(createDto));

            var currentUserId = _userSessionService.GetCurrentUserId();

            var folder = _mapper.Map<Folder>(createDto);
            folder.CreatedByUserId = currentUserId;
            folder.UpdatedByUserId = currentUserId;

            // Generate unique path
            folder.Path = await GenerateUniqueFolderPathAsync(createDto.Name, createDto.ParentFolderId);

            await _unitOfWork.Folders.AddAsync(folder);
            await _unitOfWork.Folders.SaveChangesAsync();

            var result = _mapper.Map<FolderDto>(folder);
            await PopulateFolderStatistics(result);

            // Invalidate related cache
            await InvalidateFolderRelatedCacheAsync(result.Id, result.ParentFolderId);
            await InvalidateGlobalFolderCacheAsync();

            return result;
        }

        public async Task<FolderDto> UpdateFolderAsync(int folderId, UpdateFolderDto updateDto)
        {
            if (updateDto == null)
                throw new ArgumentNullException(nameof(updateDto));

            var folder = await _unitOfWork.Folders.GetByIdAsync(folderId);
            if (folder == null)
                throw new ArgumentException("Folder not found");

            var originalParentId = folder.ParentFolderId;
            var currentUserId = _userSessionService.GetCurrentUserId();

            _mapper.Map(updateDto, folder);
            folder.UpdatedAt = DateTime.UtcNow;
            folder.UpdatedByUserId = currentUserId;

            _unitOfWork.Folders.Update(folder);
            await _unitOfWork.Folders.SaveChangesAsync();

            var result = _mapper.Map<FolderDto>(folder);
            await PopulateFolderStatistics(result);

            // Invalidate cache for both old and new parent folders
            await Task.WhenAll(
                InvalidateFolderRelatedCacheAsync(folderId, originalParentId),
                InvalidateFolderRelatedCacheAsync(folderId, result.ParentFolderId),
                InvalidateGlobalFolderCacheAsync(),
                InvalidateFolderValidationCacheAsync()
            );

            return result;
        }

        public async Task<bool> DeleteFolderAsync(int folderId, bool deleteFiles = false)
        {
            var folder = await _unitOfWork.Folders.GetByIdAsync(folderId);
            if (folder == null)
                return false;

            var parentFolderId = folder.ParentFolderId;
            var currentUserId = _userSessionService.GetCurrentUserId();

            // Check if folder can be deleted
            if (!deleteFiles && (await _unitOfWork.Folders.HasFilesAsync(folderId) || await _unitOfWork.Folders.HasSubFoldersAsync(folderId)))
            {
                throw new InvalidOperationException("Cannot delete folder that contains files or subfolders unless deleteFiles is true");
            }

            var success = await _unitOfWork.Folders.SoftDeleteAsync(folderId, currentUserId);
            if (success)
            {
                await Task.WhenAll(
                    InvalidateFolderRelatedCacheAsync(folderId, parentFolderId),
                    InvalidateGlobalFolderCacheAsync(),
                    InvalidateFolderValidationCacheAsync()
                );
            }

            return success;
        }

        public async Task<FolderDto> MoveFolderAsync(MoveFolderDto moveDto)
        {
            if (moveDto == null)
                throw new ArgumentNullException(nameof(moveDto));

            var folder = await _unitOfWork.Folders.GetByIdAsync(moveDto.FolderId);
            if (folder == null)
                throw new ArgumentException("Folder not found");

            var originalParentId = folder.ParentFolderId;

            // Prevent moving folder to its own descendant
            if (moveDto.NewParentFolderId.HasValue && await _unitOfWork.Folders.IsDescendantOfAsync(moveDto.NewParentFolderId.Value, moveDto.FolderId))
            {
                throw new InvalidOperationException("Cannot move folder to its own descendant");
            }

            var success = await _unitOfWork.Folders.MoveFolderAsync(moveDto.FolderId, moveDto.NewParentFolderId);
            if (!success)
                throw new InvalidOperationException("Failed to move folder");

            var updatedFolder = await _unitOfWork.Folders.GetByIdAsync(moveDto.FolderId);
            var result = _mapper.Map<FolderDto>(updatedFolder!);
            await PopulateFolderStatistics(result);

            // Invalidate cache for both old and new parent folders
            await Task.WhenAll(
                InvalidateFolderRelatedCacheAsync(moveDto.FolderId, originalParentId),
                InvalidateFolderRelatedCacheAsync(moveDto.FolderId, moveDto.NewParentFolderId),
                InvalidateGlobalFolderCacheAsync(),
                InvalidateFolderValidationCacheAsync()
            );

            return result;
        }

        public async Task<bool> RenameFolderAsync(int folderId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return false;

            var folder = await _unitOfWork.Folders.GetByIdAsync(folderId);
            if (folder == null)
                return false;

            var currentUserId = _userSessionService.GetCurrentUserId();

            folder.Name = newName.Trim();
            folder.UpdatedAt = DateTime.UtcNow;
            folder.UpdatedByUserId = currentUserId;

            // Update path
            folder.Path = await GenerateUniqueFolderPathAsync(newName, folder.ParentFolderId);

            _unitOfWork.Folders.Update(folder);
            await _unitOfWork.Folders.SaveChangesAsync();

            await Task.WhenAll(
                InvalidateFolderMetadataCacheAsync(folderId),
                InvalidateGlobalFolderCacheAsync(),
                InvalidateFolderValidationCacheAsync()
            );

            return true;
        }

        public async Task<FolderDto> CopyFolderAsync(int folderId, int? destinationFolderId, string? newName = null)
        {
            var originalFolder = await _unitOfWork.Folders.GetByIdAsync(folderId);
            if (originalFolder == null)
                throw new ArgumentException("Folder not found");

            var currentUserId = _userSessionService.GetCurrentUserId();
            var folderName = newName ?? $"Copy of {originalFolder.Name}";

            var newFolder = new Folder
            {
                Name = folderName,
                Description = originalFolder.Description,
                ParentFolderId = destinationFolderId,
                IsPublic = originalFolder.IsPublic,
                FolderType = originalFolder.FolderType,
                Metadata = originalFolder.Metadata,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            newFolder.Path = await GenerateUniqueFolderPathAsync(folderName, destinationFolderId);

            await _unitOfWork.Folders.AddAsync(newFolder);
            await _unitOfWork.Folders.SaveChangesAsync();

            var result = _mapper.Map<FolderDto>(newFolder);
            await PopulateFolderStatistics(result);

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

                var folder = await _unitOfWork.Folders.GetByIdAsync(folderId);
                if (folder == null)
                    return null;

                var dto = _mapper.Map<FolderDto>(folder);
                await PopulateFolderStatistics(dto);
                return dto;
            }, _folderMetadataCacheTTL) ?? throw new InvalidOperationException($"Folder with ID {folderId} could not be retrieved.");
        }

        public async Task<List<FolderDto>> GetFoldersAsync(int? parentFolderId = null)
        {
            var cacheKey = _cacheKeyService.GetCollectionKey<FolderDto>("by_parent", parentFolderId ?? 0);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for folder list: parent {ParentFolderId}", parentFolderId);

                var folders = parentFolderId.HasValue
                    ? await _unitOfWork.Folders.GetSubFoldersAsync(parentFolderId.Value)
                    : await _unitOfWork.Folders.GetRootFoldersAsync();

                var folderDtos = await MapFoldersToDto(folders);

                // Cache individual folder metadata for future single folder requests
                if (_enableAggressiveCaching && folderDtos.Any())
                {
                    await CacheFolderMetadataListAsync(folderDtos);
                }

                return folderDtos;
            }, _folderListCacheTTL) ?? new List<FolderDto>();
        }

        public async Task<FolderTreeDto> GetFolderTreeAsync(int? rootFolderId = null)
        {
            var cacheKey = _cacheKeyService.GetCollectionKey<FolderTreeDto>("tree", rootFolderId ?? 0);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for folder tree: root {RootFolderId}", rootFolderId);
                return await BuildFolderTreeAsync(rootFolderId);
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

                var folders = await _unitOfWork.Folders.SearchFoldersByNameAsync(searchTerm);
                var folderDtos = await MapFoldersToDto(folders);

                // Cache individual folder metadata
                if (_enableAggressiveCaching && folderDtos.Any())
                {
                    await CacheFolderMetadataListAsync(folderDtos, TimeSpan.FromMinutes(5));
                }

                return folderDtos;
            }, _folderSearchCacheTTL) ?? new List<FolderDto>();
        }

        public async Task<string> GetFolderPathAsync(int folderId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("folder_path", folderId);

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var folder = await _unitOfWork.Folders.GetByIdAsync(folderId);
                return new StringWrapper(folder?.Path ?? string.Empty);
            }, _folderMetadataCacheTTL);

            return result?.Value ?? string.Empty;
        }

        public async Task<List<FolderDto>> GetFolderBreadcrumbsAsync(int folderId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("folder_breadcrumbs", folderId);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for folder breadcrumbs: {FolderId}", folderId);

                var ancestors = await _unitOfWork.Folders.GetAncestorsAsync(folderId);
                return await MapFoldersToDto(ancestors);
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

                var folder = await _unitOfWork.Folders.GetByPathAsync(path);
                if (folder == null)
                    return null;

                var dto = _mapper.Map<FolderDto>(folder);
                await PopulateFolderStatistics(dto);
                return dto;
            }, _folderMetadataCacheTTL);
        }

        public async Task<Dictionary<string, object>> GetFolderStatisticsAsync(int folderId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("folder_statistics", folderId);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for folder statistics: {FolderId}", folderId);

                var fileCount = await _unitOfWork.Folders.GetTotalFileCountAsync(folderId, includeSubfolders: false);
                var subFolderCount = (await _unitOfWork.Folders.GetSubFoldersAsync(folderId)).Count();
                var totalSize = await _unitOfWork.Folders.GetTotalSizeAsync(folderId, includeSubfolders: false);
                var totalSizeWithSubfolders = await _unitOfWork.Folders.GetTotalSizeAsync(folderId, includeSubfolders: true);
                var depth = await _unitOfWork.Folders.GetDepthAsync(folderId);

                return new Dictionary<string, object>
                {
                    ["fileCount"] = fileCount,
                    ["subFolderCount"] = subFolderCount,
                    ["totalSize"] = totalSize,
                    ["totalSizeWithSubfolders"] = totalSizeWithSubfolders,
                    ["totalSizeFormatted"] = FormatFileSize(totalSize),
                    ["totalSizeWithSubfoldersFormatted"] = FormatFileSize(totalSizeWithSubfolders),
                    ["depth"] = depth,
                    ["generatedAt"] = DateTime.UtcNow
                };
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
                var exists = await _unitOfWork.Folders.AnyAsync(f => f.Id == folderId);
                return new BoolWrapper(exists);
            }, _folderValidationCacheTTL);

            var exists = result?.Value ?? false;
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
                var isValid = await _unitOfWork.Folders.IsPathUniqueAsync(name, excludeFolderId);
                return new BoolWrapper(isValid);
            }, _folderValidationCacheTTL);

            var isValid = result?.Value ?? false;
            _logger.LogDebug("Folder name validation for '{Name}' in parent {ParentId}: {IsValid}",
                name, parentFolderId, isValid);

            return isValid;
        }

        public async Task<bool> IsSubFolderOfAsync(int childFolderId, int parentFolderId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("folder_hierarchy_check", childFolderId, parentFolderId);

            var result = await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                var isSubFolder = await _unitOfWork.Folders.IsDescendantOfAsync(childFolderId, parentFolderId);
                return new BoolWrapper(isSubFolder);
            }, _folderValidationCacheTTL);

            return result?.Value ?? false;
        }

        #endregion

        #region System Folder Operations (Cached)

        public async Task<FolderDto> GetOrCreateSystemFolderAsync(FolderType folderType)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("system_folder", folderType.ToString());

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for system folder: {FolderType}", folderType);

                var existingFolder = (await _unitOfWork.Folders.GetFoldersByTypeAsync(folderType)).FirstOrDefault();
                if (existingFolder != null)
                {
                    var dto = _mapper.Map<FolderDto>(existingFolder);
                    await PopulateFolderStatistics(dto);
                    return dto;
                }

                // Create system folder
                var createDto = new CreateFolderDto
                {
                    Name = GetSystemFolderName(folderType),
                    Description = GetSystemFolderDescription(folderType),
                    FolderType = folderType,
                    IsPublic = folderType == FolderType.CompanyAssets
                };

                return await CreateFolderAsync(createDto);
            }, _folderMetadataCacheTTL) ?? throw new InvalidOperationException($"System folder {folderType} could not be created.");
        }

        public async Task<FolderDto> GetUserAvatarFolderAsync(int userId)
        {
            var cacheKey = _cacheKeyService.GetCustomKey("user_avatar_folder", userId);

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for user avatar folder: {UserId}", userId);

                var userAvatarsFolder = await GetOrCreateSystemFolderAsync(FolderType.UserAvatars);

                var userFolder = (await _unitOfWork.Folders.GetSubFoldersAsync(userAvatarsFolder.Id))
                    .FirstOrDefault(f => f.Name.Equals($"user_{userId}", StringComparison.OrdinalIgnoreCase));

                if (userFolder != null)
                {
                    var dto = _mapper.Map<FolderDto>(userFolder);
                    await PopulateFolderStatistics(dto);
                    return dto;
                }

                // Create user-specific avatar folder
                var createDto = new CreateFolderDto
                {
                    Name = $"user_{userId}",
                    Description = $"Avatar folder for user {userId}",
                    ParentFolderId = userAvatarsFolder.Id,
                    FolderType = FolderType.UserAvatars,
                    IsPublic = false
                };

                return await CreateFolderAsync(createDto);
            }, _folderMetadataCacheTTL) ?? throw new InvalidOperationException($"User avatar folder for user {userId} could not be created.");
        }

        public async Task<FolderDto> GetCompanyAssetsFolderAsync()
        {
            var cacheKey = _cacheKeyService.GetCustomKey("company_assets_folder");

            return await _cacheService.GetOrAddAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Cache miss for company assets folder");
                return await GetOrCreateSystemFolderAsync(FolderType.CompanyAssets);
            }, _folderMetadataCacheTTL) ?? throw new InvalidOperationException("Company assets folder could not be created.");
        }

        #endregion

        #region Private Helper Methods

        private async Task<List<FolderDto>> MapFoldersToDto(IEnumerable<Folder> folders)
        {
            var folderDtos = new List<FolderDto>();

            foreach (var folder in folders)
            {
                var dto = _mapper.Map<FolderDto>(folder);
                await PopulateFolderStatistics(dto);
                folderDtos.Add(dto);
            }

            return folderDtos;
        }

        private async Task PopulateFolderStatistics(FolderDto folderDto)
        {
            try
            {
                var statistics = await GetFolderStatisticsAsync(folderDto.Id);

                folderDto.FileCount = (int)(statistics.GetValueOrDefault("fileCount", 0));
                folderDto.SubFolderCount = (int)(statistics.GetValueOrDefault("subFolderCount", 0));
                folderDto.TotalSize = (long)(statistics.GetValueOrDefault("totalSize", 0L));
                folderDto.TotalSizeFormatted = statistics.GetValueOrDefault("totalSizeFormatted", "0 B")?.ToString() ?? "0 B";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to populate statistics for folder {FolderId}", folderDto.Id);
                // Set default values
                folderDto.FileCount = 0;
                folderDto.SubFolderCount = 0;
                folderDto.TotalSize = 0;
                folderDto.TotalSizeFormatted = "0 B";
            }
        }

        private async Task<FolderTreeDto> BuildFolderTreeAsync(int? rootFolderId)
        {
            var folders = rootFolderId.HasValue
                ? await _unitOfWork.Folders.GetSubFoldersAsync(rootFolderId.Value)
                : await _unitOfWork.Folders.GetRootFoldersAsync();

            var treeDto = new FolderTreeDto();

            if (rootFolderId.HasValue)
            {
                var rootFolder = await _unitOfWork.Folders.GetByIdAsync(rootFolderId.Value);
                if (rootFolder != null)
                {
                    treeDto.Id = rootFolder.Id;
                    treeDto.Name = rootFolder.Name;
                    treeDto.Path = rootFolder.Path;
                    treeDto.ParentFolderId = rootFolder.ParentFolderId;
                    treeDto.FolderType = rootFolder.FolderType;
                    treeDto.IsPublic = rootFolder.IsPublic;
                }
            }

            treeDto.Children = await BuildFolderTreeChildren(folders);
            return treeDto;
        }

        private async Task<List<FolderTreeDto>> BuildFolderTreeChildren(IEnumerable<Folder> folders)
        {
            var children = new List<FolderTreeDto>();

            foreach (var folder in folders)
            {
                var child = new FolderTreeDto
                {
                    Id = folder.Id,
                    Name = folder.Name,
                    Path = folder.Path,
                    ParentFolderId = folder.ParentFolderId,
                    FolderType = folder.FolderType,
                    IsPublic = folder.IsPublic,
                    FileCount = await _unitOfWork.Folders.GetTotalFileCountAsync(folder.Id),
                    HasSubFolders = await _unitOfWork.Folders.HasSubFoldersAsync(folder.Id)
                };

                if (child.HasSubFolders)
                {
                    var subFolders = await _unitOfWork.Folders.GetSubFoldersAsync(folder.Id);
                    child.Children = await BuildFolderTreeChildren(subFolders);
                }

                children.Add(child);
            }

            return children;
        }

        private async Task<string> GenerateUniqueFolderPathAsync(string name, int? parentFolderId)
        {
            var basePath = name.Trim();

            if (parentFolderId.HasValue)
            {
                var parent = await _unitOfWork.Folders.GetByIdAsync(parentFolderId.Value);
                if (parent != null)
                {
                    basePath = $"{parent.Path}/{basePath}";
                }
            }

            return await _unitOfWork.Folders.GenerateUniquePathAsync(basePath, parentFolderId);
        }

        private static string GetSystemFolderName(FolderType folderType)
        {
            return folderType switch
            {
                FolderType.Images => "Images",
                FolderType.Documents => "Documents",
                FolderType.Videos => "Videos",
                FolderType.Audio => "Audio",
                FolderType.UserAvatars => "User Avatars",
                FolderType.CompanyAssets => "Company Assets",
                FolderType.Temporary => "Temporary Files",
                _ => "General"
            };
        }

        private static string GetSystemFolderDescription(FolderType folderType)
        {
            return folderType switch
            {
                FolderType.Images => "System folder for image files",
                FolderType.Documents => "System folder for document files",
                FolderType.Videos => "System folder for video files",
                FolderType.Audio => "System folder for audio files",
                FolderType.UserAvatars => "System folder for user profile pictures",
                FolderType.CompanyAssets => "System folder for company branding and assets",
                FolderType.Temporary => "System folder for temporary files",
                _ => "General purpose system folder"
            };
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

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

        #region Cache Invalidation Methods

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
                    _cacheService.RemoveByPatternAsync($"folder:list:by_parent:{parentFolderId}:*"),
                    _cacheService.RemoveByPatternAsync($"folder:list:paged:*"),
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
                    "folder:query:search:*",
                    "folder:query:search_paged:*"
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

        #endregion

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

                _disposed = true;
            }
        }

        #endregion
    }
}