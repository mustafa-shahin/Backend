using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Repositories
{
    public class ContactDetailsRepository : Repository<ContactDetails>, IContactDetailsRepository
    {
        public ContactDetailsRepository(ApplicationDbContext context, ILogger<ContactDetailsRepository> logger)
            : base(context, logger)
        {
        }

        public ContactDetailsRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<ContactDetails?> GetDefaultContactDetailsAsync(int entityId, string entityType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType))
                    throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

                var contactDetails = await _context.ContactDetails
                    .Where(c => !c.IsDeleted && c.IsDefault)
                    .Where(c => (entityType == "User" && EF.Property<int?>(c, "UserId") == entityId) ||
                               (entityType == "Company" && EF.Property<int?>(c, "CompanyId") == entityId) ||
                               (entityType == "Location" && EF.Property<int?>(c, "LocationId") == entityId))
                    .FirstOrDefaultAsync();

                _logger.LogDebug("Retrieved default contact details for {EntityType} {EntityId}: {Found}",
                    entityType, entityId, contactDetails != null ? "Found" : "Not found");

                return contactDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default contact details for {EntityType} {EntityId}",
                    entityType, entityId);
                throw;
            }
        }

        public async Task<IEnumerable<ContactDetails>> GetContactDetailsByEntityAsync(int entityId, string entityType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType))
                    throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

                var contactDetails = await _context.ContactDetails
                    .Where(c => !c.IsDeleted)
                    .Where(c => (entityType == "User" && EF.Property<int?>(c, "UserId") == entityId) ||
                               (entityType == "Company" && EF.Property<int?>(c, "CompanyId") == entityId) ||
                               (entityType == "Location" && EF.Property<int?>(c, "LocationId") == entityId))
                    .OrderByDescending(c => c.IsDefault)
                    .ThenBy(c => c.ContactType)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} contact details for {EntityType} {EntityId}",
                    contactDetails.Count, entityType, entityId);

                return contactDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact details for {EntityType} {EntityId}",
                    entityType, entityId);
                throw;
            }
        }

        public async Task<IEnumerable<ContactDetails>> GetContactDetailsByTypeAsync(string contactType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(contactType))
                    throw new ArgumentException("Contact type cannot be null or empty", nameof(contactType));

                var contactDetails = await _dbSet
                    .Where(c => !c.IsDeleted && c.ContactType == contactType)
                    .OrderBy(c => c.Email)
                    .ThenBy(c => c.PrimaryPhone)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} contact details of type {ContactType}",
                    contactDetails.Count, contactType);

                return contactDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact details by type {ContactType}", contactType);
                throw;
            }
        }

        public async Task<ContactDetails?> GetByEmailAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    throw new ArgumentException("Email cannot be null or empty", nameof(email));

                var contactDetails = await _dbSet
                    .Where(c => !c.IsDeleted && (c.Email == email || c.SecondaryEmail == email))
                    .FirstOrDefaultAsync();

                _logger.LogDebug("Retrieved contact details by email {Email}: {Found}",
                    email, contactDetails != null ? "Found" : "Not found");

                return contactDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact details by email {Email}", email);
                throw;
            }
        }

        public async Task<ContactDetails?> GetByPhoneAsync(string phone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phone))
                    throw new ArgumentException("Phone cannot be null or empty", nameof(phone));

                var contactDetails = await _dbSet
                    .Where(c => !c.IsDeleted && (c.PrimaryPhone == phone ||
                                                c.SecondaryPhone == phone ||
                                                c.Mobile == phone ||
                                                c.WhatsAppNumber == phone))
                    .FirstOrDefaultAsync();

                _logger.LogDebug("Retrieved contact details by phone {Phone}: {Found}",
                    phone, contactDetails != null ? "Found" : "Not found");

                return contactDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving contact details by phone {Phone}", phone);
                throw;
            }
        }

        public async Task<bool> SetDefaultContactDetailsAsync(int contactDetailsId, int entityId, string entityType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType))
                    throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Remove default from all other contact details for this entity
                    var existingContactDetails = await _context.ContactDetails
                        .Where(c => !c.IsDeleted && c.IsDefault)
                        .Where(c => (entityType == "User" && EF.Property<int?>(c, "UserId") == entityId) ||
                                   (entityType == "Company" && EF.Property<int?>(c, "CompanyId") == entityId) ||
                                   (entityType == "Location" && EF.Property<int?>(c, "LocationId") == entityId))
                        .ToListAsync();

                    foreach (var contact in existingContactDetails)
                    {
                        contact.IsDefault = false;
                        contact.UpdatedAt = DateTime.UtcNow;
                    }

                    // Set the new default contact details
                    var newDefaultContactDetails = await _dbSet
                        .FirstOrDefaultAsync(c => c.Id == contactDetailsId && !c.IsDeleted);

                    if (newDefaultContactDetails == null)
                    {
                        _logger.LogWarning("Contact details {ContactDetailsId} not found for setting as default",
                            contactDetailsId);
                        return false;
                    }

                    newDefaultContactDetails.IsDefault = true;
                    newDefaultContactDetails.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogDebug("Set contact details {ContactDetailsId} as default for {EntityType} {EntityId}",
                        contactDetailsId, entityType, entityId);

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
                _logger.LogError(ex, "Error setting default contact details {ContactDetailsId} for {EntityType} {EntityId}",
                    contactDetailsId, entityType, entityId);
                throw;
            }
        }

        public async Task<int> CountContactDetailsByEntityAsync(int entityId, string entityType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityType))
                    throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

                var count = await _context.ContactDetails
                    .Where(c => !c.IsDeleted)
                    .Where(c => (entityType == "User" && EF.Property<int?>(c, "UserId") == entityId) ||
                               (entityType == "Company" && EF.Property<int?>(c, "CompanyId") == entityId) ||
                               (entityType == "Location" && EF.Property<int?>(c, "LocationId") == entityId))
                    .CountAsync();

                _logger.LogDebug("Count of contact details for {EntityType} {EntityId}: {Count}",
                    entityType, entityId, count);

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting contact details for {EntityType} {EntityId}",
                    entityType, entityId);
                throw;
            }
        }

        public async Task<bool> EmailExistsAsync(string email, int? excludeContactDetailsId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    throw new ArgumentException("Email cannot be null or empty", nameof(email));

                var query = _dbSet.Where(c => !c.IsDeleted && (c.Email == email || c.SecondaryEmail == email));

                if (excludeContactDetailsId.HasValue)
                    query = query.Where(c => c.Id != excludeContactDetailsId.Value);

                var exists = await query.AnyAsync();

                _logger.LogDebug("Email exists check for '{Email}': {Exists}", email, exists);

                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if email exists '{Email}'", email);
                throw;
            }
        }

        public async Task<bool> PhoneExistsAsync(string phone, int? excludeContactDetailsId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phone))
                    throw new ArgumentException("Phone cannot be null or empty", nameof(phone));

                var query = _dbSet.Where(c => !c.IsDeleted && (
                    c.PrimaryPhone == phone ||
                    c.SecondaryPhone == phone ||
                    c.Mobile == phone ||
                    c.WhatsAppNumber == phone));

                if (excludeContactDetailsId.HasValue)
                    query = query.Where(c => c.Id != excludeContactDetailsId.Value);

                var exists = await query.AnyAsync();

                _logger.LogDebug("Phone exists check for '{Phone}': {Exists}", phone, exists);

                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if phone exists '{Phone}'", phone);
                throw;
            }
        }

        public IQueryable<ContactDetails> GetQueryableWithEntityFilter(string? entityType = null, int? entityId = null)
        {
            var query = _dbSet.Where(c => !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(entityType) && entityId.HasValue)
            {
                var normalizedEntityType = entityType.ToLowerInvariant();
                query = normalizedEntityType switch
                {
                    "user" => query.Where(c => EF.Property<int?>(c, "UserId") == entityId.Value),
                    "company" => query.Where(c => EF.Property<int?>(c, "CompanyId") == entityId.Value),
                    "location" => query.Where(c => EF.Property<int?>(c, "LocationId") == entityId.Value),
                    _ => query
                };
            }

            return query;
        }

        public IQueryable<ContactDetails> ApplySearchFilter(IQueryable<ContactDetails> query, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return query;

            var lowerSearchTerm = searchTerm.ToLower();
            return query.Where(c =>
                (c.Email != null && c.Email.ToLower().Contains(lowerSearchTerm)) ||
                (c.SecondaryEmail != null && c.SecondaryEmail.ToLower().Contains(lowerSearchTerm)) ||
                (c.PrimaryPhone != null && c.PrimaryPhone.Contains(searchTerm)) ||
                (c.SecondaryPhone != null && c.SecondaryPhone.Contains(searchTerm)) ||
                (c.Mobile != null && c.Mobile.Contains(searchTerm)) ||
                (c.WhatsAppNumber != null && c.WhatsAppNumber.Contains(searchTerm)) ||
                (c.ContactType != null && c.ContactType.ToLower().Contains(lowerSearchTerm)) ||
                (c.Website != null && c.Website.ToLower().Contains(lowerSearchTerm)));
        }
    }
}