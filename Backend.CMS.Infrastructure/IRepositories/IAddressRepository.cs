using Backend.CMS.Domain.Entities;
using Backend.CMS.Application.DTOs;

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
        Task<PagedResult<Address>> SearchAddressesAsync(string searchTerm, int page, int pageSize);
        Task<bool> AddressExistsAsync(string street, string city, string postalCode, int? excludeAddressId = null);
        Task<PagedResult<Address>> GetPagedAddressesByEntityAsync(int entityId, string entityType, int page, int pageSize);
        Task<PagedResult<Address>> GetPagedAddressesByTypeAsync(string addressType, int page, int pageSize);
    }
}