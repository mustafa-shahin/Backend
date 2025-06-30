using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class FileRepository : Repository<FileEntity>, IFileRepository
    {
        public FileRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<FileEntity>> GetFilesByFolderIdAsync(int? folderId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FolderId == folderId)
                              .OrderBy(f => f.OriginalFileName)
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetFilesByTypeAsync(FileType fileType)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FileType == fileType)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetPublicFilesAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.IsPublic)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<FileEntity?> GetByStoredFileNameAsync(string storedFileName)
        {
            return await _dbSet.AsNoTracking()
                              .FirstOrDefaultAsync(f => !f.IsDeleted && f.StoredFileName == storedFileName);
        }

        public async Task<FileEntity?> GetByHashAsync(string hash)
        {
            return await _dbSet.AsNoTracking()
                              .FirstOrDefaultAsync(f => !f.IsDeleted && f.Hash == hash);
        }

        public async Task<IEnumerable<FileEntity>> SearchFilesByNameAsync(string searchTerm)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted &&
                                     (f.OriginalFileName.Contains(searchTerm) ||
                                      (f.Description != null && f.Description.Contains(searchTerm))))
                              .OrderBy(f => f.OriginalFileName)
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetFilesByExtensionAsync(string extension)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FileExtension == extension)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetFilesByContentTypeAsync(string contentType)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.ContentType == contentType)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetUnprocessedFilesAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && !f.IsProcessed)
                              .OrderBy(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetFilesByUserIdAsync(int userId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.CreatedByUserId == userId)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetRecentFilesAsync(int count = 10)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted)
                              .OrderByDescending(f => f.CreatedAt)
                              .Take(count)
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetLargeFilesAsync(long minSizeInBytes)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FileSize >= minSizeInBytes)
                              .OrderByDescending(f => f.FileSize)
                              .ToListAsync();
        }

        public async Task<long> GetTotalFileSizeAsync()
        {
            return await _dbSet.Where(f => !f.IsDeleted)
                              .SumAsync(f => f.FileSize);
        }

        public async Task<long> GetTotalFileSizeByFolderAsync(int? folderId)
        {
            return await _dbSet.Where(f => !f.IsDeleted && f.FolderId == folderId)
                              .SumAsync(f => f.FileSize);
        }

        public async Task<int> GetFileCountByTypeAsync(FileType fileType)
        {
            return await _dbSet.Where(f => !f.IsDeleted && f.FileType == fileType)
                              .CountAsync();
        }

        public async Task<bool> FileExistsByHashAsync(string hash)
        {
            return await _dbSet.AnyAsync(f => !f.IsDeleted && f.Hash == hash);
        }

        public async Task<bool> UpdateDownloadCountAsync(int fileId)
        {
            var file = await _dbSet.FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);
            if (file == null) return false;

            file.DownloadCount++;
            file.LastAccessedAt = DateTime.UtcNow;
            file.UpdatedAt = DateTime.UtcNow;

            Update(file);
            await SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateLastAccessedAsync(int fileId)
        {
            var file = await _dbSet.FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);
            if (file == null) return false;

            file.LastAccessedAt = DateTime.UtcNow;
            file.UpdatedAt = DateTime.UtcNow;

            Update(file);
            await SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<FileEntity>> GetDuplicateFilesAsync()
        {
            var duplicateHashes = await _dbSet.Where(f => !f.IsDeleted && f.Hash != null)
                                             .GroupBy(f => f.Hash)
                                             .Where(g => g.Count() > 1)
                                             .Select(g => g.Key)
                                             .ToListAsync();

            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && duplicateHashes.Contains(f.Hash))
                              .OrderBy(f => f.Hash)
                              .ThenBy(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetOrphanedFilesAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted &&
                                     f.FolderId.HasValue &&
                                     !_context.Set<Folder>().Any(folder => folder.Id == f.FolderId && !folder.IsDeleted))
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetFilesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted &&
                                     f.CreatedAt >= startDate &&
                                     f.CreatedAt <= endDate)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetFilesWithThumbnailsAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.ThumbnailContent != null)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<FileEntity>> GetFilesWithoutThumbnailsAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted &&
                                     f.ThumbnailContent == null &&
                                     f.FileType == FileType.Image)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }
    }
}