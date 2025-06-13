using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface ICompanyRepository : IRepository<Company>
    {
        Task<Company?> GetCompanyWithDetailsAsync();
        Task<Company?> GetCompanyWithDetailsAsync(int companyId);
    }
}
