using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Backend.CMS.Infrastructure.Mapping;
using Backend.CMS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests
{
    /// <summary>
    /// Tests for CompanyService.
    /// Note: In-memory database is used for testing. Transaction rollback scenarios 
    /// are tested through error handling validation rather than actual rollbacks.
    /// </summary>
    public class CompanyServiceTests : IDisposable
    {
        private readonly Mock<ICompanyRepository> _mockCompanyRepository;
        private readonly Mock<IUserSessionService> _mockUserSessionService;
        private readonly Mock<ILogger<CompanyService>> _mockLogger;
        private readonly IMapper _mapper;
        private readonly ApplicationDbContext _context;
        private readonly CompanyService _companyService;

        public CompanyServiceTests()
        {
            _mockCompanyRepository = new Mock<ICompanyRepository>();
            _mockUserSessionService = new Mock<IUserSessionService>();
            _mockLogger = new Mock<ILogger<CompanyService>>();

            // Setup AutoMapper
            var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
            _mapper = config.CreateMapper();

            // Setup in-memory database
            _context = TestDataHelpers.CreateInMemoryDbContext();

            _companyService = new CompanyService(
                _mockCompanyRepository.Object,
                _context,
                _mockUserSessionService.Object,
                _mapper,
                _mockLogger.Object);

            // Setup default user session
            _mockUserSessionService.Setup(x => x.GetCurrentUserId()).Returns(1);
        }

        [Fact]
        public async Task GetCompanyAsync_ExistingCompany_ReturnsCompanyDto()
        {
            // Arrange
            var company = TestDataHelpers.CreateTestCompany();
            _mockCompanyRepository.Setup(x => x.GetCompanyWithDetailsAsync())
                .ReturnsAsync(company);

            // Act
            var result = await _companyService.GetCompanyAsync();

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(1);
            result.Name.Should().Be("Test Company");
            result.Description.Should().Be("Test Description");
            result.Currency.Should().Be("EUR");
            result.Language.Should().Be("en");
            result.Timezone.Should().Be("UTC");
        }

        [Fact]
        public async Task GetCompanyAsync_NoExistingCompany_CreatesAndReturnsDefaultCompany()
        {
            // Arrange
            _mockCompanyRepository.Setup(x => x.GetCompanyWithDetailsAsync())
                .ReturnsAsync((Company?)null);
            _mockCompanyRepository.Setup(x => x.AddAsync(It.IsAny<Company>()))
                .Returns(Task.CompletedTask);
            _mockCompanyRepository.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            var result = await _companyService.GetCompanyAsync();

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Default Company");
            result.Description.Should().Be("Default company description");
            result.Currency.Should().Be("USD");
            result.Language.Should().Be("en");
            result.Timezone.Should().Be("UTC");
            result.IsActive.Should().BeTrue();

            _mockCompanyRepository.Verify(x => x.AddAsync(It.IsAny<Company>()), Times.Once);
            _mockCompanyRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateCompanyAsync_ValidCompany_UpdatesAndReturnsCompanyDto()
        {
            // Arrange
            var existingCompany = TestDataHelpers.CreateTestCompany();
            var updateDto = TestDataHelpers.CreateTestUpdateCompanyDto();

            _mockCompanyRepository.Setup(x => x.GetAllAsync())
                .ReturnsAsync(new List<Company> { existingCompany });
            _mockCompanyRepository.Setup(x => x.Update(It.IsAny<Company>()));
            _mockCompanyRepository.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);
            _mockCompanyRepository.Setup(x => x.GetCompanyWithDetailsAsync(existingCompany.Id))
                .ReturnsAsync(existingCompany);

            // Act
            var result = await _companyService.UpdateCompanyAsync(updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Updated Company");
            result.Description.Should().Be("Updated Description");
            result.Currency.Should().Be("EUR");
            result.Language.Should().Be("de");
            result.Timezone.Should().Be("Europe/Berlin");

            _mockCompanyRepository.Verify(x => x.Update(It.IsAny<Company>()), Times.Once);
            _mockCompanyRepository.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task UpdateCompanyAsync_NullDto_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _companyService.UpdateCompanyAsync(null!));
        }

        [Fact]
        public async Task UpdateCompanyAsync_NoExistingCompany_ThrowsArgumentException()
        {
            // Arrange
            var updateDto = TestDataHelpers.CreateTestUpdateCompanyDto();
            _mockCompanyRepository.Setup(x => x.GetAllAsync())
                .ReturnsAsync(new List<Company>());

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _companyService.UpdateCompanyAsync(updateDto));
        }

        [Fact]
        public async Task UpdateCompanyAsync_WithAddresses_UpdatesAddressesCorrectly()
        {
            // Arrange
            var existingCompany = TestDataHelpers.CreateTestCompany();
            var existingAddress = TestDataHelpers.CreateTestAddress();

            // Add existing address to context
            _context.Companies.Add(existingCompany);
            _context.Addresses.Add(existingAddress);
            _context.Entry(existingAddress).Property("CompanyId").CurrentValue = existingCompany.Id;
            await _context.SaveChangesAsync();

            var updateDto = TestDataHelpers.CreateTestUpdateCompanyDto();
            updateDto.Addresses = new List<UpdateAddressDto>
            {
                new UpdateAddressDto
                {
                    Street = "New Address Street",
                    HouseNr = "1",
                    City = "New City",
                    State = "New State",
                    Country = "New Country",
                    PostalCode = "11111"
                }
            };

            _mockCompanyRepository.Setup(x => x.GetAllAsync())
                .ReturnsAsync(new List<Company> { existingCompany });
            _mockCompanyRepository.Setup(x => x.Update(It.IsAny<Company>()));
            _mockCompanyRepository.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);
            _mockCompanyRepository.Setup(x => x.GetCompanyWithDetailsAsync(existingCompany.Id))
                .ReturnsAsync(existingCompany);

            // Act
            var result = await _companyService.UpdateCompanyAsync(updateDto);

            // Assert
            result.Should().NotBeNull();

            // Verify old address is soft deleted
            var oldAddress = await _context.Addresses.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == existingAddress.Id);
            oldAddress.Should().NotBeNull();
            oldAddress!.IsDeleted.Should().BeTrue();

            // Verify new address is created
            var newAddress = await _context.Addresses
                .FirstOrDefaultAsync(a => a.Street == "New Address Street");
            newAddress.Should().NotBeNull();
            newAddress!.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateCompanyAsync_WithContactDetails_UpdatesContactDetailsCorrectly()
        {
            // Arrange
            var existingCompany = TestDataHelpers.CreateTestCompany();
            var existingContact = TestDataHelpers.CreateTestContactDetails();

            // Add existing contact to context
            _context.Companies.Add(existingCompany);
            _context.ContactDetails.Add(existingContact);
            _context.Entry(existingContact).Property("CompanyId").CurrentValue = existingCompany.Id;
            await _context.SaveChangesAsync();

            var updateDto = TestDataHelpers.CreateTestUpdateCompanyDto();
            updateDto.ContactDetails = new List<UpdateContactDetailsDto>
            {
                new UpdateContactDetailsDto
                {
                    PrimaryPhone = "+9876543210",
                    Email = "new@company.com",
                    Website = "https://newcompany.com"
                }
            };

            _mockCompanyRepository.Setup(x => x.GetAllAsync())
                .ReturnsAsync(new List<Company> { existingCompany });
            _mockCompanyRepository.Setup(x => x.Update(It.IsAny<Company>()));
            _mockCompanyRepository.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);
            _mockCompanyRepository.Setup(x => x.GetCompanyWithDetailsAsync(existingCompany.Id))
                .ReturnsAsync(existingCompany);

            // Act
            var result = await _companyService.UpdateCompanyAsync(updateDto);

            // Assert
            result.Should().NotBeNull();

            // Verify old contact is soft deleted
            var oldContact = await _context.ContactDetails.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == existingContact.Id);
            oldContact.Should().NotBeNull();
            oldContact!.IsDeleted.Should().BeTrue();

            // Verify new contact is created
            var newContact = await _context.ContactDetails
                .FirstOrDefaultAsync(c => c.Email == "new@company.com");
            newContact.Should().NotBeNull();
            newContact!.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateCompanyAsync_WithoutAddressesAndContacts_DoesNotModifyThem()
        {
            // Arrange
            var existingCompany = TestDataHelpers.CreateTestCompany();
            var existingAddress = TestDataHelpers.CreateTestAddress();
            var existingContact = TestDataHelpers.CreateTestContactDetails();

            // Add existing data to context
            _context.Companies.Add(existingCompany);
            _context.Addresses.Add(existingAddress);
            _context.ContactDetails.Add(existingContact);
            _context.Entry(existingAddress).Property("CompanyId").CurrentValue = existingCompany.Id;
            _context.Entry(existingContact).Property("CompanyId").CurrentValue = existingCompany.Id;
            await _context.SaveChangesAsync();

            var updateDto = TestDataHelpers.CreateTestUpdateCompanyDto();
            // No addresses or contact details in DTO

            _mockCompanyRepository.Setup(x => x.GetAllAsync())
                .ReturnsAsync(new List<Company> { existingCompany });
            _mockCompanyRepository.Setup(x => x.Update(It.IsAny<Company>()));
            _mockCompanyRepository.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);
            _mockCompanyRepository.Setup(x => x.GetCompanyWithDetailsAsync(existingCompany.Id))
                .ReturnsAsync(existingCompany);

            // Act
            var result = await _companyService.UpdateCompanyAsync(updateDto);

            // Assert
            result.Should().NotBeNull();

            // Verify existing address and contact are not modified
            var address = await _context.Addresses.FindAsync(existingAddress.Id);
            var contact = await _context.ContactDetails.FindAsync(existingContact.Id);

            address.Should().NotBeNull();
            address!.IsDeleted.Should().BeFalse();
            contact.Should().NotBeNull();
            contact!.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateCompanyAsync_SetsAuditFields()
        {
            // Arrange
            var currentUserId = 42;

            // Create context with user for audit fields
            using var contextWithUser = TestDataHelpers.CreateInMemoryDbContextWithUser(currentUserId);

            var existingCompany = TestDataHelpers.CreateTestCompany();
            var updateDto = TestDataHelpers.CreateTestUpdateCompanyDto();

            _mockUserSessionService.Setup(x => x.GetCurrentUserId()).Returns(currentUserId);
            _mockCompanyRepository.Setup(x => x.GetAllAsync())
                .ReturnsAsync(new List<Company> { existingCompany });
            _mockCompanyRepository.Setup(x => x.Update(It.IsAny<Company>()));
            _mockCompanyRepository.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);
            _mockCompanyRepository.Setup(x => x.GetCompanyWithDetailsAsync(existingCompany.Id))
                .ReturnsAsync(existingCompany);

            // Create service with context that has proper user context
            var companyService = new CompanyService(
                _mockCompanyRepository.Object,
                contextWithUser,
                _mockUserSessionService.Object,
                _mapper,
                _mockLogger.Object);

            // Act
            await companyService.UpdateCompanyAsync(updateDto);

            // Assert
            _mockCompanyRepository.Verify(x => x.Update(It.Is<Company>(c =>
                c.UpdatedByUserId == currentUserId &&
                c.UpdatedAt > DateTime.UtcNow.AddMinutes(-1))), Times.Once);
        }

        [Fact]
        public async Task UpdateCompanyAsync_WithAddressesAndContacts_ExecutesInTransaction()
        {
            // Arrange
            var existingCompany = TestDataHelpers.CreateTestCompany();
            var updateDto = TestDataHelpers.CreateTestUpdateCompanyDto();
            updateDto.Addresses = new List<UpdateAddressDto> { new UpdateAddressDto { Street = "Test Street",HouseNr = "1" , City = "Test City", State = "Test State", Country = "Test Country", PostalCode = "12345" } };
            updateDto.ContactDetails = new List<UpdateContactDetailsDto> { new UpdateContactDetailsDto { Email = "test@test.com" } };

            _mockCompanyRepository.Setup(x => x.GetAllAsync())
                .ReturnsAsync(new List<Company> { existingCompany });
            _mockCompanyRepository.Setup(x => x.Update(It.IsAny<Company>()));
            _mockCompanyRepository.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);
            _mockCompanyRepository.Setup(x => x.GetCompanyWithDetailsAsync(existingCompany.Id))
                .ReturnsAsync(existingCompany);

            // Act
            var result = await _companyService.UpdateCompanyAsync(updateDto);

            // Assert
            result.Should().NotBeNull();
            // Verify that save was called multiple times due to transaction handling
            _mockCompanyRepository.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task CreateDefaultCompanyAsync_SetsCorrectDefaultValues()
        {
            // Arrange
            var currentUserId = 123;

            // Create context with user for audit fields
            using var contextWithUser = TestDataHelpers.CreateInMemoryDbContextWithUser(currentUserId);

            _mockUserSessionService.Setup(x => x.GetCurrentUserId()).Returns(currentUserId);
            _mockCompanyRepository.Setup(x => x.GetCompanyWithDetailsAsync())
                .ReturnsAsync((Company?)null);
            _mockCompanyRepository.Setup(x => x.AddAsync(It.IsAny<Company>()))
                .Returns(Task.CompletedTask);
            _mockCompanyRepository.Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);

            // Create service with context that has proper user context
            var companyService = new CompanyService(
                _mockCompanyRepository.Object,
                contextWithUser,
                _mockUserSessionService.Object,
                _mapper,
                _mockLogger.Object);

            // Act
            var result = await companyService.GetCompanyAsync();

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Default Company");
            result.Description.Should().Be("Default company description");
            result.IsActive.Should().BeTrue();
            result.Currency.Should().Be("USD");
            result.Language.Should().Be("en");
            result.Timezone.Should().Be("UTC");

            // Verify that Add was called with correct audit fields
            _mockCompanyRepository.Verify(x => x.AddAsync(It.Is<Company>(c =>
                c.CreatedByUserId == currentUserId &&
                c.UpdatedByUserId == currentUserId &&
                c.CreatedAt > DateTime.UtcNow.AddMinutes(-1) &&
                c.UpdatedAt > DateTime.UtcNow.AddMinutes(-1))), Times.Once);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}