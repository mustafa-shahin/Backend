using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IAddressService
    {
        Task<AddressDto> GetAddressByIdAsync(int addressId);
        Task<List<AddressDto>> GetAddressesByEntityAsync(string entityType, int entityId);
        Task<AddressDto> CreateAddressAsync(CreateAddressDto createAddressDto, string entityType, int entityId);
        Task<AddressDto> UpdateAddressAsync(int addressId, UpdateAddressDto updateAddressDto);
        Task<bool> DeleteAddressAsync(int addressId);
        Task<bool> SetDefaultAddressAsync(int addressId, string entityType, int entityId);
    }
}