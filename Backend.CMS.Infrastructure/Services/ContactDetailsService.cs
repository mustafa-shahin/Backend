using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class ContactDetailsService : IContactDetailsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;
        private readonly ILogger<ContactDetailsService> _logger;

        private static readonly HashSet<string> ValidEntityTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "user", "company", "location"
        };

        public ContactDetailsService(
            IUnitOfWork unitOfWork,
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            IMapper mapper,
            ILogger<ContactDetailsService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
                var contactDetails = await _unitOfWork.ContactDetails.GetByIdAsync(contactId);
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

        public async Task<PaginatedResult<ContactDetailsDto>> GetContactDetailsPagedAsync(ContactDetailsSearchDto searchDto)
        {
            if (searchDto == null)
                throw new ArgumentNullException(nameof(searchDto));

            try
            {
                // Validate pagination parameters
                ValidatePaginationParameters(searchDto.PageNumber, searchDto.PageSize);

                // Build the base query using repository
                var query = _unitOfWork.ContactDetails.GetQueryableWithEntityFilter(searchDto.EntityType, searchDto.EntityId);

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(searchDto.SearchTerm))
                {
                    query = _unitOfWork.ContactDetails.ApplySearchFilter(query, searchDto.SearchTerm);
                }

                // Apply additional filters
                query = ApplyContactDetailsFilters(query, searchDto);

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Apply sorting and pagination
                query = ApplySorting(query, searchDto.SortBy, searchDto.SortDirection);

                var contactDetails = await query
                    .Skip((searchDto.PageNumber - 1) * searchDto.PageSize)
                    .Take(searchDto.PageSize)
                    .ToListAsync();

                var contactDetailsDtos = _mapper.Map<List<ContactDetailsDto>>(contactDetails);

                var result = new PaginatedResult<ContactDetailsDto>(
                    contactDetailsDtos,
                    searchDto.PageNumber,
                    searchDto.PageSize,
                    totalCount);

                _logger.LogDebug("Retrieved {Count} contact details (page {Page} of {TotalPages})",
                    contactDetailsDtos.Count, searchDto.PageNumber, result.TotalPages);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged contact details");
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
                var contactDetails = await _unitOfWork.ContactDetails.GetContactDetailsByEntityAsync(entityId, entityType);
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
                var contactDetails = await _unitOfWork.ContactDetails.GetByIdAsync(contactId);
                if (contactDetails == null)
                {
                    _logger.LogWarning("Contact details {ContactId} not found for update", contactId);
                    throw new ArgumentException("Contact details not found");
                }

                var currentUserId = _userSessionService.GetCurrentUserId();

                _mapper.Map(updateContactDetailsDto, contactDetails);
                contactDetails.UpdatedAt = DateTime.UtcNow;
                contactDetails.UpdatedByUserId = currentUserId;

                _unitOfWork.ContactDetails.Update(contactDetails);
                await _unitOfWork.ContactDetails.SaveChangesAsync();
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
                var result = await _unitOfWork.ContactDetails.SoftDeleteAsync(contactId, currentUserId);

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

            try
            {
                return await _unitOfWork.ContactDetails.SetDefaultContactDetailsAsync(contactId, entityId, entityType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default contact details {ContactId} for {EntityType} {EntityId}",
                    contactId, entityType, entityId);
                throw;
            }
        }

        public async Task<List<ContactDetailsDto>> GetRecentContactDetailsAsync(int count)
        {
            if (count <= 0 || count > 50)
                throw new ArgumentException("Count must be between 1 and 50", nameof(count));

            try
            {
                var contactDetails = await _unitOfWork.ContactDetails.GetQueryable()
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(count)
                    .ToListAsync();

                return _mapper.Map<List<ContactDetailsDto>>(contactDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent contact details");
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetContactDetailsStatisticsAsync()
        {
            try
            {
                var totalContactDetails = await _unitOfWork.ContactDetails.CountAsync();
                var defaultContactDetails = await _unitOfWork.ContactDetails.CountAsync(c => c.IsDefault);
                var contactDetailsByType = await _unitOfWork.ContactDetails.GetQueryable()
                    .GroupBy(c => c.ContactType ?? "Default")
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToListAsync();

                var contactDetailsWithEmail = await _unitOfWork.ContactDetails.CountAsync(c => !string.IsNullOrEmpty(c.Email));
                var contactDetailsWithPhone = await _unitOfWork.ContactDetails.CountAsync(c => !string.IsNullOrEmpty(c.PrimaryPhone));
                var contactDetailsWithWebsite = await _unitOfWork.ContactDetails.CountAsync(c => !string.IsNullOrEmpty(c.Website));

                return new Dictionary<string, object>
                {
                    ["totalContactDetails"] = totalContactDetails,
                    ["defaultContactDetails"] = defaultContactDetails,
                    ["contactDetailsWithEmail"] = contactDetailsWithEmail,
                    ["contactDetailsWithPhone"] = contactDetailsWithPhone,
                    ["contactDetailsWithWebsite"] = contactDetailsWithWebsite,
                    ["contactDetailsByType"] = contactDetailsByType.ToDictionary(x => x.Type, x => x.Count),
                    ["generatedAt"] = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contact details statistics");
                throw;
            }
        }

        public async Task<bool> BulkUpdateContactDetailsAsync(IEnumerable<int> contactIds, UpdateContactDetailsDto updateDto)
        {
            if (!contactIds?.Any() == true)
                throw new ArgumentException("Contact details IDs cannot be null or empty", nameof(contactIds));

            if (updateDto == null)
                throw new ArgumentNullException(nameof(updateDto));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var contactDetails = await _unitOfWork.ContactDetails.FindAsync(c => contactIds.Contains(c.Id));
                var updateTime = DateTime.UtcNow;

                foreach (var contact in contactDetails)
                {
                    _mapper.Map(updateDto, contact);
                    contact.UpdatedAt = updateTime;
                    contact.UpdatedByUserId = currentUserId;
                }

                _context.ContactDetails.UpdateRange(contactDetails);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Bulk updated {Count} contact details by user {UserId}", contactDetails.Count(), currentUserId);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error bulk updating contact details");
                throw;
            }
        }

        public async Task<bool> BulkDeleteContactDetailsAsync(IEnumerable<int> contactIds)
        {
            if (!contactIds?.Any() == true)
                throw new ArgumentException("Contact details IDs cannot be null or empty", nameof(contactIds));

            try
            {
                var currentUserId = _userSessionService.GetCurrentUserId();
                var contactDetails = await _unitOfWork.ContactDetails.FindAsync(c => contactIds.Contains(c.Id));

                await _unitOfWork.ContactDetails.SoftDeleteRangeAsync(contactDetails, currentUserId);

                _logger.LogInformation("Bulk deleted {Count} contact details by user {UserId}", contactDetails.Count(), currentUserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting contact details");
                throw;
            }
        }

        #region Private Helper Methods

        private IQueryable<ContactDetails> ApplyContactDetailsFilters(IQueryable<ContactDetails> query, ContactDetailsSearchDto searchDto)
        {
            if (!string.IsNullOrWhiteSpace(searchDto.ContactType))
            {
                query = query.Where(c => c.ContactType == searchDto.ContactType);
            }

            if (searchDto.IsDefault.HasValue)
            {
                query = query.Where(c => c.IsDefault == searchDto.IsDefault.Value);
            }

            if (searchDto.CreatedAfter.HasValue)
            {
                query = query.Where(c => c.CreatedAt >= searchDto.CreatedAfter.Value);
            }

            if (searchDto.CreatedBefore.HasValue)
            {
                query = query.Where(c => c.CreatedAt <= searchDto.CreatedBefore.Value);
            }

            return query;
        }

        private static IQueryable<ContactDetails> ApplySorting(IQueryable<ContactDetails> query, string sortBy, string sortDirection)
        {
            var isDescending = sortDirection?.ToLower() == "desc";

            return sortBy?.ToLower() switch
            {
                "email" => isDescending ? query.OrderByDescending(c => c.Email) : query.OrderBy(c => c.Email),
                "primaryphone" => isDescending ? query.OrderByDescending(c => c.PrimaryPhone) : query.OrderBy(c => c.PrimaryPhone),
                "website" => isDescending ? query.OrderByDescending(c => c.Website) : query.OrderBy(c => c.Website),
                "contacttype" => isDescending ? query.OrderByDescending(c => c.ContactType) : query.OrderBy(c => c.ContactType),
                "isdefault" => isDescending ? query.OrderByDescending(c => c.IsDefault) : query.OrderBy(c => c.IsDefault),
                "createdat" => isDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
                "updatedat" => isDescending ? query.OrderByDescending(c => c.UpdatedAt) : query.OrderBy(c => c.UpdatedAt),
                _ => isDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt)
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
                _logger.LogWarning("{EntityType} with ID {EntityId} not found for contact details creation", entityType, entityId);
                throw new ArgumentException($"{entityType} with ID {entityId} not found");
            }
        }

        private static void ValidatePaginationParameters(int pageNumber, int pageSize)
        {
            if (pageNumber < 1)
                throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));

            if (pageSize < 1)
                throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

            if (pageSize > 100)
                throw new ArgumentException("Page size cannot exceed 100", nameof(pageSize));
        }

        #endregion
    }
}