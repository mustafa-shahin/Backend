using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class AddressService : IAddressService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;
        private readonly ILogger<AddressService> _logger;

        private static readonly HashSet<string> ValidEntityTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "user", "company", "location"
        };

        public AddressService(
           IUnitOfWork unitOfWork,
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            IMapper mapper,
            ILogger<AddressService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AddressDto> GetAddressByIdAsync(int addressId)
        {
            if (addressId <= 0)
                throw new ArgumentException("Address ID must be greater than 0", nameof(addressId));

            try
            {
                var address = await _unitOfWork.Addresses.GetByIdAsync(addressId);
                if (address == null)
                {
                    _logger.LogWarning("Address {AddressId} not found", addressId);
                    throw new ArgumentException("Address not found");
                }

                return _mapper.Map<AddressDto>(address);
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                _logger.LogError(ex, "Error retrieving address {AddressId}", addressId);
                throw;
            }
        }

        public async Task<PaginatedResult<AddressDto>> GetAddressesPaginatedAsync(AddressSearchDto searchDto)
        {
            if (searchDto == null)
                throw new ArgumentNullException(nameof(searchDto));

            try
            {
                var query = BuildAddressQuery(searchDto);

                query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

                var pagedResult = await _unitOfWork.Addresses.GetPagedResultAsync(
                    searchDto.PageNumber,
                    searchDto.PageSize,
                    predicate: null, // predicate already applied in BuildAddressQuery
                    orderBy: null, // ordering already applied in ApplySorting
                    cancellationToken: default);

                var totalCount = await query.CountAsync();
                var addresses = await query
                    .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToListAsync();

                var addressDtos = _mapper.Map<List<AddressDto>>(addresses);

                return new PaginatedResult<AddressDto>(
                    addressDtos,
                    searchDto.PageNumber,
                    searchDto.PageSize,
                    totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged addresses");
                throw;
            }
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
                var addresses = await _unitOfWork.Addresses.GetAddressesByEntityAsync(entityId, entityType);
                return _mapper.Map<List<AddressDto>>(addresses);
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                _logger.LogError(ex, "Error retrieving addresses for entity {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<PaginatedResult<AddressDto>> GetAddressesByEntityPaginatedAsync(string entityType, int entityId, int pageNumber, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(entityType))
                throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

            if (entityId <= 0)
                throw new ArgumentException("Entity ID must be greater than 0", nameof(entityId));

            if (!IsValidEntityType(entityType))
                throw new ArgumentException($"Invalid entity type: {entityType}");

            try
            {
                var query = _unitOfWork.Addresses.GetAddressesByEntityQueryable(entityId, entityType)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.AddressType)
                    .ThenBy(a => a.Street);

                var totalCount = await query.CountAsync();
                var addresses = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var addressDtos = _mapper.Map<List<AddressDto>>(addresses);

                return new PaginatedResult<AddressDto>(
                    addressDtos,
                    pageNumber,
                    pageSize,
                    totalCount);
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                _logger.LogError(ex, "Error retrieving paged addresses for entity {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<PaginatedResult<AddressDto>> GetAddressesByTypePaginatedAsync(string addressType, int pageNumber, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(addressType))
                throw new ArgumentException("Address type cannot be null or empty", nameof(addressType));

            try
            {
                var query = _unitOfWork.Addresses.GetAddressesByTypeQueryable(addressType)
                    .OrderBy(a => a.Country)
                    .ThenBy(a => a.State)
                    .ThenBy(a => a.City)
                    .ThenBy(a => a.Street);

                var totalCount = await query.CountAsync();
                var addresses = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var addressDtos = _mapper.Map<List<AddressDto>>(addresses);

                return new PaginatedResult<AddressDto>(
                    addressDtos,
                    pageNumber,
                    pageSize,
                    totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged addresses by type {AddressType}", addressType);
                throw;
            }
        }

        public async Task<PaginatedResult<AddressDto>> SearchAddressesPaginatedAsync(string searchTerm, int pageNumber, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));

            try
            {
                var query = _unitOfWork.Addresses.SearchAddressesQueryable(searchTerm)
                    .OrderBy(a => a.Country)
                    .ThenBy(a => a.State)
                    .ThenBy(a => a.City)
                    .ThenBy(a => a.Street);

                var totalCount = await query.CountAsync();
                var addresses = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var addressDtos = _mapper.Map<List<AddressDto>>(addresses);

                return new PaginatedResult<AddressDto>(
                    addressDtos,
                    pageNumber,
                    pageSize,
                    totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching paged addresses with term '{SearchTerm}'", searchTerm);
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
                var normalizedEntityType = entityType.ToLowerInvariant();
                var propertyName = GetEntityIdPropertyName(normalizedEntityType);
                _context.Entry(address).Property(propertyName).CurrentValue = entityId;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Address {AddressId} created for {EntityType} {EntityId} by user {UserId}",
                    address.Id, entityType, entityId, currentUserId);

                return _mapper.Map<AddressDto>(address);
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var address = await _unitOfWork.Addresses.GetByIdAsync(addressId);
                if (address == null)
                {
                    _logger.LogWarning("Address {AddressId} not found for update", addressId);
                    throw new ArgumentException("Address not found");
                }

                var currentUserId = _userSessionService.GetCurrentUserId();

                _mapper.Map(updateAddressDto, address);
                address.UpdatedAt = DateTime.UtcNow;
                address.UpdatedByUserId = currentUserId;

                _unitOfWork.Addresses.Update(address);
                await _unitOfWork.Addresses.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Address {AddressId} updated by user {UserId}", addressId, currentUserId);

                return _mapper.Map<AddressDto>(address);
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating address {AddressId}", addressId);
                throw;
            }
        }

        public async Task<bool> DeleteAddressAsync(int addressId)
        {
            if (addressId <= 0)
                throw new ArgumentException("Address ID must be greater than 0", nameof(addressId));

            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var result = await _unitOfWork.Addresses.SoftDeleteAsync(addressId, currentUserId);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting address {AddressId}", addressId);
                throw;
            }
        }

        public async Task<bool> SetDefaultAddressAsync(int addressId, string entityType, int entityId)
        {
            if (addressId <= 0)
                throw new ArgumentException("Address ID must be greater than 0", nameof(addressId));

            if (string.IsNullOrWhiteSpace(entityType))
                throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

            if (entityId <= 0)
                throw new ArgumentException("Entity ID must be greater than 0", nameof(entityId));

            if (!IsValidEntityType(entityType))
                throw new ArgumentException($"Invalid entity type: {entityType}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var address = await _unitOfWork.Addresses.GetByIdAsync(addressId);
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

                // Build query for all addresses of this entity using repository
                var query = _unitOfWork.Addresses.GetAddressesByEntityQueryable(entityId, entityType);
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

        public async Task<List<AddressDto>> GetRecentAddressesAsync(int count)
        {
            if (count <= 0 || count > 50)
                throw new ArgumentException("Count must be between 1 and 50", nameof(count));

            try
            {
                var query = _unitOfWork.Addresses.GetAddressesQueryable()
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(count);

                var addresses = await query.ToListAsync();
                return _mapper.Map<List<AddressDto>>(addresses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent addresses");
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetAddressStatisticsAsync()
        {
            try
            {
                var totalAddresses = await _unitOfWork.Addresses.CountAsync();
                var defaultAddresses = await _unitOfWork.Addresses.CountAsync(a => a.IsDefault);

                var addressesByType = await _unitOfWork.Addresses.GetAddressesQueryable()
                    .GroupBy(a => a.AddressType ?? "Default")
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync();

                var addressesByCountry = await _unitOfWork.Addresses.GetAddressesQueryable()
                    .GroupBy(a => a.Country)
                    .Select(g => new { Country = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(10)
                    .ToListAsync();

                return new Dictionary<string, object>
                {
                    ["totalAddresses"] = totalAddresses,
                    ["defaultAddresses"] = defaultAddresses,
                    ["addressesByType"] = addressesByType.ToDictionary(x => x.Type, x => x.Count),
                    ["topCountries"] = addressesByCountry.ToDictionary(x => x.Country, x => x.Count),
                    ["generatedAt"] = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting address statistics");
                throw;
            }
        }

        public async Task<bool> BulkUpdateAddressesAsync(IEnumerable<int> addressIds, UpdateAddressDto updateDto)
        {
            if (!addressIds?.Any() == true)
                throw new ArgumentException("Address IDs cannot be null or empty", nameof(addressIds));

            if (updateDto == null)
                throw new ArgumentNullException(nameof(updateDto));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var addresses = await _unitOfWork.Addresses.FindAsync(a => addressIds.Contains(a.Id));
                var updateTime = DateTime.UtcNow;

                foreach (var address in addresses)
                {
                    _mapper.Map(updateDto, address);
                    address.UpdatedAt = updateTime;
                    address.UpdatedByUserId = currentUserId;
                }

                _context.Addresses.UpdateRange(addresses);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Bulk updated {Count} addresses by user {UserId}", addresses.Count(), currentUserId);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error bulk updating addresses");
                throw;
            }
        }

        public async Task<bool> BulkDeleteAddressesAsync(IEnumerable<int> addressIds)
        {
            if (!addressIds?.Any() == true)
                throw new ArgumentException("Address IDs cannot be null or empty", nameof(addressIds));

            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var addresses = await _unitOfWork.Addresses.FindAsync(a => addressIds.Contains(a.Id));

                await _unitOfWork.Addresses.SoftDeleteRangeAsync(addresses, currentUserId);

                _logger.LogInformation("Bulk deleted {Count} addresses by user {UserId}", addresses.Count(), currentUserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting addresses");
                throw;
            }
        }

        #region Private Helper Methods

        private IQueryable<Address> BuildAddressQuery(AddressSearchDto searchDto)
        {
            var query = _unitOfWork.Addresses.GetAddressesQueryable();

            if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
            {
                query = _unitOfWork.Addresses.SearchAddressesQueryable(searchDto.SearchTerm);
            }

            if (!string.IsNullOrWhiteSpace(searchDto.Country))
            {
                query = query.Where(a => a.Country == searchDto.Country);
            }

            if (!string.IsNullOrWhiteSpace(searchDto.State))
            {
                query = query.Where(a => a.State == searchDto.State);
            }

            if (!string.IsNullOrWhiteSpace(searchDto.City))
            {
                query = query.Where(a => a.City == searchDto.City);
            }

            if (!string.IsNullOrWhiteSpace(searchDto.AddressType))
            {
                query = query.Where(a => a.AddressType == searchDto.AddressType);
            }

            if (searchDto.IsDefault.HasValue)
            {
                query = query.Where(a => a.IsDefault == searchDto.IsDefault.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchDto.EntityType) && searchDto.EntityId.HasValue)
            {
                var entityQuery = _unitOfWork.Addresses.GetAddressesByEntityQueryable(searchDto.EntityId.Value, searchDto.EntityType);
                query = query.Where(a => entityQuery.Any(eq => eq.Id == a.Id));
            }

            if (searchDto.CreatedAfter.HasValue)
            {
                query = query.Where(a => a.CreatedAt >= searchDto.CreatedAfter.Value);
            }

            if (searchDto.CreatedBefore.HasValue)
            {
                query = query.Where(a => a.CreatedAt <= searchDto.CreatedBefore.Value);
            }

            return query;
        }

        private static IQueryable<Address> ApplySorting(IQueryable<Address> query, string sortBy, string sortDirection)
        {
            var isDescending = sortDirection?.ToLower() == "desc";

            return sortBy?.ToLower() switch
            {
                "street" => isDescending ? query.OrderByDescending(a => a.Street) : query.OrderBy(a => a.Street),
                "city" => isDescending ? query.OrderByDescending(a => a.City) : query.OrderBy(a => a.City),
                "state" => isDescending ? query.OrderByDescending(a => a.State) : query.OrderBy(a => a.State),
                "country" => isDescending ? query.OrderByDescending(a => a.Country) : query.OrderBy(a => a.Country),
                "postalcode" => isDescending ? query.OrderByDescending(a => a.PostalCode) : query.OrderBy(a => a.PostalCode),
                "addresstype" => isDescending ? query.OrderByDescending(a => a.AddressType) : query.OrderBy(a => a.AddressType),
                "isdefault" => isDescending ? query.OrderByDescending(a => a.IsDefault) : query.OrderBy(a => a.IsDefault),
                "createdat" => isDescending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt),
                "updatedat" => isDescending ? query.OrderByDescending(a => a.UpdatedAt) : query.OrderBy(a => a.UpdatedAt),
                _ => isDescending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt)
            };
        }

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