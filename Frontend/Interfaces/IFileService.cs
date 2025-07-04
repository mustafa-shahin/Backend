using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Components.Forms;

namespace Frontend.Interfaces
{
    /// <summary>
    /// Frontend file service interface with file management capabilities
    /// </summary>
    public interface IFileService
    {
        #region Core File Operations with Pagination

        /// <summary>
        /// Get paginated list of files with filtering and sorting
        /// </summary>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Items per page (1-100, default: 10)</param>
        /// <param name="folderId">Optional folder ID filter</param>
        /// <param name="search">Optional search term</param>
        /// <param name="fileType">Optional file type filter</param>
        /// <param name="isPublic">Optional public/private filter</param>
        /// <param name="sortBy">Sort field (default: CreatedAt)</param>
        /// <param name="sortDirection">Sort direction (Asc/Desc, default: Desc)</param>
        /// <returns>Paginated result with files</returns>
        Task<PagedResult<FileDto>> GetFilesAsync(
            int pageNumber = 1,
            int pageSize = 10,
            int? folderId = null,
            string? search = null,
            FileType? fileType = null,
            bool? isPublic = null,
            string sortBy = "CreatedAt",
            string sortDirection = "Desc");

        /// <summary>
        /// Advanced search with  filtering
        /// </summary>
        /// <param name="searchDto">Search criteria with pagination</param>
        /// <returns>Paginated search results</returns>
        Task<PagedResult<FileDto>> SearchFilesAsync(FileSearchDto searchDto);

        /// <summary>
        /// Get files in a specific folder with pagination
        /// </summary>
        /// <param name="folderId">Folder ID (null for root)</param>
        /// <param name="pageNumber">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Paginated files in folder</returns>
        Task<PagedResult<FileDto>> GetFilesByFolderAsync(int? folderId, int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// Get single file by ID
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>File details with URLs</returns>
        Task<FileDto?> GetFileByIdAsync(int id);

        #endregion

        #region File Upload Operations

        /// <summary>
        /// Upload single file
        /// </summary>
        /// <param name="uploadDto">Upload data</param>
        /// <returns>Upload result with file details</returns>
        Task<FileUploadResultDto?> UploadFileAsync(FileUploadDto uploadDto);

        /// <summary>
        /// Upload multiple files
        /// </summary>
        /// <param name="uploadDto">Multiple file upload data</param>
        /// <returns>Bulk operation result</returns>
        Task<BulkOperationResultDto> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto);

        #endregion

        #region File Management Operations

        /// <summary>
        /// Update file metadata
        /// </summary>
        /// <param name="id">File ID</param>
        /// <param name="updateDto">Update data</param>
        /// <returns>Updated file details</returns>
        Task<FileDto?> UpdateFileAsync(int id, UpdateFileDto updateDto);

        /// <summary>
        /// Delete single file
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteFileAsync(int id);

        /// <summary>
        /// Delete multiple files
        /// </summary>
        /// <param name="fileIds">File IDs to delete</param>
        /// <returns>Bulk operation result</returns>
        Task<BulkOperationResultDto> DeleteMultipleFilesAsync(List<int> fileIds);

        /// <summary>
        /// Move file to different folder
        /// </summary>
        /// <param name="moveDto">Move operation data</param>
        /// <returns>Updated file details</returns>
        Task<FileDto?> MoveFileAsync(MoveFileDto moveDto);

        /// <summary>
        /// Copy file
        /// </summary>
        /// <param name="copyDto">Copy operation data</param>
        /// <returns>New file details</returns>
        Task<FileDto?> CopyFileAsync(CopyFileDto copyDto);

        #endregion

        #region File Access and Preview

        /// <summary>
        /// Get file preview information
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>Preview data with URLs</returns>
        Task<FilePreviewDto> GetFilePreviewAsync(int id);

        /// <summary>
        /// Get recent files
        /// </summary>
        /// <param name="count">Number of files to retrieve</param>
        /// <returns>List of recent files</returns>
        Task<List<FileDto>> GetRecentFilesAsync(int count = 10);

