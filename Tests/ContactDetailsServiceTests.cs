using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
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
    /// Tests for ContactDetailsService.
    /// Note: In-memory database is used for testing. Transaction rollback scenarios 
    /// are tested through error handling validation rather than actual rollbacks.
    /// </summary>
    public class ContactDetailsServiceTests : IDisposable
    {
        private readonly Mock<IRepository<ContactDetails>> _mockContactDetailsRepository;
        private readonly Mock<IUserSessionService> _mockUserSessionService;
        private readonly Mock<ILogger<ContactDetailsService>> _mockLogger;
        private readonly IMapper _mapper;
        private readonly ApplicationDbContext _context;
        private readonly ContactDetailsService _contactDetailsService;

        public ContactDetailsServiceTests()
        {
            _mockContactDetailsRepository = new Mock<IRepository<ContactDetails>>();
            _mockUserSessionService = new Mock<IUserSessionService>();
            _mockLogger = new Mock<ILogger<ContactDetailsService>>();

            // Setup AutoMapper
            var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
            _mapper = config.CreateMapper();

            // Setup in-memory database
            _context = TestDataHelpers.CreateInMemoryDbContext();

            _contactDetailsService = new ContactDetailsService(
                _mockContactDetailsRepository.Object,
                _context,
                _mockUserSessionService.Object,
                _mapper,
                _mockLogger.Object);

            // Setup default user session
            _mockUserSessionService.Setup(x => x.GetCurrentUserId()).Returns(1);
        }

        [Fact]
        public async Task GetContactDetailsByIdAsync_ValidId_ReturnsContactDetailsDto()
        {
            // Arrange
            var contactDetails = TestDataHelpers.CreateTestContactDetails();
            _mockContactDetailsRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(contactDetails);

            // Act
            var result = await _contactDetailsService.GetContactDetailsByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(1);
            result.PrimaryPhone.Should().Be("+1234567890");
            result.Email.Should().Be("contact@test.com");
            result.Website.Should().Be("https://test.com");
        }

        [Fact]
        public async Task GetContactDetailsByIdAsync_InvalidId_ThrowsArgumentException()
        {
            // Arrange
            _mockContactDetailsRepository.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((ContactDetails?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _contactDetailsService.GetContactDetailsByIdAsync(999));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetContactDetailsByIdAsync_ZeroOrNegativeId_ThrowsArgumentException(int contactId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _contactDetailsService.GetContactDetailsByIdAsync(contactId));
        }

        [Fact]
        public async Task GetContactDetailsByEntityAsync_ValidUserEntity_ReturnsContactDetails()
        {
            // Arrange
            var user = TestDataHelpers.CreateTestUser();
            var contactDetails = TestDataHelpers.CreateTestContactDetails();

            _context.Users.Add(user);
            _context.ContactDetails.Add(contactDetails);
            _context.Entry(contactDetails).Property("UserId").CurrentValue = user.Id;
            await _context.SaveChangesAsync();

            // Act
            var result = await _contactDetailsService.GetContactDetailsByEntityAsync("user", user.Id);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().Email.Should().Be("contact@test.com");
        }

        [Fact]
        public async Task GetContactDetailsByEntityAsync_ValidCompanyEntity_ReturnsContactDetails()
        {
            // Arrange
            var company = TestDataHelpers.CreateTestCompany();
            var contactDetails = TestDataHelpers.CreateTestContactDetails();

            _context.Companies.Add(company);
            _context.ContactDetails.Add(contactDetails);
            _context.Entry(contactDetails).Property("CompanyId").CurrentValue = company.Id;
            await _context.SaveChangesAsync();

            // Act
            var result = await _contactDetailsService.GetContactDetailsByEntityAsync("company", company.Id);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().Email.Should().Be("contact@test.com");
        }

        [Fact]
        public async Task GetContactDetailsByEntityAsync_ValidLocationEntity_ReturnsContactDetails()
        {
            // Arrange
            var company = TestDataHelpers.CreateTestCompany();
            var location = TestDataHelpers.CreateTestLocation(1, company.Id);
            var contactDetails = TestDataHelpers.CreateTestContactDetails();

            _context.Companies.Add(company);
            _context.Locations.Add(location);
            _context.ContactDetails.Add(contactDetails);
            _context.Entry(contactDetails).Property("LocationId").CurrentValue = location.Id;
            await _context.SaveChangesAsync();

            // Act
            var result = await _contactDetailsService.GetContactDetailsByEntityAsync("location", location.Id);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().Email.Should().Be("contact@test.com");
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("store")] // Old entity type should not work
        public async Task GetContactDetailsByEntityAsync_InvalidEntityType_ThrowsArgumentException(string entityType)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _contactDetailsService.GetContactDetailsByEntityAsync(entityType, 1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetContactDetailsByEntityAsync_InvalidEntityId_ThrowsArgumentException(int entityId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _contactDetailsService.GetContactDetailsByEntityAsync("user", entityId));
        }

        [Fact]
        public async Task CreateContactDetailsAsync_ValidData_ReturnsCreatedContactDetails()
        {
            // Arrange
            var user = TestDataHelpers.CreateTestUser();
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var createDto = new CreateContactDetailsDto
            {
                PrimaryPhone = "+9876543210",
                Email = "new@example.com",
                Website = "https://newexample.com",
                IsDefault = false
            };

            // Act
            var result = await _contactDetailsService.CreateContactDetailsAsync(createDto, "user", user.Id);

            // Assert
            result.Should().NotBeNull();
            result.PrimaryPhone.Should().Be("+9876543210");
            result.Email.Should().Be("new@example.com");
            result.Website.Should().Be("https://newexample.com");

            // Verify contact details was added to database
            var savedContactDetails = await _context.ContactDetails.FirstOrDefaultAsync();
            savedContactDetails.Should().NotBeNull();
            savedContactDetails!.Email.Should().Be("new@example.com");
        }

        [Fact]
        public async Task CreateContactDetailsAsync_NullDto_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _contactDetailsService.CreateContactDetailsAsync(null!, "user", 1));
        }

        [Fact]
        public async Task CreateContactDetailsAsync_InvalidEntityType_ThrowsArgumentException()
        {
            // Arrange
            var createDto = new CreateContactDetailsDto { Email = "test@test.com" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _contactDetailsService.CreateContactDetailsAsync(createDto, "invalid", 1));
        }

        [Fact]
        public async Task CreateContactDetailsAsync_NonExistentEntity_ThrowsArgumentException()
        {
            // Arrange
            var createDto = new CreateContactDetailsDto { Email = "test@test.com" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _contactDetailsService.CreateContactDetailsAsync(createDto, "user", 999));
        }

        [Fact]
        public async Task UpdateContactDetailsAsync_ValidData_ReturnsUpdatedContactDetails()
        {
            // Arrange
            var contactDetails = TestDataHelpers.CreateTestContactDetails();
            var updateDto = new UpdateContactDetailsDto
            {
                PrimaryPhone = "+9999999999",
                Email = "updated@test.com",
                Website = "https://updated.com",
                IsDefault = true
            };

            _mockContactDetailsRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(contactDetails);
            _mockContactDetailsRepository.Setup(x => x.Update(It.IsAny<ContactDetails>()));
            _mockContactDetailsRepository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _contactDetailsService.UpdateContactDetailsAsync(1, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Email.Should().Be("updated@test.com");
            result.IsDefault.Should().BeTrue();

            _mockContactDetailsRepository.Verify(x => x.Update(It.IsAny<ContactDetails>()), Times.Once);
            _mockContactDetailsRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateContactDetailsAsync_InvalidId_ThrowsArgumentException()
        {
            // Arrange
            var updateDto = new UpdateContactDetailsDto { Email = "test@test.com" };
            _mockContactDetailsRepository.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((ContactDetails?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _contactDetailsService.UpdateContactDetailsAsync(999, updateDto));
        }

        [Fact]
        public async Task UpdateContactDetailsAsync_NullDto_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _contactDetailsService.UpdateContactDetailsAsync(1, null!));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task UpdateContactDetailsAsync_ZeroOrNegativeId_ThrowsArgumentException(int contactId)
        {
            // Arrange
            var updateDto = new UpdateContactDetailsDto { Email = "test@test.com" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _contactDetailsService.UpdateContactDetailsAsync(contactId, updateDto));
        }

        [Fact]
        public async Task DeleteContactDetailsAsync_ValidId_ReturnsTrue()
        {
            // Arrange
            _mockContactDetailsRepository.Setup(x => x.SoftDeleteAsync(1, 1)).ReturnsAsync(true);

            // Act
            var result = await _contactDetailsService.DeleteContactDetailsAsync(1);

            // Assert
            result.Should().BeTrue();
            _mockContactDetailsRepository.Verify(x => x.SoftDeleteAsync(1, 1), Times.Once);
        }

        [Fact]
        public async Task DeleteContactDetailsAsync_InvalidId_ReturnsFalse()
        {
            // Arrange
            _mockContactDetailsRepository.Setup(x => x.SoftDeleteAsync(999, 1)).ReturnsAsync(false);

            // Act
            var result = await _contactDetailsService.DeleteContactDetailsAsync(999);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task DeleteContactDetailsAsync_ZeroOrNegativeId_ThrowsArgumentException(int contactId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _contactDetailsService.DeleteContactDetailsAsync(contactId));
        }

        [Fact]
        public async Task SetDefaultContactDetailsAsync_ValidData_ReturnsTrue()
        {
            // Arrange
            var user = TestDataHelpers.CreateTestUser();
            var contactDetails1 = TestDataHelpers.CreateTestContactDetails(1);
            var contactDetails2 = TestDataHelpers.CreateTestContactDetails(2);
            contactDetails1.IsDefault = true;
            contactDetails2.Email = "contact2@test.com";

            _context.Users.Add(user);
            _context.ContactDetails.AddRange(contactDetails1, contactDetails2);
            _context.Entry(contactDetails1).Property("UserId").CurrentValue = user.Id;
            _context.Entry(contactDetails2).Property("UserId").CurrentValue = user.Id;
            await _context.SaveChangesAsync();

            _mockContactDetailsRepository.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(contactDetails2);

            // Act
            var result = await _contactDetailsService.SetDefaultContactDetailsAsync(2, "user", user.Id);

            // Assert
            result.Should().BeTrue();

            // Verify the default status was updated
            var updatedContactDetails1 = await _context.ContactDetails.FindAsync(1);
            var updatedContactDetails2 = await _context.ContactDetails.FindAsync(2);

            updatedContactDetails1?.IsDefault.Should().BeFalse();
            updatedContactDetails2?.IsDefault.Should().BeTrue();
        }

        [Fact]
        public async Task SetDefaultContactDetailsAsync_InvalidEntityType_ThrowsArgumentException()
        {
            // Arrange
            var contactDetails = TestDataHelpers.CreateTestContactDetails();
            _mockContactDetailsRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(contactDetails);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _contactDetailsService.SetDefaultContactDetailsAsync(1, "invalid", 1));
        }

        [Fact]
        public async Task SetDefaultContactDetailsAsync_ContactDetailsNotBelongingToEntity_ThrowsArgumentException()
        {
            // Arrange
            var user1 = TestDataHelpers.CreateTestUser(1);
            var user2 = TestDataHelpers.CreateTestUser(2, "user2@example.com");
            var contactDetails = TestDataHelpers.CreateTestContactDetails();

            _context.Users.AddRange(user1, user2);
            _context.ContactDetails.Add(contactDetails);
            _context.Entry(contactDetails).Property("UserId").CurrentValue = user1.Id;
            await _context.SaveChangesAsync();

            _mockContactDetailsRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(contactDetails);

            // Act & Assert - Try to set default for user2 but contact details belongs to user1
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _contactDetailsService.SetDefaultContactDetailsAsync(1, "user", user2.Id));
        }

        [Fact]
        public async Task SetDefaultContactDetailsAsync_NonExistentContactDetails_ReturnsFalse()
        {
            // Arrange
            _mockContactDetailsRepository.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((ContactDetails?)null);

            // Act
            var result = await _contactDetailsService.SetDefaultContactDetailsAsync(999, "user", 1);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task SetDefaultContactDetailsAsync_ZeroOrNegativeContactId_ThrowsArgumentException(int contactId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _contactDetailsService.SetDefaultContactDetailsAsync(contactId, "user", 1));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task SetDefaultContactDetailsAsync_NullOrEmptyEntityType_ThrowsArgumentException(string? entityType)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _contactDetailsService.SetDefaultContactDetailsAsync(1, entityType!, 1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task SetDefaultContactDetailsAsync_ZeroOrNegativeEntityId_ThrowsArgumentException(int entityId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _contactDetailsService.SetDefaultContactDetailsAsync(1, "user", entityId));
        }

        [Fact]
        public async Task CreateContactDetailsAsync_SetsAuditFields()
        {
            // Arrange
            var currentUserId = 42;

            // Create a context that will properly handle audit fields
            using var contextWithUser = TestDataHelpers.CreateInMemoryDbContextWithUser(currentUserId);

            var user = TestDataHelpers.CreateTestUser();
            contextWithUser.Users.Add(user);
            await contextWithUser.SaveChangesAsync();

            var createDto = new CreateContactDetailsDto { Email = "test@test.com" };
            _mockUserSessionService.Setup(x => x.GetCurrentUserId()).Returns(currentUserId);

            // Create service with the context that has proper user context
            var contactDetailsService = new ContactDetailsService(
                _mockContactDetailsRepository.Object,
                contextWithUser,
                _mockUserSessionService.Object,
                _mapper,
                _mockLogger.Object);

            // Act
            await contactDetailsService.CreateContactDetailsAsync(createDto, "user", user.Id);

            // Assert
            var savedContactDetails = await contextWithUser.ContactDetails.FirstOrDefaultAsync();
            savedContactDetails.Should().NotBeNull();
            savedContactDetails!.CreatedByUserId.Should().Be(currentUserId);
            savedContactDetails.UpdatedByUserId.Should().Be(currentUserId);
            savedContactDetails.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
            savedContactDetails.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task UpdateContactDetailsAsync_SetsAuditFields()
        {
            // Arrange
            var currentUserId = 42;

            // Create context with user for audit fields
            using var contextWithUser = TestDataHelpers.CreateInMemoryDbContextWithUser(currentUserId);

            var contactDetails = TestDataHelpers.CreateTestContactDetails();
            var updateDto = new UpdateContactDetailsDto { Email = "updated@test.com" };

            _mockUserSessionService.Setup(x => x.GetCurrentUserId()).Returns(currentUserId);
            _mockContactDetailsRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(contactDetails);
            _mockContactDetailsRepository.Setup(x => x.Update(It.IsAny<ContactDetails>()));
            _mockContactDetailsRepository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

            // Create service with the context that has proper user context
            var contactDetailsService = new ContactDetailsService(
                _mockContactDetailsRepository.Object,
                contextWithUser,
                _mockUserSessionService.Object,
                _mapper,
                _mockLogger.Object);

            // Act
            await contactDetailsService.UpdateContactDetailsAsync(1, updateDto);

            // Assert
            _mockContactDetailsRepository.Verify(x => x.Update(It.Is<ContactDetails>(c =>
                c.UpdatedByUserId == currentUserId &&
                c.UpdatedAt > DateTime.UtcNow.AddMinutes(-1))), Times.Once);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}