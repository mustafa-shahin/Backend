using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IAddressService
    {
        Task<AddressDto> GetAddressByIdAsync(int addressId);
        Task<PaginatedResult<AddressDto>> GetAddressesPaginatedAsync(AddressSearchDto searchDto);
        Task<List<AddressDto>> GetAddressesByEntityAsync(string entityType, int entityId);
        Task<PaginatedResult<AddressDto>> GetAddressesByEntityPaginatedAsync(string entityType, int entityId, int pageNumber, int pageSize);
        Task<PaginatedResult<AddressDto>> GetAddressesByTypePaginatedAsync(string addressType, int pageNumber, int pageSize);
        Task<PaginatedResult<AddressDto>> SearchAddressesPaginatedAsync(string searchTerm, int pageNumber, int pageSize);
        Task<AddressDto> CreateAddressAsync(CreateAddressDto createAddressDto, string entityType, int entityId);
        Task<AddressDto> UpdateAddressAsync(int addressId, UpdateAddressDto updateAddressDto);
        Task<bool> DeleteAddressAsync(int addressId);
        Task<bool> SetDefaultAddressAsync(int addressId, string entityType, int entityId);
        Task<List<AddressDto>> GetRecentAddressesAsync(int count);
        Task<Dictionary<string, object>> GetAddressStatisticsAsync();
        Task<bool> BulkUpdateAddressesAsync(IEnumerable<int> addressIds, UpdateAddressDto updateDto);
        Task<bool> BulkDeleteAddressesAsync(IEnumerable<int> addressIds);
    }
}