using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRepository<Address> _addressRepository;
        private readonly IRepository<ContactDetails> _contactDetailsRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<UserService> _logger;
        private readonly IUserSessionService _userSessionService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly ApplicationDbContext _context;

        public UserService(
            IUserRepository userRepository,
            IRepository<Address> addressRepository,
            IRepository<ContactDetails> contactDetailsRepository,
            IUserSessionService userSessionService,
            ICacheInvalidationService cacheInvalidationService,
            IMapper mapper,
            ILogger<UserService> logger,
            ApplicationDbContext context)
        {
            _userRepository = userRepository;
            _addressRepository = addressRepository;
            _contactDetailsRepository = contactDetailsRepository;
            _userSessionService = userSessionService;
            _cacheInvalidationService = cacheInvalidationService;
            _mapper = mapper;
            _logger = logger;
            _context = context;
        }

        public async Task<UserDto> GetUserByIdAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user == null ? throw new ArgumentException("User not found") : _mapper.Map<UserDto>(user);
        }

        public async Task<UserDto> GetUserByEmailAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            return user == null ? throw new ArgumentException("User not found") : _mapper.Map<UserDto>(user);
        }

        public async Task<UserDto> GetUserByUsernameAsync(string username)
        {
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null)
                throw new ArgumentException("User not found");

            return _mapper.Map<UserDto>(user);
        }

        public async Task<(List<UserListDto> users, int totalCount)> GetUsersAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var totalCount = string.IsNullOrEmpty(search)
                ? await _userRepository.CountAsync()
                : await _userRepository.CountSearchAsync(search);

            var users = string.IsNullOrEmpty(search)
                ? await _userRepository.GetPagedWithRelatedAsync(page, pageSize)
                : await _userRepository.SearchUsersAsync(search, page, pageSize);

            var userDtos = _mapper.Map<List<UserListDto>>(users);

            return (userDtos, totalCount);
        }

        public async Task<UserDto> CreateUserAsync(CreateUserDto createUserDto)
        {
            // Validation
            if (await _userRepository.EmailExistsAsync(createUserDto.Email))
                throw new ArgumentException("Email already exists");

            if (await _userRepository.UsernameExistsAsync(createUserDto.Username))
                throw new ArgumentException("Username already exists");

            // Validate avatar file if provided
            if (createUserDto.AvatarFileId.HasValue)
            {
                await ValidateAvatarFileAsync(createUserDto.AvatarFileId.Value);
            }

            var currentUserId = await GetCurrentUserIdSafeAsync();
            var currentUserRole = _userSessionService.GetCurrentUserRole();

            // Role validation
            if (!_userSessionService.CanCreateUserWithRole(createUserDto.Role))
                throw new UnauthorizedAccessException("You don't have permission to create users with this role");

            var user = _mapper.Map<User>(createUserDto);
            user.CreatedByUserId = currentUserId;
            user.UpdatedByUserId = currentUserId;
            user.CreatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();

            // Handle addresses
            if (createUserDto.Addresses?.Any() == true)
            {
                foreach (var addressDto in createUserDto.Addresses)
                {
                    var address = _mapper.Map<Address>(addressDto);
                    address.CreatedByUserId = currentUserId;
                    address.UpdatedByUserId = currentUserId;
                    address.CreatedAt = DateTime.UtcNow;
                    address.UpdatedAt = DateTime.UtcNow;

                    _context.Addresses.Add(address);
                    _context.Entry(address).Property("UserId").CurrentValue = user.Id;
                }
            }

            // Handle contact details
            if (createUserDto.ContactDetails?.Any() == true)
            {
                foreach (var contactDto in createUserDto.ContactDetails)
                {
                    var contact = _mapper.Map<ContactDetails>(contactDto);
                    contact.CreatedByUserId = currentUserId;
                    contact.UpdatedByUserId = currentUserId;
                    contact.CreatedAt = DateTime.UtcNow;
                    contact.UpdatedAt = DateTime.UtcNow;

                    _context.ContactDetails.Add(contact);
                    _context.Entry(contact).Property("UserId").CurrentValue = user.Id;
                }
            }

            await _context.SaveChangesAsync();

            // Invalidate user cache
            await _cacheInvalidationService.InvalidateUserCacheAsync(user.Id);

            _logger.LogInformation("User {UserId} created successfully by user {CurrentUserId}", user.Id, currentUserId);

            var createdUser = await _userRepository.GetByIdAsync(user.Id);
            return _mapper.Map<UserDto>(createdUser);
        }

        public async Task<UserDto> UpdateUserAsync(int userId, UpdateUserDto updateUserDto)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found");

            var currentUserId = await GetCurrentUserIdSafeAsync();

            // Validation
            if (await _userRepository.EmailExistsAsync(updateUserDto.Email, userId))
                throw new ArgumentException("Email already exists");

            if (await _userRepository.UsernameExistsAsync(updateUserDto.Username, userId))
                throw new ArgumentException("Username already exists");

            // Validate avatar file if provided
            if (updateUserDto.AvatarFileId.HasValue)
            {
                await ValidateAvatarFileAsync(updateUserDto.AvatarFileId.Value);
            }

            // Role validation
            if (!_userSessionService.CanCreateUserWithRole(updateUserDto.Role))
                throw new UnauthorizedAccessException("You don't have permission to assign this role");

            // Update user properties
            _mapper.Map(updateUserDto, user);
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = currentUserId;

            _userRepository.Update(user);

            // Handle addresses
            if (updateUserDto.Addresses?.Any() == true)
            {
                // Get existing addresses
                var existingAddresses = await _context.Addresses
                    .Where(a => EF.Property<int?>(a, "UserId") == userId && !a.IsDeleted)
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
                foreach (var addressDto in updateUserDto.Addresses)
                {
                    var address = _mapper.Map<Address>(addressDto);
                    address.CreatedByUserId = currentUserId;
                    address.UpdatedByUserId = currentUserId;
                    address.CreatedAt = DateTime.UtcNow;
                    address.UpdatedAt = DateTime.UtcNow;

                    _context.Addresses.Add(address);
                    _context.Entry(address).Property("UserId").CurrentValue = userId;
                }
            }

            // Handle contact details
            if (updateUserDto.ContactDetails?.Any() == true)
            {
                // Get existing contact details
                var existingContacts = await _context.ContactDetails
                    .Where(c => EF.Property<int?>(c, "UserId") == userId && !c.IsDeleted)
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
                foreach (var contactDto in updateUserDto.ContactDetails)
                {
                    var contact = _mapper.Map<ContactDetails>(contactDto);
                    contact.CreatedByUserId = currentUserId;
                    contact.UpdatedByUserId = currentUserId;
                    contact.CreatedAt = DateTime.UtcNow;
                    contact.UpdatedAt = DateTime.UtcNow;

                    _context.ContactDetails.Add(contact);
                    _context.Entry(contact).Property("UserId").CurrentValue = userId;
                }
            }

            await _context.SaveChangesAsync();

            // Invalidate user cache
            await _cacheInvalidationService.InvalidateUserCacheAsync(userId);

            // If the current user updated their own profile, update their session
            var sessionUserId = _userSessionService.GetCurrentUserId();
            if (sessionUserId.HasValue && sessionUserId.Value == userId)
            {
                try
                {
                    await _userSessionService.UpdateSessionAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update session after user profile update for user {UserId}", userId);
                }
            }

            _logger.LogInformation("User {UserId} updated successfully by user {CurrentUserId}", userId, currentUserId);

            var updatedUser = await _userRepository.GetByIdAsync(userId);
            return _mapper.Map<UserDto>(updatedUser);
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var currentUserId = await GetCurrentUserIdSafeAsync();
            var result = await _userRepository.SoftDeleteAsync(userId, currentUserId);

            if (result)
            {
                // Invalidate user cache
                await _cacheInvalidationService.InvalidateUserCacheAsync(userId);
                _logger.LogInformation("User {UserId} deleted successfully by user {CurrentUserId}", userId, currentUserId);
            }

            return result;
        }

        public async Task<bool> ActivateUserAsync(int userId)
        {
            var currentUserId = await GetCurrentUserIdSafeAsync();
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return false;

            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = currentUserId;
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            // Invalidate user cache
            await _cacheInvalidationService.InvalidateUserCacheAsync(userId);

            _logger.LogInformation("User {UserId} activated successfully by user {CurrentUserId}", userId, currentUserId);

            return true;
        }

        public async Task<bool> DeactivateUserAsync(int userId)
        {
            var currentUserId = await GetCurrentUserIdSafeAsync();
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return false;

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = currentUserId;
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            // Invalidate user cache
            await _cacheInvalidationService.InvalidateUserCacheAsync(userId);

            _logger.LogInformation("User {UserId} deactivated successfully by user {CurrentUserId}", userId, currentUserId);

            return true;
        }

        public async Task<bool> LockUserAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            var currentUserId = await GetCurrentUserIdSafeAsync();
            if (user == null)
                return false;

            user.IsLocked = true;
            user.LockoutEnd = DateTime.UtcNow.AddDays(30);
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = currentUserId;
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            // Invalidate user cache
            await _cacheInvalidationService.InvalidateUserCacheAsync(userId);

            _logger.LogInformation("User {UserId} locked successfully by user {CurrentUserId}", userId, currentUserId);

            return true;
        }

        public async Task<bool> UnlockUserAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            var currentUserId = await GetCurrentUserIdSafeAsync();
            if (user == null)
                return false;

            user.IsLocked = false;
            user.LockoutEnd = null;
            user.FailedLoginAttempts = 0;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = currentUserId;
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            // Invalidate user cache
            await _cacheInvalidationService.InvalidateUserCacheAsync(userId);

            _logger.LogInformation("User {UserId} unlocked successfully by user {CurrentUserId}", userId, currentUserId);

            return true;
        }

        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            var currentUserId = await GetCurrentUserIdSafeAsync();
            if (user == null)
                return false;

            if (!BCrypt.Net.BCrypt.Verify(changePasswordDto.CurrentPassword, user.PasswordHash))
                return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);
            user.PasswordChangedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = currentUserId;
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            // Invalidate user cache
            await _cacheInvalidationService.InvalidateUserCacheAsync(userId);

            _logger.LogInformation("Password changed successfully for user {UserId}", userId);

            return true;
        }

        public async Task<bool> ResetPasswordAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
                return true; // Don't reveal if email exists

            // Generate reset token and send email
            _logger.LogInformation("Password reset requested for email {Email}", email);
            return true;
        }

        public async Task<bool> ValidateUserCredentialsAsync(string email, string password)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
                return false;

            return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }

        public async Task<UserDto> UpdateUserPreferencesAsync(int userId, Dictionary<string, object> preferences)
        {
            var user = await _userRepository.GetByIdAsync(userId) ?? throw new ArgumentException("User not found");
            var currentUserId = await GetCurrentUserIdSafeAsync();

            user.Preferences = preferences;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = currentUserId;
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            // Invalidate user cache
            await _cacheInvalidationService.InvalidateUserCacheAsync(userId);

            // If the current user updated their own preferences, update their session
            var sessionUserId = _userSessionService.GetCurrentUserId();
            if (sessionUserId.HasValue && sessionUserId.Value == userId)
            {
                try
                {
                    await _userSessionService.UpdateSessionAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update session after user preferences update for user {UserId}", userId);
                }
            }

            _logger.LogInformation("User preferences updated for user {UserId}", userId);

            return _mapper.Map<UserDto>(user);
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            var user = await _userRepository.GetByEmailVerificationTokenAsync(token);
            var currentUserId = await GetCurrentUserIdSafeAsync();
            if (user == null)
                return false;

            user.EmailVerifiedAt = DateTime.UtcNow;
            user.EmailVerificationToken = null;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = currentUserId;
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            // Invalidate user cache
            await _cacheInvalidationService.InvalidateUserCacheAsync(user.Id);

            _logger.LogInformation("Email verified successfully for user {UserId}", user.Id);

            return true;
        }

        public async Task<bool> SendEmailVerificationAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return false;

            // Generate verification token and send email
            _logger.LogInformation("Email verification sent for user {UserId}", userId);
            return true;
        }

        public async Task<UserDto> UpdateUserAvatarAsync(int userId, int? avatarFileId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found");

            // Validate avatar file if provided
            if (avatarFileId.HasValue)
            {
                await ValidateAvatarFileAsync(avatarFileId.Value);
            }

            var currentUserId = await GetCurrentUserIdSafeAsync();

            user.AvatarFileId = avatarFileId;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = currentUserId;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            // Invalidate user cache
            await _cacheInvalidationService.InvalidateUserCacheAsync(userId);

            _logger.LogInformation("Avatar updated for user {UserId}", userId);

            return _mapper.Map<UserDto>(user);
        }

        public async Task<UserDto> RemoveUserAvatarAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found");

            var currentUserId = await GetCurrentUserIdSafeAsync();

            user.AvatarFileId = null;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedByUserId = currentUserId;

            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            // Invalidate user cache
            await _cacheInvalidationService.InvalidateUserCacheAsync(userId);

            _logger.LogInformation("Avatar removed for user {UserId}", userId);

            return _mapper.Map<UserDto>(user);
        }

        #region Private Helper Methods

        /// <summary>
        /// Safely gets the current user ID with fallback to system user if session is not available
        /// </summary>
        private async Task<int?> GetCurrentUserIdSafeAsync()
        {
            try
            {
                // First try to get from session service
                var userId = _userSessionService.GetCurrentUserId();
                if (userId.HasValue)
                {
                    return userId;
                }

                // If session service doesn't have the user ID, try to get the session asynchronously
                var session = await _userSessionService.GetCurrentSessionAsync();
                if (session?.CurrentUser != null)
                {
                    return session.CurrentUser.Id;
                }

                // Fallback to a system user ID or null
                _logger.LogWarning("Could not determine current user ID from session, using system fallback");
                return null; // Or return a system user ID if you have one
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user ID safely, using system fallback");
                return null; // Or return a system user ID if you have one
            }
        }

        /// <summary>
        /// Validates that the provided file ID is a valid image file for avatar use
        /// </summary>
        private async Task ValidateAvatarFileAsync(int fileId)
        {
            var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);
            if (file == null)
                throw new ArgumentException($"File with ID {fileId} not found");

            if (file.FileType != Domain.Enums.FileType.Image)
                throw new ArgumentException($"File with ID {fileId} is not an image");

            // Additional validation for avatar images (optional)
            if (file.FileSize > 5 * 1024 * 1024) // 5MB limit
                throw new ArgumentException("Avatar image must be smaller than 5MB");
        }

        #endregion
    }
}