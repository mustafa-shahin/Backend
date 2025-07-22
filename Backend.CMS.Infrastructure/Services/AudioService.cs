using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;

namespace Backend.CMS.Infrastructure.Services
{
    public class AudioService : IAudioService
    {
        private readonly IAudioRepository _audioRepository;
        private readonly IMapper _mapper;

        public AudioService(IAudioRepository audioRepository, IMapper mapper)
        {
            _audioRepository = audioRepository;
            _mapper = mapper;
        }

        public async Task<AudioDto?> GetByIdAsync(int id)
        {
            var audio = await _audioRepository.GetByIdAsync(id);
            return audio != null ? _mapper.Map<AudioDto>(audio) : null;
        }

        public async Task<IEnumerable<AudioDto>> GetAllAsync()
        {
            var audios = await _audioRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<AudioDto>>(audios);
        }

        public async Task<IEnumerable<AudioDto>> GetByFolderIdAsync(int folderId)
        {
            var audios = await _audioRepository.GetByFolderIdAsync(folderId);
            return _mapper.Map<IEnumerable<AudioDto>>(audios);
        }

        public async Task<AudioDto> CreateAsync(CreateAudioDto createDto)
        {
            var audio = _mapper.Map<Audio>(createDto);
            
            // Generate filename if not provided
            if (string.IsNullOrEmpty(audio.FileName))
            {
                var extension = Path.GetExtension(audio.Name) ?? ".mp3";
                audio.FileName = $"{Guid.NewGuid()}{extension}";
            }
            
            // Set extension from filename
            audio.Extension = Path.GetExtension(audio.FileName);
            
            // Set size from content
            audio.Size = audio.Content.Length;

            await _audioRepository.AddAsync(audio);
            await _audioRepository.SaveChangesAsync();
            
            return _mapper.Map<AudioDto>(audio);
        }

        public async Task<AudioDto> UpdateAsync(int id, UpdateAudioDto updateDto)
        {
            var audio = await _audioRepository.GetByIdAsync(id);
            if (audio == null)
                throw new ArgumentException($"Audio with ID {id} not found");

            _mapper.Map(updateDto, audio);
            
            _audioRepository.Update(audio);
            await _audioRepository.SaveChangesAsync();
            
            return _mapper.Map<AudioDto>(audio);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var audio = await _audioRepository.GetByIdAsync(id);
            if (audio == null)
                return false;

            _audioRepository.Remove(audio);
            await _audioRepository.SaveChangesAsync();
            return true;
        }

        public async Task<AudioDto?> GetByNameAsync(string name)
        {
            var audio = await _audioRepository.GetByNameAsync(name);
            return audio != null ? _mapper.Map<AudioDto>(audio) : null;
        }

        public async Task<IEnumerable<AudioDto>> GetByContentTypeAsync(string contentType)
        {
            var audios = await _audioRepository.GetByContentTypeAsync(contentType);
            return _mapper.Map<IEnumerable<AudioDto>>(audios);
        }

        public async Task<PaginatedResult<AudioDto>> GetPagedAsync(int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _audioRepository.GetCountAsync();
            
            if (totalCount == 0)
            {
                return new PaginatedResult<AudioDto>
                {
                    Data = new List<AudioDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var audios = await _audioRepository.GetPagedAsync(pageNumber, pageSize);
            var audioDtos = _mapper.Map<List<AudioDto>>(audios);

            return new PaginatedResult<AudioDto>
            {
                Data = audioDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PaginatedResult<AudioDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _audioRepository.GetCountByFolderIdAsync(folderId);
            
            if (totalCount == 0)
            {
                return new PaginatedResult<AudioDto>
                {
                    Data = new List<AudioDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var audios = await _audioRepository.GetByFolderIdPagedAsync(folderId, pageNumber, pageSize);
            var audioDtos = _mapper.Map<List<AudioDto>>(audios);

            return new PaginatedResult<AudioDto>
            {
                Data = audioDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<List<AudioDto>> GetAudiosByEntityAsync(string entityType, int entityId)
        {
            var audios = await _audioRepository.GetByEntityAsync(entityType, entityId);
            return _mapper.Map<List<AudioDto>>(audios);
        }

        public async Task<PaginatedResult<AudioDto>> GetAudiosPagedAsync(AudioSearchDto searchDto)
        {
            // For now, implement basic search functionality
            // You may need to extend the repository to support more advanced filtering
            var pageNumber = Math.Max(1, searchDto.PageNumber);
            var pageSize = Math.Clamp(searchDto.PageSize, 1, 100);

            IEnumerable<Audio> audios;
            int totalCount;

            if (searchDto.FolderId.HasValue)
            {
                audios = await _audioRepository.GetByFolderIdPagedAsync(searchDto.FolderId.Value, pageNumber, pageSize);
                totalCount = await _audioRepository.GetCountByFolderIdAsync(searchDto.FolderId.Value);
            }
            else
            {
                audios = await _audioRepository.GetPagedAsync(pageNumber, pageSize);
                totalCount = await _audioRepository.GetCountAsync();
            }

            var audioDtos = _mapper.Map<List<AudioDto>>(audios);

            // Apply client-side filtering if needed
            if (!string.IsNullOrEmpty(searchDto.Name))
            {
                audioDtos = audioDtos.Where(a => a.Name.Contains(searchDto.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return new PaginatedResult<AudioDto>
            {
                Data = audioDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        // Implement IFileService<AudioDto, CreateAudioDto> interface methods
        public async Task<AudioDto> UploadAsync(CreateAudioDto createDto)
        {
            return await CreateAsync(createDto);
        }

        public async Task<FileModel> DownloadAsync(int id)
        {
            return await DownloadAudioAsync(id);
        }

        public async Task<AudioDto> UploadAudioAsync(FileUploadDto uploadDto)
        {
            var createDto = new CreateAudioDto
            {
                Name = uploadDto.Name ?? uploadDto.File.FileName,
                Description = uploadDto.Description,
                ContentType = uploadDto.ContentType,
                FolderId = uploadDto.FolderId
            };
            
            return await CreateAsync(createDto);
        }

        public async Task<List<AudioDto>> UploadMultipleAudiosAsync(MultipleFileUploadDto uploadDto)
        {
            var results = new List<AudioDto>();
            
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
                
                var result = await UploadAudioAsync(singleUpload);
                results.Add(result);
            }
            
            return results;
        }

        public async Task<AudioDto?> GetAudioByIdAsync(int id)
        {
            return await GetByIdAsync(id);
        }

        public async Task<AudioDto> UpdateAudioAsync(int id, UpdateAudioDto updateDto)
        {
            return await UpdateAsync(id, updateDto);
        }

        public async Task<bool> DeleteAudioAsync(int id)
        {
            return await DeleteAsync(id);
        }

        public async Task<FileModel> DownloadAudioAsync(int id)
        {
            var audio = await _audioRepository.GetByIdAsync(id);
            if (audio == null)
                throw new KeyNotFoundException($"Audio with id {id} not found.");

            return new FileModel
            {
                Content = audio.Content,
                ContentType = audio.ContentType,
                FileName = audio.FileName
            };
        }
    }
}