        /// <summary>
        /// Get file system statistics
        /// </summary>
        /// <returns>Statistics data</returns>
        Task<Dictionary<string, object>> GetFileStatisticsAsync();

        /// <summary>
        /// Generate thumbnail for file
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>True if successful</returns>
        Task<bool> GenerateThumbnailAsync(int id);

        #endregion

        #region Download and Streaming

        /// <summary>
        /// Generate secure download token
        /// </summary>
        /// <param name="fileId">File ID</param>
        /// <returns>Download token</returns>
        Task<string> GenerateDownloadTokenAsync(int fileId);

        /// <summary>
        /// Download file (handles public/private automatically)
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>Download task</returns>
        Task DownloadFileAsync(int id);

        /// <summary>
        /// Get file stream
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>Stream, content type, and filename</returns>
        Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int id);

        /// <summary>
        /// Get thumbnail stream
        /// </summary>
        /// <param name="id">File ID</param>
        /// <returns>Thumbnail stream data</returns>
        Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int id);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Bulk update multiple files
        /// </summary>
        /// <param name="fileIds">File IDs</param>
        /// <param name="updateDto">Update data</param>
        /// <returns>Bulk operation result</returns>
        Task<BulkOperationResultDto> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto);

        /// <summary>
        /// Bulk move multiple files
        /// </summary>
        /// <param name="fileIds">File IDs</param>
        /// <param name="destinationFolderId">Target folder ID</param>
        /// <returns>Bulk operation result</returns>
        Task<BulkOperationResultDto> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId);

        /// <summary>
        /// Bulk copy multiple files
        /// </summary>
        /// <param name="fileIds">File IDs</param>
        /// <param name="destinationFolderId">Target folder ID</param>
        /// <returns>Bulk operation result with new files</returns>
        Task<BulkOperationResultDto> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId);

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get file download URL (from file DTO URLs)
        /// </summary>
        /// <param name="fileId">File ID</param>
        /// <returns>Download URL</returns>
        string GetFileUrl(int fileId);

        /// <summary>
        /// Get thumbnail URL (from file DTO URLs)
        /// </summary>
        /// <param name="fileId">File ID</param>
        /// <returns>Thumbnail URL</returns>
        string GetThumbnailUrl(int fileId);

        /// <summary>
        /// Get preview URL (from file DTO URLs)
        /// </summary>
        /// <param name="fileId">File ID</param>
        /// <returns>Preview URL</returns>
        string GetPreviewUrl(int fileId);

        /// <summary>
        /// Format file size in human-readable format
        /// </summary>
        /// <param name="bytes">Size in bytes</param>
        /// <returns>Formatted size string</returns>
        string FormatFileSize(long bytes);

        /// <summary>
        /// Format duration in human-readable format
        /// </summary>
        /// <param name="duration">Duration</param>
        /// <returns>Formatted duration string</returns>
        string FormatDuration(TimeSpan duration);

        #endregion

        #region Diagnostics and Integrity

        /// <summary>
        /// Verify file integrity
        /// </summary>
        /// <param name="fileId">File ID</param>
        /// <returns>True if integrity is valid</returns>
        Task<bool> VerifyFileIntegrityAsync(int fileId);

        /// <summary>
        /// Get file diagnostic information
        /// </summary>
        /// <param name="fileId">File ID</param>
        /// <returns>Diagnostic data</returns>
        Task<object> GetFileDiagnosticInfoAsync(int fileId);

        #endregion

        #region Cache and Performance

        /// <summary>
        /// Clear file cache (if implemented)
        /// </summary>
        /// <returns>Task completion</returns>
        Task ClearCacheAsync();

        /// <summary>
        /// Preload file metadata for better performance
        /// </summary>
        /// <param name="fileIds">File IDs to preload</param>
        /// <returns>Task completion</returns>
        Task PreloadFilesAsync(List<int> fileIds);

        #endregion
    }
}