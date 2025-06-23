using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Backend.CMS.Infrastructure.Data;

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

        public static ApplicationDbContext CreateInMemoryDbContext(string databaseName = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        public static void SeedTestData(ApplicationDbContext context)
        {
            var user = CreateTestUser();
            var company = CreateTestCompany();
            var location = CreateTestLocation();
            var address = CreateTestAddress();
            var contactDetails = CreateTestContactDetails();

            context.Users.Add(user);
            context.Companies.Add(company);
            context.Locations.Add(location);
            context.Addresses.Add(address);
            context.ContactDetails.Add(contactDetails);

            // Set up polymorphic relationships using shadow properties
            context.Entry(address).Property("UserId").CurrentValue = user.Id;
            context.Entry(contactDetails).Property("UserId").CurrentValue = user.Id;

            context.SaveChanges();
        }

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
    }
}