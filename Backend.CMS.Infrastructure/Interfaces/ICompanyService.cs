using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface ICompanyService
    {
        Task<CompanyDto> GetCompanyAsync();
        Task<CompanyDto> UpdateCompanyAsync(UpdateCompanyDto updateCompanyDto);
    }
}