using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class AddressRepository : Repository<Address>, IAddressRepository
    {
        public AddressRepository(ApplicationDbContext context, ILogger<AddressRepository> logger)
            : base(context, logger)
        {
        }

        public AddressRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Address?> GetDefaultAddressAsync(int entityId, string entityType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType))
                    throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

                var address = await _context.Addresses
                    .Where(a => !a.IsDeleted && a.IsDefault)
                    .Where(a => (entityType == "User" && EF.Property<int?>(a, "UserId") == entityId) ||
                               (entityType == "Company" && EF.Property<int?>(a, "CompanyId") == entityId) ||
                               (entityType == "Location" && EF.Property<int?>(a, "LocationId") == entityId))
                    .FirstOrDefaultAsync();

                _logger.LogDebug("Retrieved default address for {EntityType} {EntityId}: {Found}",
                    entityType, entityId, address != null ? "Found" : "Not found");

                return address;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default address for {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<IEnumerable<Address>> GetAddressesByEntityAsync(int entityId, string entityType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType))
                    throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

                var addresses = await _context.Addresses
                    .Where(a => !a.IsDeleted)
                    .Where(a => (entityType == "User" && EF.Property<int?>(a, "UserId") == entityId) ||
                               (entityType == "Company" && EF.Property<int?>(a, "CompanyId") == entityId) ||
                               (entityType == "Location" && EF.Property<int?>(a, "LocationId") == entityId))
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.AddressType)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} addresses for {EntityType} {EntityId}",
                    addresses.Count, entityType, entityId);

                return addresses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving addresses for {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<IEnumerable<Address>> GetAddressesByTypeAsync(string addressType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(addressType))
                    throw new ArgumentException("Address type cannot be null or empty", nameof(addressType));

                var addresses = await _dbSet
                    .Where(a => !a.IsDeleted && a.AddressType == addressType)
                    .OrderBy(a => a.City)
                    .ThenBy(a => a.Street)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} addresses of type {AddressType}", addresses.Count, addressType);

                return addresses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving addresses by type {AddressType}", addressType);
                throw;
            }
        }

        public async Task<IEnumerable<Address>> GetAddressesByLocationAsync(string city, string state, string country)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(country))
                    throw new ArgumentException("City, state, and country cannot be null or empty");

                var addresses = await _dbSet
                    .Where(a => !a.IsDeleted &&
                               a.City == city &&
                               a.State == state &&
                               a.Country == country)
                    .OrderBy(a => a.Street)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} addresses in {City}, {State}, {Country}",
                    addresses.Count, city, state, country);

                return addresses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving addresses by location {City}, {State}, {Country}",
                    city, state, country);
                throw;
            }
        }

        public async Task<bool> SetDefaultAddressAsync(int addressId, int entityId, string entityType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType))
                    throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Remove default from all other addresses for this entity
                    var existingAddresses = await _context.Addresses
                        .Where(a => !a.IsDeleted && a.IsDefault)
                        .Where(a => (entityType == "User" && EF.Property<int?>(a, "UserId") == entityId) ||
                                   (entityType == "Company" && EF.Property<int?>(a, "CompanyId") == entityId) ||
                                   (entityType == "Location" && EF.Property<int?>(a, "LocationId") == entityId))
                        .ToListAsync();

                    foreach (var addr in existingAddresses)
                    {
                        addr.IsDefault = false;
                        addr.UpdatedAt = DateTime.UtcNow;
                    }

                    // Set the new default address
                    var newDefaultAddress = await _dbSet
                        .FirstOrDefaultAsync(a => a.Id == addressId && !a.IsDeleted);

                    if (newDefaultAddress == null)
                    {
                        _logger.LogWarning("Address {AddressId} not found for setting as default", addressId);
                        return false;
                    }

                    newDefaultAddress.IsDefault = true;
                    newDefaultAddress.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogDebug("Set address {AddressId} as default for {EntityType} {EntityId}",
                        addressId, entityType, entityId);

                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default address {AddressId} for {EntityType} {EntityId}",
                    addressId, entityType, entityId);
                throw;
            }
        }

        public async Task<int> CountAddressesByEntityAsync(int entityId, string entityType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType))
                    throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

                var count = await _context.Addresses
                    .Where(a => !a.IsDeleted)
                    .Where(a => (entityType == "User" && EF.Property<int?>(a, "UserId") == entityId) ||
                               (entityType == "Company" && EF.Property<int?>(a, "CompanyId") == entityId) ||
                               (entityType == "Location" && EF.Property<int?>(a, "LocationId") == entityId))
                    .CountAsync();

                _logger.LogDebug("Count of addresses for {EntityType} {EntityId}: {Count}",
                    entityType, entityId, count);

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting addresses for {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<bool> AddressExistsAsync(string street, string city, string postalCode, int? excludeAddressId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(postalCode))
                    throw new ArgumentException("Street, city, and postal code cannot be null or empty");

                var query = _dbSet.Where(a => !a.IsDeleted &&
                                            a.Street == street &&
                                            a.City == city &&
                                            a.PostalCode == postalCode);

                if (excludeAddressId.HasValue)
                    query = query.Where(a => a.Id != excludeAddressId.Value);

                var exists = await query.AnyAsync();

                _logger.LogDebug("Address exists check for '{Street}, {City}, {PostalCode}': {Exists}",
                    street, city, postalCode, exists);

                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if address exists '{Street}, {City}, {PostalCode}'",
                    street, city, postalCode);
                throw;
            }
        }

        public IQueryable<Address> GetAddressesQueryable()
        {
            return _dbSet.Where(a => !a.IsDeleted);
        }

        public IQueryable<Address> GetAddressesByEntityQueryable(int entityId, string entityType)
        {
            if (string.IsNullOrWhiteSpace(entityType))
                throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

            var query = _context.Addresses.Where(a => !a.IsDeleted);

            return entityType.ToLowerInvariant() switch
            {
                "user" => query.Where(a => EF.Property<int?>(a, "UserId") == entityId),
                "company" => query.Where(a => EF.Property<int?>(a, "CompanyId") == entityId),
                "location" => query.Where(a => EF.Property<int?>(a, "LocationId") == entityId),
                _ => throw new ArgumentException($"Invalid entity type: {entityType}")
            };
        }

        public IQueryable<Address> GetAddressesByTypeQueryable(string addressType)
        {
            if (string.IsNullOrWhiteSpace(addressType))
                throw new ArgumentException("Address type cannot be null or empty", nameof(addressType));

            return _dbSet.Where(a => !a.IsDeleted && a.AddressType == addressType);
        }

        public IQueryable<Address> SearchAddressesQueryable(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));

            var lowerSearchTerm = searchTerm.ToLower();
            return _dbSet.Where(a => !a.IsDeleted && (
                a.Street.ToLower().Contains(lowerSearchTerm) ||
                a.City.ToLower().Contains(lowerSearchTerm) ||
                a.State.ToLower().Contains(lowerSearchTerm) ||
                a.Country.ToLower().Contains(lowerSearchTerm) ||
                a.PostalCode.Contains(searchTerm) ||
                (a.AddressType != null && a.AddressType.ToLower().Contains(lowerSearchTerm)) ||
                (a.HouseNr != null && a.HouseNr.ToLower().Contains(lowerSearchTerm)) ||
                (a.Notes != null && a.Notes.ToLower().Contains(lowerSearchTerm))));
        }
    }
}