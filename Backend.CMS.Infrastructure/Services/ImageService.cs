using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class ImageService : FileServiceBase<Image, ImageDto, CreateImageDto>, IImageService
    {

        public ImageService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<ImageService> logger) 
            : base(unitOfWork, mapper, logger)
        {
        }

        public async Task<ImageDto?> GetByIdAsync(int id)
        {
            var image = await _unitOfWork.Images.GetByIdAsync(id);
            return image != null ? _mapper.Map<ImageDto>(image) : null;
        }

        public async Task<IEnumerable<ImageDto>> GetAllAsync()
        {
            var images = await _unitOfWork.Images.GetAllAsync();
            return _mapper.Map<IEnumerable<ImageDto>>(images);
        }

        public async Task<IEnumerable<ImageDto>> GetByFolderIdAsync(int folderId)
        {
            var images = await _unitOfWork.Images.GetByFolderIdAsync(folderId);
            return _mapper.Map<IEnumerable<ImageDto>>(images);
        }

        public async Task<ImageDto> CreateAsync(CreateImageDto createDto)
        {
            var image = _mapper.Map<Image>(createDto);
            
            // Generate filename if not provided
            if (string.IsNullOrEmpty(image.FileName))
            {
                var extension = Path.GetExtension(image.Name) ?? ".jpg";
                image.FileName = $"{Guid.NewGuid()}{extension}";
            }
            
            // Set extension from filename
            image.Extension = Path.GetExtension(image.FileName);
            
            // Set size from content
            image.Size = image.Content.Length;

            await _unitOfWork.Images.AddAsync(image);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<ImageDto>(image);
        }

        public async Task<ImageDto> UpdateAsync(int id, UpdateImageDto updateDto)
        {
            var image = await _unitOfWork.Images.GetByIdAsync(id);
            if (image == null)
                throw new ArgumentException($"Image with ID {id} not found");

            _mapper.Map(updateDto, image);
            
            _unitOfWork.Images.Update(image);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<ImageDto>(image);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var image = await _unitOfWork.Images.GetByIdAsync(id);
            if (image == null)
                return false;

            _unitOfWork.Images.Remove(image);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<ImageDto?> GetByNameAsync(string name)
        {
            var image = await _unitOfWork.Images.GetByNameAsync(name);
            return image != null ? _mapper.Map<ImageDto>(image) : null;
        }

        public async Task<IEnumerable<ImageDto>> GetByContentTypeAsync(string contentType)
        {
            var images = await _unitOfWork.Images.GetByContentTypeAsync(contentType);
            return _mapper.Map<IEnumerable<ImageDto>>(images);
        }

        public async Task<PaginatedResult<ImageDto>> GetPagedAsync(int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _unitOfWork.Images.GetCountAsync();
            
            if (totalCount == 0)
            {
                return new PaginatedResult<ImageDto>
                {
                    Data = new List<ImageDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var images = await _unitOfWork.Images.GetPagedAsync(pageNumber, pageSize);
            var imageDtos = _mapper.Map<List<ImageDto>>(images);

            return new PaginatedResult<ImageDto>
            {
                Data = imageDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PaginatedResult<ImageDto>> GetByFolderIdPagedAsync(int folderId, int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var totalCount = await _unitOfWork.Images.GetCountByFolderIdAsync(folderId);
            
            if (totalCount == 0)
            {
                return new PaginatedResult<ImageDto>
                {
                    Data = new List<ImageDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            var images = await _unitOfWork.Images.GetByFolderIdPagedAsync(folderId, pageNumber, pageSize);
            var imageDtos = _mapper.Map<List<ImageDto>>(images);

            return new PaginatedResult<ImageDto>
            {
                Data = imageDtos,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<List<ImageDto>> GetImagesByEntityAsync(string entityType, int entityId)
        {
            var images = await _unitOfWork.Images.GetByEntityAsync(entityType, entityId);
            return _mapper.Map<List<ImageDto>>(images);
        }

        // Upload/Download methods required by interface
        public async Task<ImageDto> UploadImageAsync(FileUploadDto uploadDto)
        {
            if (uploadDto?.File == null || uploadDto.File.Length == 0)
                throw new ArgumentException("Image file is required");

            var createDto = new CreateImageDto
            {
                Name = uploadDto.Name ?? uploadDto.File.FileName,
                Description = uploadDto.Description,
                FolderId = uploadDto.FolderId,
                Alt = uploadDto.Alt,
                ContentType = uploadDto.File.ContentType
            };

            // Read file content efficiently with pre-allocated buffer
            byte[] fileContent;
            using (var memoryStream = new MemoryStream((int)uploadDto.File.Length))
            {
                await uploadDto.File.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            // Verify content integrity
            if (fileContent.Length != uploadDto.File.Length)
            {
                _logger.LogError("File corruption detected during upload. Expected {ExpectedSize} bytes, got {ActualSize} bytes", 
                    uploadDto.File.Length, fileContent.Length);
                throw new InvalidOperationException("File corruption detected during upload");
            }

            createDto.Content = fileContent;
            return await UploadAsync(createDto);
        }

        public async Task<List<ImageDto>> UploadMultipleImagesAsync(MultipleFileUploadDto uploadDto)
        {
            if (uploadDto?.Files == null || !uploadDto.Files.Any())
                throw new ArgumentException("At least one image file is required");

            var results = new List<ImageDto>();
            foreach (var file in uploadDto.Files)
            {
                try
                {
                    var createDto = new CreateImageDto
                    {
                        Name = file.FileName,
                        Description = uploadDto.Description,
                        FolderId = uploadDto.FolderId,
                        ContentType = file.ContentType
                    };

                    // Read file content efficiently with pre-allocated buffer
                    byte[] fileContent;
                    using (var memoryStream = new MemoryStream((int)file.Length))
                    {
                        await file.CopyToAsync(memoryStream);
                        fileContent = memoryStream.ToArray();
                    }

                    // Verify content integrity
                    if (fileContent.Length != file.Length)
                    {
                        _logger.LogError("File corruption detected during upload. Expected {ExpectedSize} bytes, got {ActualSize} bytes for file {FileName}", 
                            file.Length, fileContent.Length, file.FileName);
                        continue; // Skip corrupted file
                    }

                    createDto.Content = fileContent;
                    var result = await UploadAsync(createDto);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload image {FileName}", file.FileName);
                    // Continue with next file
                }
            }
            return results;
        }

        public async Task<ImageDto> UploadImageForEntityAsync(FileUploadDto uploadDto)
        {
            if (uploadDto?.File == null || uploadDto.File.Length == 0)
                throw new ArgumentException("Image file is required");

            if (string.IsNullOrWhiteSpace(uploadDto.EntityType))
                throw new ArgumentException("Entity type is required");

            if (uploadDto.EntityId <= 0)
                throw new ArgumentException("Entity ID must be greater than 0");

            var createDto = new CreateImageDto
            {
                Name = uploadDto.File.FileName,
                Description = uploadDto.Description ?? $"Image uploaded for {uploadDto.EntityType} {uploadDto.EntityId}",
                FolderId = uploadDto.FolderId,
                Alt = uploadDto.Alt,
                ContentType = uploadDto.File.ContentType,
                EntityType = uploadDto.EntityType,
                EntityId = uploadDto.EntityId
            };

            // Read file content efficiently with pre-allocated buffer
            byte[] fileContent;
            using (var memoryStream = new MemoryStream((int)uploadDto.File.Length))
            {
                await uploadDto.File.CopyToAsync(memoryStream);
                fileContent = memoryStream.ToArray();
            }

            // Verify content integrity
            if (fileContent.Length != uploadDto.File.Length)
            {
                _logger.LogError("File corruption detected during upload. Expected {ExpectedSize} bytes, got {ActualSize} bytes for file {FileName}", 
                    uploadDto.File.Length, fileContent.Length, uploadDto.File.FileName);
                throw new InvalidOperationException("File corruption detected during upload");
            }

            createDto.Content = fileContent;
            
            _logger.LogInformation("File content read: {ContentLength} bytes for file {FileName}", 
                createDto.Content.Length, uploadDto.File.FileName);

            return await CreateAsync(createDto);
        }

        public async Task<List<ImageDto>> UploadMultipleImagesForEntityAsync(MultipleFileUploadDto uploadDto)
        {
            if (uploadDto?.Files == null || !uploadDto.Files.Any())
                throw new ArgumentException("At least one image file is required");

            if (string.IsNullOrWhiteSpace(uploadDto.EntityType))
                throw new ArgumentException("Entity type is required");

            if (uploadDto.EntityId <= 0)
                throw new ArgumentException("Entity ID must be greater than 0");

            var results = new List<ImageDto>();
            foreach (var file in uploadDto.Files)
            {
                try
                {
                    var createDto = new CreateImageDto
                    {
                        Name = file.FileName,
                        Description = $"Image uploaded for {uploadDto.EntityType} {uploadDto.EntityId}",
                        FolderId = uploadDto.FolderId,
                        ContentType = file.ContentType,
                        EntityType = uploadDto.EntityType,
                        EntityId = uploadDto.EntityId
                    };

                    // Read file content efficiently with pre-allocated buffer
                    byte[] fileContent;
                    using (var memoryStream = new MemoryStream((int)file.Length))
                    {
                        await file.CopyToAsync(memoryStream);
                        fileContent = memoryStream.ToArray();
                    }

                    // Verify content integrity
                    if (fileContent.Length != file.Length)
                    {
                        _logger.LogError("File corruption detected during upload. Expected {ExpectedSize} bytes, got {ActualSize} bytes for file {FileName}", 
                            file.Length, fileContent.Length, file.FileName);
                        continue; // Skip corrupted file
                    }

                    createDto.Content = fileContent;
                    var result = await CreateAsync(createDto);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload image {FileName}", file.FileName);
                    // Continue with next file
                }
            }
            return results;
        }

        public async Task<FileModel> DownloadImageAsync(int id)
        {
            return await DownloadAsync(id);
        }

        // Abstract method implementations from FileServiceBase
        protected override IRepository<Image> GetRepository()
        {
            return _unitOfWork.Images;
        }

        protected override string GetFileNameFromDto(CreateImageDto dto)
        {
            return !string.IsNullOrEmpty(dto.Name) ? dto.Name : $"{Guid.NewGuid()}.jpg";
        }

        protected override byte[]? GetContentFromDto(CreateImageDto dto)
        {
            return dto.Content;
        }

        protected override string GetContentTypeFromDto(CreateImageDto dto)
        {
            return dto.ContentType;
        }

        protected override async Task OnEntityCreatingAsync(Image entity, CreateImageDto createDto)
        {
            // Set image-specific properties
            if (createDto.Alt != null)
                entity.Alt = createDto.Alt;
            
            // TODO: Extract width/height from image content if needed
            // This would require an image processing library
            
            await base.OnEntityCreatingAsync(entity, createDto);
        }
    }
}