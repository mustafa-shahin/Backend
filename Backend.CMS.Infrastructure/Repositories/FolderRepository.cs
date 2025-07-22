using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;


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
            // Check all file types
            var hasImages = await _context.Set<Image>().AnyAsync(f => !f.IsDeleted && f.FolderId == folderId);
            if (hasImages) return true;
            
            var hasVideos = await _context.Set<Video>().AnyAsync(f => !f.IsDeleted && f.FolderId == folderId);
            if (hasVideos) return true;
            
            var hasAudios = await _context.Set<Audio>().AnyAsync(f => !f.IsDeleted && f.FolderId == folderId);
            if (hasAudios) return true;
            
            var hasDocuments = await _context.Set<Document>().AnyAsync(f => !f.IsDeleted && f.FolderId == folderId);
            if (hasDocuments) return true;
            
            var hasArchives = await _context.Set<Archive>().AnyAsync(f => !f.IsDeleted && f.FolderId == folderId);
            if (hasArchives) return true;
            
            var hasOtherFiles = await _context.Set<OtherFile>().AnyAsync(f => !f.IsDeleted && f.FolderId == folderId);
            return hasOtherFiles;
        }

        public async Task<int> GetTotalFileCountAsync(int folderId, bool includeSubfolders = false)
        {
            if (!includeSubfolders)
            {
                // Count all file types in the folder
                var imageCount = await _context.Set<Image>().Where(f => !f.IsDeleted && f.FolderId == folderId).CountAsync();
                var videoCount = await _context.Set<Video>().Where(f => !f.IsDeleted && f.FolderId == folderId).CountAsync();
                var audioCount = await _context.Set<Audio>().Where(f => !f.IsDeleted && f.FolderId == folderId).CountAsync();
                var documentCount = await _context.Set<Document>().Where(f => !f.IsDeleted && f.FolderId == folderId).CountAsync();
                var archiveCount = await _context.Set<Archive>().Where(f => !f.IsDeleted && f.FolderId == folderId).CountAsync();
                var otherFileCount = await _context.Set<OtherFile>().Where(f => !f.IsDeleted && f.FolderId == folderId).CountAsync();
                
                return imageCount + videoCount + audioCount + documentCount + archiveCount + otherFileCount;
            }

            // Get all descendant folder IDs
            var descendantIds = await GetDescendantIdsAsync(folderId);
            descendantIds.Add(folderId);

            // Count all file types in descendant folders
            var descendantImageCount = await _context.Set<Image>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).CountAsync();
            var descendantVideoCount = await _context.Set<Video>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).CountAsync();
            var descendantAudioCount = await _context.Set<Audio>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).CountAsync();
            var descendantDocumentCount = await _context.Set<Document>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).CountAsync();
            var descendantArchiveCount = await _context.Set<Archive>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).CountAsync();
            var descendantOtherFileCount = await _context.Set<OtherFile>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).CountAsync();
            
            return descendantImageCount + descendantVideoCount + descendantAudioCount + descendantDocumentCount + descendantArchiveCount + descendantOtherFileCount;
        }

        public async Task<long> GetTotalSizeAsync(int folderId, bool includeSubfolders = false)
        {
            if (!includeSubfolders)
            {
                // Sum sizes of all file types in the folder
                var imageSize = await _context.Set<Image>().Where(f => !f.IsDeleted && f.FolderId == folderId).SumAsync(f => f.Size);
                var videoSize = await _context.Set<Video>().Where(f => !f.IsDeleted && f.FolderId == folderId).SumAsync(f => f.Size);
                var audioSize = await _context.Set<Audio>().Where(f => !f.IsDeleted && f.FolderId == folderId).SumAsync(f => f.Size);
                var documentSize = await _context.Set<Document>().Where(f => !f.IsDeleted && f.FolderId == folderId).SumAsync(f => f.Size);
                var archiveSize = await _context.Set<Archive>().Where(f => !f.IsDeleted && f.FolderId == folderId).SumAsync(f => f.Size);
                var otherFileSize = await _context.Set<OtherFile>().Where(f => !f.IsDeleted && f.FolderId == folderId).SumAsync(f => f.Size);
                
                return imageSize + videoSize + audioSize + documentSize + archiveSize + otherFileSize;
            }

            // Get all descendant folder IDs
            var descendantIds = await GetDescendantIdsAsync(folderId);
            descendantIds.Add(folderId);

            // Sum sizes of all file types in descendant folders
            var descendantImageSize = await _context.Set<Image>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).SumAsync(f => f.Size);
            var descendantVideoSize = await _context.Set<Video>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).SumAsync(f => f.Size);
            var descendantAudioSize = await _context.Set<Audio>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).SumAsync(f => f.Size);
            var descendantDocumentSize = await _context.Set<Document>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).SumAsync(f => f.Size);
            var descendantArchiveSize = await _context.Set<Archive>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).SumAsync(f => f.Size);
            var descendantOtherFileSize = await _context.Set<OtherFile>().Where(f => !f.IsDeleted && descendantIds.Contains(f.FolderId ?? 0)).SumAsync(f => f.Size);
            
            return descendantImageSize + descendantVideoSize + descendantAudioSize + descendantDocumentSize + descendantArchiveSize + descendantOtherFileSize;
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
            // Get folder IDs that contain files from all file types
            var imagefolders = await _context.Set<Image>().Where(f => !f.IsDeleted && f.FolderId.HasValue).Select(f => f.FolderId.Value).ToListAsync();
            var videoFolders = await _context.Set<Video>().Where(f => !f.IsDeleted && f.FolderId.HasValue).Select(f => f.FolderId.Value).ToListAsync();
            var audioFolders = await _context.Set<Audio>().Where(f => !f.IsDeleted && f.FolderId.HasValue).Select(f => f.FolderId.Value).ToListAsync();
            var documentFolders = await _context.Set<Document>().Where(f => !f.IsDeleted && f.FolderId.HasValue).Select(f => f.FolderId.Value).ToListAsync();
            var archiveFolders = await _context.Set<Archive>().Where(f => !f.IsDeleted && f.FolderId.HasValue).Select(f => f.FolderId.Value).ToListAsync();
            var otherFileFolders = await _context.Set<OtherFile>().Where(f => !f.IsDeleted && f.FolderId.HasValue).Select(f => f.FolderId.Value).ToListAsync();
            
            var foldersWithFiles = imagefolders.Union(videoFolders).Union(audioFolders).Union(documentFolders).Union(archiveFolders).Union(otherFileFolders).Distinct().ToList();

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