using Backend.CMS.Domain.Entities.Files;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class FileRepository : Repository<BaseFileEntity>, IFileRepository
    {
        public FileRepository(ApplicationDbContext context) : base(context)
        {
        }

        #region Basic File Operations

        public async Task<IEnumerable<BaseFileEntity>> GetFilesByFolderIdAsync(int? folderId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FolderId == folderId)
                              .OrderBy(f => f.OriginalFileName)
                              .ToListAsync();
        }

        public async Task<IEnumerable<BaseFileEntity>> GetFilesByTypeAsync(FileType fileType)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FileType == fileType)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<BaseFileEntity>> GetPublicFilesAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.IsPublic)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<BaseFileEntity?> GetByStoredFileNameAsync(string storedFileName)
        {
            return await _dbSet.AsNoTracking()
                              .FirstOrDefaultAsync(f => !f.IsDeleted && f.StoredFileName == storedFileName);
        }

        public async Task<BaseFileEntity?> GetByHashAsync(string hash)
        {
            return await _dbSet.AsNoTracking()
                              .FirstOrDefaultAsync(f => !f.IsDeleted && f.Hash == hash);
        }

        public async Task<IEnumerable<BaseFileEntity>> SearchFilesByNameAsync(string searchTerm)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && 
                                     (f.OriginalFileName.Contains(searchTerm) || 
                                      f.Description != null && f.Description.Contains(searchTerm)))
                              .OrderBy(f => f.OriginalFileName)
                              .ToListAsync();
        }

        public async Task<IEnumerable<BaseFileEntity>> GetFilesByExtensionAsync(string extension)
        {
            var normalizedExtension = extension.TrimStart('.').ToLowerInvariant();
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FileExtension.ToLower() == normalizedExtension)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<BaseFileEntity>> GetFilesByContentTypeAsync(string contentType)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.ContentType == contentType)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<BaseFileEntity>> GetUnprocessedFilesAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && !f.IsProcessed)
                              .OrderBy(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<BaseFileEntity>> GetFilesByUserIdAsync(int userId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.CreatedByUserId.HasValue && f.CreatedByUserId.Value == userId)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<BaseFileEntity>> GetRecentFilesAsync(int count = 10)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted)
                              .OrderByDescending(f => f.CreatedAt)
                              .Take(count)
                              .ToListAsync();
        }

        public async Task<IEnumerable<BaseFileEntity>> GetLargeFilesAsync(long minSizeInBytes)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FileSize >= minSizeInBytes)
                              .OrderByDescending(f => f.FileSize)
                              .ToListAsync();
        }

        #endregion

        #region Statistics and Analytics

        public async Task<long> GetTotalFileSizeAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted)
                              .SumAsync(f => f.FileSize);
        }

        public async Task<long> GetTotalFileSizeByFolderAsync(int? folderId)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && f.FolderId == folderId)
                              .SumAsync(f => f.FileSize);
        }

        public async Task<int> GetFileCountByTypeAsync(FileType fileType)
        {
            return await _dbSet.AsNoTracking()
                              .CountAsync(f => !f.IsDeleted && f.FileType == fileType);
        }

        public async Task<bool> FileExistsByHashAsync(string hash)
        {
            return await _dbSet.AsNoTracking()
                              .AnyAsync(f => !f.IsDeleted && f.Hash == hash);
        }

        public async Task<bool> UpdateDownloadCountAsync(int fileId)
        {
            var file = await _dbSet.FindAsync(fileId);
            if (file == null || file.IsDeleted) return false;

            file.DownloadCount++;
            file.LastAccessedAt = DateTime.UtcNow;
            file.UpdatedAt = DateTime.UtcNow;

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateLastAccessedAsync(int fileId)
        {
            var file = await _dbSet.FindAsync(fileId);
            if (file == null || file.IsDeleted) return false;

            file.LastAccessedAt = DateTime.UtcNow;
            file.UpdatedAt = DateTime.UtcNow;

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<IEnumerable<BaseFileEntity>> GetDuplicateFilesAsync()
        {
            var duplicateHashes = await _dbSet.AsNoTracking()
                                             .Where(f => !f.IsDeleted && f.Hash != null)
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

        public async Task<IEnumerable<BaseFileEntity>> GetOrphanedFilesAsync()
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && 
                                     f.FolderId != null && 
                                     !_context.Folders.Any(folder => folder.Id == f.FolderId))
                              .ToListAsync();
        }

        public async Task<IEnumerable<BaseFileEntity>> GetFilesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbSet.AsNoTracking()
                              .Where(f => !f.IsDeleted && 
                                     f.CreatedAt >= startDate && 
                                     f.CreatedAt <= endDate)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<BaseFileEntity>> GetFilesWithThumbnailsAsync()
        {
            return await _dbSet.AsNoTracking()
                              .OfType<ImageFileEntity>()
                              .Where(f => !f.IsDeleted && f.ThumbnailContent != null)
                              .Cast<BaseFileEntity>()
                              .Union(_dbSet.AsNoTracking()
                                          .OfType<VideoFileEntity>()
                                          .Where(f => !f.IsDeleted && f.ThumbnailContent != null)
                                          .Cast<BaseFileEntity>())
                              .Union(_dbSet.AsNoTracking()
                                          .OfType<DocumentFileEntity>()
                                          .Where(f => !f.IsDeleted && f.ThumbnailContent != null)
                                          .Cast<BaseFileEntity>())
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<BaseFileEntity>> GetFilesWithoutThumbnailsAsync()
        {
            return await _dbSet.AsNoTracking()
                              .OfType<ImageFileEntity>()
                              .Where(f => !f.IsDeleted && f.ThumbnailContent == null)
                              .Cast<BaseFileEntity>()
                              .Union(_dbSet.AsNoTracking()
                                          .OfType<VideoFileEntity>()
                                          .Where(f => !f.IsDeleted && f.ThumbnailContent == null)
                                          .Cast<BaseFileEntity>())
                              .Union(_dbSet.AsNoTracking()
                                          .OfType<DocumentFileEntity>()
                                          .Where(f => !f.IsDeleted && f.ThumbnailContent == null)
                                          .Cast<BaseFileEntity>())
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        #endregion

        #region Type-Specific Queries

        public async Task<IEnumerable<ImageFileEntity>> GetImageFilesAsync(int? folderId = null)
        {
            var query = _dbSet.AsNoTracking()
                             .OfType<ImageFileEntity>()
                             .Where(f => !f.IsDeleted);

            if (folderId.HasValue)
                query = query.Where(f => f.FolderId == folderId);

            return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
        }

        public async Task<IEnumerable<ImageFileEntity>> GetImagesByDimensionsAsync(int? minWidth = null, int? maxWidth = null, int? minHeight = null, int? maxHeight = null)
        {
            var query = _dbSet.AsNoTracking()
                             .OfType<ImageFileEntity>()
                             .Where(f => !f.IsDeleted);

            if (minWidth.HasValue)
                query = query.Where(f => f.Width >= minWidth);
            if (maxWidth.HasValue)
                query = query.Where(f => f.Width <= maxWidth);
            if (minHeight.HasValue)
                query = query.Where(f => f.Height >= minHeight);
            if (maxHeight.HasValue)
                query = query.Where(f => f.Height <= maxHeight);

            return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
        }

        public async Task<IEnumerable<ImageFileEntity>> GetImagesWithGeoLocationAsync()
        {
            return await _dbSet.AsNoTracking()
                              .OfType<ImageFileEntity>()
                              .Where(f => !f.IsDeleted && f.Latitude != null && f.Longitude != null)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<VideoFileEntity>> GetVideoFilesAsync(int? folderId = null)
        {
            var query = _dbSet.AsNoTracking()
                             .OfType<VideoFileEntity>()
                             .Where(f => !f.IsDeleted);

            if (folderId.HasValue)
                query = query.Where(f => f.FolderId == folderId);

            return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
        }

        public async Task<IEnumerable<VideoFileEntity>> GetVideosByQualityAsync(int minWidth, int minHeight)
        {
            return await _dbSet.AsNoTracking()
                              .OfType<VideoFileEntity>()
                              .Where(f => !f.IsDeleted && 
                                     f.Width >= minWidth && 
                                     f.Height >= minHeight)
                              .OrderByDescending(f => f.Width)
                              .ThenByDescending(f => f.Height)
                              .ToListAsync();
        }

        public async Task<IEnumerable<VideoFileEntity>> GetVideosByDurationAsync(TimeSpan? minDuration = null, TimeSpan? maxDuration = null)
        {
            var query = _dbSet.AsNoTracking()
                             .OfType<VideoFileEntity>()
                             .Where(f => !f.IsDeleted && f.Duration != null);

            if (minDuration.HasValue)
                query = query.Where(f => f.Duration >= minDuration);
            if (maxDuration.HasValue)
                query = query.Where(f => f.Duration <= maxDuration);

            return await query.OrderByDescending(f => f.Duration).ToListAsync();
        }

        public async Task<IEnumerable<AudioFileEntity>> GetAudioFilesAsync(int? folderId = null)
        {
            var query = _dbSet.AsNoTracking()
                             .OfType<AudioFileEntity>()
                             .Where(f => !f.IsDeleted);

            if (folderId.HasValue)
                query = query.Where(f => f.FolderId == folderId);

            return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
        }

        public async Task<IEnumerable<AudioFileEntity>> GetAudioByArtistAsync(string artist)
        {
            return await _dbSet.AsNoTracking()
                              .OfType<AudioFileEntity>()
                              .Where(f => !f.IsDeleted && 
                                     f.Artist != null && 
                                     f.Artist.ToLower().Contains(artist.ToLower()))
                              .OrderBy(f => f.Album)
                              .ThenBy(f => f.TrackNumber)
                              .ToListAsync();
        }

        public async Task<IEnumerable<AudioFileEntity>> GetAudioByAlbumAsync(string album)
        {
            return await _dbSet.AsNoTracking()
                              .OfType<AudioFileEntity>()
                              .Where(f => !f.IsDeleted && 
                                     f.Album != null && 
                                     f.Album.ToLower().Contains(album.ToLower()))
                              .OrderBy(f => f.TrackNumber)
                              .ToListAsync();
        }

        public async Task<IEnumerable<AudioFileEntity>> GetAudioByGenreAsync(string genre)
        {
            return await _dbSet.AsNoTracking()
                              .OfType<AudioFileEntity>()
                              .Where(f => !f.IsDeleted && 
                                     f.Genre != null && 
                                     f.Genre.ToLower().Contains(genre.ToLower()))
                              .OrderBy(f => f.Artist)
                              .ThenBy(f => f.Album)
                              .ToListAsync();
        }

        public async Task<IEnumerable<DocumentFileEntity>> GetDocumentFilesAsync(int? folderId = null)
        {
            var query = _dbSet.AsNoTracking()
                             .OfType<DocumentFileEntity>()
                             .Where(f => !f.IsDeleted);

            if (folderId.HasValue)
                query = query.Where(f => f.FolderId == folderId);

            return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
        }

        public async Task<IEnumerable<DocumentFileEntity>> GetDocumentsByAuthorAsync(string author)
        {
            return await _dbSet.AsNoTracking()
                              .OfType<DocumentFileEntity>()
                              .Where(f => !f.IsDeleted && 
                                     f.Author != null && 
                                     f.Author.ToLower().Contains(author.ToLower()))
                              .OrderByDescending(f => f.CreationDate)
                              .ToListAsync();
        }

        public async Task<IEnumerable<DocumentFileEntity>> GetPasswordProtectedDocumentsAsync()
        {
            return await _dbSet.AsNoTracking()
                              .OfType<DocumentFileEntity>()
                              .Where(f => !f.IsDeleted && f.IsPasswordProtected)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<ArchiveFileEntity>> GetArchiveFilesAsync(int? folderId = null)
        {
            var query = _dbSet.AsNoTracking()
                             .OfType<ArchiveFileEntity>()
                             .Where(f => !f.IsDeleted);

            if (folderId.HasValue)
                query = query.Where(f => f.FolderId == folderId);

            return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
        }

        public async Task<IEnumerable<ArchiveFileEntity>> GetCorruptedArchivesAsync()
        {
            return await _dbSet.AsNoTracking()
                              .OfType<ArchiveFileEntity>()
                              .Where(f => !f.IsDeleted && f.IsCorrupted)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<ArchiveFileEntity>> GetPasswordProtectedArchivesAsync()
        {
            return await _dbSet.AsNoTracking()
                              .OfType<ArchiveFileEntity>()
                              .Where(f => !f.IsDeleted && f.IsPasswordProtected)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<OtherFileEntity>> GetOtherFilesAsync(int? folderId = null)
        {
            var query = _dbSet.AsNoTracking()
                             .OfType<OtherFileEntity>()
                             .Where(f => !f.IsDeleted);

            if (folderId.HasValue)
                query = query.Where(f => f.FolderId == folderId);

            return await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
        }

        public async Task<IEnumerable<OtherFileEntity>> GetExecutableFilesAsync()
        {
            return await _dbSet.AsNoTracking()
                              .OfType<OtherFileEntity>()
                              .Where(f => !f.IsDeleted && f.IsExecutable)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        public async Task<IEnumerable<OtherFileEntity>> GetPotentiallyDangerousFilesAsync()
        {
            return await _dbSet.AsNoTracking()
                              .OfType<OtherFileEntity>()
                              .Where(f => !f.IsDeleted && f.IsPotentiallyDangerous)
                              .OrderByDescending(f => f.CreatedAt)
                              .ToListAsync();
        }

        #endregion

    }
}