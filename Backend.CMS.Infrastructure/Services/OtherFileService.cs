using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;

namespace Backend.CMS.Infrastructure.Services
{
    public class OtherFileService : IOtherFileService
    {
        private readonly IOtherFileRepository _otherFileRepository;
        private readonly IMapper _mapper;

        public OtherFileService(IOtherFileRepository otherFileRepository, IMapper mapper)
        {
            _otherFileRepository = otherFileRepository;
            _mapper = mapper;
        }

        public async Task<OtherFileDto?> GetByIdAsync(int id)
        {
            var otherFile = await _otherFileRepository.GetByIdAsync(id);
            return otherFile != null ? _mapper.Map<OtherFileDto>(otherFile) : null;
        }

        public async Task<IEnumerable<OtherFileDto>> GetAllAsync()
        {
            var otherFiles = await _otherFileRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<OtherFileDto>>(otherFiles);
        }

        public async Task<IEnumerable<OtherFileDto>> GetByFolderIdAsync(int folderId)
        {
            var otherFiles = await _otherFileRepository.GetByFolderIdAsync(folderId);
            return _mapper.Map<IEnumerable<OtherFileDto>>(otherFiles);
        }

        public async Task<OtherFileDto> CreateAsync(CreateOtherFileDto createDto)
        {
            var otherFile = _mapper.Map<OtherFile>(createDto);
            
            // Generate filename if not provided
            if (string.IsNullOrEmpty(otherFile.FileName))
            {
                var extension = Path.GetExtension(otherFile.Name) ?? ".bin";
                otherFile.FileName = $"{Guid.NewGuid()}{extension}";
            }
            
            // Set extension from filename
            otherFile.Extension = Path.GetExtension(otherFile.FileName);
            
            // Set size from content
            otherFile.Size = otherFile.Content.Length;

            await _otherFileRepository.AddAsync(otherFile);
            await _otherFileRepository.SaveChangesAsync();
            
            return _mapper.Map<OtherFileDto>(otherFile);
        }

        public async Task<OtherFileDto> UpdateAsync(int id, UpdateOtherFileDto updateDto)
        {
            var otherFile = await _otherFileRepository.GetByIdAsync(id);
            if (otherFile == null)
                throw new ArgumentException($"OtherFile with ID {id} not found");

            _mapper.Map(updateDto, otherFile);
            
            _otherFileRepository.Update(otherFile);
            await _otherFileRepository.SaveChangesAsync();
            
            return _mapper.Map<OtherFileDto>(otherFile);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var otherFile = await _otherFileRepository.GetByIdAsync(id);
            if (otherFile == null)
                return false;

            _otherFileRepository.Remove(otherFile);
            await _otherFileRepository.SaveChangesAsync();
            return true;
        }

        public async Task<OtherFileDto?> GetByNameAsync(string name)
        {
            var otherFile = await _otherFileRepository.GetByNameAsync(name);
            return otherFile != null ? _mapper.Map<OtherFileDto>(otherFile) : null;
        }

        public async Task<IEnumerable<OtherFileDto>> GetByContentTypeAsync(string contentType)
        {
            var otherFiles = await _otherFileRepository.GetByContentTypeAsync(contentType);
            return _mapper.Map<IEnumerable<OtherFileDto>>(otherFiles);
        }

