using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;

namespace Backend.CMS.Infrastructure.Services
{
    public class VideoService : IVideoService
    {
        private readonly IVideoRepository _videoRepository;
        private readonly IMapper _mapper;

        public VideoService(IVideoRepository videoRepository, IMapper mapper)
        {
            _videoRepository = videoRepository;
            _mapper = mapper;
        }

        public async Task<VideoDto?> GetByIdAsync(int id)
        {
            var video = await _videoRepository.GetByIdAsync(id);
            return video != null ? _mapper.Map<VideoDto>(video) : null;
        }

        public async Task<IEnumerable<VideoDto>> GetAllAsync()
        {
            var videos = await _videoRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<VideoDto>>(videos);
        }

        public async Task<IEnumerable<VideoDto>> GetByFolderIdAsync(int folderId)
        {
            var videos = await _videoRepository.GetByFolderIdAsync(folderId);
            return _mapper.Map<IEnumerable<VideoDto>>(videos);
        }

        public async Task<VideoDto> CreateAsync(CreateVideoDto createDto)
        {
            var video = _mapper.Map<Video>(createDto);
            
            // Generate filename if not provided
            if (string.IsNullOrEmpty(video.FileName))
            {
                var extension = Path.GetExtension(video.Name) ?? ".mp4";
                video.FileName = $"{Guid.NewGuid()}{extension}";
            }
            
            // Set extension from filename
            video.Extension = Path.GetExtension(video.FileName);
            
            // Set size from content
            video.Size = video.Content.Length;

            await _videoRepository.AddAsync(video);
            await _videoRepository.SaveChangesAsync();
            
            return _mapper.Map<VideoDto>(video);
        }

        public async Task<VideoDto> UpdateAsync(int id, UpdateVideoDto updateDto)
        {
            var video = await _videoRepository.GetByIdAsync(id);
            if (video == null)
                throw new ArgumentException($"Video with ID {id} not found");

            _mapper.Map(updateDto, video);
            
            _videoRepository.Update(video);
            await _videoRepository.SaveChangesAsync();
            
            return _mapper.Map<VideoDto>(video);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var video = await _videoRepository.GetByIdAsync(id);
            if (video == null)
                return false;

            _videoRepository.Remove(video);
            await _videoRepository.SaveChangesAsync();
            return true;
        }

        public async Task<VideoDto?> GetByNameAsync(string name)
        {
            var video = await _videoRepository.GetByNameAsync(name);
            return video != null ? _mapper.Map<VideoDto>(video) : null;
        }

        public async Task<IEnumerable<VideoDto>> GetByContentTypeAsync(string contentType)
        {
            var videos = await _videoRepository.GetByContentTypeAsync(contentType);
            return _mapper.Map<IEnumerable<VideoDto>>(videos);
        }

        public async Task<PaginatedResult<VideoDto>> GetPagedAsync(int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _videoRepository.GetCountAsync();
            
            if (totalCount == 0)
            {
                return new PaginatedResult<VideoDto>
                {
                    Data = new List<VideoDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var videos = await _videoRepository.GetPagedAsync(pageNumber, pageSize);
            var videoDtos = _mapper.Map<List<VideoDto>>(videos);

            return new PaginatedResult<VideoDto>
            {
                Data = videoDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PaginatedResult<VideoDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _videoRepository.GetCountByFolderIdAsync(folderId);
            
            if (totalCount == 0)
            {
                return new PaginatedResult<VideoDto>
                {
                    Data = new List<VideoDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var videos = await _videoRepository.GetByFolderIdPagedAsync(folderId, pageNumber, pageSize);
            var videoDtos = _mapper.Map<List<VideoDto>>(videos);

            return new PaginatedResult<VideoDto>
            {
                Data = videoDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<List<VideoDto>> GetVideosByEntityAsync(string entityType, int entityId)
        {
            var videos = await _videoRepository.GetByEntityAsync(entityType, entityId);
            return _mapper.Map<List<VideoDto>>(videos);
        }

        // Implement IFileService<VideoDto, CreateVideoDto> interface methods
        public async Task<VideoDto> UploadAsync(CreateVideoDto createDto)
        {
            return await CreateAsync(createDto);
        }

        public async Task<FileModel> DownloadAsync(int id)
        {
            return await DownloadVideoAsync(id);
        }

        public async Task<VideoDto> UploadVideoAsync(FileUploadDto uploadDto)
        {
            var createDto = new CreateVideoDto
            {
                Name = uploadDto.Name ?? uploadDto.File.FileName,
                Description = uploadDto.Description,
                ContentType = uploadDto.ContentType,
                FolderId = uploadDto.FolderId
            };
            
            return await CreateAsync(createDto);
        }

        public async Task<List<VideoDto>> UploadMultipleVideosAsync(MultipleFileUploadDto uploadDto)
        {
            var results = new List<VideoDto>();
            
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
                
                var result = await UploadVideoAsync(singleUpload);
                results.Add(result);
            }
            
            return results;
        }

        public async Task<FileModel> DownloadVideoAsync(int id)
        {
            var video = await _videoRepository.GetByIdAsync(id);
            if (video == null)
                throw new KeyNotFoundException($"Video with id {id} not found.");

            return new FileModel
            {
                Content = video.Content,
                ContentType = video.ContentType,
                FileName = video.FileName
            };
        }
    }
}