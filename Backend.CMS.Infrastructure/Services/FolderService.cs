using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Backend.CMS.Infrastructure.Services
{
    public class FolderService : IFolderService, IDisposable
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserSessionService _userSessionService;
        private readonly IMapper _mapper;
        private readonly ILogger<FolderService> _logger;
        private readonly IConfiguration _configuration;

        // Semaphore management for concurrent operations
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _folderSemaphores;
        private readonly Timer _semaphoreCleanupTimer;
        private readonly Lock _semaphoreLock = new();

        // Performance settings
        private readonly int _maxConcurrentOperations;

        // Pagination settings
        private readonly int _defaultPageSize;
        private readonly int _maxPageSize;

        private bool _disposed = false;

        public FolderService(
            IUnitOfWork unitOfWork,
            IUserSessionService userSessionService,
            IMapper mapper,
            ILogger<FolderService> logger,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Load performance settings
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

        #region Paginated Operations (Core Service Logic with Database-Level Pagination)

        public async Task<PaginatedResult<FolderDto>> GetFoldersPagedAsync(int? parentFolderId = null, int pageNumber = 1, int pageSize = 10)
        {
            // Validate and normalize pagination parameters
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize <= 0 ? _defaultPageSize : pageSize, 1, _maxPageSize);

            _logger.LogDebug("Getting paginated folders: parent {ParentFolderId}, page {PageNumber}, size {PageSize}",
                parentFolderId, pageNumber, pageSize);

            await _operationSemaphore.WaitAsync();
            try
            {
                // Build query for efficient database-level pagination
                var query = _unitOfWork.Folders.GetQueryable();

                // Apply parent folder filter
                if (parentFolderId.HasValue)
                {
                    query = query.Where(f => f.ParentFolderId == parentFolderId.Value);
                }
                else
                {
                    query = query.Where(f => f.ParentFolderId == null);
                }

                query = query.OrderBy(f => f.Name);

                var totalCount = await query.CountAsync();

                var folders = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var folderDtos = await MapFoldersToDto(folders);

                var result = new PaginatedResult<FolderDto>(folderDtos, pageNumber, pageSize, totalCount);

                _logger.LogDebug("Retrieved {Count} folders out of {TotalCount} total for parent {ParentFolderId}",
                    folders.Count, totalCount, parentFolderId);

                return result;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<PaginatedResult<FolderDto>> SearchFoldersPagedAsync(string searchTerm, int pageNumber = 1, int pageSize = 10)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return PaginatedResult<FolderDto>.Empty(pageNumber, pageSize);

            // Validate and normalize pagination parameters
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize <= 0 ? _defaultPageSize : pageSize, 1, _maxPageSize);

            _logger.LogDebug("Searching paginated folders: {SearchTerm}, page {PageNumber}, size {PageSize}",
                searchTerm, pageNumber, pageSize);

            await _operationSemaphore.WaitAsync();
            try
            {

                var query = _unitOfWork.Folders.GetQueryable();


                var searchTermLower = searchTerm.ToLowerInvariant();
                query = query.Where(f =>
                    EF.Functions.Like(f.Name.ToLower(), $"%{searchTermLower}%") ||
                    (f.Description != null && EF.Functions.Like(f.Description.ToLower(), $"%{searchTermLower}%")));

                query = query.OrderBy(f => f.Name);

                var totalCount = await query.CountAsync();

                var folders = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                
                var folderDtos = await MapFoldersToDto(folders);

                var result = new PaginatedResult<FolderDto>(folderDtos, pageNumber, pageSize, totalCount);

                _logger.LogDebug("Found {Count} folders out of {TotalCount} total matching '{SearchTerm}'",
                    folders.Count, totalCount, searchTerm);

                return result;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        #endregion

        #region Create/Update/Delete Operations

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
            await _unitOfWork.SaveChangesAsync();

            var result = _mapper.Map<FolderDto>(folder);
            await PopulateFolderStatistics(result);

            _logger.LogInformation("Created folder: {FolderName} (ID: {FolderId})", folder.Name, folder.Id);

            return result;
        }

        public async Task<FolderDto> UpdateFolderAsync(int folderId, UpdateFolderDto updateDto)
        {
            if (updateDto == null)
                throw new ArgumentNullException(nameof(updateDto));

            var folder = await _unitOfWork.Folders.GetByIdAsync(folderId);
            if (folder == null)
                throw new ArgumentException("Folder not found");

            var currentUserId = _userSessionService.GetCurrentUserId();

            _mapper.Map(updateDto, folder);
            folder.UpdatedAt = DateTime.UtcNow;
            folder.UpdatedByUserId = currentUserId;

            _unitOfWork.Folders.Update(folder);
            await _unitOfWork.SaveChangesAsync();

            var result = _mapper.Map<FolderDto>(folder);
            await PopulateFolderStatistics(result);

            _logger.LogInformation("Updated folder: {FolderName} (ID: {FolderId})", folder.Name, folder.Id);

            return result;
        }

        public async Task<bool> DeleteFolderAsync(int folderId, bool deleteFiles = false)
        {
            var folder = await _unitOfWork.Folders.GetByIdAsync(folderId);
            if (folder == null)
                return false;

            var currentUserId = _userSessionService.GetCurrentUserId();

            // Check if folder can be deleted
            if (!deleteFiles && (await _unitOfWork.Folders.HasFilesAsync(folderId) || await _unitOfWork.Folders.HasSubFoldersAsync(folderId)))
            {
                throw new InvalidOperationException("Cannot delete folder that contains files or subfolders unless deleteFiles is true");
            }

            var success = await _unitOfWork.Folders.SoftDeleteAsync(folderId, currentUserId);

            if (success)
            {
                _logger.LogInformation("Deleted folder: {FolderName} (ID: {FolderId})", folder.Name, folder.Id);
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

            _logger.LogInformation("Moved folder: {FolderName} (ID: {FolderId}) to parent {NewParentId}",
                folder.Name, folder.Id, moveDto.NewParentFolderId);

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
            var oldName = folder.Name;

            folder.Name = newName.Trim();
            folder.UpdatedAt = DateTime.UtcNow;
            folder.UpdatedByUserId = currentUserId;

            // Update path
            folder.Path = await GenerateUniqueFolderPathAsync(newName, folder.ParentFolderId);

            _unitOfWork.Folders.Update(folder);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Renamed folder from '{OldName}' to '{NewName}' (ID: {FolderId})",
                oldName, newName, folderId);

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
            await _unitOfWork.SaveChangesAsync();

            var result = _mapper.Map<FolderDto>(newFolder);
            await PopulateFolderStatistics(result);

            _logger.LogInformation("Copied folder: {OriginalName} to {NewName} (ID: {NewFolderId})",
                originalFolder.Name, folderName, newFolder.Id);

            return result;
        }

        #endregion

        #region Read Operations

        public async Task<FolderDto> GetFolderByIdAsync(int folderId)
        {
            if (folderId <= 0)
                throw new ArgumentException("Folder ID must be greater than 0", nameof(folderId));

            _logger.LogDebug("Getting folder by ID: {FolderId}", folderId);

            var folder = await _unitOfWork.Folders.GetByIdAsync(folderId);
            if (folder == null)
                throw new ArgumentException($"Folder with ID {folderId} not found");

            var dto = _mapper.Map<FolderDto>(folder);
            await PopulateFolderStatistics(dto);

            return dto;
        }

        public async Task<List<FolderDto>> GetFoldersAsync(int? parentFolderId = null)
        {
            _logger.LogDebug("Getting folders for parent: {ParentFolderId}", parentFolderId);

            // Use queryable for efficient data access
            var query = _unitOfWork.Folders.GetQueryable();

            if (parentFolderId.HasValue)
            {
                query = query.Where(f => f.ParentFolderId == parentFolderId.Value);
            }
            else
            {
                query = query.Where(f => f.ParentFolderId == null);
            }

            query = query.OrderBy(f => f.Name);
            var folders = await query.ToListAsync();

            var folderDtos = await MapFoldersToDto(folders);

            return folderDtos;
        }

        public async Task<FolderTreeDto> GetFolderTreeAsync(int? rootFolderId = null)
        {
            _logger.LogDebug("Building folder tree for root: {RootFolderId}", rootFolderId);
            return await BuildFolderTreeAsync(rootFolderId);
        }

        public async Task<List<FolderDto>> SearchFoldersAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<FolderDto>();

            _logger.LogDebug("Searching folders for term: {SearchTerm}", searchTerm);

            // Use queryable for efficient database search
            var query = _unitOfWork.Folders.GetQueryable();
            var searchTermLower = searchTerm.ToLowerInvariant();

            query = query.Where(f =>
                EF.Functions.Like(f.Name.ToLower(), $"%{searchTermLower}%") ||
                (f.Description != null && EF.Functions.Like(f.Description.ToLower(), $"%{searchTermLower}%")));

            query = query.OrderBy(f => f.Name);
            var folders = await query.ToListAsync();

            var folderDtos = await MapFoldersToDto(folders);

            return folderDtos;
        }

        public async Task<string> GetFolderPathAsync(int folderId)
        {
            var folder = await _unitOfWork.Folders.GetByIdAsync(folderId);
            return folder?.Path ?? string.Empty;
        }

        public async Task<List<FolderDto>> GetFolderBreadcrumbsAsync(int folderId)
        {
            _logger.LogDebug("Getting breadcrumbs for folder: {FolderId}", folderId);

            var ancestors = await _unitOfWork.Folders.GetAncestorsAsync(folderId);
            return await MapFoldersToDto(ancestors);
        }

        public async Task<FolderDto?> GetFolderByPathAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            _logger.LogDebug("Getting folder by path: {Path}", path);

            var folder = await _unitOfWork.Folders.GetByPathAsync(path);
            if (folder == null)
                return null;

            var dto = _mapper.Map<FolderDto>(folder);
            await PopulateFolderStatistics(dto);
            return dto;
        }

        public async Task<Dictionary<string, object>> GetFolderStatisticsAsync(int folderId)
        {
            _logger.LogDebug("Getting statistics for folder: {FolderId}", folderId);

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
        }

        #endregion

        #region Validation Operations

        public async Task<bool> FolderExistsAsync(int folderId)
        {
            if (folderId <= 0)
                return false;

            var exists = await _unitOfWork.Folders.AnyAsync(f => f.Id == folderId);
            _logger.LogDebug("Folder existence check for {FolderId}: {Exists}", folderId, exists);

            return exists;
        }

        public async Task<bool> ValidateFolderNameAsync(string name, int? parentFolderId = null, int? excludeFolderId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var isValid = await _unitOfWork.Folders.IsPathUniqueAsync(name, excludeFolderId);
            _logger.LogDebug("Folder name validation for '{Name}' in parent {ParentId}: {IsValid}",
                name, parentFolderId, isValid);

            return isValid;
        }

        public async Task<bool> IsSubFolderOfAsync(int childFolderId, int parentFolderId)
        {
            var isSubFolder = await _unitOfWork.Folders.IsDescendantOfAsync(childFolderId, parentFolderId);
            return isSubFolder;
        }

        #endregion

        #region System Folder Operations

        public async Task<FolderDto> GetOrCreateSystemFolderAsync(FolderType folderType)
        {
            _logger.LogDebug("Getting or creating system folder: {FolderType}", folderType);

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

            var result = await CreateFolderAsync(createDto);
            _logger.LogInformation("Created system folder: {FolderType} (ID: {FolderId})", folderType, result.Id);

            return result;
        }

        public async Task<FolderDto> GetUserAvatarFolderAsync(int userId)
        {
            _logger.LogDebug("Getting user avatar folder for user: {UserId}", userId);

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

            var result = await CreateFolderAsync(createDto);
            _logger.LogInformation("Created user avatar folder for user {UserId} (ID: {FolderId})", userId, result.Id);

            return result;
        }

        public async Task<FolderDto> GetCompanyAssetsFolderAsync()
        {
            _logger.LogDebug("Getting company assets folder");
            return await GetOrCreateSystemFolderAsync(FolderType.CompanyAssets);
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
            var query = _unitOfWork.Folders.GetQueryable();

            if (rootFolderId.HasValue)
            {
                query = query.Where(f => f.ParentFolderId == rootFolderId.Value);
            }
            else
            {
                query = query.Where(f => f.ParentFolderId == null);
            }

            var folders = await query.OrderBy(f => f.Name).ToListAsync();

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
                    var subQuery = _unitOfWork.Folders.GetQueryable()
                        .Where(f => f.ParentFolderId == folder.Id)
                        .OrderBy(f => f.Name);
                    var subFolders = await subQuery.ToListAsync();
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