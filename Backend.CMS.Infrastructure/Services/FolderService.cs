using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Backend.CMS.Infrastructure.Services
{
    public class FolderService : IFolderService, IDisposable
    {
        private readonly IFolderRepository _folderRepository;
        private readonly IRepository<FileEntity> _fileRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IMapper _mapper;
        private readonly ILogger<FolderService> _logger;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _pathSemaphores;
        private readonly Timer _semaphoreCleanupTimer;
        private readonly object _semaphoreLock = new();
        private bool _disposed = false;

        public FolderService(
            IFolderRepository folderRepository,
            IRepository<FileEntity> fileRepository,
            IUserSessionService userSessionService,
            IMapper mapper,
            ILogger<FolderService> logger)
        {
            _folderRepository = folderRepository ?? throw new ArgumentNullException(nameof(folderRepository));
            _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _operationSemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);
            _pathSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

            // Cleanup unused semaphores every 15 minutes
            _semaphoreCleanupTimer = new Timer(CleanupUnusedSemaphores, null,
                TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
        }

        public async Task<FolderDto> CreateFolderAsync(CreateFolderDto createDto)
        {
            if (string.IsNullOrWhiteSpace(createDto.Name))
                throw new ArgumentException("Folder name is required");

            var sanitizedName = createDto.Name.Trim();
            var pathSemaphore = GetPathSemaphore(sanitizedName);

            await _operationSemaphore.WaitAsync();
            try
            {
                await pathSemaphore.WaitAsync();
                try
                {
                    // Validate parent folder exists if specified
                    if (createDto.ParentFolderId.HasValue)
                    {
                        var parentFolder = await _folderRepository.GetByIdAsync(createDto.ParentFolderId.Value);
                        if (parentFolder == null)
                            throw new ArgumentException("Parent folder not found");
                    }

                    // Check for duplicate names in the same parent
                    if (!await ValidateFolderNameAsync(sanitizedName, createDto.ParentFolderId))
                        throw new ArgumentException("A folder with this name already exists in the specified location");

                    var currentUserId = _userSessionService.GetCurrentUserId();
                    var path = await GenerateFolderPathAsync(sanitizedName, createDto.ParentFolderId);

                    var folder = new Folder
                    {
                        Name = sanitizedName,
                        Description = createDto.Description?.Trim(),
                        Path = path,
                        ParentFolderId = createDto.ParentFolderId,
                        IsPublic = createDto.IsPublic,
                        FolderType = createDto.FolderType,
                        Metadata = createDto.Metadata ?? new Dictionary<string, object>(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedByUserId = currentUserId,
                        UpdatedByUserId = currentUserId
                    };

                    await _folderRepository.AddAsync(folder);
                    await _folderRepository.SaveChangesAsync();

                    _logger.LogInformation("Folder created: {FolderName} (ID: {FolderId}) by user {UserId}",
                        folder.Name, folder.Id, currentUserId);

                    return await MapFolderToDto(folder);
                }
                finally
                {
                    pathSemaphore.Release();
                }
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<FolderDto> GetFolderByIdAsync(int folderId)
        {
            var folder = await _folderRepository.GetByIdAsync(folderId);
            if (folder == null)
                throw new ArgumentException("Folder not found");

            return await MapFolderToDto(folder);
        }

        public async Task<List<FolderDto>> GetFoldersAsync(int? parentFolderId = null)
        {
            var folders = await _folderRepository.FindAsync(f => f.ParentFolderId == parentFolderId);
            var sortedFolders = folders.OrderBy(f => f.Name).ToList();

            // Use parallel processing for better performance
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
            var tasks = sortedFolders.Select(async folder =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await MapFolderToDto(folder);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var folderDtos = await Task.WhenAll(tasks);
            semaphore.Dispose();

            return folderDtos.ToList();
        }

        public async Task<FolderTreeDto> GetFolderTreeAsync(int? rootFolderId = null)
        {
            await _operationSemaphore.WaitAsync();
            try
            {
                // Load all folders at once to prevent N+1 queries
                var allFolders = (await _folderRepository.GetAllAsync()).ToList();
                var allFiles = (await _fileRepository.GetAllAsync()).ToList();

                // Create lookup dictionaries for performance
                var folderLookup = allFolders.ToLookup(f => f.ParentFolderId);
                var fileLookup = allFiles.ToLookup(f => f.FolderId);

                var rootFolders = folderLookup[rootFolderId].OrderBy(f => f.Name).ToList();

                var treeDtos = new List<FolderTreeDto>();
                foreach (var folder in rootFolders)
                {
                    var treeDto = BuildFolderTree(folder, folderLookup, fileLookup);
                    treeDtos.Add(treeDto);
                }

                // Return virtual root if no specific root folder
                if (rootFolderId == null && treeDtos.Any())
                {
                    return new FolderTreeDto
                    {
                        Id = 0,
                        Name = "Root",
                        Path = "/",
                        ParentFolderId = null,
                        Children = treeDtos,
                        FolderType = FolderType.General,
                        IsPublic = false,
                        FileCount = 0,
                        HasSubFolders = treeDtos.Any()
                    };
                }

                return treeDtos.FirstOrDefault() ?? new FolderTreeDto();
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<FolderDto> UpdateFolderAsync(int folderId, UpdateFolderDto updateDto)
        {
            if (string.IsNullOrWhiteSpace(updateDto.Name))
                throw new ArgumentException("Folder name is required");

            var sanitizedName = updateDto.Name.Trim();
            var pathSemaphore = GetPathSemaphore(sanitizedName);

            await _operationSemaphore.WaitAsync();
            try
            {
                await pathSemaphore.WaitAsync();
                try
                {
                    var folder = await _folderRepository.GetByIdAsync(folderId);
                    if (folder == null)
                        throw new ArgumentException("Folder not found");

                    // Check for duplicate names if name is changing
                    if (folder.Name != sanitizedName)
                    {
                        if (!await ValidateFolderNameAsync(sanitizedName, folder.ParentFolderId, folderId))
                            throw new ArgumentException("A folder with this name already exists in the specified location");
                    }

                    var currentUserId = _userSessionService.GetCurrentUserId();
                    var oldPath = folder.Path;
                    var nameChanged = folder.Name != sanitizedName;

                    folder.Name = sanitizedName;
                    folder.Description = updateDto.Description?.Trim();
                    folder.IsPublic = updateDto.IsPublic;
                    folder.Metadata = updateDto.Metadata ?? new Dictionary<string, object>();
                    folder.UpdatedAt = DateTime.UtcNow;
                    folder.UpdatedByUserId = currentUserId;

                    // Update path if name changed
                    if (nameChanged)
                    {
                        folder.Path = await GenerateFolderPathAsync(folder.Name, folder.ParentFolderId);

                        // Update paths of all subfolders in batch
                        await UpdateSubfolderPathsAsync(folder, oldPath);
                    }

                    _folderRepository.Update(folder);
                    await _folderRepository.SaveChangesAsync();

                    _logger.LogInformation("Folder updated: {FolderName} (ID: {FolderId}) by user {UserId}",
                        folder.Name, folder.Id, currentUserId);

                    return await MapFolderToDto(folder);
                }
                finally
                {
                    pathSemaphore.Release();
                }
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<bool> DeleteFolderAsync(int folderId, bool deleteFiles = false)
        {
            await _operationSemaphore.WaitAsync();
            try
            {
                var folder = await _folderRepository.GetByIdAsync(folderId);
                if (folder == null)
                    return false;

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Check if folder has subfolders
                var subfolders = await _folderRepository.FindAsync(f => f.ParentFolderId == folderId);
                if (subfolders.Any())
                    throw new InvalidOperationException("Cannot delete folder that contains subfolders. Delete or move subfolders first.");

                // Handle files in the folder
                var files = await _fileRepository.FindAsync(f => f.FolderId == folderId);
                if (files.Any())
                {
                    if (!deleteFiles)
                        throw new InvalidOperationException("Cannot delete folder that contains files. Set deleteFiles=true to delete files or move them first.");

                    // Delete all files in parallel batches
                    await DeleteFilesInBatchesAsync(files, currentUserId);
                }

                var success = await _folderRepository.SoftDeleteAsync(folderId, currentUserId);

                if (success)
                {
                    _logger.LogInformation("Folder deleted: {FolderName} (ID: {FolderId}) by user {UserId}",
                        folder.Name, folder.Id, currentUserId);
                }

                return success;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<FolderDto> MoveFolderAsync(MoveFolderDto moveDto)
        {
            await _operationSemaphore.WaitAsync();
            try
            {
                var folder = await _folderRepository.GetByIdAsync(moveDto.FolderId);
                if (folder == null)
                    throw new ArgumentException("Folder not found");

                // Validate destination folder exists if specified
                if (moveDto.NewParentFolderId.HasValue)
                {
                    var destinationFolder = await _folderRepository.GetByIdAsync(moveDto.NewParentFolderId.Value);
                    if (destinationFolder == null)
                        throw new ArgumentException("Destination folder not found");

                    // Check for circular reference
                    if (await WouldCreateCircularReferenceAsync(moveDto.FolderId, moveDto.NewParentFolderId.Value))
                        throw new ArgumentException("Moving folder would create a circular reference");
                }

                // Check for duplicate names in destination
                if (!await ValidateFolderNameAsync(folder.Name, moveDto.NewParentFolderId, moveDto.FolderId))
                    throw new ArgumentException("A folder with this name already exists in the destination location");

                var currentUserId = _userSessionService.GetCurrentUserId();
                var oldPath = folder.Path;

                folder.ParentFolderId = moveDto.NewParentFolderId;
                folder.Path = await GenerateFolderPathAsync(folder.Name, moveDto.NewParentFolderId);
                folder.UpdatedAt = DateTime.UtcNow;
                folder.UpdatedByUserId = currentUserId;

                // Update paths of all subfolders
                await UpdateSubfolderPathsAsync(folder, oldPath);

                _folderRepository.Update(folder);
                await _folderRepository.SaveChangesAsync();

                _logger.LogInformation("Folder moved: {FolderName} (ID: {FolderId}) to parent {ParentId} by user {UserId}",
                    folder.Name, folder.Id, moveDto.NewParentFolderId, currentUserId);

                return await MapFolderToDto(folder);
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<bool> RenameFolderAsync(int folderId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return false;

            var folder = await _folderRepository.GetByIdAsync(folderId);
            if (folder == null)
                return false;

            if (!await ValidateFolderNameAsync(newName, folder.ParentFolderId, folderId))
                return false;

            var currentUserId = _userSessionService.GetCurrentUserId();
            var oldPath = folder.Path;

            folder.Name = newName.Trim();
            folder.Path = await GenerateFolderPathAsync(folder.Name, folder.ParentFolderId);
            folder.UpdatedAt = DateTime.UtcNow;
            folder.UpdatedByUserId = currentUserId;

            // Update paths of all subfolders
            await UpdateSubfolderPathsAsync(folder, oldPath);

            _folderRepository.Update(folder);
            await _folderRepository.SaveChangesAsync();

            return true;
        }

        public async Task<FolderDto> CopyFolderAsync(int folderId, int? destinationFolderId, string? newName = null)
        {
            await _operationSemaphore.WaitAsync();
            try
            {
                var sourceFolder = await _folderRepository.GetByIdAsync(folderId);
                if (sourceFolder == null)
                    throw new ArgumentException("Source folder not found");

                var folderName = newName ?? $"Copy of {sourceFolder.Name}";

                if (!await ValidateFolderNameAsync(folderName, destinationFolderId))
                    throw new ArgumentException("A folder with this name already exists in the destination location");

                var copiedFolder = await CopyFolderRecursivelyAsync(sourceFolder, destinationFolderId, folderName);

                return await MapFolderToDto(copiedFolder);
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<string> GetFolderPathAsync(int folderId)
        {
            var folder = await _folderRepository.GetByIdAsync(folderId);
            return folder?.Path ?? string.Empty;
        }

        public async Task<List<FolderDto>> GetFolderBreadcrumbsAsync(int folderId)
        {
            var breadcrumbs = new List<FolderDto>();
            var currentFolder = await _folderRepository.GetByIdAsync(folderId);

            while (currentFolder != null)
            {
                breadcrumbs.Insert(0, await MapFolderToDto(currentFolder));

                if (currentFolder.ParentFolderId.HasValue)
                {
                    currentFolder = await _folderRepository.GetByIdAsync(currentFolder.ParentFolderId.Value);
                }
                else
                {
                    break;
                }
            }

            return breadcrumbs;
        }

        public async Task<FolderDto?> GetFolderByPathAsync(string path)
        {
            var folder = await _folderRepository.FirstOrDefaultAsync(f => f.Path == path);
            return folder != null ? await MapFolderToDto(folder) : null;
        }

        public async Task<List<FolderDto>> SearchFoldersAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<FolderDto>();

            var folders = await _folderRepository.FindAsync(f =>
                f.Name.Contains(searchTerm) ||
                (f.Description != null && f.Description.Contains(searchTerm)));

            var folderDtos = new List<FolderDto>();
            foreach (var folder in folders.OrderBy(f => f.Name))
            {
                folderDtos.Add(await MapFolderToDto(folder));
            }

            return folderDtos;
        }

        public async Task<bool> FolderExistsAsync(int folderId)
        {
            return await _folderRepository.AnyAsync(f => f.Id == folderId);
        }

        public async Task<bool> ValidateFolderNameAsync(string name, int? parentFolderId = null, int? excludeFolderId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var trimmedName = name.Trim();
            var query = await _folderRepository.FindAsync(f =>
                f.Name == trimmedName &&
                f.ParentFolderId == parentFolderId);

            if (excludeFolderId.HasValue)
            {
                query = query.Where(f => f.Id != excludeFolderId.Value);
            }

            return !query.Any();
        }

        public async Task<bool> IsSubFolderOfAsync(int childFolderId, int parentFolderId)
        {
            var childFolder = await _folderRepository.GetByIdAsync(childFolderId);
            if (childFolder == null)
                return false;

            var currentParentId = childFolder.ParentFolderId;

            while (currentParentId.HasValue)
            {
                if (currentParentId == parentFolderId)
                    return true;

                var parentFolder = await _folderRepository.GetByIdAsync(currentParentId.Value);
                currentParentId = parentFolder?.ParentFolderId;
            }

            return false;
        }

        public async Task<Dictionary<string, object>> GetFolderStatisticsAsync(int folderId)
        {
            var files = await _fileRepository.FindAsync(f => f.FolderId == folderId);
            var subfolders = await _folderRepository.FindAsync(f => f.ParentFolderId == folderId);

            var totalSize = files.Sum(f => f.FileSize);
            var fileCount = files.Count();
            var subfolderCount = subfolders.Count();

            return new Dictionary<string, object>
            {
                ["fileCount"] = fileCount,
                ["subfolderCount"] = subfolderCount,
                ["totalSize"] = totalSize,
                ["totalSizeFormatted"] = FormatFileSize(totalSize),
                ["lastModified"] = files.Any() ? files.Max(f => f.UpdatedAt) : (DateTime?)null
            };
        }

        public async Task<FolderDto> GetOrCreateSystemFolderAsync(FolderType folderType)
        {
            var existingFolder = await _folderRepository.FirstOrDefaultAsync(f => f.FolderType == folderType);

            if (existingFolder != null)
            {
                return await MapFolderToDto(existingFolder);
            }

            var currentUserId = _userSessionService.GetCurrentUserId();
            var folderName = GetSystemFolderName(folderType);

            var systemFolder = new Folder
            {
                Name = folderName,
                Description = $"System folder for {folderName.ToLower()}",
                Path = $"/{folderName.ToLower()}",
                ParentFolderId = null,
                IsPublic = folderType != FolderType.UserAvatars,
                FolderType = folderType,
                Metadata = new Dictionary<string, object> { ["isSystem"] = true },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            await _folderRepository.AddAsync(systemFolder);
            await _folderRepository.SaveChangesAsync();

            return await MapFolderToDto(systemFolder);
        }

        public async Task<FolderDto> GetUserAvatarFolderAsync(int userId)
        {
            var avatarFolder = await GetOrCreateSystemFolderAsync(FolderType.UserAvatars);

            var userFolder = await _folderRepository.FirstOrDefaultAsync(f =>
                f.ParentFolderId == avatarFolder.Id &&
                f.Name == $"user_{userId}");

            if (userFolder != null)
            {
                return await MapFolderToDto(userFolder);
            }

            var currentUserId = _userSessionService.GetCurrentUserId();

            var newUserFolder = new Folder
            {
                Name = $"user_{userId}",
                Description = $"Avatar folder for user {userId}",
                Path = $"{avatarFolder.Path}/user_{userId}",
                ParentFolderId = avatarFolder.Id,
                IsPublic = false,
                FolderType = FolderType.UserAvatars,
                Metadata = new Dictionary<string, object> { ["userId"] = userId },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            await _folderRepository.AddAsync(newUserFolder);
            await _folderRepository.SaveChangesAsync();

            return await MapFolderToDto(newUserFolder);
        }

        public async Task<FolderDto> GetCompanyAssetsFolderAsync()
        {
            return await GetOrCreateSystemFolderAsync(FolderType.CompanyAssets);
        }

        // Private helper methods
        private SemaphoreSlim GetPathSemaphore(string path)
        {
            lock (_semaphoreLock)
            {
                return _pathSemaphores.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            }
        }

        private async Task<string> GenerateFolderPathAsync(string folderName, int? parentFolderId)
        {
            if (!parentFolderId.HasValue)
            {
                return $"/{folderName.ToLower()}";
            }

            var parentFolder = await _folderRepository.GetByIdAsync(parentFolderId.Value);
            if (parentFolder == null)
            {
                return $"/{folderName.ToLower()}";
            }

            return $"{parentFolder.Path}/{folderName.ToLower()}";
        }

        private async Task UpdateSubfolderPathsAsync(Folder parentFolder, string oldPath)
        {
            var subfolders = await _folderRepository.FindAsync(f => f.ParentFolderId == parentFolder.Id);
            var subfoldersList = subfolders.ToList();

            if (!subfoldersList.Any())
                return;

            const int batchSize = 50;
            var batches = subfoldersList.Chunk(batchSize);

            foreach (var batch in batches)
            {
                var tasks = batch.Select(async subfolder =>
                {
                    var oldSubfolderPath = subfolder.Path;
                    subfolder.Path = subfolder.Path.Replace(oldPath, parentFolder.Path);
                    subfolder.UpdatedAt = DateTime.UtcNow;
                    subfolder.UpdatedByUserId = _userSessionService.GetCurrentUserId();

                    _folderRepository.Update(subfolder);

                    // Recursively update nested subfolders
                    await UpdateSubfolderPathsAsync(subfolder, oldSubfolderPath);
                });

                await Task.WhenAll(tasks);
                await _folderRepository.SaveChangesAsync();
            }
        }

        private async Task<bool> WouldCreateCircularReferenceAsync(int folderId, int newParentId)
        {
            var currentParentId = newParentId;

            while (currentParentId != 0)
            {
                if (currentParentId == folderId)
                    return true;

                var parentFolder = await _folderRepository.GetByIdAsync(currentParentId);
                if (parentFolder?.ParentFolderId.HasValue == true)
                    currentParentId = parentFolder.ParentFolderId.Value;
                else
                    break;
            }

            return false;
        }

        private FolderTreeDto BuildFolderTree(Folder folder, ILookup<int?, Folder> folderLookup, ILookup<int?, FileEntity> fileLookup)
        {
            var children = folderLookup[folder.Id].OrderBy(f => f.Name).ToList();
            var files = fileLookup[folder.Id];

            var childrenDtos = children.Select(child => BuildFolderTree(child, folderLookup, fileLookup)).ToList();

            return new FolderTreeDto
            {
                Id = folder.Id,
                Name = folder.Name,
                Path = folder.Path,
                ParentFolderId = folder.ParentFolderId,
                Children = childrenDtos,
                FolderType = folder.FolderType,
                IsPublic = folder.IsPublic,
                FileCount = files.Count(),
                HasSubFolders = childrenDtos.Any()
            };
        }

        private async Task<FolderDto> MapFolderToDto(Folder folder)
        {
            // Use parallel queries for better performance
            var filesTask = _fileRepository.FindAsync(f => f.FolderId == folder.Id);
            var subfoldersTask = _folderRepository.FindAsync(f => f.ParentFolderId == folder.Id);

            await Task.WhenAll(filesTask, subfoldersTask);

            var files = await filesTask;
            var subfolders = await subfoldersTask;
            var totalSize = files.Sum(f => f.FileSize);

            return new FolderDto
            {
                Id = folder.Id,
                Name = folder.Name,
                Description = folder.Description,
                Path = folder.Path,
                ParentFolderId = folder.ParentFolderId,
                IsPublic = folder.IsPublic,
                Metadata = folder.Metadata,
                FolderType = folder.FolderType,
                CreatedAt = folder.CreatedAt,
                UpdatedAt = folder.UpdatedAt,
                FileCount = files.Count(),
                SubFolderCount = subfolders.Count(),
                TotalSize = totalSize,
                TotalSizeFormatted = FormatFileSize(totalSize)
            };
        }

        private async Task DeleteFilesInBatchesAsync(IEnumerable<FileEntity> files, int? currentUserId)
        {
            const int batchSize = 50;
            var filesList = files.ToList();
            var batches = filesList.Chunk(batchSize);

            foreach (var batch in batches)
            {
                var tasks = batch.Select(file => _fileRepository.SoftDeleteAsync(file, currentUserId));
                await Task.WhenAll(tasks);
            }
        }

        private async Task<Folder> CopyFolderRecursivelyAsync(Folder sourceFolder, int? destinationFolderId, string? newName = null)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();
            var folderName = newName ?? sourceFolder.Name;

            var copyFolder = new Folder
            {
                Name = folderName,
                Description = sourceFolder.Description,
                Path = await GenerateFolderPathAsync(folderName, destinationFolderId),
                ParentFolderId = destinationFolderId,
                IsPublic = sourceFolder.IsPublic,
                FolderType = sourceFolder.FolderType,
                Metadata = new Dictionary<string, object>(sourceFolder.Metadata),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId
            };

            await _folderRepository.AddAsync(copyFolder);
            await _folderRepository.SaveChangesAsync();

            // Copy subfolders recursively in parallel
            var subfolders = await _folderRepository.FindAsync(f => f.ParentFolderId == sourceFolder.Id);
            var copyTasks = subfolders.Select(subfolder =>
                CopyFolderRecursivelyAsync(subfolder, copyFolder.Id));

            await Task.WhenAll(copyTasks);

            return copyFolder;
        }

        private static string GetSystemFolderName(FolderType folderType)
        {
            return folderType switch
            {
                FolderType.Images => "Images",
                FolderType.Documents => "Documents",
                FolderType.Videos => "Videos",
                FolderType.Audio => "Audio",
                FolderType.UserAvatars => "UserAvatars",
                FolderType.CompanyAssets => "CompanyAssets",
                FolderType.Temporary => "Temporary",
                _ => "General"
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
                    var keysToRemove = new List<string>();

                    foreach (var kvp in _pathSemaphores)
                    {
                        if (kvp.Value.CurrentCount == 1) // Not in use
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }

                    foreach (var key in keysToRemove.Take(100)) // Limit cleanup
                    {
                        if (_pathSemaphores.TryRemove(key, out var semaphore))
                        {
                            semaphore.Dispose();
                        }
                    }

                    if (keysToRemove.Count > 0)
                    {
                        _logger.LogDebug("Cleaned up {Count} unused path semaphores",
                            Math.Min(keysToRemove.Count, 100));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during semaphore cleanup");
                }
            }
        }

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

                // Dispose all path semaphores
                foreach (var semaphore in _pathSemaphores.Values)
                {
                    semaphore.Dispose();
                }
                _pathSemaphores.Clear();

                _disposed = true;
            }
        }
    }
}