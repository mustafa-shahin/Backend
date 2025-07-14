using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface ILocationRepository : IRepository<Location>
    {
        Task<Location?> GetMainLocationAsync();
        Task<IEnumerable<Location>> GetLocationsByCompanyAsync(int companyId);
        Task<bool> LocationCodeExistsAsync(string locationCode, int? excludeLocationId = null);
        Task<Location?> GetWithAddressesAndContactsAsync(int locationId);
        IQueryable<Location> GetQueryableWithIncludes();
        IQueryable<Location> ApplySearchFilter(IQueryable<Location> query, string searchTerm);
    }
}