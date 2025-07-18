using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;

namespace Frontend.Interfaces
{
    public interface IFileService : IDisposable
    {
        #region Enhanced Core File Operations with Pagination

        /// <summary>
        /// Get paginated list of files with filtering and sorting
        /// </summary>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Items per page</param>
        /// <param name="folderId">Optional folder filter</param>
        /// <param name="search">Optional search term</param>
        /// <param name="fileType">Optional file type filter</param>
        /// <param name="isPublic">Optional visibility filter</param>
        /// <param name="sortBy">Sort field</param>
        /// <param name="sortDirection">Sort direction</param>
        /// <returns>Paginated result with file DTOs</returns>
        Task<PaginatedResult<FileDto>> GetFilesAsync(
            int pageNumber = 1,
            int pageSize = 10,
            int? folderId = null,
            string? search = null,
            FileType? fileType = null,
            bool? isPublic = null,
            string sortBy = "CreatedAt",
            string sortDirection = "Desc");

        /// <summary>
        /// Search files with advanced criteria and pagination
        /// </summary>
        /// <param name="searchDto">Search criteria with pagination</param>
        /// <returns>Paginated search results</returns>
        Task<PaginatedResult<FileDto>> SearchFilesAsync(FileSearchDto searchDto);

        /// <summary>
        /// Get files by folder with pagination
        /// </summary>
        /// <param name="folderId">Folder ID (null for root)</param>
        /// <param name="pageNumber">Page number</param>
        /// <param name="pageSize">Items per page</param>
        /// <returns>Paginated files in folder</returns>
        Task<PaginatedResult<FileDto>> GetFilesByFolderAsync(int? folderId, int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// Get single file by ID
        /// </summary>
        /// <param name="id">File identifier</param>
        /// <returns>File DTO with URLs populated</returns>
        Task<FileDto?> GetFileByIdAsync(int id);

        #endregion

        #region File Upload Operations

        /// <summary>
        /// Upload a single file
        /// </summary>
        /// <param name="uploadDto">File upload data</param>
        /// <returns>Upload result with file DTO</returns>
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
        /// Update file metadata and properties
        /// </summary>
        /// <param name="id">File identifier</param>
        /// <param name="updateDto">Update data</param>
        /// <returns>Updated file DTO</returns>
        Task<FileDto?> UpdateFileAsync(int id, UpdateFileDto updateDto);

        /// <summary>
        /// Delete a single file
        /// </summary>
        /// <param name="id">File identifier</param>
        /// <returns>True if successfully deleted</returns>
        Task<bool> DeleteFileAsync(int id);

        /// <summary>
        /// Delete multiple files
        /// </summary>
        /// <param name="fileIds">List of file identifiers</param>
        /// <returns>Bulk operation result</returns>
        Task<BulkOperationResultDto> DeleteMultipleFilesAsync(List<int> fileIds);

        /// <summary>
        /// Move file to different folder
        /// </summary>
        /// <param name="moveDto">Move operation data</param>
        /// <returns>Updated file DTO</returns>
        Task<FileDto?> MoveFileAsync(MoveFileDto moveDto);

        /// <summary>
        /// Copy file
        /// </summary>
        /// <param name="copyDto">Copy operation data</param>
        /// <returns>New file DTO</returns>
        Task<FileDto?> CopyFileAsync(CopyFileDto copyDto);

        #endregion

        #region File Access and Preview

        /// <summary>
        /// Get file preview information
        /// </summary>
        /// <param name="id">File identifier</param>
        /// <returns>Preview DTO with URLs</returns>
        Task<FilePreviewDto> GetFilePreviewAsync(int id);

        /// <summary>
        /// Get recent files
        /// </summary>
        /// <param name="count">Number of recent files</param>
        /// <returns>List of recent files</returns>
        Task<List<FileDto>> GetRecentFilesAsync(int count = 10);

        /// <summary>
        /// Get file system statistics
        /// </summary>
        /// <returns>Statistics dictionary</returns>
        Task<Dictionary<string, object>> GetFileStatisticsAsync();

        /// <summary>
        /// Generate thumbnail for image files
        /// </summary>
        /// <param name="id">File identifier</param>
        /// <returns>True if thumbnail generated</returns>
        Task<bool> GenerateThumbnailAsync(int id);

        #endregion

        #region Download and Streaming

        /// <summary>
        /// Generate download token for secure access
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Download token</returns>
        Task<string> GenerateDownloadTokenAsync(int fileId);

        /// <summary>
        /// Download file using browser download
        /// </summary>
        /// <param name="id">File identifier</param>
        /// <returns>Task completion</returns>
        Task DownloadFileAsync(int id);

        /// <summary>
        /// Get file content stream
        /// </summary>
        /// <param name="id">File identifier</param>
        /// <returns>Stream, content type, and filename</returns>
        Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int id);

