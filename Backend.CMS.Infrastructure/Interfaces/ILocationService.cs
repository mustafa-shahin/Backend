using Backend.CMS.Application.DTOs;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface ILocationService
    {
        Task<LocationDto> GetLocationByIdAsync(int locationId);
        Task<PagedResult<LocationDto>> GetLocationsPagedAsync(LocationSearchDto searchDto);
        Task<PagedResult<LocationDto>> SearchLocationsPagedAsync(LocationSearchDto searchDto);
        Task<List<LocationDto>> GetLocationsByCompanyAsync(int companyId);
        Task<LocationDto> CreateLocationAsync(CreateLocationDto createLocationDto);
        Task<LocationDto> UpdateLocationAsync(int locationId, UpdateLocationDto updateLocationDto);
        Task<bool> DeleteLocationAsync(int locationId);
        Task<bool> SetMainLocationAsync(int locationId);
        Task<LocationDto> GetMainLocationAsync();
        Task<bool> LocationCodeExistsAsync(string locationCode, int? excludeLocationId = null);
        Task<List<LocationDto>> GetRecentLocationsAsync(int count);
        Task<Dictionary<string, object>> GetLocationStatisticsAsync();
        Task<bool> BulkUpdateLocationsAsync(IEnumerable<int> locationIds, UpdateLocationDto updateDto);
        Task<bool> BulkDeleteLocationsAsync(IEnumerable<int> locationIds);

    }
}