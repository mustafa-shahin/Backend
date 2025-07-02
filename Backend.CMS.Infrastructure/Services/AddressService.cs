using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class AddressService : BaseCacheAwareService<Address, AddressDto>, IAddressService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;
        private readonly ILogger<AddressService> _logger;

        private static readonly HashSet<string> ValidEntityTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "user", "company", "location"
        };

        public AddressService(
            IRepository<Address> addressRepository,
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            ICacheService cacheService,
            IMapper mapper,
            ILogger<AddressService> logger)
            : base(addressRepository, cacheService, logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        protected override string GetEntityCacheKey(int id)
        {
            return $"address:id:{id}";
        }

        protected override string[] GetEntityCachePatterns(int id)
        {
            return new[]
            {
            $"address:*:{id}",
            $"address:id:{id}"
        };
        }

        protected override string[] GetAllEntitiesCachePatterns()
        {
            return new[]
            {
            "address:*",
            "address:entity:*"
        };
        }

        protected override async Task<AddressDto> MapToDto(Address entity)
        {
            return _mapper.Map<AddressDto>(entity);
        }

        protected override async Task<List<AddressDto>> MapToDtos(IEnumerable<Address> entities)
        {
            return _mapper.Map<List<AddressDto>>(entities);
        }
        public async Task<AddressDto> GetAddressByIdAsync(int addressId)
        {
            if (addressId <= 0)
                throw new ArgumentException("Address ID must be greater than 0", nameof(addressId));

            var address = await GetByIdAsync(addressId);
            if (address == null)
            {
                _logger.LogWarning("Address {AddressId} not found", addressId);
                throw new ArgumentException("Address not found");
            }

            return address;
        }

        public async Task<List<AddressDto>> GetAddressesByEntityAsync(string entityType, int entityId)
        {
            if (string.IsNullOrWhiteSpace(entityType))
                throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

            if (entityId <= 0)
                throw new ArgumentException("Entity ID must be greater than 0", nameof(entityId));

            if (!IsValidEntityType(entityType))
                throw new ArgumentException($"Invalid entity type: {entityType}");

            try
            {
                var query = _context.Addresses.AsQueryable();
                var normalizedEntityType = entityType.ToLowerInvariant();

                query = normalizedEntityType switch
                {
                    "user" => query.Where(a => EF.Property<int?>(a, "UserId") == entityId),
                    "company" => query.Where(a => EF.Property<int?>(a, "CompanyId") == entityId),
                    "location" => query.Where(a => EF.Property<int?>(a, "LocationId") == entityId),
                    _ => throw new ArgumentException($"Invalid entity type: {entityType}")
                };

                var addresses = await query.OrderBy(a => a.CreatedAt).ToListAsync();
                return _mapper.Map<List<AddressDto>>(addresses);
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                _logger.LogError(ex, "Error retrieving addresses for entity {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<AddressDto> CreateAddressAsync(CreateAddressDto createAddressDto, string entityType, int entityId)
        {
            if (createAddressDto == null)
                throw new ArgumentNullException(nameof(createAddressDto));

            if (string.IsNullOrWhiteSpace(entityType))
                throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

            if (entityId <= 0)
                throw new ArgumentException("Entity ID must be greater than 0", nameof(entityId));

            if (!IsValidEntityType(entityType))
                throw new ArgumentException($"Invalid entity type: {entityType}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await ValidateEntityExistsAsync(entityType, entityId);

                var address = _mapper.Map<Address>(createAddressDto);
                var currentUserId = _userSessionService.GetCurrentUserId();

                address.CreatedByUserId = currentUserId;
                address.UpdatedByUserId = currentUserId;
                address.CreatedAt = DateTime.UtcNow;
                address.UpdatedAt = DateTime.UtcNow;

                _context.Addresses.Add(address);

                var normalizedEntityType = entityType.ToLowerInvariant();
                var propertyName = GetEntityIdPropertyName(normalizedEntityType);
                _context.Entry(address).Property(propertyName).CurrentValue = entityId;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Use base class cache invalidation
                await InvalidateRelatedCaches();

                _logger.LogInformation("Address {AddressId} created for {EntityType} {EntityId} by user {UserId}",
                    address.Id, entityType, entityId, currentUserId);

                return await MapToDto(address);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating address for {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<AddressDto> UpdateAddressAsync(int addressId, UpdateAddressDto updateAddressDto)
        {
            if (addressId <= 0)
                throw new ArgumentException("Address ID must be greater than 0", nameof(addressId));

            ArgumentNullException.ThrowIfNull(updateAddressDto);

            var address = await _repository.GetByIdAsync(addressId);
            if (address == null)
            {
                _logger.LogWarning("Address {AddressId} not found for update", addressId);
                throw new ArgumentException("Address not found");
            }

            var currentUserId = _userSessionService.GetCurrentUserId();

            _mapper.Map(updateAddressDto, address);
            address.UpdatedAt = DateTime.UtcNow;
            address.UpdatedByUserId = currentUserId;

            var result = await UpdateAsync(address);

            _logger.LogInformation("Address {AddressId} updated by user {UserId}", addressId, currentUserId);

            return result;
        }

        public async Task<bool> DeleteAddressAsync(int addressId)
        {
            if (addressId <= 0)
                throw new ArgumentException("Address ID must be greater than 0", nameof(addressId));

            var currentUserId = _userSessionService.GetCurrentUserId();
            var result = await DeleteAsync(addressId, currentUserId);

            if (result)
            {
                _logger.LogInformation("Address {AddressId} deleted by user {UserId}", addressId, currentUserId);
            }
            else
            {
                _logger.LogWarning("Failed to delete address {AddressId} - not found", addressId);
            }

            return result;
        }

        public async Task<bool> SetDefaultAddressAsync(int addressId, string entityType, int entityId)
        {

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var address = await _repository.GetByIdAsync(addressId);
                if (address == null)
                {
                    _logger.LogWarning("Address {AddressId} not found for setting default", addressId);
                    return false;
                }

                var currentUserId = _userSessionService.GetCurrentUserId();
                var normalizedEntityType = entityType.ToLowerInvariant();

                // Verify that the address belongs to the specified entity
                var entityIdProperty = GetEntityIdPropertyName(normalizedEntityType);
                var addressEntityId = _context.Entry(address).Property(entityIdProperty).CurrentValue;

                if (!addressEntityId?.Equals(entityId) == true)
                {
                    _logger.LogWarning("Address {AddressId} does not belong to {EntityType} {EntityId}",
                        addressId, entityType, entityId);
                    throw new ArgumentException("Address does not belong to the specified entity");
                }

                // Build query for all addresses of this entity
                var query = _context.Addresses.AsQueryable();
                query = normalizedEntityType switch
                {
                    "user" => query.Where(a => EF.Property<int?>(a, "UserId") == entityId),
                    "company" => query.Where(a => EF.Property<int?>(a, "CompanyId") == entityId),
                    "location" => query.Where(a => EF.Property<int?>(a, "LocationId") == entityId),
                    _ => throw new ArgumentException($"Invalid entity type: {entityType}")
                };

                var allAddresses = await query.ToListAsync();

                // Batch update all addresses
                foreach (var addr in allAddresses)
                {
                    addr.IsDefault = addr.Id == addressId;
                    addr.UpdatedAt = DateTime.UtcNow;
                    addr.UpdatedByUserId = currentUserId;
                    _context.Addresses.Update(addr);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Invalidate cache for all affected addresses
                await InvalidateRelatedCaches();

                _logger.LogInformation("Default address set to {AddressId} for {EntityType} {EntityId} by user {UserId}",
                    addressId, entityType, entityId, currentUserId);

                return true;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error setting default address {AddressId} for {EntityType} {EntityId}",
                    addressId, entityType, entityId);
                throw;
            }
        }

        #region Private Helper Methods

        private static bool IsValidEntityType(string entityType)
        {
            return !string.IsNullOrWhiteSpace(entityType) && ValidEntityTypes.Contains(entityType);
        }

        private static string GetEntityIdPropertyName(string entityType)
        {
            return entityType.ToLowerInvariant() switch
            {
                "user" => "UserId",
                "company" => "CompanyId",
                "location" => "LocationId",
                _ => throw new ArgumentException($"Invalid entity type: {entityType}")
            };
        }

        private async Task ValidateEntityExistsAsync(string entityType, int entityId)
        {
            var normalizedEntityType = entityType.ToLowerInvariant();

            bool exists = normalizedEntityType switch
            {
                "user" => await _context.Users.Where(u => u.Id == entityId).Where(u => u.IsDeleted == false).AnyAsync(),
                "company" => await _context.Companies.Where(c => c.Id == entityId).Where(c => c.IsDeleted == false).AnyAsync(),
                "location" => await _context.Locations.Where(l => l.Id == entityId).Where(l => l.IsDeleted == false).AnyAsync(),
                _ => false
            };

            if (!exists)
            {
                _logger.LogWarning("{EntityType} with ID {EntityId} not found for address creation", entityType, entityId);
                throw new ArgumentException($"{entityType} with ID {entityId} not found");
            }
        }

        #endregion
    }
}