        public async Task<PaginatedResult<OtherFileDto>> GetPagedAsync(int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _otherFileRepository.GetCountAsync();
            
            if (totalCount == 0)
            {
                return new PaginatedResult<OtherFileDto>
                {
                    Data = new List<OtherFileDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var otherFiles = await _otherFileRepository.GetPagedAsync(pageNumber, pageSize);
            var otherFileDtos = _mapper.Map<List<OtherFileDto>>(otherFiles);

            return new PaginatedResult<OtherFileDto>
            {
                Data = otherFileDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PaginatedResult<OtherFileDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _otherFileRepository.GetCountByFolderIdAsync(folderId);
            
            if (totalCount == 0)
            {
                return new PaginatedResult<OtherFileDto>
                {
                    Data = new List<OtherFileDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var otherFiles = await _otherFileRepository.GetByFolderIdPagedAsync(folderId, pageNumber, pageSize);
            var otherFileDtos = _mapper.Map<List<OtherFileDto>>(otherFiles);

            return new PaginatedResult<OtherFileDto>
            {
                Data = otherFileDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        // Implement IFileService<OtherFileDto, CreateOtherFileDto> interface methods
        public async Task<OtherFileDto> UploadAsync(CreateOtherFileDto createDto)
        {
            return await CreateAsync(createDto);
        }

        public async Task<FileModel> DownloadAsync(int id)
        {
            return await DownloadOtherFileAsync(id);
        }

        // Additional methods to match other file services
        public async Task<PaginatedResult<OtherFileDto>> GetOtherFilesPagedAsync(OtherFileSearchDto searchDto)
        {
            var pageNumber = Math.Max(1, searchDto.PageNumber);
            var pageSize = Math.Clamp(searchDto.PageSize, 1, 100);

            IEnumerable<OtherFile> otherFiles;
            int totalCount;

            if (searchDto.FolderId.HasValue)
            {
                otherFiles = await _otherFileRepository.GetByFolderIdPagedAsync(searchDto.FolderId.Value, pageNumber, pageSize);
                totalCount = await _otherFileRepository.GetCountByFolderIdAsync(searchDto.FolderId.Value);
            }
            else
            {
                otherFiles = await _otherFileRepository.GetPagedAsync(pageNumber, pageSize);
                totalCount = await _otherFileRepository.GetCountAsync();
            }

            var otherFileDtos = _mapper.Map<List<OtherFileDto>>(otherFiles);

            // Apply client-side filtering if needed
            if (!string.IsNullOrEmpty(searchDto.Name))
            {
                otherFileDtos = otherFileDtos.Where(f => f.Name.Contains(searchDto.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(searchDto.ContentType))
            {
                otherFileDtos = otherFileDtos.Where(f => f.ContentType.Contains(searchDto.ContentType, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return new PaginatedResult<OtherFileDto>
            {
                Data = otherFileDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<OtherFileDto> UploadOtherFileAsync(FileUploadDto uploadDto)
        {
            var createDto = new CreateOtherFileDto
            {
                Name = uploadDto.Name ?? uploadDto.File.FileName,
                Description = uploadDto.Description,
                ContentType = uploadDto.ContentType,
                FolderId = uploadDto.FolderId
            };
            
            return await CreateAsync(createDto);
        }

        public async Task<List<OtherFileDto>> UploadMultipleOtherFilesAsync(MultipleFileUploadDto uploadDto)
        {
            var results = new List<OtherFileDto>();
            
            foreach (var file in uploadDto.Files)
            {
                byte[] content;
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    content = memoryStream.ToArray();
                }

                var singleUpload = new FileUploadDto
                {
                    File = file,
                    Name = file.FileName,
                    Description = uploadDto.Description,
                    FolderId = uploadDto.FolderId
                };
                
                var result = await UploadOtherFileAsync(singleUpload);
                results.Add(result);
            }
            
            return results;
        }

        public async Task<OtherFileDto?> GetOtherFileByIdAsync(int id)
        {
            return await GetByIdAsync(id);
        }

        public async Task<OtherFileDto> UpdateOtherFileAsync(int id, UpdateOtherFileDto updateDto)
        {
            return await UpdateAsync(id, updateDto);
        }

        public async Task<bool> DeleteOtherFileAsync(int id)
        {
            return await DeleteAsync(id);
        }

        public async Task<FileModel> DownloadOtherFileAsync(int id)
        {
            var otherFile = await _otherFileRepository.GetByIdAsync(id);
            if (otherFile == null)
                throw new KeyNotFoundException($"Other file with id {id} not found.");

            return new FileModel
            {
                Content = otherFile.Content,
                ContentType = otherFile.ContentType,
                FileName = otherFile.FileName
            };
        }
    }
}