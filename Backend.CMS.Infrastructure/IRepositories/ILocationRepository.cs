using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.IRepositories;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface ILocationRepository : IRepository<Location>
    {
        Task<Location?> GetMainLocationAsync();
        Task<IEnumerable<Location>> GetLocationsByCompanyAsync(int companyId);
        Task<Location?> GetWithAddressesAndContactsAsync(int locationId);
        Task<bool> LocationCodeExistsAsync(string locationCode, int? excludeLocationId = null);
        Task<IEnumerable<Location>> SearchLocationsAsync(string searchTerm, int page, int pageSize);
    }
}