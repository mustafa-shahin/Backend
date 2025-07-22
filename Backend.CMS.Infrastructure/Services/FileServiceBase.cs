using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Common;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class FileModel
    {
        public byte[] Content { get; set; } = [];
        public string ContentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public interface IFileService<TDto, TCreateDto>
    {
        Task<TDto> UploadAsync(TCreateDto createDto);
        Task<FileModel> DownloadAsync(int id);
    }

    public abstract class FileServiceBase<TEntity, TDto, TCreateDto> : IFileService<TDto, TCreateDto>
        where TEntity : BaseEntity, new()
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected readonly IMapper _mapper;
        protected readonly ILogger _logger;

        protected FileServiceBase(IUnitOfWork unitOfWork, IMapper mapper, ILogger logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public virtual async Task<TDto> UploadAsync(TCreateDto createDto)
        {
            if (createDto == null) throw new ArgumentNullException(nameof(createDto));

            var entity = _mapper.Map<TEntity>(createDto);
            var now = DateTime.UtcNow;

            SetFileMetadata(entity, createDto, now);

            await OnEntityCreatingAsync(entity, createDto);

            await GetRepository().AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully uploaded {EntityType} with ID {Id}", typeof(TEntity).Name, entity.Id);
            return _mapper.Map<TDto>(entity);
        }

        public virtual async Task<FileModel> DownloadAsync(int id)
        {
            var entity = await GetRepository().GetByIdAsync(id);
            if (entity == null)
                throw new KeyNotFoundException($"{typeof(TEntity).Name} with id {id} not found.");

            var content = GetFileContent(entity);
            var contentType = GetContentType(entity);
            var fileName = GetFileName(entity);

            return new FileModel
            {
                Content = content,
                ContentType = contentType,
                FileName = fileName
            };
        }

        protected virtual async Task OnEntityCreatingAsync(TEntity entity, TCreateDto createDto)
        {
            await Task.CompletedTask;
        }

        protected virtual void SetFileMetadata(TEntity entity, TCreateDto createDto, DateTime now)
        {
            var fileName = GetFileNameFromDto(createDto);
            var content = GetContentFromDto(createDto);
            var contentType = GetContentTypeFromDto(createDto);

            SetProperty(entity, nameof(BaseEntity.CreatedAt), now);
            SetProperty(entity, nameof(BaseEntity.UpdatedAt), now);
            SetProperty(entity, "FileName", fileName);
            SetProperty(entity, "Extension", Path.GetExtension(fileName)?.TrimStart('.') ?? string.Empty);
            SetProperty(entity, "Size", (long)(content?.Length ?? 0));
            SetProperty(entity, "ContentType", contentType);
        }

        protected virtual void SetProperty(TEntity entity, string propertyName, object value)
        {
            var property = typeof(TEntity).GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(entity, value);
            }
        }

        protected virtual byte[] GetFileContent(TEntity entity)
        {
            var property = typeof(TEntity).GetProperty("Content");
            return property?.GetValue(entity) as byte[] ?? [];
        }

        protected virtual string GetContentType(TEntity entity)
        {
            var property = typeof(TEntity).GetProperty("ContentType");
            return property?.GetValue(entity) as string ?? string.Empty;
        }

        protected virtual string GetFileName(TEntity entity)
        {
            var property = typeof(TEntity).GetProperty("FileName");
            return property?.GetValue(entity) as string ?? string.Empty;
        }

        protected abstract IRepository<TEntity> GetRepository();
        protected abstract string GetFileNameFromDto(TCreateDto dto);
        protected abstract byte[]? GetContentFromDto(TCreateDto dto);
        protected abstract string GetContentTypeFromDto(TCreateDto dto);
    }
}