        /// <summary>
        /// Get thumbnail stream
        /// </summary>
        /// <param name="id">File identifier</param>
        /// <returns>Thumbnail stream, content type, and filename</returns>
        Task<(Stream stream, string contentType, string fileName)> GetThumbnailStreamAsync(int id);

        #endregion

        #region Video and Audio Streaming

        /// <summary>
        /// Get streaming URL for video/audio files
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Streaming URL</returns>
        string GetStreamingUrl(int fileId);

        /// <summary>
        /// Get streaming URL with authentication token for private files
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Streaming URL with token</returns>
        Task<string> GetStreamingUrlWithTokenAsync(int fileId);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Update multiple files with same data
        /// </summary>
        /// <param name="fileIds">List of file identifiers</param>
        /// <param name="updateDto">Update data</param>
        /// <returns>Bulk operation result</returns>
        Task<BulkOperationResultDto> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto);

        /// <summary>
        /// Move multiple files to new folder
        /// </summary>
        /// <param name="fileIds">List of file identifiers</param>
        /// <param name="destinationFolderId">Target folder ID</param>
        /// <returns>Bulk operation result</returns>
        Task<BulkOperationResultDto> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId);

        /// <summary>
        /// Copy multiple files to new folder
        /// </summary>
        /// <param name="fileIds">List of file identifiers</param>
        /// <param name="destinationFolderId">Target folder ID</param>
        /// <returns>Bulk operation result with new files</returns>
        Task<BulkOperationResultDto> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId);

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get file URL (download for non-streamable, streaming for video/audio)
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>File URL</returns>
        string GetFileUrl(int fileId);

        /// <summary>
        /// Get thumbnail URL
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Thumbnail URL</returns>
        string GetThumbnailUrl(int fileId);

        /// <summary>
        /// Get preview URL
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Preview URL</returns>
        string GetPreviewUrl(int fileId);

        /// <summary>
        /// Format file size in human readable format
        /// </summary>
        /// <param name="bytes">Size in bytes</param>
        /// <returns>Formatted size string</returns>
        string FormatFileSize(long bytes);

        /// <summary>
        /// Format duration in human readable format
        /// </summary>
        /// <param name="duration">Duration timespan</param>
        /// <returns>Formatted duration string</returns>
        string FormatDuration(TimeSpan duration);

        #endregion

        #region Diagnostics and Integrity

        /// <summary>
        /// Verify file integrity using hash validation
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>True if file integrity is valid</returns>
        Task<bool> VerifyFileIntegrityAsync(int fileId);

        /// <summary>
        /// Get detailed diagnostic information for a file
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Diagnostic information object</returns>
        Task<object> GetFileDiagnosticInfoAsync(int fileId);

        #endregion

        #region Cache and Performance

        /// <summary>
        /// Clear all cached data
        /// </summary>
        /// <returns>Task completion</returns>
        Task ClearCacheAsync();

        /// <summary>
        /// Preload files into cache
        /// </summary>
        /// <param name="fileIds">List of file identifiers to preload</param>
        /// <returns>Task completion</returns>
        Task PreloadFilesAsync(List<int> fileIds);

        #endregion

        #region Entity-Specific Operations

        /// <summary>
        /// Get files linked to a specific entity
        /// </summary>
        /// <param name="entityType">Entity type (e.g., "Category", "Product")</param>
        /// <param name="entityId">Entity identifier</param>
        /// <param name="fileType">Optional file type filter</param>
        /// <returns>List of files linked to the entity</returns>
        Task<List<FileDto>> GetFilesForEntityAsync(string entityType, int entityId, FileType? fileType = null);

        /// <summary>
        /// Upload file and link it to a specific entity
        /// </summary>
        /// <param name="uploadDto">Upload data with entity information</param>
        /// <returns>Upload result with file DTO</returns>
        Task<FileUploadResultDto?> UploadFileForEntityAsync(FileUploadDto uploadDto);

        /// <summary>
        /// Upload multiple files and link them to a specific entity
        /// </summary>
        /// <param name="uploadDto">Multiple upload data with entity information</param>
        /// <returns>Bulk operation result</returns>
        Task<BulkOperationResultDto> UploadMultipleFilesForEntityAsync(MultipleFileUploadDto uploadDto);

        /// <summary>
        /// Delete all files linked to a specific entity
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="entityId">Entity identifier</param>
        /// <returns>Bulk operation result</returns>
        Task<BulkOperationResultDto> DeleteFilesForEntityAsync(string entityType, int entityId);

        /// <summary>
        /// Count files linked to a specific entity
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="entityId">Entity identifier</param>
        /// <param name="fileType">Optional file type filter</param>
        /// <returns>Number of files linked to the entity</returns>
        Task<int> CountFilesForEntityAsync(string entityType, int entityId, FileType? fileType = null);

        #endregion
    }
}