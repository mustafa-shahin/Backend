using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class DocumentService : FileServiceBase<Document, DocumentDto, CreateDocumentDto>, IDocumentService
    {
        public DocumentService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<DocumentService> logger)
            : base(unitOfWork, mapper, logger)
        {
        }

        public async Task<DocumentDto?> GetByIdAsync(int id)
        {
            var document = await _unitOfWork.Documents.GetByIdAsync(id);
            return document != null ? _mapper.Map<DocumentDto>(document) : null;
        }

        public async Task<IEnumerable<DocumentDto>> GetAllAsync()
        {
            var documents = await _unitOfWork.Documents.GetAllAsync();
            return _mapper.Map<IEnumerable<DocumentDto>>(documents);
        }

        public async Task<IEnumerable<DocumentDto>> GetByFolderIdAsync(int folderId)
        {
            var documents = await _unitOfWork.Documents.GetByFolderIdAsync(folderId);
            return _mapper.Map<IEnumerable<DocumentDto>>(documents);
        }

        public async Task<DocumentDto> CreateAsync(CreateDocumentDto createDto)
        {
            var document = _mapper.Map<Document>(createDto);
            
            // Generate filename if not provided
            if (string.IsNullOrEmpty(document.FileName))
            {
                var extension = Path.GetExtension(document.Name) ?? ".pdf";
                document.FileName = $"{Guid.NewGuid()}{extension}";
            }
            
            // Set extension from filename
            document.Extension = Path.GetExtension(document.FileName);
            
            // Set size from content
            document.Size = document.Content.Length;

            await _unitOfWork.Documents.AddAsync(document);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<DocumentDto>(document);
        }

        public async Task<DocumentDto> UpdateAsync(int id, UpdateDocumentDto updateDto)
        {
            var document = await _unitOfWork.Documents.GetByIdAsync(id);
            if (document == null)
                throw new ArgumentException($"Document with ID {id} not found");

            _mapper.Map(updateDto, document);
            
            _unitOfWork.Documents.Update(document);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<DocumentDto>(document);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var document = await _unitOfWork.Documents.GetByIdAsync(id);
            if (document == null)
                return false;

            _unitOfWork.Documents.Remove(document);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<DocumentDto?> GetByNameAsync(string name)
        {
            var document = await _unitOfWork.Documents.GetByNameAsync(name);
            return document != null ? _mapper.Map<DocumentDto>(document) : null;
        }

        public async Task<IEnumerable<DocumentDto>> GetByContentTypeAsync(string contentType)
        {
            var documents = await _unitOfWork.Documents.GetByContentTypeAsync(contentType);
            return _mapper.Map<IEnumerable<DocumentDto>>(documents);
        }

        public async Task<PaginatedResult<DocumentDto>> GetPagedAsync(int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _unitOfWork.Documents.GetCountAsync();
            
            if (totalCount == 0)
            {
                return new PaginatedResult<DocumentDto>
                {
                    Data = new List<DocumentDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var documents = await _unitOfWork.Documents.GetPagedAsync(pageNumber, pageSize);
            var documentDtos = _mapper.Map<List<DocumentDto>>(documents);

            return new PaginatedResult<DocumentDto>
            {
                Data = documentDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PaginatedResult<DocumentDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _unitOfWork.Documents.GetCountByFolderIdAsync(folderId);
            
            if (totalCount == 0)
            {
                return new PaginatedResult<DocumentDto>
                {
                    Data = new List<DocumentDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var documents = await _unitOfWork.Documents.GetByFolderIdPagedAsync(folderId, pageNumber, pageSize);
            var documentDtos = _mapper.Map<List<DocumentDto>>(documents);

            return new PaginatedResult<DocumentDto>
            {
                Data = documentDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        // Missing methods called by controllers
        public async Task<PaginatedResult<DocumentDto>> GetDocumentsPagedAsync(DocumentSearchDto searchDto)
        {
            searchDto.PageNumber = Math.Max(1, searchDto.PageNumber);
            searchDto.PageSize = Math.Clamp(searchDto.PageSize, 1, 100);

            var totalCount = await _unitOfWork.Documents.GetCountAsync();
            var documents = await _unitOfWork.Documents.GetPagedAsync(searchDto.PageNumber, searchDto.PageSize);
            var documentDtos = _mapper.Map<List<DocumentDto>>(documents);

            return new PaginatedResult<DocumentDto>
            {
                Data = documentDtos,
                PageNumber = searchDto.PageNumber,
                PageSize = searchDto.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<DocumentDto> UploadDocumentAsync(FileUploadDto uploadDto)
        {
            var createDto = new CreateDocumentDto
            {
                Name = uploadDto.Name ?? uploadDto.File.FileName,
                Description = uploadDto.Description,
                FolderId = uploadDto.FolderId,
                ContentType = uploadDto.File.ContentType
            };

            using var memoryStream = new MemoryStream();
            await uploadDto.File.CopyToAsync(memoryStream);
            createDto.Content = memoryStream.ToArray();

            return await UploadAsync(createDto);
        }

        public async Task<List<DocumentDto>> UploadMultipleDocumentsAsync(MultipleFileUploadDto uploadDto)
        {
            var results = new List<DocumentDto>();
            foreach (var file in uploadDto.Files)
            {
                var createDto = new CreateDocumentDto
                {
                    Name = file.FileName,
                    Description = uploadDto.Description,
                    FolderId = uploadDto.FolderId,
                    ContentType = file.ContentType
                };

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                createDto.Content = memoryStream.ToArray();

                var result = await UploadAsync(createDto);
                results.Add(result);
            }
            return results;
        }

        public async Task<DocumentDto?> GetDocumentByIdAsync(int id)
        {
            return await GetByIdAsync(id);
        }

        public async Task<DocumentDto> UpdateDocumentAsync(int id, UpdateDocumentDto updateDto)
        {
            return await UpdateAsync(id, updateDto);
        }

        public async Task<bool> DeleteDocumentAsync(int id)
        {
            return await DeleteAsync(id);
        }

        public async Task<FileModel> DownloadDocumentAsync(int id)
        {
            return await DownloadAsync(id);
        }

        // Abstract method implementations from FileServiceBase
        protected override IRepository<Document> GetRepository()
        {
            return _unitOfWork.Documents;
        }

        protected override string GetFileNameFromDto(CreateDocumentDto dto)
        {
            return !string.IsNullOrEmpty(dto.Name) ? dto.Name : $"{Guid.NewGuid()}.pdf";
        }

        protected override byte[]? GetContentFromDto(CreateDocumentDto dto)
        {
            return dto.Content;
        }

        protected override string GetContentTypeFromDto(CreateDocumentDto dto)
        {
            return dto.ContentType;
        }
    }
}