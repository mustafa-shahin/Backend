using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Application.Interfaces
{
    public interface ILocationService
    {
        Task<LocationDto> GetLocationByIdAsync(int locationId);
        Task<List<LocationDto>> GetLocationsAsync(int page, int pageSize); 
        Task<List<LocationDto>> GetLocationsByCompanyAsync(int companyId);
        Task<LocationDto> CreateLocationAsync(CreateLocationDto createLocationDto);
        Task<LocationDto> UpdateLocationAsync(int locationId, UpdateLocationDto updateLocationDto);
        Task<bool> DeleteLocationAsync(int locationId);
        Task<bool> SetMainLocationAsync(int locationId);
        Task<LocationDto> GetMainLocationAsync();
        Task<bool> LocationCodeExistsAsync(string locationCode, int? excludeLocationId = null);
        Task<List<LocationDto>> SearchLocationsAsync(string searchTerm, int page, int pageSize);
    }
}