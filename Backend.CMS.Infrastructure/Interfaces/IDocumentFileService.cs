using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IDocumentFileService
    {
        Task<DocumentFileDto> UploadDocumentAsync(FileUploadDto uploadDto);
        Task<List<DocumentFileDto>> UploadMultipleDocumentsAsync(MultipleFileUploadDto uploadDto);
        Task<PaginatedResult<DocumentFileDto>> GetDocumentsPagedAsync(DocumentSearchDto searchDto);
        Task<DocumentFileDto?> GetDocumentByIdAsync(int fileId);
        Task<DocumentFileDto> UpdateDocumentAsync(int fileId, UpdateDocumentDto updateDto);
        Task<bool> DeleteDocumentAsync(int fileId);
        Task<bool> ExtractDocumentMetadataAsync(int fileId);
        Task<bool> GenerateDocumentThumbnailAsync(int fileId, int pageNumber = 1);
        Task<bool> ExtractDocumentTextAsync(int fileId);
    }
}