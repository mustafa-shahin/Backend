using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class FolderRepository : Repository<Folder>, IFolderRepository
    {
        public FolderRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Folder>> GetRootFoldersAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.ParentFolderId == null)
                              .OrderBy(f => f.Name)
                              .ToListAsync();
        }

        public async Task<IEnumerable<Folder>> GetSubFoldersAsync(int parentFolderId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.ParentFolderId == parentFolderId)
                              .OrderBy(f => f.Name)
                              .ToListAsync();
        }

        public async Task<Folder?> GetByPathAsync(string path)
        {
            return await _dbSet.AsNoTracking()
                              .FirstOrDefaultAsync(f => !f.IsDeleted && f.Path == path);
        }

        public async Task<IEnumerable<Folder>> GetFoldersByTypeAsync(FolderType folderType)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FolderType == folderType)
                              .OrderBy(f => f.Name)
                              .ToListAsync();
        }

        public async Task<IEnumerable<Folder>> GetPublicFoldersAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.IsPublic)
                              .OrderBy(f => f.Name)
                              .ToListAsync();
        }

        public async Task<IEnumerable<Folder>> SearchFoldersByNameAsync(string searchTerm)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted &&
                                     (f.Name.Contains(searchTerm) ||
                                      (f.Description != null && f.Description.Contains(searchTerm))))
                              .OrderBy(f => f.Name)
                              .ToListAsync();
        }

        public async Task<IEnumerable<Folder>> GetFolderHierarchyAsync(int folderId)
        {
            var folders = new List<Folder>();
            var currentFolder = await GetByIdAsync(folderId);

            while (currentFolder != null)
            {
                folders.Insert(0, currentFolder);
                if (currentFolder.ParentFolderId.HasValue)
                {
                    currentFolder = await GetByIdAsync(currentFolder.ParentFolderId.Value);
                }
                else
                {
                    break;
                }
            }

            return folders;
        }

        public async Task<IEnumerable<Folder>> GetFoldersByUserIdAsync(int userId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.CreatedByUserId == userId)
                              .OrderBy(f => f.Name)
                              .ToListAsync();
        }

        public async Task<string> GenerateUniquePathAsync(string basePath, int? parentFolderId = null)
        {
            var path = basePath;
            var counter = 1;

            while (await IsPathUniqueAsync(path) == false)
            {
                path = $"{basePath}_{counter}";
                counter++;
            }

            return path;
        }

        public async Task<bool> IsPathUniqueAsync(string path, int? excludeFolderId = null)
        {
            var query = _dbSet.Where(f => !f.IsDeleted && f.Path == path);

            if (excludeFolderId.HasValue)
            {
                query = query.Where(f => f.Id != excludeFolderId.Value);
            }

            return !await query.AnyAsync();
        }

        public async Task<bool> HasSubFoldersAsync(int folderId)
        {
            return await _dbSet.AnyAsync(f => !f.IsDeleted && f.ParentFolderId == folderId);
        }

        public async Task<bool> HasFilesAsync(int folderId)
        {
            return await _context.Set<FileEntity>()
                                .AnyAsync(f => !f.IsDeleted && f.FolderId == folderId);
        }

        public async Task<int> GetTotalFileCountAsync(int folderId, bool includeSubfolders = false)
        {
            if (!includeSubfolders)
            {
                return await _context.Set<FileEntity>()
                                   .Where(f => !f.IsDeleted && f.FolderId == folderId)
                                   .CountAsync();
            }

            // Get all descendant folder IDs
            var descendantIds = await GetDescendantIdsAsync(folderId);
            descendantIds.Add(folderId);

            return await _context.Set<FileEntity>()
                               .Where(f => !f.IsDeleted && descendantIds.Contains(f.Id))
                               .CountAsync();
        }

        public async Task<long> GetTotalSizeAsync(int folderId, bool includeSubfolders = false)
        {
            if (!includeSubfolders)
            {
                return await _context.Set<FileEntity>()
                                   .Where(f => !f.IsDeleted && f.FolderId == folderId)
                                   .SumAsync(f => f.FileSize);
            }

            // Get all descendant folder IDs
            var descendantIds = await GetDescendantIdsAsync(folderId);
            descendantIds.Add(folderId);

            return await _context.Set<FileEntity>()
                               .Where(f => !f.IsDeleted && descendantIds.Contains(f.Id))
                               .SumAsync(f => f.FileSize);
        }

        public async Task<int> GetDepthAsync(int folderId)
        {
            var depth = 0;
            var currentFolder = await GetByIdAsync(folderId);

            while (currentFolder?.ParentFolderId.HasValue == true)
            {
                depth++;
                currentFolder = await GetByIdAsync(currentFolder.ParentFolderId.Value);
            }

            return depth;
        }

        public async Task<IEnumerable<Folder>> GetAncestorsAsync(int folderId)
        {
            var ancestors = new List<Folder>();
            var currentFolder = await GetByIdAsync(folderId);

            while (currentFolder?.ParentFolderId.HasValue == true)
            {
                var parent = await GetByIdAsync(currentFolder.ParentFolderId.Value);
                if (parent != null)
                {
                    ancestors.Insert(0, parent);
                    currentFolder = parent;
                }
                else
                {
                    break;
                }
            }

            return ancestors;
        }

        public async Task<IEnumerable<Folder>> GetDescendantsAsync(int folderId)
        {
            var descendants = new List<Folder>();
            var directChildren = await GetSubFoldersAsync(folderId);

            foreach (var child in directChildren)
            {
                descendants.Add(child);
                var childDescendants = await GetDescendantsAsync(child.Id);
                descendants.AddRange(childDescendants);
            }

            return descendants;
        }

        public async Task<bool> IsDescendantOfAsync(int childFolderId, int ancestorFolderId)
        {
            var currentFolder = await GetByIdAsync(childFolderId);

            while (currentFolder?.ParentFolderId.HasValue == true)
            {
                if (currentFolder.ParentFolderId.Value == ancestorFolderId)
                {
                    return true;
                }
                currentFolder = await GetByIdAsync(currentFolder.ParentFolderId.Value);
            }

            return false;
        }

        public async Task<IEnumerable<Folder>> GetEmptyFoldersAsync()
        {
            var foldersWithFiles = await _context.Set<FileEntity>()
                                                .Where(f => !f.IsDeleted && f.FolderId.HasValue)
                                                .Select(f => f.FolderId.Value)
                                                .Distinct()
                                                .ToListAsync();

            var foldersWithSubfolders = await _dbSet.Where(f => !f.IsDeleted && f.ParentFolderId.HasValue)
                                                   .Select(f => f.ParentFolderId.Value)
                                                   .Distinct()
                                                   .ToListAsync();

            var occupiedFolderIds = foldersWithFiles.Union(foldersWithSubfolders).ToList();

            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && !occupiedFolderIds.Contains(f.Id))
                              .OrderBy(f => f.Name)
                              .ToListAsync();
        }

        public async Task<IEnumerable<Folder>> GetFoldersByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted &&
                                     f.CreatedAt >= startDate &&
                                     f.CreatedAt <= endDate)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<bool> CanDeleteFolderAsync(int folderId)
        {
            // Check if folder has files or subfolders
            var hasFiles = await HasFilesAsync(folderId);
            var hasSubfolders = await HasSubFoldersAsync(folderId);

            return !hasFiles && !hasSubfolders;
        }

        public async Task<bool> MoveFolderAsync(int folderId, int? newParentFolderId)
        {
            var folder = await _dbSet.FirstOrDefaultAsync(f => f.Id == folderId && !f.IsDeleted);
            if (folder == null) return false;

            // Prevent moving folder to its own descendant
            if (newParentFolderId.HasValue && await IsDescendantOfAsync(newParentFolderId.Value, folderId))
            {
                return false;
            }

            folder.ParentFolderId = newParentFolderId;
            folder.UpdatedAt = DateTime.UtcNow;

            // Update path based on new parent
            if (newParentFolderId.HasValue)
            {
                var parentFolder = await GetByIdAsync(newParentFolderId.Value);
                if (parentFolder != null)
                {
                    folder.Path = $"{parentFolder.Path}/{folder.Name}";
                }
            }
            else
            {
                folder.Path = folder.Name;
            }

            Update(folder);
            await SaveChangesAsync();
            return true;
        }

        private async Task<List<int>> GetDescendantIdsAsync(int folderId)
        {
            var descendantIds = new List<int>();
            var directChildren = await _dbSet.AsNoTracking()
                                           .Where(f => !f.IsDeleted && f.ParentFolderId == folderId)
                                           .Select(f => f.Id)
                                           .ToListAsync();

            foreach (var childId in directChildren)
            {
                descendantIds.Add(childId);
                var childDescendantIds = await GetDescendantIdsAsync(childId);
                descendantIds.AddRange(childDescendantIds);
            }

            return descendantIds;
        }
    }
}