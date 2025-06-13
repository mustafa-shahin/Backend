using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface ILocationRepository : IRepository<Location>
    {
        Task<Location?> GetMainLocationAsync();
        Task<IEnumerable<Location>> GetLocationsByCompanyAsync(int companyId);
        Task<bool> LocationCodeExistsAsync(string locationCode, int? excludeLocationId = null);
        Task<IEnumerable<Location>> SearchLocationsAsync(string searchTerm, int page, int pageSize);
        Task<IEnumerable<Location>> GetPagedWithRelatedAsync(int page, int pageSize);

    }
}