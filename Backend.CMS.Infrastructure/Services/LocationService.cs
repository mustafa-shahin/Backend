using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class LocationService : ILocationService
    {
        private readonly ILocationRepository _locationRepository;
        private readonly IRepository<Company> _companyRepository;
        private readonly IRepository<LocationOpeningHour> _openingHourRepository;
        private readonly ApplicationDbContext _context;
        private readonly IUserSessionService _userSessionService;
        private readonly IMapper _mapper;
        private readonly ILogger<LocationService> _logger;

        public LocationService(
            ILocationRepository locationRepository,
            IRepository<Company> companyRepository,
            IRepository<LocationOpeningHour> openingHourRepository,
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            IMapper mapper,
            ILogger<LocationService> logger)
        {
            _locationRepository = locationRepository ?? throw new ArgumentNullException(nameof(locationRepository));
            _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
            _openingHourRepository = openingHourRepository ?? throw new ArgumentNullException(nameof(openingHourRepository));
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
                var location = await _locationRepository.GetWithAddressesAndContactsAsync(locationId);
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

        public async Task<List<LocationDto>> GetLocationsAsync(int page, int pageSize)
        {
            if (page <= 0)
                throw new ArgumentException("Page must be greater than 0", nameof(page));

            if (pageSize <= 0 || pageSize > 100)
                throw new ArgumentException("Page size must be between 1 and 100", nameof(pageSize));

            try
            {
                var locations = await _locationRepository.GetPagedWithRelatedAsync(page, pageSize);
                return _mapper.Map<List<LocationDto>>(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving locations for page {Page}, size {PageSize}", page, pageSize);
                throw;
            }
        }

        public async Task<List<LocationDto>> GetLocationsByCompanyAsync(int companyId)
        {
            if (companyId <= 0)
                throw new ArgumentException("Company ID must be greater than 0", nameof(companyId));

            try
            {
                var locations = await _locationRepository.GetLocationsByCompanyAsync(companyId);
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
                var companies = await _companyRepository.GetAllAsync();
                var company = companies.FirstOrDefault();

                if (company == null)
                {
                    _logger.LogWarning("No company found for location creation");
                    throw new ArgumentException("Company not found. Please create a company first.");
                }

                // Validate location code uniqueness
                if (!string.IsNullOrEmpty(createLocationDto.LocationCode) &&
                    await _locationRepository.LocationCodeExistsAsync(createLocationDto.LocationCode))
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

                await _locationRepository.AddAsync(location);
                await _locationRepository.SaveChangesAsync();

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

                var createdLocation = await _locationRepository.GetWithAddressesAndContactsAsync(location.Id);
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
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null)
                {
                    _logger.LogWarning("Location {LocationId} not found for update", locationId);
                    throw new ArgumentException("Location not found");
                }

                // Validate location code uniqueness
                if (!string.IsNullOrEmpty(updateLocationDto.LocationCode) &&
                    await _locationRepository.LocationCodeExistsAsync(updateLocationDto.LocationCode, locationId))
                {
                    throw new ArgumentException("Location code already exists");
                }

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Update location properties
                _mapper.Map(updateLocationDto, location);
                location.UpdatedAt = DateTime.UtcNow;
                location.UpdatedByUserId = currentUserId;
                _locationRepository.Update(location);

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

                var updatedLocation = await _locationRepository.GetWithAddressesAndContactsAsync(locationId);
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
                var result = await _locationRepository.SoftDeleteAsync(locationId, currentUserId);

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
                var location = await _locationRepository.GetByIdAsync(locationId);
                if (location == null)
                {
                    _logger.LogWarning("Location {LocationId} not found for setting as main", locationId);
                    return false;
                }

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Batch update all locations for the same company
                var allLocations = await _locationRepository.FindAsync(l => l.CompanyId == location.CompanyId);
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
                var mainLocation = await _locationRepository.GetMainLocationAsync();
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
                return await _locationRepository.LocationCodeExistsAsync(locationCode, excludeLocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking location code existence: {LocationCode}", locationCode);
                throw;
            }
        }

        public async Task<List<LocationDto>> SearchLocationsAsync(string searchTerm, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));

            if (page <= 0)
                throw new ArgumentException("Page must be greater than 0", nameof(page));

            if (pageSize <= 0 || pageSize > 100)
                throw new ArgumentException("Page size must be between 1 and 100", nameof(pageSize));

            try
            {
                var locations = await _locationRepository.SearchLocationsAsync(searchTerm, page, pageSize);
                return _mapper.Map<List<LocationDto>>(locations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching locations with term '{SearchTerm}'", searchTerm);
                throw;
            }
        }

        #region Private Helper Methods

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
                await _openingHourRepository.AddRangeAsync(openingHours);
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
            var existingOpeningHours = await _openingHourRepository.FindAsync(oh => oh.LocationId == locationId);
            if (existingOpeningHours.Any())
            {
                foreach (var existingHour in existingOpeningHours)
                {
                    await _openingHourRepository.SoftDeleteAsync(existingHour, currentUserId);
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
                await _openingHourRepository.AddRangeAsync(newOpeningHours);
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