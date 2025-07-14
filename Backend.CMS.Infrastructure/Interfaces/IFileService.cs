using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IFileService
    {
        #region File Upload Operations

        /// <summary>
        /// Upload a single file with
        /// </summary>
        /// <param name="uploadDto">File upload data transfer object</param>
        /// <returns>File data transfer object with generated URLs</returns>
        Task<FileDto> UploadFileAsync(FileUploadDto uploadDto);

        /// <summary>
        /// Upload multiple files with parallel processing support
        /// </summary>
        /// <param name="uploadDto">Multiple file upload data transfer object</param>
        /// <returns>List of uploaded file DTOs</returns>
        Task<List<FileDto>> UploadMultipleFilesAsync(MultipleFileUploadDto uploadDto);

        /// <summary>
        /// Upload file from URL with validation and processing
        /// </summary>
        /// <param name="url">Source URL</param>
        /// <param name="folderId">Optional folder ID</param>
        /// <param name="description">Optional description</param>
        /// <returns>Uploaded file DTO</returns>
        Task<FileDto> UploadFileFromUrlAsync(string url, int? folderId = null, string? description = null);

        #endregion

        #region File Retrieval Operations with Pagination

        /// <summary>
        /// Get paginated list of files with filtering and sorting
        /// </summary>
        /// <param name="searchDto">Search and pagination criteria</param>
        /// <returns>Paginated result with file DTOs including URLs</returns>
        Task<PaginatedResult<FileDto>> GetFilesPagedAsync(FileSearchDto searchDto);

        /// <summary>
        /// Search files with advanced criteria and pagination
        /// </summary>
        /// <param name="searchDto">Search criteria with pagination</param>
        /// <returns>Paginated search results</returns>
        Task<PaginatedResult<FileDto>> SearchFilesPagedAsync(FileSearchDto searchDto);

        /// <summary>
        /// Get files by folder with pagination
        /// </summary>
        /// <param name="folderId">Folder ID (null for root)</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Items per page</param>
        /// <returns>Paginated files in folder</returns>
        Task<PaginatedResult<FileDto>> GetFilesByFolderPagedAsync(int? folderId, int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// Get single file by ID with URLs
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>File DTO with all URLs populated</returns>
        Task<FileDto> GetFileByIdAsync(int fileId);

        #endregion



        #region File Stream Operations

        /// <summary>
        /// Get file content stream for download
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Stream, content type, and filename</returns>
        Task<(Stream stream, string contentType, string fileName)> GetFileStreamAsync(int fileId);

        /// <summary>
        /// Get thumbnail stream if available
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Thumbnail stream, content type, and filename</returns>
        Task<ThumbnailResult> GetThumbnailStreamAsync(int fileId);

        #endregion

        #region File Management Operations

        /// <summary>
        /// Update file metadata and properties
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <param name="updateDto">Update data</param>
        /// <returns>Updated file DTO with refreshed URLs</returns>
        Task<FileDto> UpdateFileAsync(int fileId, UpdateFileDto updateDto);

        /// <summary>
        /// Soft delete a single file
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>True if successfully deleted</returns>
        Task<bool> DeleteFileAsync(int fileId);

        /// <summary>
        /// Soft delete multiple files
        /// </summary>
        /// <param name="fileIds">List of file identifiers</param>
        /// <returns>True if all files deleted successfully</returns>
        Task<bool> DeleteMultipleFilesAsync(List<int> fileIds);

        /// <summary>
        /// Move file to different folder
        /// </summary>
        /// <param name="moveDto">Move operation data</param>
        /// <returns>Updated file DTO with new URLs</returns>
        Task<FileDto> MoveFileAsync(MoveFileDto moveDto);

        /// <summary>
        /// Create a copy of an existing file
        /// </summary>
        /// <param name="copyDto">Copy operation data</param>
        /// <returns>New file DTO with generated URLs</returns>
        Task<FileDto> CopyFileAsync(CopyFileDto copyDto);

        /// <summary>
        /// Rename a file
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <param name="newName">New filename</param>
        /// <returns>True if successfully renamed</returns>
        Task<bool> RenameFileAsync(int fileId, string newName);

        #endregion

        #region File Preview and Processing

        /// <summary>
        /// Get file preview information with URLs
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Preview DTO with all relevant URLs</returns>
        Task<FilePreviewDto> GetFilePreviewAsync(int fileId);


        /// <summary>
        /// Generate thumbnail for image files
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>True if thumbnail generated successfully</returns>
        Task<bool> GenerateThumbnailAsync(int fileId);

        /// <summary>
        /// Process file (thumbnails, metadata extraction, etc.)
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>True if processing completed successfully</returns>
        Task<bool> ProcessFileAsync(int fileId);

        #endregion

        #region File Access and Statistics

        /// <summary>
        /// Record file access for analytics
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <param name="accessType">Type of access</param>
        /// <returns>Task completion</returns>
        Task RecordFileAccessAsync(int fileId, FileAccessType accessType);

        /// <summary>
        /// Get recent files with URLs
        /// </summary>
        /// <param name="count">Number of recent files to retrieve</param>
        /// <returns>List of recent files with URLs</returns>
        Task<List<FileDto>> GetRecentFilesAsync(int count = 10);

        /// <summary>
        /// Get file system statistics
        /// </summary>
        /// <returns>Statistics dictionary</returns>
        Task<Dictionary<string, object>> GetFileStatisticsAsync();

        #endregion

        #region File Validation and Utilities

        /// <summary>
        /// Validate uploaded file against security and business rules
        /// </summary>
        /// <param name="file">Uploaded file</param>
        /// <returns>True if file passes all validation</returns>
        Task<bool> ValidateFileAsync(IFormFile file);

        /// <summary>
        /// Calculate SHA256 hash of file stream
        /// </summary>
        /// <param name="stream">File stream</param>
        /// <returns>Base64 encoded hash</returns>
        Task<string> GetFileHashAsync(Stream stream);

        /// <summary>
        /// Check if file exists by ID
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>True if file exists</returns>
        Task<bool> FileExistsAsync(int fileId);

        /// <summary>
        /// Calculate total size of files in folder
        /// </summary>
        /// <param name="folderId">Folder ID (null for all files)</param>
        /// <returns>Total size in bytes</returns>
        Task<long> GetTotalFileSizeAsync(int? folderId = null);

        #endregion

        #region File Integrity and Diagnostics

        /// <summary>
        /// Verify file integrity using hash and content validation
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>True if file integrity is valid</returns>
        Task<bool> VerifyFileIntegrityAsync(int fileId);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Update multiple files with same data
        /// </summary>
        /// <param name="fileIds">List of file identifiers</param>
        /// <param name="updateDto">Update data to apply</param>
        /// <returns>True if all files updated successfully</returns>
        Task<bool> BulkUpdateFilesAsync(List<int> fileIds, UpdateFileDto updateDto);

        /// <summary>
        /// Move multiple files to new folder
        /// </summary>
        /// <param name="fileIds">List of file identifiers</param>
        /// <param name="destinationFolderId">Target folder ID</param>
        /// <returns>True if all files moved successfully</returns>
        Task<bool> BulkMoveFilesAsync(List<int> fileIds, int? destinationFolderId);

        /// <summary>
        /// Copy multiple files to new folder
        /// </summary>
        /// <param name="fileIds">List of file identifiers</param>
        /// <param name="destinationFolderId">Target folder ID</param>
        /// <returns>List of new file DTOs with URLs</returns>
        Task<List<FileDto>> BulkCopyFilesAsync(List<int> fileIds, int? destinationFolderId);

        #endregion
    }
}