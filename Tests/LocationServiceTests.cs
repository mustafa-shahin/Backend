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
    /// Tests for LocationService.
    /// Note: In-memory database is used for testing. Transaction rollback scenarios 
    /// are tested through error handling validation rather than actual rollbacks.
    /// </summary>
    public class LocationServiceTests : IDisposable
    {
        private readonly Mock<ILocationRepository> _mockLocationRepository;
        private readonly Mock<IRepository<Company>> _mockCompanyRepository;
        private readonly Mock<IRepository<LocationOpeningHour>> _mockOpeningHourRepository;
        private readonly Mock<IUserSessionService> _mockUserSessionService;
        private readonly Mock<ILogger<LocationService>> _mockLogger;
        private readonly IMapper _mapper;
        private readonly ApplicationDbContext _context;
        private readonly LocationService _locationService;

        public LocationServiceTests()
        {
            _mockLocationRepository = new Mock<ILocationRepository>();
            _mockCompanyRepository = new Mock<IRepository<Company>>();
            _mockOpeningHourRepository = new Mock<IRepository<LocationOpeningHour>>();
            _mockUserSessionService = new Mock<IUserSessionService>();
            _mockLogger = new Mock<ILogger<LocationService>>();

            // Setup AutoMapper
            var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
            _mapper = config.CreateMapper();

            // Setup in-memory database
            _context = TestDataHelpers.CreateInMemoryDbContext();

            _locationService = new LocationService(
                _mockLocationRepository.Object,
                _mockCompanyRepository.Object,
                _mockOpeningHourRepository.Object,
                _context,
                _mockUserSessionService.Object,
                _mapper,
                _mockLogger.Object);

            // Setup default user session
            _mockUserSessionService.Setup(x => x.GetCurrentUserId()).Returns(1);
        }

        [Fact]
        public async Task GetLocationByIdAsync_ValidId_ReturnsLocationDto()
        {
            // Arrange
            var location = TestDataHelpers.CreateTestLocation();
            _mockLocationRepository.Setup(x => x.GetWithAddressesAndContactsAsync(1)).ReturnsAsync(location);

            // Act
            var result = await _locationService.GetLocationByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(1);
            result.Name.Should().Be("Test Location");
            result.LocationType.Should().Be("Branch");
        }

        [Fact]
        public async Task GetLocationByIdAsync_InvalidId_ThrowsArgumentException()
        {
            // Arrange
            _mockLocationRepository.Setup(x => x.GetWithAddressesAndContactsAsync(999)).ReturnsAsync((Location?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.GetLocationByIdAsync(999));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetLocationByIdAsync_ZeroOrNegativeId_ThrowsArgumentException(int locationId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.GetLocationByIdAsync(locationId));
        }

        [Fact]
        public async Task GetLocationsAsync_ValidParameters_ReturnsLocationsList()
        {
            // Arrange
            var locations = new List<Location> { TestDataHelpers.CreateTestLocation() };
            _mockLocationRepository.Setup(x => x.GetPagedWithRelatedAsync(1, 10)).ReturnsAsync(locations);

            // Act
            var result = await _locationService.GetLocationsAsync(1, 10);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("Test Location");
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(-1, 10)]
        public async Task GetLocationsAsync_ZeroOrNegativePage_ThrowsArgumentException(int page, int pageSize)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.GetLocationsAsync(page, pageSize));
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(1, -1)]
        [InlineData(1, 101)]
        public async Task GetLocationsAsync_InvalidPageSize_ThrowsArgumentException(int page, int pageSize)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.GetLocationsAsync(page, pageSize));
        }

        [Fact]
        public async Task GetLocationsByCompanyAsync_ValidCompanyId_ReturnsLocationsList()
        {
            // Arrange
            var locations = new List<Location> { TestDataHelpers.CreateTestLocation() };
            _mockLocationRepository.Setup(x => x.GetLocationsByCompanyAsync(1)).ReturnsAsync(locations);

            // Act
            var result = await _locationService.GetLocationsByCompanyAsync(1);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("Test Location");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetLocationsByCompanyAsync_ZeroOrNegativeCompanyId_ThrowsArgumentException(int companyId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.GetLocationsByCompanyAsync(companyId));
        }

        [Fact]
        public async Task CreateLocationAsync_ValidData_ReturnsCreatedLocation()
        {
            // Arrange
            var company = TestDataHelpers.CreateTestCompany();
            var createDto = new CreateLocationDto
            {
                Name = "New Location",
                Description = "New Description",
                LocationType = "Branch",
                IsActive = true
            };

            _mockCompanyRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Company> { company });
            _mockLocationRepository.Setup(x => x.LocationCodeExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
            _mockLocationRepository.Setup(x => x.AddAsync(It.IsAny<Location>())).Returns(Task.CompletedTask);
            _mockLocationRepository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            _mockLocationRepository.Setup(x => x.GetWithAddressesAndContactsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Location { Id = 1, Name = "New Location", CompanyId = company.Id });

            // Act
            var result = await _locationService.CreateLocationAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("New Location");
            _mockLocationRepository.Verify(x => x.AddAsync(It.IsAny<Location>()), Times.Once);
        }

        [Fact]
        public async Task CreateLocationAsync_NullDto_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _locationService.CreateLocationAsync(null!));
        }

        [Fact]
        public async Task CreateLocationAsync_NoCompanyExists_ThrowsArgumentException()
        {
            // Arrange
            var createDto = new CreateLocationDto { Name = "Test Location" };
            _mockCompanyRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Company>());

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.CreateLocationAsync(createDto));
        }

        [Fact]
        public async Task CreateLocationAsync_DuplicateLocationCode_ThrowsArgumentException()
        {
            // Arrange
            var company = TestDataHelpers.CreateTestCompany();
            var createDto = new CreateLocationDto
            {
                Name = "New Location",
                LocationCode = "LOC001"
            };

            _mockCompanyRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Company> { company });
            _mockLocationRepository.Setup(x => x.LocationCodeExistsAsync("LOC001", null)).ReturnsAsync(true);


            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.CreateLocationAsync(createDto));
        }

        [Fact]
        public async Task CreateLocationAsync_WithAddressesAndContacts_CreatesAllRelatedData()
        {
            // Arrange
            var company = TestDataHelpers.CreateTestCompany();
            var createDto = new CreateLocationDto
            {
                Name = "New Location",
                Addresses = new List<CreateAddressDto>
                {
                    new CreateAddressDto { Street = "123 Test St", HouseNr = "1", City = "Test City", State = "Test State", Country = "Test Country", PostalCode = "12345" }
                },
                ContactDetails = new List<CreateContactDetailsDto>
                {
                    new CreateContactDetailsDto { Email = "test@location.com", PrimaryPhone = "+1234567890" }
                },
                OpeningHours = new List<CreateLocationOpeningHourDto>
                {
                    new CreateLocationOpeningHourDto { DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeOnly(9, 0), CloseTime = new TimeOnly(17, 0) }
                }
            };

            _mockCompanyRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Company> { company });
            _mockLocationRepository.Setup(x => x.LocationCodeExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
            _mockLocationRepository.Setup(x => x.AddAsync(It.IsAny<Location>())).Returns(Task.CompletedTask);
            _mockLocationRepository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            _mockLocationRepository.Setup(x => x.GetWithAddressesAndContactsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Location { Id = 1, Name = "New Location", CompanyId = company.Id });
            _mockOpeningHourRepository.Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<LocationOpeningHour>>())).Returns(Task.CompletedTask);

            // Act
            var result = await _locationService.CreateLocationAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("New Location");

            // Verify addresses and contacts were added
            var savedAddress = await _context.Addresses.FirstOrDefaultAsync();
            var savedContact = await _context.ContactDetails.FirstOrDefaultAsync();

            savedAddress.Should().NotBeNull();
            savedAddress!.Street.Should().Be("123 Test St");
            savedContact.Should().NotBeNull();
            savedContact!.Email.Should().Be("test@location.com");

            _mockOpeningHourRepository.Verify(x => x.AddRangeAsync(It.IsAny<IEnumerable<LocationOpeningHour>>()), Times.Once);
        }

        [Fact]
        public async Task UpdateLocationAsync_ValidData_ReturnsUpdatedLocation()
        {
            // Arrange
            var location = TestDataHelpers.CreateTestLocation();
            var updateDto = new UpdateLocationDto
            {
                Name = "Updated Location",
                Description = "Updated Description",
                LocationType = "Headquarters",
                IsActive = true
            };

            _mockLocationRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(location);
            _mockLocationRepository.Setup(x => x.LocationCodeExistsAsync(It.IsAny<string>(), 1)).ReturnsAsync(false);
            _mockLocationRepository.Setup(x => x.GetWithAddressesAndContactsAsync(1)).ReturnsAsync(location);

            // Act
            var result = await _locationService.UpdateLocationAsync(1, updateDto);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Updated Location"); // Returned from mock
            _mockLocationRepository.Verify(x => x.Update(It.IsAny<Location>()), Times.Once);
        }

        [Fact]
        public async Task UpdateLocationAsync_InvalidId_ThrowsArgumentException()
        {
            // Arrange
            var updateDto = new UpdateLocationDto { Name = "Updated Location" };
            _mockLocationRepository.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Location?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.UpdateLocationAsync(999, updateDto));
        }

        [Fact]
        public async Task UpdateLocationAsync_DuplicateLocationCode_ThrowsArgumentException()
        {
            // Arrange
            var location = TestDataHelpers.CreateTestLocation();
            var updateDto = new UpdateLocationDto
            {
                Name = "Updated Location",
                LocationCode = "LOC001"
            };

            _mockLocationRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(location);
            _mockLocationRepository.Setup(x => x.LocationCodeExistsAsync("LOC001", 1)).ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.UpdateLocationAsync(1, updateDto));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task UpdateLocationAsync_ZeroOrNegativeId_ThrowsArgumentException(int locationId)
        {
            // Arrange
            var updateDto = new UpdateLocationDto { Name = "Updated Location" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.UpdateLocationAsync(locationId, updateDto));
        }

        [Fact]
        public async Task UpdateLocationAsync_NullDto_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _locationService.UpdateLocationAsync(1, null!));
        }

        [Fact]
        public async Task DeleteLocationAsync_ValidId_ReturnsTrue()
        {
            // Arrange
            _mockLocationRepository.Setup(x => x.SoftDeleteAsync(1, 1)).ReturnsAsync(true);

            // Act
            var result = await _locationService.DeleteLocationAsync(1);

            // Assert
            result.Should().BeTrue();
            _mockLocationRepository.Verify(x => x.SoftDeleteAsync(1, 1), Times.Once);
        }

        [Fact]
        public async Task DeleteLocationAsync_InvalidId_ReturnsFalse()
        {
            // Arrange
            _mockLocationRepository.Setup(x => x.SoftDeleteAsync(999, 1)).ReturnsAsync(false);

            // Act
            var result = await _locationService.DeleteLocationAsync(999);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task DeleteLocationAsync_ZeroOrNegativeId_ThrowsArgumentException(int locationId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.DeleteLocationAsync(locationId));
        }

        [Fact]
        public async Task SetMainLocationAsync_ValidId_ReturnsTrue()
        {
            // Arrange
            var company = TestDataHelpers.CreateTestCompany();
            var location1 = TestDataHelpers.CreateTestLocation(1, company.Id);
            var location2 = TestDataHelpers.CreateTestLocation(2, company.Id);
            location1.IsMainLocation = true;

            _context.Companies.Add(company);
            _context.Locations.AddRange(location1, location2);
            await _context.SaveChangesAsync();

            _mockLocationRepository.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(location2);
            _mockLocationRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Location, bool>>>()))
                .ReturnsAsync(new List<Location> { location1, location2 });

            // Act
            var result = await _locationService.SetMainLocationAsync(2);

            // Assert
            result.Should().BeTrue();

            // Verify main location status was updated
            var updatedLocation1 = await _context.Locations.FindAsync(1);
            var updatedLocation2 = await _context.Locations.FindAsync(2);

            updatedLocation1?.IsMainLocation.Should().BeFalse();
            updatedLocation2?.IsMainLocation.Should().BeTrue();
        }

        [Fact]
        public async Task SetMainLocationAsync_InvalidId_ReturnsFalse()
        {
            // Arrange
            _mockLocationRepository.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Location?)null);

            // Act
            var result = await _locationService.SetMainLocationAsync(999);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task SetMainLocationAsync_ZeroOrNegativeId_ThrowsArgumentException(int locationId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.SetMainLocationAsync(locationId));
        }

        [Fact]
        public async Task GetMainLocationAsync_MainLocationExists_ReturnsLocationDto()
        {
            // Arrange
            var mainLocation = TestDataHelpers.CreateTestLocation();
            mainLocation.IsMainLocation = true;
            _mockLocationRepository.Setup(x => x.GetMainLocationAsync()).ReturnsAsync(mainLocation);

            // Act
            var result = await _locationService.GetMainLocationAsync();

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Test Location");
            result.IsMainLocation.Should().BeTrue();
        }

        [Fact]
        public async Task GetMainLocationAsync_NoMainLocation_ThrowsArgumentException()
        {
            // Arrange
            _mockLocationRepository.Setup(x => x.GetMainLocationAsync()).ReturnsAsync((Location?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.GetMainLocationAsync());
        }

        [Fact]
        public async Task LocationCodeExistsAsync_ValidCode_ReturnsExpectedResult()
        {
            // Fix for CS0854: Replace the optional argument usage with explicit argument passing.
            _mockLocationRepository.Setup(x => x.LocationCodeExistsAsync("LOC001", null)).ReturnsAsync(true);
            _mockLocationRepository.Setup(x => x.LocationCodeExistsAsync("LOC002", null)).ReturnsAsync(false);

            // Act
            var result1 = await _locationService.LocationCodeExistsAsync("LOC001");
            var result2 = await _locationService.LocationCodeExistsAsync("LOC002");

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeFalse();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task LocationCodeExistsAsync_NullOrEmptyCode_ThrowsArgumentException(string locationCode)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.LocationCodeExistsAsync(locationCode));
        }

        [Fact]
        public async Task SearchLocationsAsync_ValidParameters_ReturnsLocationsList()
        {
            // Arrange
            var locations = new List<Location> { TestDataHelpers.CreateTestLocation() };
            _mockLocationRepository.Setup(x => x.SearchLocationsAsync("test", 1, 10)).ReturnsAsync(locations);

            // Act
            var result = await _locationService.SearchLocationsAsync("test", 1, 10);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("Test Location");
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task SearchLocationsAsync_NullOrEmptySearchTerm_ThrowsArgumentException(string searchTerm)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.SearchLocationsAsync(searchTerm, 1, 10));
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(-1, 10)]
        public async Task SearchLocationsAsync_ZeroOrNegativePage_ThrowsArgumentException(int page, int pageSize)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.SearchLocationsAsync("test", page, pageSize));
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(1, -1)]
        [InlineData(1, 101)]
        public async Task SearchLocationsAsync_InvalidPageSize_ThrowsArgumentException(int page, int pageSize)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _locationService.SearchLocationsAsync("test", page, pageSize));
        }

        [Fact]
        public async Task CreateLocationAsync_SetsAuditFields()
        {
            // Arrange
            var currentUserId = 42;

            // Create context with user for audit fields
            using var contextWithUser = TestDataHelpers.CreateInMemoryDbContextWithUser(currentUserId);

            var company = TestDataHelpers.CreateTestCompany();
            var createDto = new CreateLocationDto { Name = "New Location" };

            _mockUserSessionService.Setup(x => x.GetCurrentUserId()).Returns(currentUserId);
            _mockCompanyRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Company> { company });
            _mockLocationRepository.Setup(x => x.LocationCodeExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
            _mockLocationRepository.Setup(x => x.AddAsync(It.IsAny<Location>())).Returns(Task.CompletedTask);
            _mockLocationRepository.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            _mockLocationRepository.Setup(x => x.GetWithAddressesAndContactsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Location { Id = 1, Name = "New Location", CompanyId = company.Id });

            // Create service with context that has proper user context
            var locationService = new LocationService(
                _mockLocationRepository.Object,
                _mockCompanyRepository.Object,
                _mockOpeningHourRepository.Object,
                contextWithUser,
                _mockUserSessionService.Object,
                _mapper,
                _mockLogger.Object);

            // Act
            await locationService.CreateLocationAsync(createDto);

            // Assert
            _mockLocationRepository.Verify(x => x.AddAsync(It.Is<Location>(l =>
                l.CreatedByUserId == currentUserId &&
                l.UpdatedByUserId == currentUserId &&
                l.CreatedAt > DateTime.UtcNow.AddMinutes(-1) &&
                l.UpdatedAt > DateTime.UtcNow.AddMinutes(-1))), Times.Once);
        }

        [Fact]
        public async Task UpdateLocationAsync_SetsAuditFields()
        {
            // Arrange
            var currentUserId = 42;

            // Create context with user for audit fields
            using var contextWithUser = TestDataHelpers.CreateInMemoryDbContextWithUser(currentUserId);

            var location = TestDataHelpers.CreateTestLocation();
            var updateDto = new UpdateLocationDto { Name = "Updated Location" };

            _mockUserSessionService.Setup(x => x.GetCurrentUserId()).Returns(currentUserId);
            _mockLocationRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(location);
            _mockLocationRepository.Setup(x => x.LocationCodeExistsAsync(It.IsAny<string>(), 1)).ReturnsAsync(false);
            _mockLocationRepository.Setup(x => x.GetWithAddressesAndContactsAsync(1)).ReturnsAsync(location);

            // Create service with context that has proper user context
            var locationService = new LocationService(
                _mockLocationRepository.Object,
                _mockCompanyRepository.Object,
                _mockOpeningHourRepository.Object,
                contextWithUser,
                _mockUserSessionService.Object,
                _mapper,
                _mockLogger.Object);

            // Act
            await locationService.UpdateLocationAsync(1, updateDto);

            // Assert
            _mockLocationRepository.Verify(x => x.Update(It.Is<Location>(l =>
                l.UpdatedByUserId == currentUserId &&
                l.UpdatedAt > DateTime.UtcNow.AddMinutes(-1))), Times.Once);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
    //_mockLocationRepository.Setup(x => x.LocationCodeExistsAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
}