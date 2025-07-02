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
    public class CompanyService : BaseCacheAwareService<Company, CompanyDto>, ICompanyService
    {
        private readonly ICompanyRepository _companyRepository;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;
        private new readonly ILogger<CompanyService> _logger;

        public CompanyService(
            ICompanyRepository companyRepository,
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            ICacheService cacheService,
            IMapper mapper,
            ILogger<CompanyService> logger)
            : base(companyRepository, cacheService, logger)
        {
            _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        protected override string GetEntityCacheKey(int id)
        {
            return $"company:id:{id}";
        }

        protected override string[] GetEntityCachePatterns(int id)
        {
            return new[]
            {
            $"company:*:{id}",
            $"company:id:{id}",
            "company:main"
        };
        }

        protected override string[] GetAllEntitiesCachePatterns()
        {
            return
            ["company:*"];
        }

        protected override async Task<CompanyDto> MapToDto(Company entity)
        {
            return _mapper.Map<CompanyDto>(entity);
        }

        protected override async Task<List<CompanyDto>> MapToDtos(IEnumerable<Company> entities)
        {
            return _mapper.Map<List<CompanyDto>>(entities);
        }
        public async Task<CompanyDto> GetCompanyAsync()
        {
            try
            {
                var companies = await GetAllAsync();
                var company = companies.FirstOrDefault();

                if (company == null)
                {
                    _logger.LogInformation("No company found, creating default company");
                    var newCompany = await CreateDefaultCompanyAsync();
                    return _mapper.Map<CompanyDto>(newCompany);
                }

                return company;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving company information");
                throw;
            }
        }

        public async Task<CompanyDto> UpdateCompanyAsync(UpdateCompanyDto updateCompanyDto)
        {
            if (updateCompanyDto == null)
                throw new ArgumentNullException(nameof(updateCompanyDto));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var companies = await _repository.GetAllAsync();
                var company = companies.FirstOrDefault();

                if (company == null)
                {
                    _logger.LogWarning("No company found for update");
                    throw new ArgumentException("Company not found");
                }

                var currentUserId = _userSessionService.GetCurrentUserId();

                // Update company basic properties
                _mapper.Map(updateCompanyDto, company);
                company.UpdatedAt = DateTime.UtcNow;
                company.UpdatedByUserId = currentUserId;

                // Handle related data updates in parallel
                var tasks = new List<Task>();

                if (updateCompanyDto.Addresses?.Any() == true)
                {
                    tasks.Add(UpdateCompanyAddressesAsync(company.Id, updateCompanyDto.Addresses, currentUserId));
                }

                if (updateCompanyDto.ContactDetails?.Any() == true)
                {
                    tasks.Add(UpdateCompanyContactDetailsAsync(company.Id, updateCompanyDto.ContactDetails, currentUserId));
                }

                if (tasks.Any())
                {
                    await Task.WhenAll(tasks);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Use base class for cache invalidation
                await InvalidateEntityCaches(company.Id);

                _logger.LogInformation("Company {CompanyId} updated by user {UserId}", company.Id, currentUserId);

                // Return updated company with details
                var updatedCompany = await _companyRepository.GetCompanyWithDetailsAsync(company.Id);
                return _mapper.Map<CompanyDto>(updatedCompany);
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating company");
                throw;
            }
        }

        #region Private Helper Methods

        private async Task<Company> CreateDefaultCompanyAsync()
        {
            var currentUserId = _userSessionService.GetCurrentUserId();

            var company = new Company
            {
                Name = "Default Company",
                Description = "Default company description",
                IsActive = true,
                Currency = "USD",
                Language = "en",
                Timezone = "UTC",
                CreatedByUserId = currentUserId,
                UpdatedByUserId = currentUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _companyRepository.AddAsync(company);
            await _companyRepository.SaveChangesAsync();

            _logger.LogInformation("Default company created by user {UserId}", currentUserId);

            return company;
        }

        private async Task UpdateCompanyAddressesAsync(int companyId, IEnumerable<UpdateAddressDto> addressDtos, int? currentUserId)
        {
            // Get existing addresses - avoid optional parameters in LINQ
            var existingAddresses = await _context.Addresses
                .Where(a => EF.Property<int?>(a, "CompanyId") == companyId)
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
                var address = _mapper.Map<Address>(addressDto);
                address.CreatedByUserId = currentUserId;
                address.UpdatedByUserId = currentUserId;
                address.CreatedAt = createTime;
                address.UpdatedAt = createTime;

                newAddresses.Add(address);
            }

            if (newAddresses.Any())
            {
                _context.Addresses.AddRange(newAddresses);

                // Set foreign keys after adding to context
                foreach (var address in newAddresses)
                {
                    _context.Entry(address).Property("CompanyId").CurrentValue = companyId;
                }
            }

            _logger.LogDebug("Updated {DeletedCount} addresses and created {CreatedCount} addresses for company {CompanyId}",
                existingAddresses.Count, newAddresses.Count, companyId);
        }

        private async Task UpdateCompanyContactDetailsAsync(int companyId, IEnumerable<UpdateContactDetailsDto> contactDtos, int? currentUserId)
        {
            // Get existing contact details - avoid optional parameters in LINQ
            var existingContacts = await _context.ContactDetails
                .Where(c => EF.Property<int?>(c, "CompanyId") == companyId)
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
                var contact = _mapper.Map<ContactDetails>(contactDto);
                contact.CreatedByUserId = currentUserId;
                contact.UpdatedByUserId = currentUserId;
                contact.CreatedAt = createTime;
                contact.UpdatedAt = createTime;

                newContacts.Add(contact);
            }

            if (newContacts.Any())
            {
                _context.ContactDetails.AddRange(newContacts);

                // Set foreign keys after adding to context
                foreach (var contact in newContacts)
                {
                    _context.Entry(contact).Property("CompanyId").CurrentValue = companyId;
                }
            }

            _logger.LogDebug("Updated {DeletedCount} contacts and created {CreatedCount} contacts for company {CompanyId}",
                existingContacts.Count, newContacts.Count, companyId);
        }

        #endregion
    }
}