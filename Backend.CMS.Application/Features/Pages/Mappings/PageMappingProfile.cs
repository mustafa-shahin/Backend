using AutoMapper;
using Backend.CMS.Application.Features.Pages.DTOs;
using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Application.Features.Pages.Mappings
{
    public class PageMappingProfile : Profile
    {
        public PageMappingProfile()
        {
            CreateMap<Page, PageDto>();
            CreateMap<PageComponent, PageComponentDto>();

            CreateMap<CreatePageDto, Page>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Components, opt => opt.Ignore())
                .ForMember(dest => dest.Permissions, opt => opt.Ignore());

            CreateMap<UpdatePageDto, Page>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Components, opt => opt.Ignore())
                .ForMember(dest => dest.Permissions, opt => opt.Ignore());
        }
    }
}
