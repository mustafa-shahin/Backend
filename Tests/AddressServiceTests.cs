using AutoMapper;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.Interfaces;
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
    /// Tests for AddressService.
    /// Note: In-memory database is used for testing. Transaction rollback scenarios 
    /// are tested through error handling validation rather than actual rollbacks.
    /// </summary>
    public class AddressServiceTests : IDisposable
    {
        private readonly Mock<IRepository<Address>> _mockAddressRepository;
        private readonly Mock<IUserSessionService> _mockUserSessionService;
        private readonly Mock<ILogger<AddressService>> _mockLogger;
        private readonly IMapper _mapper;
        private readonly ApplicationDbContext _context;
        private readonly AddressService _addressService;

        public AddressServiceTests()
        {
            _mockAddressRepository = new Mock<IRepository<Address>>();
            _mockUserSessionService = new Mock<IUserSessionService>();
            _mockLogger = new Mock<ILogger<AddressService>>();

            // Setup AutoMapper
            var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
            _mapper = config.CreateMapper();

            // Setup in-memory database
            _context = TestDataHelpers.CreateInMemoryDbContext();

            _addressService = new AddressService(
                _mockAddressRepository.Object,
                _context,
                _mockUserSessionService.Object,
                _mapper,
                _mockLogger.Object);

            // Setup default user session
            _mockUserSessionService.Setup(x => x.GetCurrentUserId()).Returns(1);
        }

        [Fact]
        public async Task GetAddressByIdAsync_ValidId_ReturnsAddressDto()
        {
            // Arrange
            var address = TestDataHelpers.CreateTestAddress();
            _mockAddressRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(address);

            // Act
            var result = await _addressService.GetAddressByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(1);
            result.Street.Should().Be("123 Test Street");
            result.City.Should().Be("Test City");
        }

        [Fact]
        public async Task GetAddressByIdAsync_InvalidId_ThrowsArgumentException()
        {
            // Arrange
            _mockAddressRepository.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Address?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _addressService.GetAddressByIdAsync(999));
        }

        [Fact]
        public async Task GetAddressByIdAsync_ZeroOrNegativeId_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _addressService.GetAddressByIdAsync(0));
            await Assert.ThrowsAsync<ArgumentException>(() => _addressService.GetAddressByIdAsync(-1));
        }

        [Fact]
        public async Task GetAddressesByEntityAsync_ValidUserEntity_ReturnsAddresses()
        {
            // Arrange
            var user = TestDataHelpers.CreateTestUser();
            var address = TestDataHelpers.CreateTestAddress();

            _context.Users.Add(user);
            _context.Addresses.Add(address);
            _context.Entry(address).Property("UserId").CurrentValue = user.Id;
            await _context.SaveChangesAsync();

            // Act
            var result = await _addressService.GetAddressesByEntityAsync("user", user.Id);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().Street.Should().Be("123 Test Street");
        }

        [Fact]
        public async Task GetAddressesByEntityAsync_ValidCompanyEntity_ReturnsAddresses()
        {
            // Arrange
            var company = TestDataHelpers.CreateTestCompany();
            var address = TestDataHelpers.CreateTestAddress();

            _context.Companies.Add(company);
            _context.Addresses.Add(address);
            _context.Entry(address).Property("CompanyId").CurrentValue = company.Id;
            await _context.SaveChangesAsync();

            // Act
            var result = await _addressService.GetAddressesByEntityAsync("company", company.Id);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().Street.Should().Be("123 Test Street");
        }

        [Fact]
        public async Task GetAddressesByEntityAsync_ValidLocationEntity_ReturnsAddresses()
        {
            // Arrange
            var company = TestDataHelpers.CreateTestCompany();
            var location = TestDataHelpers.CreateTestLocation(1, company.Id);
            var address = TestDataHelpers.CreateTestAddress();

            _context.Companies.Add(company);
            _context.Locations.Add(location);
            _context.Addresses.Add(address);
            _context.Entry(address).Property("LocationId").CurrentValue = location.Id;
            await _context.SaveChangesAsync();

            // Act
            var result = await _addressService.GetAddressesByEntityAsync("location", location.Id);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().Street.Should().Be("123 Test Street");
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("store")] // Old entity type should not work
        public async Task GetAddressesByEntityAsync_InvalidEntityType_ThrowsArgumentException(string entityType)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _addressService.GetAddressesByEntityAsync(entityType, 1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetAddressesByEntityAsync_InvalidEntityId_ThrowsArgumentException(int entityId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _addressService.GetAddressesByEntityAsync("user", entityId));
        }

        [Fact]
        public async Task CreateAddressAsync_ValidData_ReturnsCreatedAddress()
        {
            // Arrange
            var user = TestDataHelpers.CreateTestUser();
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var createDto = TestDataHelpers.CreateTestCreateAddressDto();

            // Act
            var result = await _addressService.CreateAddressAsync(createDto, "user", user.Id);

            // Assert
            result.Should().NotBeNull();
            result.Street.Should().Be("456 New Street");
            result.City.Should().Be("New City");

            // Verify address was added to database
            var savedAddress = await _context.Addresses.FirstOrDefaultAsync();
            savedAddress.Should().NotBeNull();
            savedAddress!.Street.Should().Be("456 New Street");
        }

        [Fact]
        public async Task CreateAddressAsync_NullDto_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _addressService.CreateAddressAsync(null!, "user", 1));
        }

        [Fact]
        public async Task CreateAddressAsync_InvalidEntityType_ThrowsArgumentException()
        {
            // Arrange
            var createDto = TestDataHelpers.CreateTestCreateAddressDto();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _addressService.CreateAddressAsync(createDto, "invalid", 1));
        }

        [Fact]
        public async Task CreateAddressAsync_NonExistentEntity_ThrowsArgumentException()
        {
            // Arrange
            var createDto = TestDataHelpers.CreateTestCreateAddressDto();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _addressService.CreateAddressAsync(createDto, "user", 999));
        }

        [Fact]
        public async Task UpdateAddressAsync_ValidData_ReturnsUpdatedAddress()
        {
            // Arrange
            var address = TestDataHelpers.CreateTestAddress();
            var updateDto = TestDataHelpers.CreateTestUpdateAddressDto();

            _mockAddressRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(address);
            _mockAddressRepository.Setup(x => x.Update(It.IsAny<Address>()));
            _mockAddressRepository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _addressService.UpdateAddressAsync(1, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Street.Should().Be("789 Updated Street");
            result.IsDefault.Should().BeTrue();

            _mockAddressRepository.Verify(x => x.Update(It.IsAny<Address>()), Times.Once);
            _mockAddressRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateAddressAsync_InvalidId_ThrowsArgumentException()
        {
            // Arrange
            var updateDto = TestDataHelpers.CreateTestUpdateAddressDto();
            _mockAddressRepository.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Address?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _addressService.UpdateAddressAsync(999, updateDto));
        }

        [Fact]
        public async Task UpdateAddressAsync_NullDto_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _addressService.UpdateAddressAsync(1, null!));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task UpdateAddressAsync_ZeroOrNegativeId_ThrowsArgumentException(int addressId)
        {
            // Arrange
            var updateDto = TestDataHelpers.CreateTestUpdateAddressDto();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _addressService.UpdateAddressAsync(addressId, updateDto));
        }

        [Fact]
        public async Task DeleteAddressAsync_ValidId_ReturnsTrue()
        {
            // Arrange
            _mockAddressRepository.Setup(x => x.SoftDeleteAsync(1, 1)).ReturnsAsync(true);

            // Act
            var result = await _addressService.DeleteAddressAsync(1);

            // Assert
            result.Should().BeTrue();
            _mockAddressRepository.Verify(x => x.SoftDeleteAsync(1, 1), Times.Once);
        }

        [Fact]
        public async Task DeleteAddressAsync_InvalidId_ReturnsFalse()
        {
            // Arrange
            _mockAddressRepository.Setup(x => x.SoftDeleteAsync(999, 1)).ReturnsAsync(false);

            // Act
            var result = await _addressService.DeleteAddressAsync(999);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task DeleteAddressAsync_ZeroOrNegativeId_ThrowsArgumentException(int addressId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _addressService.DeleteAddressAsync(addressId));
        }

        [Fact]
        public async Task SetDefaultAddressAsync_ValidData_ReturnsTrue()
        {
            // Arrange
            var user = TestDataHelpers.CreateTestUser();
            var address1 = TestDataHelpers.CreateTestAddress(1);
            var address2 = TestDataHelpers.CreateTestAddress(2);
            address1.IsDefault = true;

            _context.Users.Add(user);
            _context.Addresses.AddRange(address1, address2);
            _context.Entry(address1).Property("UserId").CurrentValue = user.Id;
            _context.Entry(address2).Property("UserId").CurrentValue = user.Id;
            await _context.SaveChangesAsync();

            _mockAddressRepository.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(address2);

            // Act
            var result = await _addressService.SetDefaultAddressAsync(2, "user", user.Id);

            // Assert
            result.Should().BeTrue();

            // Verify the default status was updated
            var updatedAddress1 = await _context.Addresses.FindAsync(1);
            var updatedAddress2 = await _context.Addresses.FindAsync(2);

            updatedAddress1?.IsDefault.Should().BeFalse();
            updatedAddress2?.IsDefault.Should().BeTrue();
        }

        [Fact]
        public async Task SetDefaultAddressAsync_InvalidEntityType_ThrowsArgumentException()
        {
            // Arrange
            var address = TestDataHelpers.CreateTestAddress();
            _mockAddressRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(address);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _addressService.SetDefaultAddressAsync(1, "invalid", 1));
        }

        [Fact]
        public async Task SetDefaultAddressAsync_AddressNotBelongingToEntity_ThrowsArgumentException()
        {
            // Arrange
            var user1 = TestDataHelpers.CreateTestUser(1);
            var user2 = TestDataHelpers.CreateTestUser(2, "user2@example.com");
            var address = TestDataHelpers.CreateTestAddress();

            _context.Users.AddRange(user1, user2);
            _context.Addresses.Add(address);
            _context.Entry(address).Property("UserId").CurrentValue = user1.Id;
            await _context.SaveChangesAsync();

            _mockAddressRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(address);

            // Act & Assert - Try to set default for user2 but address belongs to user1
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _addressService.SetDefaultAddressAsync(1, "user", user2.Id));
        }

        [Fact]
        public async Task SetDefaultAddressAsync_NonExistentAddress_ReturnsFalse()
        {
            // Arrange
            _mockAddressRepository.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Address?)null);

            // Act
            var result = await _addressService.SetDefaultAddressAsync(999, "user", 1);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task SetDefaultAddressAsync_ZeroOrNegativeAddressId_ThrowsArgumentException(int addressId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _addressService.SetDefaultAddressAsync(addressId, "user", 1));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task SetDefaultAddressAsync_NullOrEmptyEntityType_ThrowsArgumentException(string? entityType)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _addressService.SetDefaultAddressAsync(1, entityType!, 1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task SetDefaultAddressAsync_ZeroOrNegativeEntityId_ThrowsArgumentException(int entityId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _addressService.SetDefaultAddressAsync(1, "user", entityId));
        }

        [Fact]
        public async Task CreateAddressAsync_SetsAuditFields()
        {
            // Arrange
            var currentUserId = 42;

            // Create a context that will properly handle audit fields
            using var contextWithUser = TestDataHelpers.CreateInMemoryDbContextWithUser(currentUserId);

            var user = TestDataHelpers.CreateTestUser();
            contextWithUser.Users.Add(user);
            await contextWithUser.SaveChangesAsync();

            var createDto = TestDataHelpers.CreateTestCreateAddressDto();
            _mockUserSessionService.Setup(x => x.GetCurrentUserId()).Returns(currentUserId);

            // Create service with the context that has proper user context
            var addressService = new AddressService(
                _mockAddressRepository.Object,
                contextWithUser,
                _mockUserSessionService.Object,
                _mapper,
                _mockLogger.Object);

            // Act
            await addressService.CreateAddressAsync(createDto, "user", user.Id);

            // Assert
            var savedAddress = await contextWithUser.Addresses.FirstOrDefaultAsync();
            savedAddress.Should().NotBeNull();
            savedAddress!.CreatedByUserId.Should().Be(currentUserId);
            savedAddress.UpdatedByUserId.Should().Be(currentUserId);
            savedAddress.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
            savedAddress.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}