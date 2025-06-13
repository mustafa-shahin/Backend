using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Application.Interfaces
{
    public interface ICompanyService
    {
        Task<CompanyDto> GetCompanyAsync();
        Task<CompanyDto> UpdateCompanyAsync(UpdateCompanyDto updateCompanyDto);
    }
}