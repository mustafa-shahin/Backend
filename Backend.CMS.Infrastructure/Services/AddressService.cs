using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Services
{
    public class AddressService : IAddressService
    {
        private readonly IRepository<Address> _addressRepository;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;

        public AddressService(
            IRepository<Address> addressRepository,
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            IMapper mapper)
        {
            _addressRepository = addressRepository;
            _context = context;
            _mapper = mapper;
            _userSessionService = userSessionService;
        }

        public async Task<AddressDto> GetAddressByIdAsync(int addressId)
        {
            var address = await _addressRepository.GetByIdAsync(addressId);
            if (address == null)
                throw new ArgumentException("Address not found");

            return _mapper.Map<AddressDto>(address);
        }

        public async Task<List<AddressDto>> GetAddressesByEntityAsync(string entityType, int entityId)
        {
            //Properly handle shadow properties for polymorphic relationships
            var query = _context.Addresses.AsQueryable();

            switch (entityType.ToLower())
            {
                case "user":
                    query = query.Where(a => EF.Property<int?>(a, "UserId") == entityId);
                    break;
                case "company":
                    query = query.Where(a => EF.Property<int?>(a, "CompanyId") == entityId);
                    break;
                case "location": // Changed from "store"
                    query = query.Where(a => EF.Property<int?>(a, "LocationId") == entityId);
                    break;
                default:
                    throw new ArgumentException($"Invalid entity type: {entityType}");
            }

            var addresses = await query.ToListAsync();
            return _mapper.Map<List<AddressDto>>(addresses);
        }

        public async Task<AddressDto> CreateAddressAsync(CreateAddressDto createAddressDto, string entityType, int entityId)
        {
            // Validate entity type
            if (!IsValidEntityType(entityType))
                throw new ArgumentException($"Invalid entity type: {entityType}");

            // Validate that the entity exists
            await ValidateEntityExistsAsync(entityType, entityId);

            var address = _mapper.Map<Address>(createAddressDto);
            var currentUserId = _userSessionService.GetCurrentUserId();

            address.CreatedByUserId = currentUserId;
            address.UpdatedByUserId = currentUserId;
            address.CreatedAt = DateTime.UtcNow;
            address.UpdatedAt = DateTime.UtcNow;

            // Add to context first
            _context.Addresses.Add(address);

            // Set the foreign key using shadow property AFTER adding to context
            switch (entityType.ToLower())
            {
                case "user":
                    _context.Entry(address).Property("UserId").CurrentValue = entityId;
                    break;
                case "company":
                    _context.Entry(address).Property("CompanyId").CurrentValue = entityId;
                    break;
                case "location":
                    _context.Entry(address).Property("LocationId").CurrentValue = entityId;
                    break;
            }

            await _context.SaveChangesAsync();

            return _mapper.Map<AddressDto>(address);
        }

        public async Task<AddressDto> UpdateAddressAsync(int addressId, UpdateAddressDto updateAddressDto)
        {
            var address = await _addressRepository.GetByIdAsync(addressId);
            var currentUserId = _userSessionService.GetCurrentUserId();

            if (address == null)
                throw new ArgumentException("Address not found");

            _mapper.Map(updateAddressDto, address);
            address.UpdatedAt = DateTime.UtcNow;
            address.UpdatedByUserId = currentUserId;

            _addressRepository.Update(address);
            await _addressRepository.SaveChangesAsync();

            return _mapper.Map<AddressDto>(address);
        }

        public async Task<bool> DeleteAddressAsync(int addressId)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();
            return await _addressRepository.SoftDeleteAsync(addressId, currentUserId);
        }

        public async Task<bool> SetDefaultAddressAsync(int addressId, string entityType, int entityId)
        {
            // Validate entity type
            if (!IsValidEntityType(entityType))
                throw new ArgumentException($"Invalid entity type: {entityType}");

            var address = await _addressRepository.GetByIdAsync(addressId);
            var currentUserId = _userSessionService.GetCurrentUserId();

            if (address == null)
                return false;

            // Verify that the address belongs to the specified entity
            var entityIdProperty = GetEntityIdPropertyName(entityType);
            var addressEntityId = _context.Entry(address).Property(entityIdProperty).CurrentValue;

            if (!addressEntityId?.Equals(entityId) == true)
                throw new ArgumentException("Address does not belong to the specified entity");

            // Unset default for all other addresses of this entity using proper query
            var query = _context.Addresses.AsQueryable();

            switch (entityType.ToLower())
            {
                case "user":
                    query = query.Where(a => EF.Property<int?>(a, "UserId") == entityId);
                    break;
                case "company":
                    query = query.Where(a => EF.Property<int?>(a, "CompanyId") == entityId);
                    break;
                case "location":
                    query = query.Where(a => EF.Property<int?>(a, "LocationId") == entityId);
                    break;
            }

            var allAddresses = await query.ToListAsync();

            foreach (var addr in allAddresses)
            {
                addr.IsDefault = false;
                addr.UpdatedAt = DateTime.UtcNow;
                addr.UpdatedByUserId = currentUserId;
                _context.Addresses.Update(addr);
            }

            // Set this address as default
            address.IsDefault = true;
            address.UpdatedAt = DateTime.UtcNow;
            address.UpdatedByUserId = currentUserId;
            _context.Addresses.Update(address);

            await _context.SaveChangesAsync();
            return true;
        }

        // Helper methods for validation and consistency
        private static bool IsValidEntityType(string entityType)
        {
            var validTypes = new[] { "user", "company", "location" };
            return validTypes.Contains(entityType.ToLower());
        }

        private static string GetEntityIdPropertyName(string entityType)
        {
            return entityType.ToLower() switch
            {
                "user" => "UserId",
                "company" => "CompanyId",
                "location" => "LocationId",
                _ => throw new ArgumentException($"Invalid entity type: {entityType}")
            };
        }

        private async Task ValidateEntityExistsAsync(string entityType, int entityId)
        {
            bool exists = entityType.ToLower() switch
            {
                "user" => await _context.Users.AnyAsync(u => u.Id == entityId),
                "company" => await _context.Companies.AnyAsync(c => c.Id == entityId),
                "store" => await _context.Locations.AnyAsync(s => s.Id == entityId),
                _ => false
            };

            if (!exists)
                throw new ArgumentException($"{entityType} with ID {entityId} not found");
        }
    }

}