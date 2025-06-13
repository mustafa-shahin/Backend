using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Services
{
    public class LocationService : ILocationService
    {
        private readonly ILocationRepository _locationRepository;
        private readonly IRepository<Company> _companyRepository;
        private readonly IRepository<LocationOpeningHour> _openingHourRepository;
        private readonly IRepository<Address> _addressRepository;
        private readonly IRepository<ContactDetails> _contactRepository;
        private readonly ApplicationDbContext _context;
        private readonly IUserSessionService _userSessionService;
        private readonly IMapper _mapper;

        public LocationService(
            ILocationRepository locationRepository,
            IRepository<Company> companyRepository,
            IRepository<LocationOpeningHour> openingHourRepository,
            IRepository<Address> addressRepository,
            IRepository<ContactDetails> contactRepository,
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            IMapper mapper)
        {
            _locationRepository = locationRepository;
            _companyRepository = companyRepository;
            _openingHourRepository = openingHourRepository;
            _addressRepository = addressRepository;
            _contactRepository = contactRepository;
            _context = context;
            _userSessionService = userSessionService;
            _mapper = mapper;
        }

        public async Task<LocationDto> GetLocationByIdAsync(int locationId)
        {
            var location = await _locationRepository.GetByIdAsync(locationId);
            if (location == null)
                throw new ArgumentException("Location not found");

            return _mapper.Map<LocationDto>(location);
        }

        public async Task<List<LocationDto>> GetLocationsAsync(int page, int pageSize)
        {
            var locations = await _locationRepository.GetPagedWithRelatedAsync(page, pageSize);
            return _mapper.Map<List<LocationDto>>(locations);
        }

        public async Task<List<LocationDto>> GetLocationsByCompanyAsync(int companyId)
        {
            var locations = await _locationRepository.GetLocationsByCompanyAsync(companyId);
            return _mapper.Map<List<LocationDto>>(locations);
        }

        public async Task<LocationDto> CreateLocationAsync(CreateLocationDto createLocationDto)
        {
            var companies = await _companyRepository.GetAllAsync();
            var company = companies.FirstOrDefault();

            if (company == null)
                throw new ArgumentException("Company not found. Please create a company first.");

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

            await _locationRepository.AddAsync(location);
            await _locationRepository.SaveChangesAsync();

            // Add opening hours if provided
            if (createLocationDto.OpeningHours?.Any() == true)
            {
                var openingHours = createLocationDto.OpeningHours.Select(oh => new LocationOpeningHour
                {
                    LocationId = location.Id,
                    DayOfWeek = oh.DayOfWeek,
                    OpenTime = oh.OpenTime,
                    CloseTime = oh.CloseTime,
                    IsClosed = oh.IsClosed,
                    IsOpen24Hours = oh.IsOpen24Hours,
                    Notes = oh.Notes,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                });

                await _openingHourRepository.AddRangeAsync(openingHours);
                await _openingHourRepository.SaveChangesAsync();
            }

            // Add addresses if provided
            if (createLocationDto.Addresses?.Any() == true)
            {
                foreach (var addressDto in createLocationDto.Addresses)
                {
                    var address = _mapper.Map<Address>(addressDto);
                    address.CreatedByUserId = currentUserId;
                    address.UpdatedByUserId = currentUserId;

                    _context.Entry(address).Property("LocationId").CurrentValue = location.Id;
                    _context.Addresses.Add(address);
                }
            }

            // Add contact details if provided
            if (createLocationDto.ContactDetails?.Any() == true)
            {
                foreach (var contactDto in createLocationDto.ContactDetails)
                {
                    var contact = _mapper.Map<ContactDetails>(contactDto);
                    contact.CreatedByUserId = currentUserId;
                    contact.UpdatedByUserId = currentUserId;

                    _context.Entry(contact).Property("LocationId").CurrentValue = location.Id;
                    _context.ContactDetails.Add(contact);
                }
            }

            await _context.SaveChangesAsync();
            var createdLocation = await _locationRepository.GetByIdAsync(location.Id);
            return _mapper.Map<LocationDto>(createdLocation);
        }

        public async Task<LocationDto> UpdateLocationAsync(int locationId, UpdateLocationDto updateLocationDto)
        {
            var location = await _locationRepository.GetByIdAsync(locationId);
            if (location == null)
                throw new ArgumentException("Location not found");

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

            // Update opening hours
            if (updateLocationDto.OpeningHours?.Any() == true)
            {
                var existingOpeningHours = await _openingHourRepository.FindAsync(oh => oh.LocationId == locationId);
                foreach (var existingHour in existingOpeningHours)
                {
                    await _openingHourRepository.SoftDeleteAsync(existingHour, currentUserId);
                }

                var newOpeningHours = updateLocationDto.OpeningHours.Select(oh => new LocationOpeningHour
                {
                    LocationId = location.Id,
                    DayOfWeek = oh.DayOfWeek,
                    OpenTime = oh.OpenTime,
                    CloseTime = oh.CloseTime,
                    IsClosed = oh.IsClosed,
                    IsOpen24Hours = oh.IsOpen24Hours,
                    Notes = oh.Notes,
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                });

                await _openingHourRepository.AddRangeAsync(newOpeningHours);
            }

            // Update ALL addresses (not just primary)
            if (updateLocationDto.Addresses?.Any() == true)
            {
                // Get existing addresses for this location
                var existingAddresses = await _context.Addresses
                    .Where(a => EF.Property<int?>(a, "LocationId") == locationId && !a.IsDeleted)
                    .ToListAsync();

                // Soft delete existing addresses
                foreach (var existingAddress in existingAddresses)
                {
                    existingAddress.IsDeleted = true;
                    existingAddress.DeletedAt = DateTime.UtcNow;
                    existingAddress.DeletedByUserId = currentUserId;
                    _context.Addresses.Update(existingAddress);
                }

                // Add all new addresses
                foreach (var addressDto in updateLocationDto.Addresses)
                {
                    var newAddress = _mapper.Map<Address>(addressDto);
                    newAddress.CreatedByUserId = currentUserId;
                    newAddress.UpdatedByUserId = currentUserId;
                    newAddress.CreatedAt = DateTime.UtcNow;
                    newAddress.UpdatedAt = DateTime.UtcNow;

                    _context.Addresses.Add(newAddress);
                    _context.Entry(newAddress).Property("LocationId").CurrentValue = locationId;
                }
            }

            // Update ALL contact details (not just primary)
            if (updateLocationDto.ContactDetails?.Any() == true)
            {
                // Get existing contact details for this location
                var existingContacts = await _context.ContactDetails
                    .Where(c => EF.Property<int?>(c, "LocationId") == locationId && !c.IsDeleted)
                    .ToListAsync();

                // Soft delete existing contact details
                foreach (var existingContact in existingContacts)
                {
                    existingContact.IsDeleted = true;
                    existingContact.DeletedAt = DateTime.UtcNow;
                    existingContact.DeletedByUserId = currentUserId;
                    _context.ContactDetails.Update(existingContact);
                }

                // Add all new contact details
                foreach (var contactDto in updateLocationDto.ContactDetails)
                {
                    var newContact = _mapper.Map<ContactDetails>(contactDto);
                    newContact.CreatedByUserId = currentUserId;
                    newContact.UpdatedByUserId = currentUserId;
                    newContact.CreatedAt = DateTime.UtcNow;
                    newContact.UpdatedAt = DateTime.UtcNow;

                    _context.ContactDetails.Add(newContact);
                    _context.Entry(newContact).Property("LocationId").CurrentValue = locationId;
                }
            }

            await _context.SaveChangesAsync();

            var updatedLocation = await _locationRepository.GetByIdAsync(locationId);
            return _mapper.Map<LocationDto>(updatedLocation);
        }

        public async Task<bool> DeleteLocationAsync(int locationId)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();
            return await _locationRepository.SoftDeleteAsync(locationId, currentUserId);
        }

        public async Task<bool> SetMainLocationAsync(int locationId)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();
            var location = await _locationRepository.GetByIdAsync(locationId);
            if (location == null)
                return false;

            // Unset all other main locations for the same company
            var allLocations = await _locationRepository.FindAsync(l => l.CompanyId == location.CompanyId);
            foreach (var l in allLocations)
            {
                l.IsMainLocation = false;
                l.UpdatedAt = DateTime.UtcNow;
                l.UpdatedByUserId = currentUserId;
                _locationRepository.Update(l);
            }

            // Set this location as main
            location.IsMainLocation = true;
            location.UpdatedAt = DateTime.UtcNow;
            location.UpdatedByUserId = currentUserId;
            _locationRepository.Update(location);

            await _locationRepository.SaveChangesAsync();
            return true;
        }

        public async Task<LocationDto> GetMainLocationAsync()
        {
            var mainLocation = await _locationRepository.GetMainLocationAsync();
            if (mainLocation == null)
                throw new ArgumentException("Main location not found");

            return _mapper.Map<LocationDto>(mainLocation);
        }

        public async Task<bool> LocationCodeExistsAsync(string locationCode, int? excludeLocationId = null)
        {
            return await _locationRepository.LocationCodeExistsAsync(locationCode, excludeLocationId);
        }

        public async Task<List<LocationDto>> SearchLocationsAsync(string searchTerm, int page, int pageSize)
        {
            var locations = await _locationRepository.SearchLocationsAsync(searchTerm, page, pageSize);
            return _mapper.Map<List<LocationDto>>(locations);
        }
    }
}