using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Backend.CMS.Infrastructure.Services
{
    public class LocationService : ILocationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ApplicationDbContext _context;
        private readonly IUserSessionService _userSessionService;
        private readonly IMapper _mapper;
        private readonly ILogger<LocationService> _logger;

        public LocationService(
            IUnitOfWork unitOfWork,
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            IMapper mapper,
            ILogger<LocationService> logger)
        {
            _unitOfWork = unitOfWork;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<LocationDto> GetLocationByIdAsync(int locationId)
        {
            if (locationId <= 0)
                throw new ArgumentException("Location ID must be greater than 0", nameof(locationId));

            try
            {
                var location = await _unitOfWork.Locations.GetWithAddressesAndContactsAsync(locationId);
                if (location == null)
                {
                    _logger.LogWarning("Location {LocationId} not found", locationId);
                    throw new ArgumentException("Location not found");
                }

                return _mapper.Map<LocationDto>(location);
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                _logger.LogError(ex, "Error retrieving location {LocationId}", locationId);
                throw;
            }
        }

        public async Task<PagedResult<LocationDto>> GetLocationsPagedAsync(LocationSearchDto searchDto)
        {
            if (searchDto == null)
                throw new ArgumentNullException(nameof(searchDto));

            try
            {
                var query = BuildLocationQuery(searchDto);

                var totalCount = await query.CountAsync();

                // Apply sorting
                query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

                // Apply pagination
                var locations = await query
                    .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .Include(l => l.Addresses.Where(a => !a.IsDeleted))
                    .Include(l => l.ContactDetails.Where(c => !c.IsDeleted))
                    .Include(l => l.OpeningHours.Where(oh => !oh.IsDeleted))
                    .ToListAsync();

                var locationDtos = _mapper.Map<List<LocationDto>>(locations);

                return new PagedResult<LocationDto>(
                    locationDtos,
                    searchDto.PageNumber,
                    searchDto.PageSize,
                    totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged locations");
                throw;
            }
        }

        public async Task<PagedResult<LocationDto>> SearchLocationsPagedAsync(LocationSearchDto searchDto)
        {
            // This method is the same as GetLocationsPagedAsync since filtering is already included
            return await GetLocationsPagedAsync(searchDto);
        }

        public async Task<List<LocationDto>> GetLocationsByCompanyAsync(int companyId)
        {
            if (companyId <= 0)
                throw new ArgumentException("Company ID must be greater than 0", nameof(companyId));

            try
            {
                var locations = await  _unitOfWork.Locations.GetLocationsByCompanyAsync(companyId);
                return _mapper.Map<List<LocationDto>>(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving locations for company {CompanyId}", companyId);
                throw;
            }
        }

        public async Task<LocationDto> CreateLocationAsync(CreateLocationDto createLocationDto)
        {
            if (createLocationDto == null)
                throw new ArgumentNullException(nameof(createLocationDto));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var companies = await _unitOfWork.Companies.GetAllAsync();
                var company = companies.FirstOrDefault();

                if (company == null)
                {
                    _logger.LogWarning("No company found for location creation");
                    throw new ArgumentException("Company not found. Please create a company first.");
                }

                // Validate location code uniqueness
                if (!string.IsNullOrEmpty(createLocationDto.LocationCode) &&
                    await  _unitOfWork.Locations.LocationCodeExistsAsync(createLocationDto.LocationCode))
                {
                    throw new ArgumentException("Location code already exists");
                }

                var currentUserId = _userSessionService.GetCurrentUserId();
                var location = _mapper.Map<Location>(createLocationDto);

                location.CompanyId = company.Id;
                location.CreatedByUserId = currentUserId;
                location.UpdatedByUserId = currentUserId;
                location.CreatedAt = DateTime.UtcNow;
                location.UpdatedAt = DateTime.UtcNow;

                await  _unitOfWork.Locations.AddAsync(location);
                await  _unitOfWork.Locations.SaveChangesAsync();

                // Handle related data creation in parallel
                var tasks = new List<Task>();

                if (createLocationDto.OpeningHours?.Any() == true)
                {
                    tasks.Add(CreateLocationOpeningHoursAsync(location.Id, createLocationDto.OpeningHours, currentUserId));
                }

                if (createLocationDto.Addresses?.Any() == true)
                {
                    tasks.Add(CreateLocationAddressesAsync(location.Id, createLocationDto.Addresses, currentUserId));
                }

                if (createLocationDto.ContactDetails?.Any() == true)
                {
                    tasks.Add(CreateLocationContactDetailsAsync(location.Id, createLocationDto.ContactDetails, currentUserId));
                }

                if (tasks.Any())
                {
                    await Task.WhenAll(tasks);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Location {LocationId} created by user {UserId}", location.Id, currentUserId);

                var createdLocation = await  _unitOfWork.Locations.GetWithAddressesAndContactsAsync(location.Id);
                return _mapper.Map<LocationDto>(createdLocation);
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating location");
                throw;
            }
        }

        public async Task<LocationDto> UpdateLocationAsync(int locationId, UpdateLocationDto updateLocationDto)
        {
            if (locationId <= 0)
                throw new ArgumentException("Location ID must be greater than 0", nameof(locationId));

            if (updateLocationDto == null)
                throw new ArgumentNullException(nameof(updateLocationDto));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var location = await  _unitOfWork.Locations.GetByIdAsync(locationId);
                if (location == null)
                {
                    _logger.LogWarning("Location {LocationId} not found for update", locationId);
                    throw new ArgumentException("Location not found");
                }

                // Validate location code uniqueness
                if (!string.IsNullOrEmpty(updateLocationDto.LocationCode) &&
                    await  _unitOfWork.Locations.LocationCodeExistsAsync(updateLocationDto.LocationCode, locationId))
                {
                    throw new ArgumentException("Location code already exists");
                }

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Update location properties
                _mapper.Map(updateLocationDto, location);
                location.UpdatedAt = DateTime.UtcNow;
                location.UpdatedByUserId = currentUserId;
                 _unitOfWork.Locations.Update(location);

                // Handle related data updates in parallel
                var tasks = new List<Task>();

                if (updateLocationDto.OpeningHours?.Any() == true)
                {
                    tasks.Add(UpdateLocationOpeningHoursAsync(locationId, updateLocationDto.OpeningHours, currentUserId));
                }

                if (updateLocationDto.Addresses?.Any() == true)
                {
                    tasks.Add(UpdateLocationAddressesAsync(locationId, updateLocationDto.Addresses, currentUserId));
                }

                if (updateLocationDto.ContactDetails?.Any() == true)
                {
                    tasks.Add(UpdateLocationContactDetailsAsync(locationId, updateLocationDto.ContactDetails, currentUserId));
                }

                if (tasks.Any())
                {
                    await Task.WhenAll(tasks);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Location {LocationId} updated by user {UserId}", locationId, currentUserId);

                var updatedLocation = await  _unitOfWork.Locations.GetWithAddressesAndContactsAsync(locationId);
                return _mapper.Map<LocationDto>(updatedLocation);
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating location {LocationId}", locationId);
                throw;
            }
        }

        public async Task<bool> DeleteLocationAsync(int locationId)
        {
            if (locationId <= 0)
                throw new ArgumentException("Location ID must be greater than 0", nameof(locationId));

            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var result = await  _unitOfWork.Locations.SoftDeleteAsync(locationId, currentUserId);

                if (result)
                {
                    _logger.LogInformation("Location {LocationId} deleted by user {UserId}", locationId, currentUserId);
                }
                else
                {
                    _logger.LogWarning("Failed to delete location {LocationId} - not found", locationId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting location {LocationId}", locationId);
                throw;
            }
        }

        public async Task<bool> SetMainLocationAsync(int locationId)
        {
            if (locationId <= 0)
                throw new ArgumentException("Location ID must be greater than 0", nameof(locationId));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var location = await  _unitOfWork.Locations.GetByIdAsync(locationId);
                if (location == null)
                {
                    _logger.LogWarning("Location {LocationId} not found for setting as main", locationId);
                    return false;
                }

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Batch update all locations for the same company
                var allLocations = await  _unitOfWork.Locations.FindAsync(l => l.CompanyId == location.CompanyId);
                var updateTime = DateTime.UtcNow;

                foreach (var l in allLocations)
                {
                    l.IsMainLocation = l.Id == locationId;
                    l.UpdatedAt = updateTime;
                    l.UpdatedByUserId = currentUserId;
                }

                _context.Locations.UpdateRange(allLocations);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Location {LocationId} set as main location by user {UserId}", locationId, currentUserId);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error setting main location {LocationId}", locationId);
                throw;
            }
        }

        public async Task<LocationDto> GetMainLocationAsync()
        {
            try
            {
                var mainLocation = await  _unitOfWork.Locations.GetMainLocationAsync();
                if (mainLocation == null)
                {
                    _logger.LogWarning("Main location not found");
                    throw new ArgumentException("Main location not found");
                }

                return _mapper.Map<LocationDto>(mainLocation);
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                _logger.LogError(ex, "Error retrieving main location");
                throw;
            }
        }

        public async Task<bool> LocationCodeExistsAsync(string locationCode, int? excludeLocationId = null)
        {
            if (string.IsNullOrWhiteSpace(locationCode))
                throw new ArgumentException("Location code cannot be null or empty", nameof(locationCode));

            try
            {
                return await  _unitOfWork.Locations.LocationCodeExistsAsync(locationCode, excludeLocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking location code existence: {LocationCode}", locationCode);
                throw;
            }
        }

        public async Task<List<LocationDto>> GetRecentLocationsAsync(int count)
        {
            if (count <= 0 || count > 50)
                throw new ArgumentException("Count must be between 1 and 50", nameof(count));

            try
            {
                var locations = await _context.Locations
                    .Where(l => !l.IsDeleted)
                    .Include(l => l.Addresses.Where(a => !a.IsDeleted))
                    .Include(l => l.ContactDetails.Where(c => !c.IsDeleted))
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(count)
                    .ToListAsync();

                return _mapper.Map<List<LocationDto>>(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent locations");
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetLocationStatisticsAsync()
        {
            try
            {
                var totalLocations = await  _unitOfWork.Locations.CountAsync();
                var activeLocations = await  _unitOfWork.Locations.CountAsync(l => l.IsActive);
                var mainLocation = await  _unitOfWork.Locations.GetMainLocationAsync();
                var locationsByType = await _context.Locations
                    .Where(l => !l.IsDeleted)
                    .GroupBy(l => l.LocationType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync();

                return new Dictionary<string, object>
                {
                    ["totalLocations"] = totalLocations,
                    ["activeLocations"] = activeLocations,
                    ["inactiveLocations"] = totalLocations - activeLocations,
                    ["hasMainLocation"] = mainLocation != null,
                    ["mainLocationId"] = mainLocation?.Id,
                    ["locationsByType"] = locationsByType.ToDictionary(x => x.Type, x => x.Count),
                    ["generatedAt"] = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location statistics");
                throw;
            }
        }

        public async Task<bool> BulkUpdateLocationsAsync(IEnumerable<int> locationIds, UpdateLocationDto updateDto)
        {
            if (!locationIds?.Any() == true)
                throw new ArgumentException("Location IDs cannot be null or empty", nameof(locationIds));

            if (updateDto == null)
                throw new ArgumentNullException(nameof(updateDto));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var locations = await  _unitOfWork.Locations.FindAsync(l => locationIds.Contains(l.Id));
                var updateTime = DateTime.UtcNow;

                foreach (var location in locations)
                {
                    _mapper.Map(updateDto, location);
                    location.UpdatedAt = updateTime;
                    location.UpdatedByUserId = currentUserId;
                }

                _context.Locations.UpdateRange(locations);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Bulk updated {Count} locations by user {UserId}", locations.Count(), currentUserId);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error bulk updating locations");
                throw;
            }
        }

        public async Task<bool> BulkDeleteLocationsAsync(IEnumerable<int> locationIds)
        {
            if (!locationIds?.Any() == true)
                throw new ArgumentException("Location IDs cannot be null or empty", nameof(locationIds));

            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var locations = await  _unitOfWork.Locations.FindAsync(l => locationIds.Contains(l.Id));

                await  _unitOfWork.Locations.SoftDeleteRangeAsync(locations, currentUserId);

                _logger.LogInformation("Bulk deleted {Count} locations by user {UserId}", locations.Count(), currentUserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting locations");
                throw;
            }
        }

        #region Private Helper Methods

        private IQueryable<Location> BuildLocationQuery(LocationSearchDto searchDto)
        {
            var query = _context.Locations
                .Where(l => !l.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
            {
                var searchTerm = searchDto.SearchTerm.ToLower();
                query = query.Where(l =>
                    l.Name.ToLower().Contains(searchTerm) ||
                    (l.LocationCode != null && l.LocationCode.ToLower().Contains(searchTerm)) ||
                    (l.Description != null && l.Description.ToLower().Contains(searchTerm)));
            }

            if (!string.IsNullOrWhiteSpace(searchDto.LocationType))
            {
                query = query.Where(l => l.LocationType == searchDto.LocationType);
            }

            if (searchDto.IsActive.HasValue)
            {
                query = query.Where(l => l.IsActive == searchDto.IsActive.Value);
            }

            if (searchDto.IsMainLocation.HasValue)
            {
                query = query.Where(l => l.IsMainLocation == searchDto.IsMainLocation.Value);
            }

            if (searchDto.CompanyId.HasValue)
            {
                query = query.Where(l => l.CompanyId == searchDto.CompanyId.Value);
            }

            if (searchDto.CreatedAfter.HasValue)
            {
                query = query.Where(l => l.CreatedAt >= searchDto.CreatedAfter.Value);
            }

            if (searchDto.CreatedBefore.HasValue)
            {
                query = query.Where(l => l.CreatedAt <= searchDto.CreatedBefore.Value);
            }

            return query;
        }

        private static IQueryable<Location> ApplySorting(IQueryable<Location> query, string sortBy, string sortDirection)
        {
            var isDescending = sortDirection?.ToLower() == "desc";

            return sortBy?.ToLower() switch
            {
                "name" => isDescending ? query.OrderByDescending(l => l.Name) : query.OrderBy(l => l.Name),
                "locationcode" => isDescending ? query.OrderByDescending(l => l.LocationCode) : query.OrderBy(l => l.LocationCode),
                "locationtype" => isDescending ? query.OrderByDescending(l => l.LocationType) : query.OrderBy(l => l.LocationType),
                "isactive" => isDescending ? query.OrderByDescending(l => l.IsActive) : query.OrderBy(l => l.IsActive),
                "ismainlocation" => isDescending ? query.OrderByDescending(l => l.IsMainLocation) : query.OrderBy(l => l.IsMainLocation),
                "createdat" => isDescending ? query.OrderByDescending(l => l.CreatedAt) : query.OrderBy(l => l.CreatedAt),
                "updatedat" => isDescending ? query.OrderByDescending(l => l.UpdatedAt) : query.OrderBy(l => l.UpdatedAt),
                _ => isDescending ? query.OrderByDescending(l => l.Name) : query.OrderBy(l => l.Name)
            };
        }

        private async Task CreateLocationOpeningHoursAsync(int locationId, IEnumerable<CreateLocationOpeningHourDto> openingHourDtos, int? currentUserId)
        {
            var openingHours = new List<LocationOpeningHour>();
            var createTime = DateTime.UtcNow;

            foreach (var ohDto in openingHourDtos)
            {
                var openingHour = new LocationOpeningHour
                {
                    LocationId = locationId,
                    DayOfWeek = ohDto.DayOfWeek,
                    OpenTime = ohDto.OpenTime,
                    CloseTime = ohDto.CloseTime,
                    IsClosed = ohDto.IsClosed,
                    IsOpen24Hours = ohDto.IsOpen24Hours,
                    Notes = ohDto.Notes,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId,
                    CreatedAt = createTime,
                    UpdatedAt = createTime
                };

                openingHours.Add(openingHour);
            }

            if (openingHours.Any())
            {
                await _unitOfWork.GetRepository<LocationOpeningHour>().AddRangeAsync(openingHours);
                _logger.LogDebug("Created {Count} opening hours for location {LocationId}", openingHours.Count, locationId);
            }
        }

        private async Task CreateLocationAddressesAsync(int locationId, IEnumerable<CreateAddressDto> addressDtos, int? currentUserId)
        {
            var addresses = new List<Address>();
            var createTime = DateTime.UtcNow;

            foreach (var addressDto in addressDtos)
            {
                var address = _mapper.Map<Address>(addressDto);
                address.CreatedByUserId = currentUserId;
                address.UpdatedByUserId = currentUserId;
                address.CreatedAt = createTime;
                address.UpdatedAt = createTime;

                addresses.Add(address);
            }

            if (addresses.Any())
            {
                _context.Addresses.AddRange(addresses);

                // Set foreign keys after adding to context
                foreach (var address in addresses)
                {
                    _context.Entry(address).Property("LocationId").CurrentValue = locationId;
                }

                _logger.LogDebug("Created {Count} addresses for location {LocationId}", addresses.Count, locationId);
            }
        }

        private async Task CreateLocationContactDetailsAsync(int locationId, IEnumerable<CreateContactDetailsDto> contactDtos, int? currentUserId)
        {
            var contacts = new List<ContactDetails>();
            var createTime = DateTime.UtcNow;

            foreach (var contactDto in contactDtos)
            {
                var contact = _mapper.Map<ContactDetails>(contactDto);
                contact.CreatedByUserId = currentUserId;
                contact.UpdatedByUserId = currentUserId;
                contact.CreatedAt = createTime;
                contact.UpdatedAt = createTime;

                contacts.Add(contact);
            }

            if (contacts.Any())
            {
                _context.ContactDetails.AddRange(contacts);

                // Set foreign keys after adding to context
                foreach (var contact in contacts)
                {
                    _context.Entry(contact).Property("LocationId").CurrentValue = locationId;
                }

                _logger.LogDebug("Created {Count} contact details for location {LocationId}", contacts.Count, locationId);
            }
        }

        private async Task UpdateLocationOpeningHoursAsync(int locationId, IEnumerable<UpdateLocationOpeningHourDto> openingHourDtos, int? currentUserId)
        {
            // Soft delete existing opening hours
            var existingOpeningHours = await _unitOfWork.GetRepository<LocationOpeningHour>().FindAsync(oh => oh.LocationId == locationId);
            if (existingOpeningHours.Any())
            {
                foreach (var existingHour in existingOpeningHours)
                {
                    await _unitOfWork.GetRepository<LocationOpeningHour>().SoftDeleteAsync(existingHour, currentUserId);
                }
            }

            // Create new opening hours
            var newOpeningHours = new List<LocationOpeningHour>();
            var createTime = DateTime.UtcNow;

            foreach (var ohDto in openingHourDtos)
            {
                var openingHour = new LocationOpeningHour
                {
                    LocationId = locationId,
                    DayOfWeek = ohDto.DayOfWeek,
                    OpenTime = ohDto.OpenTime,
                    CloseTime = ohDto.CloseTime,
                    IsClosed = ohDto.IsClosed,
                    IsOpen24Hours = ohDto.IsOpen24Hours,
                    Notes = ohDto.Notes,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId,
                    CreatedAt = createTime,
                    UpdatedAt = createTime
                };

                newOpeningHours.Add(openingHour);
            }

            if (newOpeningHours.Any())
            {
                await _unitOfWork.GetRepository<LocationOpeningHour>().AddRangeAsync(newOpeningHours);
                _logger.LogDebug("Updated opening hours for location {LocationId}", locationId);
            }
        }

        private async Task UpdateLocationAddressesAsync(int locationId, IEnumerable<UpdateAddressDto> addressDtos, int? currentUserId)
        {
            // Get existing addresses - avoid optional parameters in LINQ
            var existingAddresses = await _context.Addresses
                .Where(a => EF.Property<int?>(a, "LocationId") == locationId)
                .Where(a => a.IsDeleted == false)
                .ToListAsync();

            // Soft delete existing addresses in batch
            if (existingAddresses.Any())
            {
                var deleteTime = DateTime.UtcNow;
                foreach (var existingAddress in existingAddresses)
                {
                    existingAddress.IsDeleted = true;
                    existingAddress.DeletedAt = deleteTime;
                    existingAddress.DeletedByUserId = currentUserId;
                    existingAddress.UpdatedAt = deleteTime;
                    existingAddress.UpdatedByUserId = currentUserId;
                }

                _context.Addresses.UpdateRange(existingAddresses);
            }

            // Add new addresses in batch
            var newAddresses = new List<Address>();
            var createTime = DateTime.UtcNow;

            foreach (var addressDto in addressDtos)
            {
                var newAddress = _mapper.Map<Address>(addressDto);
                newAddress.CreatedByUserId = currentUserId;
                newAddress.UpdatedByUserId = currentUserId;
                newAddress.CreatedAt = createTime;
                newAddress.UpdatedAt = createTime;

                newAddresses.Add(newAddress);
            }

            if (newAddresses.Any())
            {
                _context.Addresses.AddRange(newAddresses);

                // Set foreign keys after adding to context
                foreach (var address in newAddresses)
                {
                    _context.Entry(address).Property("LocationId").CurrentValue = locationId;
                }
            }

            _logger.LogDebug("Updated {DeletedCount} addresses and created {CreatedCount} addresses for location {LocationId}",
                existingAddresses.Count, newAddresses.Count, locationId);
        }

        private async Task UpdateLocationContactDetailsAsync(int locationId, IEnumerable<UpdateContactDetailsDto> contactDtos, int? currentUserId)
        {
            // Get existing contact details - avoid optional parameters in LINQ
            var existingContacts = await _context.ContactDetails
                .Where(c => EF.Property<int?>(c, "LocationId") == locationId)
                .Where(c => c.IsDeleted == false)
                .ToListAsync();

            // Soft delete existing contact details in batch
            if (existingContacts.Any())
            {
                var deleteTime = DateTime.UtcNow;
                foreach (var existingContact in existingContacts)
                {
                    existingContact.IsDeleted = true;
                    existingContact.DeletedAt = deleteTime;
                    existingContact.DeletedByUserId = currentUserId;
                    existingContact.UpdatedAt = deleteTime;
                    existingContact.UpdatedByUserId = currentUserId;
                }

                _context.ContactDetails.UpdateRange(existingContacts);
            }

            // Add new contact details in batch
            var newContacts = new List<ContactDetails>();
            var createTime = DateTime.UtcNow;

            foreach (var contactDto in contactDtos)
            {
                var newContact = _mapper.Map<ContactDetails>(contactDto);
                newContact.CreatedByUserId = currentUserId;
                newContact.UpdatedByUserId = currentUserId;
                newContact.CreatedAt = createTime;
                newContact.UpdatedAt = createTime;

                newContacts.Add(newContact);
            }

            if (newContacts.Any())
            {
                _context.ContactDetails.AddRange(newContacts);

                // Set foreign keys after adding to context
                foreach (var contact in newContacts)
                {
                    _context.Entry(contact).Property("LocationId").CurrentValue = locationId;
                }
            }

            _logger.LogDebug("Updated {DeletedCount} contacts and created {CreatedCount} contacts for location {LocationId}",
                existingContacts.Count, newContacts.Count, locationId);
        }

        #endregion
    }
}