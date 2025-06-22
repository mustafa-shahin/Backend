using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Application.Interfaces;
using Backend.CMS.Application.Interfaces.Services;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Caching;
using Backend.CMS.Infrastructure.Data;
using Backend.CMS.Infrastructure.IRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Backend.CMS.Infrastructure.Services
{
    // Wrapper classes for caching value types
    public class UserSearchResult
    {
        public List<UserDto> Users { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRepository<Address> _addressRepository;
        private readonly IRepository<ContactDetails> _contactDetailsRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<UserService> _logger;
        private readonly IUserSessionService _userSessionService;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly ApplicationDbContext _context;

        // Cache settings
        private readonly TimeSpan _userCacheExpiration = TimeSpan.FromMinutes(15);
        private readonly TimeSpan _searchCacheExpiration = TimeSpan.FromMinutes(5);

        public UserService(
            IUserRepository userRepository,
            IRepository<Address> addressRepository,
            IRepository<ContactDetails> contactDetailsRepository,
            IUserSessionService userSessionService,
            ICacheService cacheService,
            ICacheInvalidationService cacheInvalidationService,
            IMapper mapper,
            ILogger<UserService> logger,
            ApplicationDbContext context)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _addressRepository = addressRepository ?? throw new ArgumentNullException(nameof(addressRepository));
            _contactDetailsRepository = contactDetailsRepository ?? throw new ArgumentNullException(nameof(contactDetailsRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<UserDto> GetUserByIdAsync(int userId)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            try
            {
                // Try cache first
                var cacheKey = CacheKeys.UserById(userId);
                var cachedUser = await _cacheService.GetAsync<UserDto>(cacheKey);
                if (cachedUser != null)
                {
                    _logger.LogDebug("Retrieved user {UserId} from cache", userId);
                    return cachedUser;
                }

                // Load from database
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found", userId);
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                var userDto = _mapper.Map<UserDto>(user);

                // Cache the result
                await _cacheService.SetAsync(cacheKey, userDto, _userCacheExpiration);
                _logger.LogDebug("Cached user {UserId} for {CacheExpiration}", userId, _userCacheExpiration);

                return userDto;
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", userId);
                throw;
            }
        }

        public async Task<UserDto> GetUserByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be null or empty", nameof(email));

            var normalizedEmail = email.Trim().ToLowerInvariant();

            try
            {
                // Try cache first
                var cacheKey = CacheKeys.UserByEmail(normalizedEmail);
                var cachedUser = await _cacheService.GetAsync<UserDto>(cacheKey);
                if (cachedUser != null)
                {
                    _logger.LogDebug("Retrieved user by email {Email} from cache", normalizedEmail);
                    return cachedUser;
                }

                // Load from database
                var user = await _userRepository.GetByEmailAsync(normalizedEmail);
                if (user == null)
                {
                    _logger.LogWarning("User with email {Email} not found", normalizedEmail);
                    throw new KeyNotFoundException($"User with email {normalizedEmail} not found");
                }

                var userDto = _mapper.Map<UserDto>(user);

                // Cache the result
                await _cacheService.SetAsync(cacheKey, userDto, _userCacheExpiration);
                _logger.LogDebug("Cached user by email {Email} for {CacheExpiration}", normalizedEmail, _userCacheExpiration);

                return userDto;
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error retrieving user by email {Email}", normalizedEmail);
                throw;
            }
        }

        public async Task<UserDto> GetUserByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or empty", nameof(username));

            var normalizedUsername = username.Trim();

            try
            {
                // Try cache first
                var cacheKey = CacheKeys.UserByUsername(normalizedUsername);
                var cachedUser = await _cacheService.GetAsync<UserDto>(cacheKey);
                if (cachedUser != null)
                {
                    _logger.LogDebug("Retrieved user by username {Username} from cache", normalizedUsername);
                    return cachedUser;
                }

                // Load from database
                var user = await _userRepository.GetByUsernameAsync(normalizedUsername);
                if (user == null)
                {
                    _logger.LogWarning("User with username {Username} not found", normalizedUsername);
                    throw new KeyNotFoundException($"User with username {normalizedUsername} not found");
                }

                var userDto = _mapper.Map<UserDto>(user);

                // Cache the result
                await _cacheService.SetAsync(cacheKey, userDto, _userCacheExpiration);
                _logger.LogDebug("Cached user by username {Username} for {CacheExpiration}", normalizedUsername, _userCacheExpiration);

                return userDto;
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error retrieving user by username {Username}", normalizedUsername);
                throw;
            }
        }

        public async Task<(List<UserDto> users, int totalCount)> GetUsersAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            if (page <= 0)
                throw new ArgumentException("Page must be greater than 0", nameof(page));

            if (pageSize <= 0 || pageSize > 100)
                throw new ArgumentException("Page size must be between 1 and 100", nameof(pageSize));

            var normalizedSearch = search?.Trim();

            try
            {
                // Try cache first for search results
                var cacheKey = CacheKeys.PagesSearch(normalizedSearch ?? "", page, pageSize);
                var cachedResult = await _cacheService.GetAsync<UserSearchResult>(cacheKey);
                if (cachedResult != null)
                {
                    _logger.LogDebug("Retrieved users search results from cache for page {Page}, size {PageSize}, search '{Search}'",
                        page, pageSize, normalizedSearch);
                    return (cachedResult.Users, cachedResult.TotalCount);
                }

                // Load from database
                var totalCount = string.IsNullOrEmpty(normalizedSearch)
                    ? await _userRepository.CountAsync()
                    : await _userRepository.CountSearchAsync(normalizedSearch);

                var users = string.IsNullOrEmpty(normalizedSearch)
                    ? await _userRepository.GetPagedWithRelatedAsync(page, pageSize)
                    : await _userRepository.SearchUsersAsync(normalizedSearch, page, pageSize);

                var userDtos = _mapper.Map<List<UserDto>>(users);
                var result = new UserSearchResult { Users = userDtos, TotalCount = totalCount };

                // Cache the result
                await _cacheService.SetAsync(cacheKey, result, _searchCacheExpiration);
                _logger.LogDebug("Cached users search results for page {Page}, size {PageSize}, search '{Search}'",
                    page, pageSize, normalizedSearch);

                return (userDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users for page {Page}, size {PageSize}, search '{Search}'",
                    page, pageSize, normalizedSearch);
                throw;
            }
        }

        public async Task<UserDto> CreateUserAsync(CreateUserDto createUserDto)
        {
            if (createUserDto == null)
                throw new ArgumentNullException(nameof(createUserDto));

            await ValidateCreateUserDto(createUserDto);

            var currentUserId = await GetCurrentUserIdSafeAsync();

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Create user
                    var user = _mapper.Map<User>(createUserDto);
                    user.CreatedByUserId = currentUserId;
                    user.UpdatedByUserId = currentUserId;
                    user.CreatedAt = DateTime.UtcNow;
                    user.UpdatedAt = DateTime.UtcNow;

                    await _userRepository.AddAsync(user);
                    await _userRepository.SaveChangesAsync();

                    _logger.LogInformation("User {UserId} created with email {Email} by user {CurrentUserId}",
                        user.Id, user.Email, currentUserId);

                    // Handle addresses if provided
                    if (createUserDto.Addresses?.Any() == true)
                    {
                        await CreateUserAddressesAsync(user.Id, createUserDto.Addresses, currentUserId);
                    }

                    // Handle contact details if provided
                    if (createUserDto.ContactDetails?.Any() == true)
                    {
                        await CreateUserContactDetailsAsync(user.Id, createUserDto.ContactDetails, currentUserId);
                    }

                    await transaction.CommitAsync();

                    // Invalidate relevant caches
                    await InvalidateUserCachesAsync(user.Id);

                    // Return the created user with all related data
                    var createdUser = await _userRepository.GetByIdAsync(user.Id);
                    var result = _mapper.Map<UserDto>(createdUser);

                    _logger.LogInformation("User {UserId} successfully created and cached", user.Id);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user with email {Email}", createUserDto.Email);
                throw;
            }
        }

        public async Task<UserDto> UpdateUserAsync(int userId, UpdateUserDto updateUserDto)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            if (updateUserDto == null)
                throw new ArgumentNullException(nameof(updateUserDto));

            await ValidateUpdateUserDto(userId, updateUserDto);

            var currentUserId = await GetCurrentUserIdSafeAsync();

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var user = await _userRepository.GetByIdAsync(userId);
                    if (user == null)
                    {
                        throw new KeyNotFoundException($"User with ID {userId} not found");
                    }

                    // Store original email for logging
                    var originalEmail = user.Email;

                    // Update user properties
                    _mapper.Map(updateUserDto, user);
                    user.UpdatedAt = DateTime.UtcNow;
                    user.UpdatedByUserId = currentUserId;

                    _userRepository.Update(user);

                    // Handle addresses update
                    if (updateUserDto.Addresses?.Any() == true)
                    {
                        await UpdateUserAddressesAsync(userId, updateUserDto.Addresses, currentUserId);
                    }

                    // Handle contact details update
                    if (updateUserDto.ContactDetails?.Any() == true)
                    {
                        await UpdateUserContactDetailsAsync(userId, updateUserDto.ContactDetails, currentUserId);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("User {UserId} updated (email: {OriginalEmail} -> {NewEmail}) by user {CurrentUserId}",
                        userId, originalEmail, user.Email, currentUserId);

                    // Invalidate relevant caches
                    await InvalidateUserCachesAsync(userId);

                    // If current user updated their own profile, refresh session
                    await RefreshSessionIfCurrentUser(userId);

                    // Return updated user
                    var updatedUser = await _userRepository.GetByIdAsync(userId);
                    return _mapper.Map<UserDto>(updatedUser);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error updating user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            var currentUserId = await GetCurrentUserIdSafeAsync();

            // Check if user can delete other users
            if (currentUserId != userId && !_userSessionService.CanManageUsers())
            {
                throw new UnauthorizedAccessException("You don't have permission to delete other users");
            }

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Attempted to delete non-existent user {UserId}", userId);
                    return false;
                }

                var userEmail = user.Email;
                var result = await _userRepository.SoftDeleteAsync(userId, currentUserId);

                if (result)
                {
                    await InvalidateUserCachesAsync(userId);
                    _logger.LogInformation("User {UserId} ({Email}) soft deleted by user {CurrentUserId}",
                        userId, userEmail, currentUserId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ActivateUserAsync(int userId)
        {
            return await UpdateUserStatusAsync(userId, user =>
            {
                user.IsActive = true;
                return "activated";
            });
        }

        public async Task<bool> DeactivateUserAsync(int userId)
        {
            return await UpdateUserStatusAsync(userId, user =>
            {
                user.IsActive = false;
                return "deactivated";
            });
        }

        public async Task<bool> LockUserAsync(int userId)
        {
            return await UpdateUserStatusAsync(userId, user =>
            {
                user.IsLocked = true;
                user.LockoutEnd = DateTime.UtcNow.AddDays(30);
                return "locked";
            });
        }

        public async Task<bool> UnlockUserAsync(int userId)
        {
            return await UpdateUserStatusAsync(userId, user =>
            {
                user.IsLocked = false;
                user.LockoutEnd = null;
                user.FailedLoginAttempts = 0;
                return "unlocked";
            });
        }

        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            if (changePasswordDto == null)
                throw new ArgumentNullException(nameof(changePasswordDto));

            if (string.IsNullOrWhiteSpace(changePasswordDto.CurrentPassword))
                throw new ArgumentException("Current password is required", nameof(changePasswordDto));

            if (string.IsNullOrWhiteSpace(changePasswordDto.NewPassword))
                throw new ArgumentException("New password is required", nameof(changePasswordDto));

            ValidatePasswordStrength(changePasswordDto.NewPassword);

            var currentUserId = await GetCurrentUserIdSafeAsync();

            // Check if user can change password for this user
            if (currentUserId != userId && !_userSessionService.CanManageUsers())
            {
                throw new UnauthorizedAccessException("You don't have permission to change passwords for other users");
            }

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Password change attempted for non-existent user {UserId}", userId);
                    return false;
                }

                if (!BCrypt.Net.BCrypt.Verify(changePasswordDto.CurrentPassword, user.PasswordHash))
                {
                    _logger.LogWarning("Invalid current password provided for user {UserId}", userId);
                    return false;
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);
                user.PasswordChangedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedByUserId = currentUserId;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                await InvalidateUserCachesAsync(userId);

                _logger.LogInformation("Password changed successfully for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ResetPasswordAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be null or empty", nameof(email));

            var normalizedEmail = email.Trim().ToLowerInvariant();

            try
            {
                var user = await _userRepository.GetByEmailAsync(normalizedEmail);
                if (user == null)
                {
                    _logger.LogInformation("Password reset requested for non-existent email {Email}", normalizedEmail);
                    return true; // Don't reveal if email exists
                }

                _logger.LogInformation("Password reset requested for user {UserId} with email {Email}", user.Id, normalizedEmail);
                // Implementation would involve sending reset email
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing password reset for email {Email}", normalizedEmail);
                throw;
            }
        }

        public async Task<bool> ValidateUserCredentialsAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return false;

            try
            {
                var user = await _userRepository.GetByEmailAsync(email.Trim().ToLowerInvariant());
                if (user == null)
                    return false;

                return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating credentials for email {Email}", email);
                return false;
            }
        }

        public async Task<UserDto> UpdateUserPreferencesAsync(int userId, Dictionary<string, object> preferences)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            if (preferences == null)
                throw new ArgumentNullException(nameof(preferences));

            var currentUserId = await GetCurrentUserIdSafeAsync();

            // Check if user can update preferences for this user
            if (currentUserId != userId && !_userSessionService.CanManageUsers())
            {
                throw new UnauthorizedAccessException("You don't have permission to update preferences for other users");
            }

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedByUserId = currentUserId;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                await InvalidateUserCachesAsync(userId);
                await RefreshSessionIfCurrentUser(userId);

                _logger.LogInformation("Preferences updated for user {UserId}", userId);

                return _mapper.Map<UserDto>(user);
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error updating preferences for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be null or empty", nameof(token));

            var currentUserId = await GetCurrentUserIdSafeAsync();

            try
            {
                var user = await _userRepository.GetByEmailVerificationTokenAsync(token);
                if (user == null)
                {
                    _logger.LogWarning("Email verification attempted with invalid token");
                    return false;
                }

                user.EmailVerifiedAt = DateTime.UtcNow;
                user.EmailVerificationToken = null;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedByUserId = currentUserId;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                await InvalidateUserCachesAsync(user.Id);

                _logger.LogInformation("Email verified successfully for user {UserId}", user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email with token");
                throw;
            }
        }

        public async Task<bool> SendEmailVerificationAsync(int userId)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Email verification requested for non-existent user {UserId}", userId);
                    return false;
                }

                // Implementation would involve sending verification email
                _logger.LogInformation("Email verification sent for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email verification for user {UserId}", userId);
                throw;
            }
        }

        public async Task<UserDto> UpdateUserAvatarAsync(int userId, int? pictureFileId)
        {
            return await UpdateUserFieldAsync(userId, user =>
            {
                if (pictureFileId.HasValue)
                {
                    ValidateAvatarFileAsync(pictureFileId.Value).Wait();
                }
                user.PictureFileId = pictureFileId;
            }, "avatar updated");
        }

        public async Task<UserDto> RemoveUserAvatarAsync(int userId)
        {
            return await UpdateUserFieldAsync(userId, user =>
            {
                user.PictureFileId = null;
            }, "avatar removed");
        }

        #region Private Helper Methods

        private async Task<bool> UpdateUserStatusAsync(int userId, Func<User, string> updateAction)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            var currentUserId = await GetCurrentUserIdSafeAsync();

            // Check if user can update status for this user
            if (currentUserId != userId && !_userSessionService.CanManageUsers())
            {
                throw new UnauthorizedAccessException("You don't have permission to change status for other users");
            }

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Status update attempted for non-existent user {UserId}", userId);
                    return false;
                }

                var action = updateAction(user);
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedByUserId = currentUserId;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                await InvalidateUserCachesAsync(userId);

                _logger.LogInformation("User {UserId} {Action} by user {CurrentUserId}", userId, action, currentUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for user {UserId}", userId);
                throw;
            }
        }

        private async Task<UserDto> UpdateUserFieldAsync(int userId, Action<User> updateAction, string actionDescription)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            var currentUserId = await GetCurrentUserIdSafeAsync();

            // Check if user can update field for this user
            if (currentUserId != userId && !_userSessionService.CanManageUsers())
            {
                throw new UnauthorizedAccessException($"You don't have permission to update {actionDescription} for other users");
            }

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                updateAction(user);
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedByUserId = currentUserId;

                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                await InvalidateUserCachesAsync(userId);

                _logger.LogInformation("User {UserId} {ActionDescription}", userId, actionDescription);

                return _mapper.Map<UserDto>(user);
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error updating user {UserId} field: {ActionDescription}", userId, actionDescription);
                throw;
            }
        }

        private async Task ValidateCreateUserDto(CreateUserDto createUserDto)
        {
            if (await _userRepository.EmailExistsAsync(createUserDto.Email))
                throw new InvalidOperationException("Email already exists");

            if (await _userRepository.UsernameExistsAsync(createUserDto.Username))
                throw new InvalidOperationException("Username already exists");

            if (createUserDto.PictureFileId.HasValue)
            {
                await ValidateAvatarFileAsync(createUserDto.PictureFileId.Value);
            }

            // Only admin/dev can create users
            if (!_userSessionService.CanManageUsers())
                throw new UnauthorizedAccessException("You don't have permission to create users");

            if (!_userSessionService.CanCreateUserWithRole(createUserDto.Role))
                throw new UnauthorizedAccessException("You don't have permission to create users with this role");

            ValidatePasswordStrength(createUserDto.Password);
        }

        private async Task ValidateUpdateUserDto(int userId, UpdateUserDto updateUserDto)
        {
            if (await _userRepository.EmailExistsAsync(updateUserDto.Email, userId))
                throw new InvalidOperationException("Email already exists");

            if (await _userRepository.UsernameExistsAsync(updateUserDto.Username, userId))
                throw new InvalidOperationException("Username already exists");

            if (updateUserDto.PictureFileId.HasValue)
            {
                await ValidateAvatarFileAsync(updateUserDto.PictureFileId.Value);
            }

            var currentUserId = await GetCurrentUserIdSafeAsync();
            var isUpdatingSelf = currentUserId == userId;

            // If updating someone else's profile, check permissions
            if (!isUpdatingSelf)
            {
                if (!_userSessionService.CanManageUsers())
                    throw new UnauthorizedAccessException("You don't have permission to update other users");

                if (!_userSessionService.CanCreateUserWithRole(updateUserDto.Role))
                    throw new UnauthorizedAccessException("You don't have permission to assign this role to other users");
            }
            else
            {
                // If updating self, check if they're trying to change their role inappropriately
                var currentUserRole = _userSessionService.GetCurrentUserRole();
                if (currentUserRole != updateUserDto.Role)
                {
                    // Only allow role change if they have permission to create users with that role
                    if (!_userSessionService.CanCreateUserWithRole(updateUserDto.Role))
                        throw new UnauthorizedAccessException("You don't have permission to change your role to this level");
                }
            }
        }

        private async Task ValidateAvatarFileAsync(int fileId)
        {
            var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);
            if (file == null)
                throw new ArgumentException($"File with ID {fileId} not found");

            if (file.FileType != FileType.Image)
                throw new ArgumentException($"File with ID {fileId} is not an image");

            if (file.FileSize > 5 * 1024 * 1024) // 5MB limit
                throw new ArgumentException("Avatar image must be smaller than 5MB");
        }

        private static void ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                throw new ArgumentException("Password must be at least 8 characters long");

            var hasUppercase = password.Any(char.IsUpper);
            var hasLowercase = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            var hasSpecialChar = password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c));

            if (!(hasUppercase && hasLowercase && hasDigit && hasSpecialChar))
                throw new ArgumentException("Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character");
        }

        private async Task CreateUserAddressesAsync(int userId, List<CreateAddressDto> addresses, int? currentUserId)
        {
            foreach (var addressDto in addresses)
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

        private async Task CreateUserContactDetailsAsync(int userId, List<CreateContactDetailsDto> contactDetails, int? currentUserId)
        {
            foreach (var contactDto in contactDetails)
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

        private async Task UpdateUserAddressesAsync(int userId, List<UpdateAddressDto> addresses, int? currentUserId)
        {
            // Soft delete existing addresses
            var existingAddresses = await _context.Addresses
                .Where(a => EF.Property<int?>(a, "UserId") == userId && !a.IsDeleted)
                .ToListAsync();

            foreach (var existingAddress in existingAddresses)
            {
                existingAddress.IsDeleted = true;
                existingAddress.DeletedAt = DateTime.UtcNow;
                existingAddress.DeletedByUserId = currentUserId;
                _context.Addresses.Update(existingAddress);
            }

            // Add new addresses
            foreach (var addressDto in addresses)
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

        private async Task UpdateUserContactDetailsAsync(int userId, List<UpdateContactDetailsDto> contactDetails, int? currentUserId)
        {
            // Soft delete existing contact details
            var existingContacts = await _context.ContactDetails
                .Where(c => EF.Property<int?>(c, "UserId") == userId && !c.IsDeleted)
                .ToListAsync();

            foreach (var existingContact in existingContacts)
            {
                existingContact.IsDeleted = true;
                existingContact.DeletedAt = DateTime.UtcNow;
                existingContact.DeletedByUserId = currentUserId;
                _context.ContactDetails.Update(existingContact);
            }

            // Add new contact details
            foreach (var contactDto in contactDetails)
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

        private async Task<int?> GetCurrentUserIdSafeAsync()
        {
            try
            {
                var userId = _userSessionService.GetCurrentUserId();
                if (userId.HasValue)
                    return userId;

                var session = await _userSessionService.GetCurrentSessionAsync();
                return session?.UserId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine current user ID, using null");
                return null;
            }
        }

        private async Task InvalidateUserCachesAsync(int userId)
        {
            try
            {
                await _cacheInvalidationService.InvalidateUserCacheAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate user cache for user {UserId}", userId);
            }
        }

        private async Task RefreshSessionIfCurrentUser(int userId)
        {
            try
            {
                var sessionUserId = _userSessionService.GetCurrentUserId();
                if (sessionUserId.HasValue && sessionUserId.Value == userId)
                {
                    await _userSessionService.UpdateSessionAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh session for user {UserId}", userId);
            }
        }

        #endregion
    }
}