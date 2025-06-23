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
    public class ContactDetailsService : IContactDetailsService
    {
        private readonly IRepository<ContactDetails> _contactDetailsRepository;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;
        private readonly ILogger<ContactDetailsService> _logger;

        private static readonly HashSet<string> ValidEntityTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "user", "company", "location"
        };

        public ContactDetailsService(
            IRepository<ContactDetails> contactDetailsRepository,
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            IMapper mapper,
            ILogger<ContactDetailsService> logger)
        {
            _contactDetailsRepository = contactDetailsRepository ?? throw new ArgumentNullException(nameof(contactDetailsRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ContactDetailsDto> GetContactDetailsByIdAsync(int contactId)
        {
            if (contactId <= 0)
                throw new ArgumentException("Contact ID must be greater than 0", nameof(contactId));

            try
            {
                var contactDetails = await _contactDetailsRepository.GetByIdAsync(contactId);
                if (contactDetails == null)
                {
                    _logger.LogWarning("Contact details {ContactId} not found", contactId);
                    throw new ArgumentException("Contact details not found");
                }

                return _mapper.Map<ContactDetailsDto>(contactDetails);
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                _logger.LogError(ex, "Error retrieving contact details {ContactId}", contactId);
                throw;
            }
        }

        public async Task<List<ContactDetailsDto>> GetContactDetailsByEntityAsync(string entityType, int entityId)
        {
            if (string.IsNullOrWhiteSpace(entityType))
                throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

            if (entityId <= 0)
                throw new ArgumentException("Entity ID must be greater than 0", nameof(entityId));

            if (!IsValidEntityType(entityType))
                throw new ArgumentException($"Invalid entity type: {entityType}");

            try
            {
                var query = _context.ContactDetails.AsQueryable();
                var normalizedEntityType = entityType.ToLowerInvariant();

                query = normalizedEntityType switch
                {
                    "user" => query.Where(c => EF.Property<int?>(c, "UserId") == entityId),
                    "company" => query.Where(c => EF.Property<int?>(c, "CompanyId") == entityId),
                    "location" => query.Where(c => EF.Property<int?>(c, "LocationId") == entityId),
                    _ => throw new ArgumentException($"Invalid entity type: {entityType}")
                };

                var contactDetails = await query.OrderBy(c => c.CreatedAt).ToListAsync();
                return _mapper.Map<List<ContactDetailsDto>>(contactDetails);
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                _logger.LogError(ex, "Error retrieving contact details for entity {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<ContactDetailsDto> CreateContactDetailsAsync(CreateContactDetailsDto createContactDetailsDto, string entityType, int entityId)
        {
            if (createContactDetailsDto == null)
                throw new ArgumentNullException(nameof(createContactDetailsDto));

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

                var contactDetails = _mapper.Map<ContactDetails>(createContactDetailsDto);
                var currentUserId = _userSessionService.GetCurrentUserId();

                contactDetails.CreatedByUserId = currentUserId;
                contactDetails.UpdatedByUserId = currentUserId;
                contactDetails.CreatedAt = DateTime.UtcNow;
                contactDetails.UpdatedAt = DateTime.UtcNow;

                // Add to context first
                _context.ContactDetails.Add(contactDetails);

                // Set the foreign key using shadow property AFTER adding to context
                var normalizedEntityType = entityType.ToLowerInvariant();
                var propertyName = GetEntityIdPropertyName(normalizedEntityType);
                _context.Entry(contactDetails).Property(propertyName).CurrentValue = entityId;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Contact details {ContactId} created for {EntityType} {EntityId} by user {UserId}",
                    contactDetails.Id, entityType, entityId, currentUserId);

                return _mapper.Map<ContactDetailsDto>(contactDetails);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating contact details for {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<ContactDetailsDto> UpdateContactDetailsAsync(int contactId, UpdateContactDetailsDto updateContactDetailsDto)
        {
            if (contactId <= 0)
                throw new ArgumentException("Contact ID must be greater than 0", nameof(contactId));

            if (updateContactDetailsDto == null)
                throw new ArgumentNullException(nameof(updateContactDetailsDto));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var contactDetails = await _contactDetailsRepository.GetByIdAsync(contactId);
                if (contactDetails == null)
                {
                    _logger.LogWarning("Contact details {ContactId} not found for update", contactId);
                    throw new ArgumentException("Contact details not found");
                }

                var currentUserId = _userSessionService.GetCurrentUserId();

                _mapper.Map(updateContactDetailsDto, contactDetails);
                contactDetails.UpdatedAt = DateTime.UtcNow;
                contactDetails.UpdatedByUserId = currentUserId;

                _contactDetailsRepository.Update(contactDetails);
                await _contactDetailsRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Contact details {ContactId} updated by user {UserId}", contactId, currentUserId);

                return _mapper.Map<ContactDetailsDto>(contactDetails);
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating contact details {ContactId}", contactId);
                throw;
            }
        }

        public async Task<bool> DeleteContactDetailsAsync(int contactId)
        {
            if (contactId <= 0)
                throw new ArgumentException("Contact ID must be greater than 0", nameof(contactId));

            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var result = await _contactDetailsRepository.SoftDeleteAsync(contactId, currentUserId);

                if (result)
                {
                    _logger.LogInformation("Contact details {ContactId} deleted by user {UserId}", contactId, currentUserId);
                }
                else
                {
                    _logger.LogWarning("Failed to delete contact details {ContactId} - not found", contactId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting contact details {ContactId}", contactId);
                throw;
            }
        }

        public async Task<bool> SetDefaultContactDetailsAsync(int contactId, string entityType, int entityId)
        {
            if (contactId <= 0)
                throw new ArgumentException("Contact ID must be greater than 0", nameof(contactId));

            if (string.IsNullOrWhiteSpace(entityType))
                throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

            if (entityId <= 0)
                throw new ArgumentException("Entity ID must be greater than 0", nameof(entityId));

            if (!IsValidEntityType(entityType))
                throw new ArgumentException($"Invalid entity type: {entityType}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var contactDetails = await _contactDetailsRepository.GetByIdAsync(contactId);
                if (contactDetails == null)
                {
                    _logger.LogWarning("Contact details {ContactId} not found for setting default", contactId);
                    return false;
                }

                var currentUserId = _userSessionService.GetCurrentUserId();
                var normalizedEntityType = entityType.ToLowerInvariant();

                // Verify that the contact details belongs to the specified entity
                var entityIdProperty = GetEntityIdPropertyName(normalizedEntityType);
                var contactEntityId = _context.Entry(contactDetails).Property(entityIdProperty).CurrentValue;

                if (!contactEntityId?.Equals(entityId) == true)
                {
                    _logger.LogWarning("Contact details {ContactId} does not belong to {EntityType} {EntityId}",
                        contactId, entityType, entityId);
                    throw new ArgumentException("Contact details does not belong to the specified entity");
                }

                // Build query for all contact details of this entity
                var query = _context.ContactDetails.AsQueryable();
                query = normalizedEntityType switch
                {
                    "user" => query.Where(c => EF.Property<int?>(c, "UserId") == entityId),
                    "company" => query.Where(c => EF.Property<int?>(c, "CompanyId") == entityId),
                    "location" => query.Where(c => EF.Property<int?>(c, "LocationId") == entityId),
                    _ => throw new ArgumentException($"Invalid entity type: {entityType}")
                };

                var allContactDetails = await query.ToListAsync();

                // Batch update all contact details
                foreach (var contact in allContactDetails)
                {
                    contact.IsDefault = contact.Id == contactId;
                    contact.UpdatedAt = DateTime.UtcNow;
                    contact.UpdatedByUserId = currentUserId;
                    _context.ContactDetails.Update(contact);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Default contact details set to {ContactId} for {EntityType} {EntityId} by user {UserId}",
                    contactId, entityType, entityId, currentUserId);

                return true;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error setting default contact details {ContactId} for {EntityType} {EntityId}",
                    contactId, entityType, entityId);
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
                _logger.LogWarning("{EntityType} with ID {EntityId} not found for contact details creation", entityType, entityId);
                throw new ArgumentException($"{entityType} with ID {entityId} not found");
            }
        }

        #endregion
    }
}