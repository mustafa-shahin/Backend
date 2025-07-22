using Backend.CMS.Application.DTOs;
using Backend.CMS.Infrastructure.Services;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IDocumentService : IFileService<DocumentDto, CreateDocumentDto>
    {
        Task<DocumentDto?> GetByIdAsync(int id);
        Task<IEnumerable<DocumentDto>> GetAllAsync();
        Task<IEnumerable<DocumentDto>> GetByFolderIdAsync(int folderId);
        Task<DocumentDto> CreateAsync(CreateDocumentDto createDto);
        Task<DocumentDto> UpdateAsync(int id, UpdateDocumentDto updateDto);
        Task<bool> DeleteAsync(int id);
        Task<DocumentDto?> GetByNameAsync(string name);
        Task<IEnumerable<DocumentDto>> GetByContentTypeAsync(string contentType);
        Task<PaginatedResult<DocumentDto>> GetPagedAsync(int pageNumber, int pageSize);
        Task<PaginatedResult<DocumentDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize);
        
        // Methods called by controllers but missing from interface
        Task<PaginatedResult<DocumentDto>> GetDocumentsPagedAsync(DocumentSearchDto searchDto);
        Task<DocumentDto> UploadDocumentAsync(FileUploadDto uploadDto);
        Task<List<DocumentDto>> UploadMultipleDocumentsAsync(MultipleFileUploadDto uploadDto);
        Task<DocumentDto?> GetDocumentByIdAsync(int id);
        Task<DocumentDto> UpdateDocumentAsync(int id, UpdateDocumentDto updateDto);
        Task<bool> DeleteDocumentAsync(int id);
        Task<FileModel> DownloadDocumentAsync(int id);
    }
}