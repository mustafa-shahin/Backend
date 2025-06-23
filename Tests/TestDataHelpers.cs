using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Backend.CMS.Infrastructure.Data;
using Xunit;
using Moq;
using System.Security.Claims;
using System.Security.Principal;

namespace Tests
{
    public static class TestDataHelpers
    {
        public static User CreateTestUser(int id = 1, string email = "test@example.com", UserRole role = UserRole.Customer)
        {
            return new User
            {
                Id = id,
                Email = email,
                Username = $"user{id}",
                FirstName = "Test",
                LastName = "User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!"),
                Role = role,
                IsActive = true,
                IsLocked = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                EmailVerifiedAt = DateTime.UtcNow
            };
        }

        public static Company CreateTestCompany(int id = 1)
        {
            return new Company
            {
                Id = id,
                Name = "Test Company",
                Description = "Test Description",
                IsActive = true,
                Currency = "EUR",
                Language = "en",
                Timezone = "UTC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static Address CreateTestAddress(int id = 1)
        {
            return new Address
            {
                Id = id,
                Street = "123 Test Street",
                HouseNr = "A",
                City = "Test City",
                State = "Test State",
                Country = "Test Country",
                PostalCode = "12345",
                IsDefault = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static Location CreateTestLocation(int id = 1, int companyId = 1)
        {
            return new Location
            {
                Id = id,
                CompanyId = companyId,
                Name = "Test Location",
                Description = "Test Description",
                LocationType = "Branch",
                IsMainLocation = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static ContactDetails CreateTestContactDetails(int id = 1)
        {
            return new ContactDetails
            {
                Id = id,
                PrimaryPhone = "+1234567890",
                Email = "contact@test.com",
                Website = "https://test.com",
                IsDefault = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static LocationOpeningHour CreateTestLocationOpeningHour(int id = 1, int locationId = 1, DayOfWeek dayOfWeek = DayOfWeek.Monday)
        {
            return new LocationOpeningHour
            {
                Id = id,
                LocationId = locationId,
                DayOfWeek = dayOfWeek,
                OpenTime = new TimeOnly(9, 0),
                CloseTime = new TimeOnly(17, 0),
                IsClosed = false,
                IsOpen24Hours = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        #region DTO Creation Methods

        public static CreateUserDto CreateTestCreateUserDto()
        {
            return new CreateUserDto
            {
                Email = "newuser@example.com",
                Username = "newuser",
                Password = "TestPassword123!",
                FirstName = "New",
                LastName = "User",
                Role = UserRole.Customer,
                IsActive = true
            };
        }

        public static UpdateUserDto CreateTestUpdateUserDto()
        {
            return new UpdateUserDto
            {
                Email = "updated@example.com",
                Username = "updateduser",
                FirstName = "Updated",
                LastName = "User",
                Role = UserRole.Customer,
                IsActive = true
            };
        }

        public static CreateAddressDto CreateTestCreateAddressDto()
        {
            return new CreateAddressDto
            {
                Street = "456 New Street",
                HouseNr = "B",
                City = "New City",
                State = "New State",
                Country = "New Country",
                PostalCode = "67890",
                IsDefault = false
            };
        }

        public static UpdateAddressDto CreateTestUpdateAddressDto()
        {
            return new UpdateAddressDto
            {
                Street = "789 Updated Street",
                HouseNr = "C",
                City = "Updated City",
                State = "Updated State",
                Country = "Updated Country",
                PostalCode = "54321",
                IsDefault = true
            };
        }

        public static CreateContactDetailsDto CreateTestCreateContactDetailsDto()
        {
            return new CreateContactDetailsDto
            {
                PrimaryPhone = "+9876543210",
                Email = "newcontact@example.com",
                Website = "https://newcontact.com",
                IsDefault = false
            };
        }

        public static UpdateContactDetailsDto CreateTestUpdateContactDetailsDto()
        {
            return new UpdateContactDetailsDto
            {
                PrimaryPhone = "+1111111111",
                Email = "updatedcontact@example.com",
                Website = "https://updatedcontact.com",
                IsDefault = true
            };
        }

        public static CreateLocationDto CreateTestCreateLocationDto()
        {
            return new CreateLocationDto
            {
                Name = "New Location",
                Description = "New Location Description",
                LocationType = "Branch",
                IsActive = true,
                IsMainLocation = false
            };
        }

        public static UpdateLocationDto CreateTestUpdateLocationDto()
        {
            return new UpdateLocationDto
            {
                Name = "Updated Location",
                Description = "Updated Location Description",
                LocationType = "Headquarters",
                IsActive = true,
                IsMainLocation = true
            };
        }

        public static CreateLocationOpeningHourDto CreateTestCreateLocationOpeningHourDto(DayOfWeek dayOfWeek = DayOfWeek.Monday)
        {
            return new CreateLocationOpeningHourDto
            {
                DayOfWeek = dayOfWeek,
                OpenTime = new TimeOnly(8, 0),
                CloseTime = new TimeOnly(18, 0),
                IsClosed = false,
                IsOpen24Hours = false
            };
        }

        public static UpdateLocationOpeningHourDto CreateTestUpdateLocationOpeningHourDto(DayOfWeek dayOfWeek = DayOfWeek.Monday)
        {
            return new UpdateLocationOpeningHourDto
            {
                DayOfWeek = dayOfWeek,
                OpenTime = new TimeOnly(9, 0),
                CloseTime = new TimeOnly(17, 0),
                IsClosed = false,
                IsOpen24Hours = false
            };
        }

        public static UpdateCompanyDto CreateTestUpdateCompanyDto()
        {
            return new UpdateCompanyDto
            {
                Name = "Updated Company",
                Description = "Updated Description",
                Currency = "EUR",
                Language = "de",
                Timezone = "Europe/Berlin"
            };
        }

        #region Test Patterns for In-Memory Database

        /// <summary>
        /// Creates a test scenario where an entity dependency doesn't exist (for testing error cases)
        /// </summary>
        public static void CreateDependencyErrorScenario(ApplicationDbContext context)
        {
            // Create some entities but deliberately leave out dependencies
            // This helps test error handling without relying on transaction rollbacks
            context.SaveChanges();
        }

        /// <summary>
        /// Asserts that audit fields are properly set on an entity
        /// </summary>
        public static void AssertAuditFieldsSet(BaseEntity entity, int? expectedUserId, DateTime? beforeTime = null)
        {
            beforeTime ??= DateTime.UtcNow.AddMinutes(-1);

            if (expectedUserId.HasValue)
            {
                Assert.Equal(expectedUserId, entity.CreatedByUserId);
                Assert.Equal(expectedUserId, entity.UpdatedByUserId);
            }

            Assert.True(entity.CreatedAt >= beforeTime);
            Assert.True(entity.UpdatedAt >= beforeTime);
            Assert.False(entity.IsDeleted);
        }

        /// <summary>
        /// Asserts that update audit fields are properly set on an entity
        /// </summary>
        public static void AssertUpdateAuditFieldsSet(BaseEntity entity, int? expectedUserId, DateTime beforeUpdateTime)
        {
            if (expectedUserId.HasValue)
            {
                Assert.Equal(expectedUserId, entity.UpdatedByUserId);
            }

            Assert.True(entity.UpdatedAt >= beforeUpdateTime);
        }

        /// <summary>
        /// Simulates a test scenario where we verify data consistency without transactions.
        /// Use this for testing error scenarios in services that use transactions.
        /// </summary>
        public static async Task<bool> VerifyDataConsistencyAsync(ApplicationDbContext context, Func<Task> operation)
        {
            try
            {
                var beforeCount = context.ChangeTracker.Entries().Count();
                await operation();
                await context.SaveChangesAsync();
                return true;
            }
            catch
            {
                // In a real database with transactions, this would rollback
                // For in-memory database, we just verify the error was handled
                return false;
            }
        }

        #endregion

        #region Database Setup Methods

        public static ApplicationDbContext CreateInMemoryDbContext(string databaseName = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .EnableSensitiveDataLogging() // Helpful for debugging tests
                .Options;

            return new ApplicationDbContext(options);
        }

        /// <summary>
        /// Creates an in-memory database context with a mocked HttpContextAccessor for testing audit fields
        /// </summary>
        public static ApplicationDbContext CreateInMemoryDbContextWithUser(int? currentUserId, string databaseName = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .EnableSensitiveDataLogging()
                .Options;

            // Create a mock HttpContextAccessor that returns the specified user ID
            var mockHttpContextAccessor = CreateMockHttpContextAccessor(currentUserId);

            return new ApplicationDbContext(options, mockHttpContextAccessor);
        }

        /// <summary>
        /// Creates a separate in-memory database context for testing error scenarios
        /// </summary>
        public static ApplicationDbContext CreateFailureTestDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()) // Always unique for isolation
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new ApplicationDbContext(options);
        }

        /// <summary>
        /// Creates a mock IHttpContextAccessor that returns the specified user ID
        /// </summary>
        private static Microsoft.AspNetCore.Http.IHttpContextAccessor CreateMockHttpContextAccessor(int? userId)
        {
            var mockHttpContextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();

            if (userId.HasValue)
            {
                var mockHttpContext = new Mock<Microsoft.AspNetCore.Http.HttpContext>();
                var mockUser = new Mock<ClaimsPrincipal>();
                var mockIdentity = new Mock<IIdentity>();

                mockIdentity.Setup(x => x.IsAuthenticated).Returns(true);
                mockUser.Setup(x => x.Identity).Returns(mockIdentity.Object);
                mockUser.Setup(x => x.FindFirst("sub")).Returns(new Claim("sub", userId.Value.ToString()));
                mockHttpContext.Setup(x => x.User).Returns(mockUser.Object);
                mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);
            }
            else
            {
                mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((Microsoft.AspNetCore.Http.HttpContext?)null);
            }

            return mockHttpContextAccessor.Object;
        }

        public static void SeedTestData(ApplicationDbContext context)
        {
            var user = CreateTestUser();
            var company = CreateTestCompany();
            var location = CreateTestLocation();
            var address = CreateTestAddress();
            var contactDetails = CreateTestContactDetails();
            var openingHour = CreateTestLocationOpeningHour(1, location.Id);

            context.Users.Add(user);
            context.Companies.Add(company);
            context.Locations.Add(location);
            context.Addresses.Add(address);
            context.ContactDetails.Add(contactDetails);
            context.LocationOpeningHours.Add(openingHour);

            // Set up polymorphic relationships using shadow properties
            context.Entry(address).Property("UserId").CurrentValue = user.Id;
            context.Entry(contactDetails).Property("UserId").CurrentValue = user.Id;

            context.SaveChanges();
        }

        /// <summary>
        /// Seeds test data specifically for error scenario testing
        /// </summary>
        public static void SeedErrorTestData(ApplicationDbContext context)
        {
            // Create minimal test data for error scenarios
            var user = CreateTestUser();
            var company = CreateTestCompany();

            context.Users.Add(user);
            context.Companies.Add(company);
            context.SaveChanges();
        }

        /// <summary>
        /// Verifies that an entity was soft deleted
        /// </summary>
        public static async Task<bool> IsEntitySoftDeletedAsync<T>(ApplicationDbContext context, int entityId) where T : BaseEntity
        {
            var entity = await context.Set<T>().IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == entityId);
            return entity?.IsDeleted == true;
        }

        /// <summary>
        /// Verifies that an entity exists and is not soft deleted
        /// </summary>
        public static async Task<bool> EntityExistsAsync<T>(ApplicationDbContext context, int entityId) where T : BaseEntity
        {
            return await context.Set<T>().AnyAsync(e => e.Id == entityId);
        }

        public static void SeedCompanyData(ApplicationDbContext context)
        {
            var company = CreateTestCompany();
            var location = CreateTestLocation(1, company.Id);
            var address = CreateTestAddress();
            var contactDetails = CreateTestContactDetails();

            context.Companies.Add(company);
            context.Locations.Add(location);
            context.Addresses.Add(address);
            context.ContactDetails.Add(contactDetails);

            // Set up company relationships
            context.Entry(address).Property("CompanyId").CurrentValue = company.Id;
            context.Entry(contactDetails).Property("CompanyId").CurrentValue = company.Id;

            // Set up location relationships
            context.Entry(address).Property("LocationId").CurrentValue = location.Id;
            context.Entry(contactDetails).Property("LocationId").CurrentValue = location.Id;

            context.SaveChanges();
        }

        #endregion

        #region Mock Data Helpers

        public static class MockUserClaims
        {
            public static Dictionary<string, object> CreateClaims(int userId, string email, UserRole role)
            {
                return new Dictionary<string, object>
                {
                    ["sub"] = userId.ToString(),
                    ["email"] = email,
                    ["role"] = role.ToString(),
                    ["firstName"] = "Test",
                    ["lastName"] = "User"
                };
            }
        }

        #endregion
    }
    #endregion
}