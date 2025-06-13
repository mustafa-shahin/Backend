using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Backend.CMS.Infrastructure.Services
{
    public class ContactDetailsService : IContactDetailsService
    {
        private readonly IRepository<ContactDetails> _contactDetailsRepository;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;

        public ContactDetailsService(
            IRepository<ContactDetails> contactDetailsRepository,
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            IMapper mapper)
        {
            _contactDetailsRepository = contactDetailsRepository;
            _context = context;
            _mapper = mapper;
            _userSessionService = userSessionService;
        }

        public async Task<ContactDetailsDto> GetContactDetailsByIdAsync(int contactId)
        {
            var contactDetails = await _contactDetailsRepository.GetByIdAsync(contactId);
            if (contactDetails == null)
                throw new ArgumentException("Contact details not found");

            return _mapper.Map<ContactDetailsDto>(contactDetails);
        }

        public async Task<List<ContactDetailsDto>> GetContactDetailsByEntityAsync(string entityType, int entityId)
        {
            var query = _context.ContactDetails.AsQueryable();

            switch (entityType.ToLower())
            {
                case "user":
                    query = query.Where(c => EF.Property<int?>(c, "UserId") == entityId);
                    break;
                case "company":
                    query = query.Where(c => EF.Property<int?>(c, "CompanyId") == entityId);
                    break;
                case "location": // Changed from "store"
                    query = query.Where(c => EF.Property<int?>(c, "LocationId") == entityId);
                    break;
                default:
                    throw new ArgumentException($"Invalid entity type: {entityType}");
            }

            var contactDetails = await query.ToListAsync();
            return _mapper.Map<List<ContactDetailsDto>>(contactDetails);
        }

        public async Task<ContactDetailsDto> CreateContactDetailsAsync(CreateContactDetailsDto createContactDetailsDto, string entityType, int entityId)
        {
            // Validate entity type
            if (!IsValidEntityType(entityType))
                throw new ArgumentException($"Invalid entity type: {entityType}");

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
            switch (entityType.ToLower())
            {
                case "user":
                    _context.Entry(contactDetails).Property("UserId").CurrentValue = entityId;
                    break;
                case "company":
                    _context.Entry(contactDetails).Property("CompanyId").CurrentValue = entityId;
                    break;
                case "location":
                    _context.Entry(contactDetails).Property("LocationId").CurrentValue = entityId;
                    break;
            }

            await _context.SaveChangesAsync();
            return _mapper.Map<ContactDetailsDto>(contactDetails);
        }

        public async Task<ContactDetailsDto> UpdateContactDetailsAsync(int contactId, UpdateContactDetailsDto updateContactDetailsDto)
        {
            var contactDetails = await _contactDetailsRepository.GetByIdAsync(contactId);
            var currentUserId = _userSessionService.GetCurrentUserId();

            if (contactDetails == null)
                throw new ArgumentException("Contact details not found");

            _mapper.Map(updateContactDetailsDto, contactDetails);
            contactDetails.UpdatedAt = DateTime.UtcNow;
            contactDetails.UpdatedByUserId = currentUserId;

            _contactDetailsRepository.Update(contactDetails);
            await _contactDetailsRepository.SaveChangesAsync();

            return _mapper.Map<ContactDetailsDto>(contactDetails);
        }

        public async Task<bool> DeleteContactDetailsAsync(int contactId)
        {
            var currentUserId = _userSessionService.GetCurrentUserId();
            return await _contactDetailsRepository.SoftDeleteAsync(contactId, currentUserId);
        }

        public async Task<bool> SetDefaultContactDetailsAsync(int contactId, string entityType, int entityId)
        {
            // Validate entity type
            if (!IsValidEntityType(entityType))
                throw new ArgumentException($"Invalid entity type: {entityType}");

            var contactDetails = await _contactDetailsRepository.GetByIdAsync(contactId);
            var currentUserId = _userSessionService.GetCurrentUserId();

            if (contactDetails == null)
                return false;

            // Verify that the contact details belongs to the specified entity
            var entityIdProperty = GetEntityIdPropertyName(entityType);
            var contactEntityId = _context.Entry(contactDetails).Property(entityIdProperty).CurrentValue;

            if (!contactEntityId?.Equals(entityId) == true)
                throw new ArgumentException("Contact details does not belong to the specified entity");

            // Unset default for all other contact details of this entity using proper query
            var query = _context.ContactDetails.AsQueryable();

            switch (entityType.ToLower())
            {
                case "user":
                    query = query.Where(c => EF.Property<int?>(c, "UserId") == entityId);
                    break;
                case "company":
                    query = query.Where(c => EF.Property<int?>(c, "CompanyId") == entityId);
                    break;
                case "location":
                    query = query.Where(c => EF.Property<int?>(c, "LocationId") == entityId);
                    break;
            }

            var allContactDetails = await query.ToListAsync();

            foreach (var contact in allContactDetails)
            {
                contact.IsDefault = false;
                contact.UpdatedAt = DateTime.UtcNow;
                contact.UpdatedByUserId = currentUserId;
                _context.ContactDetails.Update(contact);
            }

            // Set this contact details as default
            contactDetails.IsDefault = true;
            contactDetails.UpdatedAt = DateTime.UtcNow;
            contactDetails.UpdatedByUserId = currentUserId;
            _context.ContactDetails.Update(contactDetails);

            await _context.SaveChangesAsync();
            return true;
        }
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
                "location" => "LocationId", // Changed from "store" => "StoreId"
                _ => throw new ArgumentException($"Invalid entity type: {entityType}")
            };
        }

        private async Task ValidateEntityExistsAsync(string entityType, int entityId)
        {
            bool exists = entityType.ToLower() switch
            {
                "user" => await _context.Users.AnyAsync(u => u.Id == entityId),
                "company" => await _context.Companies.AnyAsync(c => c.Id == entityId),
                "location" => await _context.Locations.AnyAsync(l => l.Id == entityId), // Changed from Stores
                _ => false
            };

            if (!exists)
                throw new ArgumentException($"{entityType} with ID {entityId} not found");
        }
    }
}