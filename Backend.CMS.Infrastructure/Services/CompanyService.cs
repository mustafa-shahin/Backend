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
    public class CompanyService : ICompanyService
    {
        private readonly ICompanyRepository _companyRepository;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IUserSessionService _userSessionService;

        public CompanyService(
            IRepository<Company> companyRepository,
            ApplicationDbContext context,
            IUserSessionService userSessionService,
            IMapper mapper,
            ICompanyRepository companyService)
        {
            _companyRepository = companyService;
            _context = context;
            _mapper = mapper;
            _userSessionService = userSessionService;
        }

        public async Task<CompanyDto> GetCompanyAsync()
        {
            var company = await _companyRepository.GetCompanyWithDetailsAsync();
            var currentUserId = _userSessionService.GetCurrentUserId();

            if (company == null)
            {
                company = new Company
                {
                    Name = "Default Company",
                    Description = "Default company description",
                    IsActive = true,
                    Currency = "USD",
                    Language = "en",
                    Timezone = "UTC",
                    CreatedByUserId = currentUserId,
                    UpdatedByUserId = currentUserId
                };

                await _companyRepository.AddAsync(company);
                await _companyRepository.SaveChangesAsync();
            }

            return _mapper.Map<CompanyDto>(company);
        }
        public async Task<CompanyDto> UpdateCompanyAsync(UpdateCompanyDto updateCompanyDto)
        {
            var companies = await _companyRepository.GetAllAsync();
            var company = companies.FirstOrDefault();
            var currentUserId = _userSessionService.GetCurrentUserId();

            if (company == null)
                throw new ArgumentException("Company not found");

            // Update company basic properties
            _mapper.Map(updateCompanyDto, company);
            company.UpdatedAt = DateTime.UtcNow;
            company.UpdatedByUserId = currentUserId;

            _companyRepository.Update(company);
            await _companyRepository.SaveChangesAsync();

            // Handle addresses
            if (updateCompanyDto.Addresses?.Any() == true)
            {
                // Get existing addresses
                var existingAddresses = await _context.Addresses
                    .Where(a => EF.Property<int?>(a, "CompanyId") == company.Id && !a.IsDeleted)
                    .ToListAsync();

                // Soft delete existing addresses
                foreach (var existingAddress in existingAddresses)
                {
                    existingAddress.IsDeleted = true;
                    existingAddress.DeletedAt = DateTime.UtcNow;
                    existingAddress.DeletedByUserId = currentUserId;
                    _context.Addresses.Update(existingAddress);
                }

                // Add new addresses
                foreach (var addressDto in updateCompanyDto.Addresses)
                {
                    var address = _mapper.Map<Address>(addressDto);
                    address.CreatedByUserId = currentUserId;
                    address.UpdatedByUserId = currentUserId;
                    address.CreatedAt = DateTime.UtcNow;
                    address.UpdatedAt = DateTime.UtcNow;

                    _context.Addresses.Add(address);
                    _context.Entry(address).Property("CompanyId").CurrentValue = company.Id;
                }
            }

            // Handle contact details
            if (updateCompanyDto.ContactDetails?.Any() == true)
            {
                // Get existing contact details
                var existingContacts = await _context.ContactDetails
                    .Where(c => EF.Property<int?>(c, "CompanyId") == company.Id && !c.IsDeleted)
                    .ToListAsync();

                // Soft delete existing contact details
                foreach (var existingContact in existingContacts)
                {
                    existingContact.IsDeleted = true;
                    existingContact.DeletedAt = DateTime.UtcNow;
                    existingContact.DeletedByUserId = currentUserId;
                    _context.ContactDetails.Update(existingContact);
                }

                // Add new contact details
                foreach (var contactDto in updateCompanyDto.ContactDetails)
                {
                    var contact = _mapper.Map<ContactDetails>(contactDto);
                    contact.CreatedByUserId = currentUserId;
                    contact.UpdatedByUserId = currentUserId;
                    contact.CreatedAt = DateTime.UtcNow;
                    contact.UpdatedAt = DateTime.UtcNow;

                    _context.ContactDetails.Add(contact);
                    _context.Entry(contact).Property("CompanyId").CurrentValue = company.Id;
                }
            }

            await _context.SaveChangesAsync();
            return _mapper.Map<CompanyDto>(company);
        }
    }
}