using AutoMapper;
using Backend.CMS.Application.DTOs;
using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using Backend.CMS.Infrastructure.Caching;
using Backend.CMS.Infrastructure.Caching.Interfaces;
using Backend.CMS.Infrastructure.Caching.Services;
using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.CMS.Infrastructure.Services
{
    public class UserSearchResult
    {
        public List<UserDto> Users { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class UserPermissions
    {
        public List<string> Permissions { get; set; } = new();
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    }

    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<UserService> _logger;
        private readonly IUserSessionService _userSessionService;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly ICacheKeyService _cacheKeyService;
        private readonly CacheOptions _cacheOptions;

        // Pagination constants
        private const int DEFAULT_PAGE_SIZE = 10;
        private const int MAX_PAGE_SIZE = 100;
        private const int MIN_PAGE_SIZE = 1;

        public UserService(
            IUnitOfWork unitOfWork,
            IUserSessionService userSessionService,
            ICacheService cacheService,
            ICacheInvalidationService cacheInvalidationService,
            ICacheKeyService cacheKeyService,
            IMapper mapper,
            ILogger<UserService> logger,
            IOptions<CacheOptions> cacheOptions)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
            _cacheKeyService = cacheKeyService ?? throw new ArgumentNullException(nameof(cacheKeyService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheOptions = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));
        }

        public async Task<UserDto> GetUserByIdAsync(int userId)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            try
            {
                var cachedUser = await _cacheService.GetEntityAsync<User>(_cacheKeyService, userId);
                if (cachedUser != null)
                {
                    _logger.LogDebug("Retrieved user {UserId} from cache", userId);
                    return _mapper.Map<UserDto>(cachedUser);
                }

                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found", userId);
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                await _cacheService.SetEntityAsync(_cacheKeyService, userId, user, _cacheOptions.DefaultExpiration);
                await CacheUserByAlternateKeysAsync(user);

                _logger.LogDebug("Cached user {UserId} for {CacheExpiration}", userId, _cacheOptions.DefaultExpiration);

                return _mapper.Map<UserDto>(user);
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
                var cacheKey = _cacheKeyService.GetCustomKey("user", "email", normalizedEmail);
                var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
                if (cachedUser != null)
                {
                    _logger.LogDebug("Retrieved user by email {Email} from cache", normalizedEmail);
                    return _mapper.Map<UserDto>(cachedUser);
                }

                var user = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
                if (user == null)
                {
                    _logger.LogWarning("User with email {Email} not found", normalizedEmail);
                    throw new KeyNotFoundException($"User with email {normalizedEmail} not found");
                }

                await CacheUserByAllKeysAsync(user);

                _logger.LogDebug("Cached user by email {Email} for {CacheExpiration}", normalizedEmail, _cacheOptions.DefaultExpiration);

                return _mapper.Map<UserDto>(user);
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
                var cacheKey = _cacheKeyService.GetCustomKey("user", "username", normalizedUsername);
                var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
                if (cachedUser != null)
                {
                    _logger.LogDebug("Retrieved user by username {Username} from cache", normalizedUsername);
                    return _mapper.Map<UserDto>(cachedUser);
                }

                var user = await _unitOfWork.Users.GetByUsernameAsync(normalizedUsername);
                if (user == null)
                {
                    _logger.LogWarning("User with username {Username} not found", normalizedUsername);
                    throw new KeyNotFoundException($"User with username {normalizedUsername} not found");
                }

                await CacheUserByAllKeysAsync(user);

                _logger.LogDebug("Cached user by username {Username} for {CacheExpiration}", normalizedUsername, _cacheOptions.DefaultExpiration);

                return _mapper.Map<UserDto>(user);
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error retrieving user by username {Username}", normalizedUsername);
                throw;
            }
        }

        public async Task<PagedResult<UserDto>> GetUsersPagedAsync(int pageNumber = 1, int pageSize = DEFAULT_PAGE_SIZE, string? search = null)
        {
            var validatedPageNumber = Math.Max(1, pageNumber);
            var validatedPageSize = Math.Clamp(pageSize, MIN_PAGE_SIZE, MAX_PAGE_SIZE);
            var normalizedSearch = search?.Trim();

            try
            {
                _logger.LogDebug("Getting paginated users: page {PageNumber}, size {PageSize}, search '{Search}'",
                    validatedPageNumber, validatedPageSize, normalizedSearch);

                var cacheKey = string.IsNullOrEmpty(normalizedSearch)
                    ? _cacheKeyService.GetCollectionKey<User>("list", validatedPageNumber, validatedPageSize)
                    : _cacheKeyService.GetCollectionKey<User>("search", normalizedSearch, validatedPageNumber, validatedPageSize);

                var cachedResult = await _cacheService.GetAsync<PagedResult<UserDto>>(cacheKey);
                if (cachedResult != null)
                {
                    _logger.LogDebug("Retrieved paginated users from cache for page {Page}, size {PageSize}, search '{Search}'",
                        validatedPageNumber, validatedPageSize, normalizedSearch);
                    return cachedResult;
                }

                var totalCount = string.IsNullOrEmpty(normalizedSearch)
                    ? await _unitOfWork.Users.CountAsync()
                    : await _unitOfWork.Users.CountSearchAsync(normalizedSearch);

                _logger.LogDebug("Total user count: {TotalCount}", totalCount);

                if (totalCount == 0)
                {
                    var emptyResult = new PagedResult<UserDto>(
                        new List<UserDto>(),
                        validatedPageNumber,
                        validatedPageSize,
                        0);

                    await _cacheService.SetAsync(cacheKey, emptyResult, _cacheOptions.ShortExpiration);
                    return emptyResult;
                }

                var totalPages = (int)Math.Ceiling((double)totalCount / validatedPageSize);
                var adjustedPageNumber = Math.Min(validatedPageNumber, totalPages);

                _logger.LogDebug("Pagination metadata: totalPages {TotalPages}, adjustedPage {AdjustedPage}",
                    totalPages, adjustedPageNumber);

                var users = string.IsNullOrEmpty(normalizedSearch)
                    ? await _unitOfWork.Users.GetPagedWithRelatedAsync(adjustedPageNumber, validatedPageSize)
                    : await _unitOfWork.Users.SearchUsersAsync(normalizedSearch, adjustedPageNumber, validatedPageSize);

                var userDtos = _mapper.Map<List<UserDto>>(users);

                var result = new PagedResult<UserDto>(
                    userDtos.AsReadOnly(),
                    adjustedPageNumber,
                    validatedPageSize,
                    totalCount);

                var cacheExpiration = string.IsNullOrEmpty(normalizedSearch)
                    ? _cacheOptions.DefaultExpiration
                    : _cacheOptions.ShortExpiration;

                await _cacheService.SetAsync(cacheKey, result, cacheExpiration);

                _logger.LogDebug("Cached paginated users result for page {Page}, size {PageSize}, search '{Search}' with expiration {Expiration}",
                    adjustedPageNumber, validatedPageSize, normalizedSearch, cacheExpiration);

                await CacheIndividualUsersAsync(users);

                _logger.LogInformation("Successfully retrieved paginated users: {UserCount} users on page {Page} of {TotalPages} (total: {TotalCount})",
                    userDtos.Count, adjustedPageNumber, result.TotalPages, totalCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated users for page {Page}, size {PageSize}, search '{Search}'",
                    validatedPageNumber, validatedPageSize, normalizedSearch);
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
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var user = _mapper.Map<User>(createUserDto);
                    user.CreatedByUserId = currentUserId;
                    user.UpdatedByUserId = currentUserId;
                    user.CreatedAt = DateTime.UtcNow;
                    user.UpdatedAt = DateTime.UtcNow;

                    await _unitOfWork.Users.AddAsync(user);
                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("User {UserId} created with email {Email} by user {CurrentUserId}",
                        user.Id, user.Email, currentUserId);

                    if (createUserDto.Addresses?.Any() == true)
                    {
                        await CreateUserAddressesAsync(user.Id, createUserDto.Addresses, currentUserId);
                    }

                    if (createUserDto.ContactDetails?.Any() == true)
                    {
                        await CreateUserContactDetailsAsync(user.Id, createUserDto.ContactDetails, currentUserId);
                    }

                    await _unitOfWork.SaveChangesAsync();

                    await CacheUserByAllKeysAsync(user);
                    await InvalidateUserListCaches();

                    var createdUser = await _unitOfWork.Users.GetByIdAsync(user.Id);
                    var result = _mapper.Map<UserDto>(createdUser);

                    _logger.LogInformation("User {UserId} successfully created and cached", user.Id);
                    return result;
                });
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
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var user = await _unitOfWork.Users.GetByIdAsync(userId);
                    if (user == null)
                    {
                        throw new KeyNotFoundException($"User with ID {userId} not found");
                    }

                    var originalEmail = user.Email;
                    var originalUsername = user.Username;

                    _mapper.Map(updateUserDto, user);
                    user.UpdatedAt = DateTime.UtcNow;
                    user.UpdatedByUserId = currentUserId;

                    _unitOfWork.Users.Update(user);

                    if (updateUserDto.Addresses?.Any() == true)
                    {
                        await UpdateUserAddressesAsync(userId, updateUserDto.Addresses, currentUserId);
                    }

                    if (updateUserDto.ContactDetails?.Any() == true)
                    {
                        await UpdateUserContactDetailsAsync(userId, updateUserDto.ContactDetails, currentUserId);
                    }

                    await _unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("User {UserId} updated (email: {OriginalEmail} -> {NewEmail}) by user {CurrentUserId}",
                        userId, originalEmail, user.Email, currentUserId);

                    if (originalEmail != user.Email)
                    {
                        await _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("user", "email", originalEmail));
                    }
                    if (originalUsername != user.Username)
                    {
                        await _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("user", "username", originalUsername));
                    }

                    await CacheUserByAllKeysAsync(user);

                    if (originalEmail != user.Email || user.UpdatedAt > DateTime.UtcNow.AddMinutes(-1))
                    {
                        await InvalidateUserRelatedCaches(userId);
                    }

                    await InvalidateUserListCaches();

                    await RefreshSessionIfCurrentUser(userId);

                    var updatedUser = await _unitOfWork.Users.GetByIdAsync(userId);
                    return _mapper.Map<UserDto>(updatedUser);
                });
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

            if (currentUserId != userId && !_userSessionService.CanManageUsers())
            {
                throw new UnauthorizedAccessException("You don't have permission to delete other users");
            }

            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var user = await _unitOfWork.Users.GetByIdAsync(userId);
                    if (user == null)
                    {
                        _logger.LogWarning("Attempted to delete non-existent user {UserId}", userId);
                        return false;
                    }

                    var userEmail = user.Email;
                    var username = user.Username;
                    var result = await _unitOfWork.Users.SoftDeleteAsync(userId, currentUserId);

                    if (result)
                    {
                        await _cacheInvalidationService.InvalidateEntityAsync<User>(userId);
                        await _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("user", "email", userEmail));
                        await _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("user", "username", username));

                        await InvalidateUserRelatedCaches(userId);
                        await InvalidateUserListCaches();

                        _logger.LogInformation("User {UserId} ({Email}) soft deleted by user {CurrentUserId}",
                            userId, userEmail, currentUserId);
                    }

                    return result;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                throw;
            }
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

            if (currentUserId != userId && !_userSessionService.CanManageUsers())
            {
                throw new UnauthorizedAccessException("You don't have permission to change passwords for other users");
            }

            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var user = await _unitOfWork.Users.GetByIdAsync(userId);
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

                    _unitOfWork.Users.Update(user);
                    await _unitOfWork.SaveChangesAsync();

                    await CacheUserByAllKeysAsync(user);
                    await InvalidateUserRelatedCaches(userId);

                    _logger.LogInformation("Password changed successfully for user {UserId}", userId);
                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> ValidateUserCredentialsAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return false;

            try
            {
                var normalizedEmail = email.Trim().ToLowerInvariant();

                var cacheKey = _cacheKeyService.GetCustomKey("user", "email", normalizedEmail);
                var user = await _cacheService.GetAsync<User>(cacheKey);

                if (user == null)
                {
                    user = await _unitOfWork.Users.GetByEmailAsync(normalizedEmail);
                    if (user != null)
                    {
                        await _cacheService.SetAsync(cacheKey, user, _cacheOptions.DefaultExpiration);
                    }
                }

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

        #region Status Management Methods

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

        #endregion

        #region Private Helper Methods

        private async Task CacheUserByAllKeysAsync(User user)
        {
            try
            {
                await _cacheService.SetEntityAsync(_cacheKeyService, user.Id, user, _cacheOptions.DefaultExpiration);
                await _cacheService.SetAsync(_cacheKeyService.GetCustomKey("user", "email", user.Email), user, _cacheOptions.DefaultExpiration);
                await _cacheService.SetAsync(_cacheKeyService.GetCustomKey("user", "username", user.Username), user, _cacheOptions.DefaultExpiration);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache user {UserId} by all keys", user.Id);
            }
        }

        private async Task CacheUserByAlternateKeysAsync(User user)
        {
            try
            {
                await _cacheService.SetAsync(_cacheKeyService.GetCustomKey("user", "email", user.Email), user, _cacheOptions.DefaultExpiration);
                await _cacheService.SetAsync(_cacheKeyService.GetCustomKey("user", "username", user.Username), user, _cacheOptions.DefaultExpiration);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache user {UserId} by alternate keys", user.Id);
            }
        }

        private async Task CacheIndividualUsersAsync(IEnumerable<User> users)
        {
            try
            {
                var tasks = users.Select(user => CacheUserByAllKeysAsync(user));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache individual users from list");
            }
        }

        private async Task InvalidateUserListCaches()
        {
            try
            {
                await _cacheInvalidationService.InvalidateByPatternAsync(_cacheKeyService.GetEntityPattern<User>());
                await _cacheInvalidationService.InvalidateByPatternAsync(_cacheKeyService.GetCustomKey("user", "search", "*"));
                await _cacheInvalidationService.InvalidateByPatternAsync(_cacheKeyService.GetCustomKey("user", "advanced-search", "*"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate user list caches");
            }
        }

        private async Task InvalidateUserRelatedCaches(int userId)
        {
            try
            {
                await _cacheService.RemoveAsync(_cacheKeyService.GetCustomKey("user", "permissions", userId));
                await _cacheInvalidationService.InvalidateByPatternAsync(_cacheKeyService.GetCustomKey("user", "sessions", userId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate user related caches for user {UserId}", userId);
            }
        }

        private async Task<bool> UpdateUserStatusAsync(int userId, Func<User, string> updateAction)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            var currentUserId = await GetCurrentUserIdSafeAsync();

            if (currentUserId != userId && !_userSessionService.CanManageUsers())
            {
                throw new UnauthorizedAccessException("You don't have permission to change status for other users");
            }

            try
            {
                return await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var user = await _unitOfWork.Users.GetByIdAsync(userId);
                    if (user == null)
                    {
                        _logger.LogWarning("Status update attempted for non-existent user {UserId}", userId);
                        return false;
                    }

                    var action = updateAction(user);
                    user.UpdatedAt = DateTime.UtcNow;
                    user.UpdatedByUserId = currentUserId;

                    _unitOfWork.Users.Update(user);
                    await _unitOfWork.SaveChangesAsync();

                    await CacheUserByAllKeysAsync(user);
                    await InvalidateUserRelatedCaches(userId);
                    await InvalidateUserListCaches();

                    _logger.LogInformation("User {UserId} {Action} by user {CurrentUserId}", userId, action, currentUserId);
                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for user {UserId}", userId);
                throw;
            }
        }

        private async Task ValidateCreateUserDto(CreateUserDto createUserDto)
        {
            if (await _unitOfWork.Users.EmailExistsAsync(createUserDto.Email))
                throw new InvalidOperationException("Email already exists");

            if (await _unitOfWork.Users.UsernameExistsAsync(createUserDto.Username))
                throw new InvalidOperationException("Username already exists");

            if (!_userSessionService.CanManageUsers())
                throw new UnauthorizedAccessException("You don't have permission to create users");

            if (!_userSessionService.CanCreateUserWithRole(createUserDto.Role))
                throw new UnauthorizedAccessException("You don't have permission to create users with this role");

            ValidatePasswordStrength(createUserDto.Password);
        }

        private async Task ValidateUpdateUserDto(int userId, UpdateUserDto updateUserDto)
        {
            if (await _unitOfWork.Users.EmailExistsAsync(updateUserDto.Email, userId))
                throw new InvalidOperationException("Email already exists");

            if (await _unitOfWork.Users.UsernameExistsAsync(updateUserDto.Username, userId))
                throw new InvalidOperationException("Username already exists");

            var currentUserId = await GetCurrentUserIdSafeAsync();
            var isUpdatingSelf = currentUserId == userId;

            if (!isUpdatingSelf)
            {
                if (!_userSessionService.CanManageUsers())
                    throw new UnauthorizedAccessException("You don't have permission to update other users");

                if (!_userSessionService.CanCreateUserWithRole(updateUserDto.Role))
                    throw new UnauthorizedAccessException("You don't have permission to assign this role to other users");
            }
            else
            {
                var currentUserRole = _userSessionService.GetCurrentUserRole();
                if (currentUserRole != updateUserDto.Role)
                {
                    if (!_userSessionService.CanCreateUserWithRole(updateUserDto.Role))
                        throw new UnauthorizedAccessException("You don't have permission to change your role to this level");
                }
            }
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

                await _unitOfWork.Addresses.AddAsync(address);
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

                await _unitOfWork.ContactDetails.AddAsync(contact);
            }
        }

        private async Task UpdateUserAddressesAsync(int userId, List<UpdateAddressDto> addresses, int? currentUserId)
        {
            var existingAddresses = await _unitOfWork.Addresses.GetAddressesByEntityAsync(userId, "User");

            foreach (var existingAddress in existingAddresses)
            {
                await _unitOfWork.Addresses.SoftDeleteAsync(existingAddress.Id, currentUserId);
            }

            foreach (var addressDto in addresses)
            {
                var address = _mapper.Map<Address>(addressDto);
                address.CreatedByUserId = currentUserId;
                address.UpdatedByUserId = currentUserId;
                address.CreatedAt = DateTime.UtcNow;
                address.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.Addresses.AddAsync(address);
            }
        }

        private async Task UpdateUserContactDetailsAsync(int userId, List<UpdateContactDetailsDto> contactDetails, int? currentUserId)
        {
            var existingContacts = await _unitOfWork.ContactDetails.GetContactDetailsByEntityAsync(userId, "User");

            foreach (var existingContact in existingContacts)
            {
                await _unitOfWork.ContactDetails.SoftDeleteAsync(existingContact.Id, currentUserId);
            }

            foreach (var contactDto in contactDetails)
            {
                var contact = _mapper.Map<ContactDetails>(contactDto);
                contact.CreatedByUserId = currentUserId;
                contact.UpdatedByUserId = currentUserId;
                contact.CreatedAt = DateTime.UtcNow;
                contact.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.ContactDetails.AddAsync(contact);
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

        public async Task<PagedResult<UserDto>> SearchUsersPagedAsync(UserSearchDto searchDto)
        {
            if (searchDto == null)
                throw new ArgumentNullException(nameof(searchDto));

            var validatedPageNumber = Math.Max(1, searchDto.PageNumber);
            var validatedPageSize = Math.Clamp(searchDto.PageSize, MIN_PAGE_SIZE, MAX_PAGE_SIZE);

            try
            {
                _logger.LogDebug("Advanced user search: page {PageNumber}, size {PageSize}, term '{SearchTerm}', role {Role}",
                    validatedPageNumber, validatedPageSize, searchDto.SearchTerm, searchDto.Role);

                var cacheKey = _cacheKeyService.GetQueryKey<User>("advanced-search", searchDto);
                var cachedResult = await _cacheService.GetAsync<PagedResult<UserDto>>(cacheKey);
                if (cachedResult != null)
                {
                    _logger.LogDebug("Retrieved advanced search results from cache");
                    return cachedResult;
                }

                // For now, use regular search with the search term from UserSearchDto
                var users = await _unitOfWork.Users.SearchUsersAsync(searchDto.SearchTerm ?? "", validatedPageNumber, validatedPageSize);
                var totalCount = await _unitOfWork.Users.CountSearchAsync(searchDto.SearchTerm ?? "");

                var userDtos = _mapper.Map<List<UserDto>>(users);

                var result = new PagedResult<UserDto>(
                    userDtos.AsReadOnly(),
                    validatedPageNumber,
                    validatedPageSize,
                    totalCount);

                await _cacheService.SetAsync(cacheKey, result, _cacheOptions.ShortExpiration);
                await CacheIndividualUsersAsync(users);

                _logger.LogInformation("Advanced user search completed: {UserCount} users on page {Page} of {TotalPages} (total: {TotalCount})",
                    userDtos.Count, validatedPageNumber, result.TotalPages, totalCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing advanced user search");
                throw;
            }
        }

        public async Task<bool> ResetPasswordAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be null or empty", nameof(email));

            try
            {
                var user = await _unitOfWork.Users.GetByEmailAsync(email.Trim().ToLowerInvariant());
                if (user == null)
                {
                    _logger.LogWarning("Password reset requested for non-existent email {Email}", email);
                    return false;
                }

                // Generate new password
                var newPassword = GenerateRandomPassword();
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                user.PasswordChangedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                // TODO: Send email with new password
                _logger.LogInformation("Password reset completed for user {UserId}", user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for email {Email}", email);
                throw;
            }
        }

        public async Task<UserDto> UpdateUserPreferencesAsync(int userId, Dictionary<string, object> preferences)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            if (preferences == null)
                throw new ArgumentNullException(nameof(preferences));

            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                // TODO: Update user preferences - this depends on how preferences are stored
                // For now, just update the timestamp
                user.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                await CacheUserByAllKeysAsync(user);

                _logger.LogInformation("User preferences updated for user {UserId}", userId);
                return _mapper.Map<UserDto>(user);
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error updating user preferences for user {UserId}", userId);
                throw;
            }
        }

        public async Task<UserDto> UpdateUserAvatarAsync(int userId, int? pictureFileId)
        {
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than 0", nameof(userId));

            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                user.PictureFileId = pictureFileId;
                user.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                await CacheUserByAllKeysAsync(user);

                _logger.LogInformation("User avatar updated for user {UserId}", userId);
                return _mapper.Map<UserDto>(user);
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error updating user avatar for user {UserId}", userId);
                throw;
            }
        }

        public async Task<UserDto> RemoveUserAvatarAsync(int userId)
        {
            return await UpdateUserAvatarAsync(userId, null);
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be null or empty", nameof(token));

            try
            {
                var user = await _unitOfWork.Users.GetByEmailVerificationTokenAsync(token);
                if (user == null)
                {
                    _logger.LogWarning("Email verification attempted with invalid token");
                    return false;
                }

                user.EmailVerifiedAt = DateTime.UtcNow;
                user.EmailVerificationToken = null;
                user.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                await CacheUserByAllKeysAsync(user);

                _logger.LogInformation("Email verified for user {UserId}", user.Id);
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
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Email verification send attempted for non-existent user {UserId}", userId);
                    return false;
                }

                // Generate verification token
                user.EmailVerificationToken = Guid.NewGuid().ToString();
                user.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.Users.Update(user);
                await _unitOfWork.SaveChangesAsync();

                // TODO: Send email with verification token
                _logger.LogInformation("Email verification sent for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email verification for user {UserId}", userId);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods (Additional)

        private static string GenerateRandomPassword(int length = 12)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        #endregion

        #region Legacy/Obsolete Methods

        [Obsolete("Use GetUsersPagedAsync instead")]
        public async Task<(List<UserDto> users, int totalCount)> GetUsersAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            var result = await GetUsersPagedAsync(page, pageSize, search);
            return (result.Data.ToList(), result.TotalCount);
        }

        #endregion
    }
}