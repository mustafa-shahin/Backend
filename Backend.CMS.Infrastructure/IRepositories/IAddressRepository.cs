using Backend.CMS.Domain.Entities;

namespace Backend.CMS.Infrastructure.IRepositories
{
    public interface IAddressRepository : IRepository<Address>
    {
        Task<Address?> GetDefaultAddressAsync(int entityId, string entityType);
        Task<IEnumerable<Address>> GetAddressesByEntityAsync(int entityId, string entityType);
        Task<IEnumerable<Address>> GetAddressesByTypeAsync(string addressType);
        Task<IEnumerable<Address>> GetAddressesByLocationAsync(string city, string state, string country);
        Task<bool> SetDefaultAddressAsync(int addressId, int entityId, string entityType);
        Task<int> CountAddressesByEntityAsync(int entityId, string entityType);
        Task<bool> AddressExistsAsync(string street, string city, string postalCode, int? excludeAddressId = null);
        IQueryable<Address> GetAddressesQueryable();
        IQueryable<Address> GetAddressesByEntityQueryable(int entityId, string entityType);
        IQueryable<Address> GetAddressesByTypeQueryable(string addressType);
        IQueryable<Address> SearchAddressesQueryable(string searchTerm);
    }
}