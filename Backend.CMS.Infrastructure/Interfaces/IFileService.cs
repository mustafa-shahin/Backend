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

        #region Type-Specific Upload Operations

        /// <summary>
        /// Upload and process an image file with metadata extraction
        /// </summary>
        /// <param name="uploadDto">Image upload data</param>
        /// <returns>Image file DTO with extracted metadata</returns>
        Task<ImageFileDto> UploadImageAsync(FileUploadDto uploadDto);

        /// <summary>
        /// Upload and process a video file with metadata extraction
        /// </summary>
        /// <param name="uploadDto">Video upload data</param>
        /// <returns>Video file DTO with extracted metadata</returns>
        Task<VideoFileDto> UploadVideoAsync(FileUploadDto uploadDto);

        /// <summary>
        /// Upload and process an audio file with metadata extraction
        /// </summary>
        /// <param name="uploadDto">Audio upload data</param>
        /// <returns>Audio file DTO with extracted metadata</returns>
        Task<AudioFileDto> UploadAudioAsync(FileUploadDto uploadDto);

        /// <summary>
        /// Upload and process a document file with metadata extraction
        /// </summary>
        /// <param name="uploadDto">Document upload data</param>
        /// <returns>Document file DTO with extracted metadata</returns>
        Task<DocumentFileDto> UploadDocumentAsync(FileUploadDto uploadDto);

        /// <summary>
        /// Upload and process an archive file with content analysis
        /// </summary>
        /// <param name="uploadDto">Archive upload data</param>
        /// <returns>Archive file DTO with extracted metadata</returns>
        Task<ArchiveFileDto> UploadArchiveAsync(FileUploadDto uploadDto);

        #endregion

        #region Type-Specific Retrieval Operations

        /// <summary>
        /// Get image files with type-specific properties
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>Paginated image files</returns>
        Task<PaginatedResult<ImageFileDto>> GetImageFilesAsync(ImageSearchDto searchDto);

        /// <summary>
        /// Get video files with type-specific properties
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>Paginated video files</returns>
        Task<PaginatedResult<VideoFileDto>> GetVideoFilesAsync(VideoSearchDto searchDto);

        /// <summary>
        /// Get audio files with type-specific properties
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>Paginated audio files</returns>
        Task<PaginatedResult<AudioFileDto>> GetAudioFilesAsync(AudioSearchDto searchDto);

        /// <summary>
        /// Get document files with type-specific properties
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>Paginated document files</returns>
        Task<PaginatedResult<DocumentFileDto>> GetDocumentFilesAsync(DocumentSearchDto searchDto);

        /// <summary>
        /// Get archive files with type-specific properties
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>Paginated archive files</returns>
        Task<PaginatedResult<ArchiveFileDto>> GetArchiveFilesAsync(ArchiveSearchDto searchDto);

        #endregion

        #region Type-Specific Operations

        /// <summary>
        /// Get image file by ID with full image metadata
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Image file DTO with metadata</returns>
        Task<ImageFileDto?> GetImageByIdAsync(int fileId);

        /// <summary>
        /// Get video file by ID with full video metadata
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Video file DTO with metadata</returns>
        Task<VideoFileDto?> GetVideoByIdAsync(int fileId);

        /// <summary>
        /// Get audio file by ID with full audio metadata
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Audio file DTO with metadata</returns>
        Task<AudioFileDto?> GetAudioByIdAsync(int fileId);

        /// <summary>
        /// Get document file by ID with full document metadata
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Document file DTO with metadata</returns>
        Task<DocumentFileDto?> GetDocumentByIdAsync(int fileId);

        /// <summary>
        /// Get archive file by ID with full archive metadata
        /// </summary>
        /// <param name="fileId">File identifier</param>
        /// <returns>Archive file DTO with metadata</returns>
        Task<ArchiveFileDto?> GetArchiveByIdAsync(int fileId);

        /// <summary>
        /// Extract and update metadata for an existing image file
        /// </summary>
        /// <param name="fileId">Image file ID</param>
        /// <returns>True if metadata was updated successfully</returns>
        Task<bool> ExtractImageMetadataAsync(int fileId);

        /// <summary>
        /// Generate thumbnail for an image at specific size
        /// </summary>
        /// <param name="fileId">Image file ID</param>
        /// <param name="width">Thumbnail width</param>
        /// <param name="height">Thumbnail height</param>
        /// <returns>True if thumbnail was generated successfully</returns>
        Task<bool> GenerateImageThumbnailAsync(int fileId, int width = 200, int height = 200);

        /// <summary>
        /// Extract video metadata and generate thumbnail
        /// </summary>
        /// <param name="fileId">Video file ID</param>
        /// <param name="thumbnailTimestamp">Timestamp for thumbnail generation</param>
        /// <returns>True if processing was successful</returns>
        Task<bool> ProcessVideoFileAsync(int fileId, TimeSpan? thumbnailTimestamp = null);

        /// <summary>
        /// Extract audio metadata including ID3 tags and album art
        /// </summary>
        /// <param name="fileId">Audio file ID</param>
        /// <returns>True if metadata extraction was successful</returns>
        Task<bool> ExtractAudioMetadataAsync(int fileId);

        /// <summary>
        /// Extract document metadata and generate thumbnail
        /// </summary>
        /// <param name="fileId">Document file ID</param>
        /// <returns>True if processing was successful</returns>
        Task<bool> ProcessDocumentFileAsync(int fileId);

        /// <summary>
        /// Analyze archive contents and extract file list
        /// </summary>
        /// <param name="fileId">Archive file ID</param>
        /// <returns>True if analysis was successful</returns>
        Task<bool> AnalyzeArchiveContentsAsync(int fileId);

        /// <summary>
        /// Test archive integrity
        /// </summary>
        /// <param name="fileId">Archive file ID</param>
        /// <returns>True if archive is valid</returns>
        Task<bool> TestArchiveIntegrityAsync(int fileId);

        #endregion
    }
}