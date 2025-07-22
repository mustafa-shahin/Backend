using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;

namespace Backend.CMS.Infrastructure.Services
{
    public class ArchiveService : IArchiveService
    {
        private readonly IArchiveRepository _archiveRepository;
        private readonly IMapper _mapper;

        public ArchiveService(IArchiveRepository archiveRepository, IMapper mapper)
        {
            _archiveRepository = archiveRepository;
            _mapper = mapper;
        }

        public async Task<ArchiveDto?> GetByIdAsync(int id)
        {
            var archive = await _archiveRepository.GetByIdAsync(id);
            return archive != null ? _mapper.Map<ArchiveDto>(archive) : null;
        }

        public async Task<IEnumerable<ArchiveDto>> GetAllAsync()
        {
            var archives = await _archiveRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<ArchiveDto>>(archives);
        }

        public async Task<IEnumerable<ArchiveDto>> GetByFolderIdAsync(int folderId)
        {
            var archives = await _archiveRepository.GetByFolderIdAsync(folderId);
            return _mapper.Map<IEnumerable<ArchiveDto>>(archives);
        }

        public async Task<ArchiveDto> CreateAsync(CreateArchiveDto createDto)
        {
            var archive = _mapper.Map<Archive>(createDto);
            
            // Generate filename if not provided
            if (string.IsNullOrEmpty(archive.FileName))
            {
                var extension = Path.GetExtension(archive.Name) ?? ".zip";
                archive.FileName = $"{Guid.NewGuid()}{extension}";
            }
            
            // Set extension from filename
            archive.Extension = Path.GetExtension(archive.FileName);
            
            // Set size from content
            archive.Size = archive.Content.Length;

            await _archiveRepository.AddAsync(archive);
            await _archiveRepository.SaveChangesAsync();
            
            return _mapper.Map<ArchiveDto>(archive);
        }

        public async Task<ArchiveDto> UpdateAsync(int id, UpdateArchiveDto updateDto)
        {
            var archive = await _archiveRepository.GetByIdAsync(id);
            if (archive == null)
                throw new ArgumentException($"Archive with ID {id} not found");

            _mapper.Map(updateDto, archive);
            
            _archiveRepository.Update(archive);
            await _archiveRepository.SaveChangesAsync();
            
            return _mapper.Map<ArchiveDto>(archive);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var archive = await _archiveRepository.GetByIdAsync(id);
            if (archive == null)
                return false;

            _archiveRepository.Remove(archive);
            await _archiveRepository.SaveChangesAsync();
            return true;
        }

        public async Task<ArchiveDto?> GetByNameAsync(string name)
        {
            var archive = await _archiveRepository.GetByNameAsync(name);
            return archive != null ? _mapper.Map<ArchiveDto>(archive) : null;
        }

        public async Task<IEnumerable<ArchiveDto>> GetByContentTypeAsync(string contentType)
        {
            var archives = await _archiveRepository.GetByContentTypeAsync(contentType);
            return _mapper.Map<IEnumerable<ArchiveDto>>(archives);
        }

        public async Task<PaginatedResult<ArchiveDto>> GetPagedAsync(int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _archiveRepository.GetCountAsync();
            
            if (totalCount == 0)
            {
                return new PaginatedResult<ArchiveDto>
                {
                    Data = new List<ArchiveDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var archives = await _archiveRepository.GetPagedAsync(pageNumber, pageSize);
            var archiveDtos = _mapper.Map<List<ArchiveDto>>(archives);

            return new PaginatedResult<ArchiveDto>
            {
                Data = archiveDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PaginatedResult<ArchiveDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _archiveRepository.GetCountByFolderIdAsync(folderId);
            
            if (totalCount == 0)
            {
                return new PaginatedResult<ArchiveDto>
                {
                    Data = new List<ArchiveDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var archives = await _archiveRepository.GetByFolderIdPagedAsync(folderId, pageNumber, pageSize);
            var archiveDtos = _mapper.Map<List<ArchiveDto>>(archives);

            return new PaginatedResult<ArchiveDto>
            {
                Data = archiveDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PaginatedResult<ArchiveDto>> GetArchivesPagedAsync(ArchiveSearchDto searchDto)
        {
            var pageNumber = Math.Max(1, searchDto.PageNumber);
            var pageSize = Math.Clamp(searchDto.PageSize, 1, 100);

            IEnumerable<Archive> archives;
            int totalCount;

            if (searchDto.FolderId.HasValue)
            {
                archives = await _archiveRepository.GetByFolderIdPagedAsync(searchDto.FolderId.Value, pageNumber, pageSize);
                totalCount = await _archiveRepository.GetCountByFolderIdAsync(searchDto.FolderId.Value);
            }
            else
            {
                archives = await _archiveRepository.GetPagedAsync(pageNumber, pageSize);
                totalCount = await _archiveRepository.GetCountAsync();
            }

            var archiveDtos = _mapper.Map<List<ArchiveDto>>(archives);

            // Apply client-side filtering if needed
            if (!string.IsNullOrEmpty(searchDto.Name))
            {
                archiveDtos = archiveDtos.Where(a => a.Name.Contains(searchDto.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(searchDto.ContentType))
            {
                archiveDtos = archiveDtos.Where(a => a.ContentType.Contains(searchDto.ContentType, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return new PaginatedResult<ArchiveDto>
            {
                Data = archiveDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        // Implement IFileService<ArchiveDto, CreateArchiveDto> interface methods
        public async Task<ArchiveDto> UploadAsync(CreateArchiveDto createDto)
        {
            return await CreateAsync(createDto);
        }

        public async Task<FileModel> DownloadAsync(int id)
        {
            return await DownloadArchiveAsync(id);
        }

        public async Task<ArchiveDto> UploadArchiveAsync(FileUploadDto uploadDto)
        {
            var createDto = new CreateArchiveDto
            {
                Name = uploadDto.Name ?? uploadDto.File.FileName,
                Description = uploadDto.Description,
                ContentType = uploadDto.ContentType,
                FolderId = uploadDto.FolderId
            };
            
            return await CreateAsync(createDto);
        }

        public async Task<List<ArchiveDto>> UploadMultipleArchivesAsync(MultipleFileUploadDto uploadDto)
        {
            var results = new List<ArchiveDto>();
            
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
                
                var result = await UploadArchiveAsync(singleUpload);
                results.Add(result);
            }
            
            return results;
        }

        public async Task<ArchiveDto?> GetArchiveByIdAsync(int id)
        {
            return await GetByIdAsync(id);
        }

        public async Task<ArchiveDto> UpdateArchiveAsync(int id, UpdateArchiveDto updateDto)
        {
            return await UpdateAsync(id, updateDto);
        }

        public async Task<bool> DeleteArchiveAsync(int id)
        {
            return await DeleteAsync(id);
        }

        public async Task<FileModel> DownloadArchiveAsync(int id)
        {
            var archive = await _archiveRepository.GetByIdAsync(id);
            if (archive == null)
                throw new KeyNotFoundException($"Archive with id {id} not found.");

            return new FileModel
            {
                Content = archive.Content,
                ContentType = archive.ContentType,
                FileName = archive.FileName
            };
        }
    }
}