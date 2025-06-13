using AutoMapper;
using Backend.CMS.Application.DTOs.Components;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.IRepositories;

namespace Backend.CMS.Infrastructure.Services
{
    public class ComponentService : IComponentService
    {
        private readonly IRepository<ComponentTemplate> _templateRepository;
        private readonly IMapper _mapper;

        public ComponentService(IRepository<ComponentTemplate> templateRepository, IMapper mapper)
        {
            _templateRepository = templateRepository;
            _mapper = mapper;
        }

        public async Task<ComponentTemplateDto> GetComponentTemplateByIdAsync(int templateId)
        {
            var template = await _templateRepository.GetByIdAsync(templateId);
            if (template == null)
                throw new ArgumentException("Component template not found");

            return _mapper.Map<ComponentTemplateDto>(template);
        }

        public async Task<List<ComponentTemplateDto>> GetComponentTemplatesAsync()
        {
            var templates = await _templateRepository.GetAllAsync();
            return _mapper.Map<List<ComponentTemplateDto>>(templates);
        }

        public async Task<ComponentLibraryDto> GetComponentLibraryAsync()
        {
            var templates = await _templateRepository.FindAsync(t => t.IsActive);
            var groupedTemplates = templates.GroupBy(t => t.Category ?? "Other");

            var library = new ComponentLibraryDto
            {
                Categories = groupedTemplates.Select(g => new ComponentCategoryDto
                {
                    Name = g.Key,
                    DisplayName = g.Key,
                    Templates = _mapper.Map<List<ComponentTemplateDto>>(g.OrderBy(t => t.SortOrder))
                }).ToList()
            };

            return library;
        }

        public async Task<List<ComponentTemplateDto>> GetComponentTemplatesByTypeAsync(ComponentType type)
        {
            var templates = await _templateRepository.FindAsync(t => t.Type == type && t.IsActive);
            return _mapper.Map<List<ComponentTemplateDto>>(templates);
        }

        public async Task<List<ComponentTemplateDto>> GetComponentTemplatesByCategoryAsync(string category)
        {
            var templates = await _templateRepository.FindAsync(t => t.Category == category && t.IsActive);
            return _mapper.Map<List<ComponentTemplateDto>>(templates);
        }

        public async Task<ComponentTemplateDto> CreateComponentTemplateAsync(CreateComponentTemplateDto createComponentTemplateDto)
        {
            var template = _mapper.Map<ComponentTemplate>(createComponentTemplateDto);
            await _templateRepository.AddAsync(template);
            await _templateRepository.SaveChangesAsync();

            return _mapper.Map<ComponentTemplateDto>(template);
        }

        public async Task<ComponentTemplateDto> UpdateComponentTemplateAsync(int templateId, UpdateComponentTemplateDto updateComponentTemplateDto)
        {
            var template = await _templateRepository.GetByIdAsync(templateId);
            if (template == null)
                throw new ArgumentException("Component template not found");

            _mapper.Map(updateComponentTemplateDto, template);
            _templateRepository.Update(template);
            await _templateRepository.SaveChangesAsync();

            return _mapper.Map<ComponentTemplateDto>(template);
        }

        public async Task<bool> DeleteComponentTemplateAsync(int templateId)
        {
            var template = await _templateRepository.GetByIdAsync(templateId);
            if (template == null)
                return false;

            _templateRepository.Remove(template);
            await _templateRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ValidateComponentDataAsync(ComponentType type, Dictionary<string, object> data)
        {
            // Implementation for validating component data against schema
            return true;
        }

        public async Task<Dictionary<string, object>> GetDefaultPropertiesAsync(ComponentType type)
        {
            var templates = await _templateRepository.FindAsync(t => t.Type == type && t.IsSystemTemplate);
            var template = templates.FirstOrDefault();

            return template?.DefaultProperties ?? new Dictionary<string, object>();
        }

        public async Task<Dictionary<string, object>> GetDefaultStylesAsync(ComponentType type)
        {
            var templates = await _templateRepository.FindAsync(t => t.Type == type && t.IsSystemTemplate);
            var template = templates.FirstOrDefault();

            return template?.DefaultStyles ?? new Dictionary<string, object>();
        }

        public async Task<List<ComponentTemplateDto>> GetSystemTemplatesAsync()
        {
            var templates = await _templateRepository.FindAsync(t => t.IsSystemTemplate && t.IsActive);
            return _mapper.Map<List<ComponentTemplateDto>>(templates);
        }

        public async Task<List<ComponentTemplateDto>> GetCustomTemplatesAsync()
        {
            var templates = await _templateRepository.FindAsync(t => !t.IsSystemTemplate && t.IsActive);
            return _mapper.Map<List<ComponentTemplateDto>>(templates);
        }
    }
}