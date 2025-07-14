using Backend.CMS.Application.DTOs;
using Frontend.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

namespace Frontend.Services
{
    public class UsersService : IUsersService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<UsersService>? _logger;

        public UsersService(HttpClient httpClient, ILogger<UsersService>? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        #region Paginated User Operations

        /// <summary>
        /// Get paginated list of users with optional search
        /// </summary>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Page size (1-100)</param>
        /// <param name="search">Optional search term</param>
        /// <returns>Paginated result with metadata</returns>
        public async Task<PaginatedResult<UserDto>> GetUsersAsync(int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                _logger?.LogDebug("Getting paginated users: page {PageNumber}, size {PageSize}, search '{Search}'",
                    pageNumber, pageSize, search);

                // Build query string with proper encoding
                var queryParams = new List<string>
                {
                    $"pageNumber={pageNumber}",
                    $"pageSize={pageSize}"
                };

                if (!string.IsNullOrWhiteSpace(search))
                {
                    queryParams.Add($"search={Uri.EscapeDataString(search)}");
                }

                var query = $"/api/v1/user?{string.Join("&", queryParams)}";

                _logger?.LogDebug("Making request to: {Url}", query);

                var response = await _httpClient.GetAsync(query);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<UserDto>>(_jsonOptions);

                    if (result == null)
                    {
                        _logger?.LogWarning("Received null response from users API");
                        return PaginatedResult<UserDto>.Empty(pageNumber, pageSize);
                    }

                    _logger?.LogInformation("Retrieved {UserCount} users on page {Page} of {TotalPages} (total: {TotalCount})",
                        result.Data.Count, result.PageNumber, result.TotalPages, result.TotalCount);

                    return result;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"Failed to get users: {response.StatusCode} - {errorContent}";

                _logger?.LogError("API request failed: {ErrorMessage}", errorMessage);
                throw new HttpRequestException(errorMessage);
            }
            catch (HttpRequestException)
            {
                throw; // Re-throw HTTP exceptions as-is
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error getting paginated users: {ex.Message}";
                _logger?.LogError(ex, "Unexpected error getting users");
                throw new Exception(errorMessage, ex);
            }
        }

        /// <summary>
        /// Advanced user search with filtering
        /// </summary>
        /// <param name="searchDto">Advanced search criteria</param>
        /// <returns>Paginated search results</returns>
        public async Task<PaginatedResult<UserDto>> SearchUsersAsync(UserSearchDto searchDto)
        {
            try
            {
                if (searchDto == null)
                    throw new ArgumentNullException(nameof(searchDto));

                _logger?.LogDebug("Advanced user search: page {PageNumber}, size {PageSize}, term '{SearchTerm}', role {Role}",
                    searchDto.PageNumber, searchDto.PageSize, searchDto.SearchTerm, searchDto.Role);

                var json = JsonSerializer.Serialize(searchDto, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/v1/user/search", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<UserDto>>(_jsonOptions);

                    if (result == null)
                    {
                        _logger?.LogWarning("Received null response from advanced user search API");
                        return PaginatedResult<UserDto>.Empty(searchDto.PageNumber, searchDto.PageSize);
                    }

                    _logger?.LogInformation("Advanced search completed: {UserCount} users on page {Page} of {TotalPages} (total: {TotalCount})",
                        result.Data.Count, result.PageNumber, result.TotalPages, result.TotalCount);

                    return result;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"Failed to search users: {response.StatusCode} - {errorContent}";

                _logger?.LogError("Advanced search API request failed: {ErrorMessage}", errorMessage);
                throw new HttpRequestException(errorMessage);
            }
            catch (HttpRequestException)
            {
                throw; // Re-throw HTTP exceptions as-is
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error in advanced user search: {ex.Message}";
                _logger?.LogError(ex, "Unexpected error in advanced user search");
                throw new Exception(errorMessage, ex);
            }
        }

        #endregion

        #region Individual User Operations

        /// <summary>
        /// Get user by ID
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>User information or null if not found</returns>
        public async Task<UserDto?> GetUserByIdAsync(int id)
        {
            try
            {
                _logger?.LogDebug("Getting user by ID: {UserId}", id);

                var response = await _httpClient.GetAsync($"/api/v1/user/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
                    _logger?.LogDebug("Successfully retrieved user {UserId}", id);
                    return user;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger?.LogDebug("User {UserId} not found", id);
                    return null;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"Failed to get user: {response.StatusCode} - {errorContent}";

                _logger?.LogError("Get user API request failed: {ErrorMessage}", errorMessage);
                throw new HttpRequestException(errorMessage);
            }
            catch (HttpRequestException)
            {
                throw; // Re-throw HTTP exceptions as-is
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error getting user {id}: {ex.Message}";
                _logger?.LogError(ex, "Unexpected error getting user {UserId}", id);
                throw new Exception(errorMessage, ex);
            }
        }

        /// <summary>
        /// Create a new user
        /// </summary>
        /// <param name="createUserDto">User creation data</param>
        /// <returns>Created user information</returns>
        public async Task<UserDto?> CreateUserAsync(CreateUserDto createUserDto)
        {
            try
            {
                if (createUserDto == null)
                    throw new ArgumentNullException(nameof(createUserDto));

                _logger?.LogDebug("Creating user with email {Email}", createUserDto.Email);

                var json = JsonSerializer.Serialize(createUserDto, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/v1/user", content);

                if (response.IsSuccessStatusCode)
                {
                    var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
                    _logger?.LogInformation("User created successfully with ID {UserId}", user?.Id);
                    return user;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"Failed to create user: {response.StatusCode} - {errorContent}";

                _logger?.LogError("Create user API request failed: {ErrorMessage}", errorMessage);
                throw new HttpRequestException(errorMessage);
            }
            catch (HttpRequestException)
            {
                throw; // Re-throw HTTP exceptions as-is
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error creating user: {ex.Message}";
                _logger?.LogError(ex, "Unexpected error creating user");
                throw new Exception(errorMessage, ex);
            }
        }

        /// <summary>
        /// Update an existing user
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="updateUserDto">User update data</param>
        /// <returns>Updated user information</returns>
        public async Task<UserDto?> UpdateUserAsync(int id, UpdateUserDto updateUserDto)
        {
            try
            {
                if (updateUserDto == null)
                    throw new ArgumentNullException(nameof(updateUserDto));

                _logger?.LogDebug("Updating user {UserId}", id);

                var json = JsonSerializer.Serialize(updateUserDto, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"/api/v1/user/{id}", content);

                if (response.IsSuccessStatusCode)
                {
                    var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
                    _logger?.LogInformation("User {UserId} updated successfully", id);
                    return user;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"Failed to update user: {response.StatusCode} - {errorContent}";

                _logger?.LogError("Update user API request failed: {ErrorMessage}", errorMessage);
                throw new HttpRequestException(errorMessage);
            }
            catch (HttpRequestException)
            {
                throw; // Re-throw HTTP exceptions as-is
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error updating user {id}: {ex.Message}";
                _logger?.LogError(ex, "Unexpected error updating user {UserId}", id);
                throw new Exception(errorMessage, ex);
            }
        }

        /// <summary>
        /// Delete a user
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        public async Task<bool> DeleteUserAsync(int id)
        {
            try
            {
                _logger?.LogDebug("Deleting user {UserId}", id);

                var response = await _httpClient.DeleteAsync($"/api/v1/user/{id}");

                var success = response.IsSuccessStatusCode;

                if (success)
                {
                    _logger?.LogInformation("User {UserId} deleted successfully", id);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogWarning("Delete user failed: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                }

                return success;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error deleting user {id}: {ex.Message}";
                _logger?.LogError(ex, "Unexpected error deleting user {UserId}", id);
                throw new Exception(errorMessage, ex);
            }
        }

        #endregion

        #region User Status Management

        /// <summary>
        /// Activate a user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        public async Task<bool> ActivateUserAsync(int id)
        {
            return await PerformUserActionAsync(id, "activate", "activating");
        }

        /// <summary>
        /// Deactivate a user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        public async Task<bool> DeactivateUserAsync(int id)
        {
            return await PerformUserActionAsync(id, "deactivate", "deactivating");
        }

        /// <summary>
        /// Lock a user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        public async Task<bool> LockUserAsync(int id)
        {
            return await PerformUserActionAsync(id, "lock", "locking");
        }

        /// <summary>
        /// Unlock a user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        public async Task<bool> UnlockUserAsync(int id)
        {
            return await PerformUserActionAsync(id, "unlock", "unlocking");
        }

        #endregion

        #region Avatar Management

        /// <summary>
        /// Update user avatar
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="avatarFileId">Avatar file ID</param>
        /// <returns>Updated user information</returns>
        public async Task<UserDto?> UpdateUserAvatarAsync(int id, int? avatarFileId)
        {
            try
            {
                _logger?.LogDebug("Updating avatar for user {UserId} with file {FileId}", id, avatarFileId);

                var requestData = new { AvatarFileId = avatarFileId };
                var json = JsonSerializer.Serialize(requestData, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"/api/v1/user/{id}/avatar", content);

                if (response.IsSuccessStatusCode)
                {
                    var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
                    _logger?.LogInformation("Avatar updated successfully for user {UserId}", id);
                    return user;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"Failed to update user avatar: {response.StatusCode} - {errorContent}";

                _logger?.LogError("Update avatar API request failed: {ErrorMessage}", errorMessage);
                throw new HttpRequestException(errorMessage);
            }
            catch (HttpRequestException)
            {
                throw; // Re-throw HTTP exceptions as-is
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error updating user avatar {id}: {ex.Message}";
                _logger?.LogError(ex, "Unexpected error updating avatar for user {UserId}", id);
                throw new Exception(errorMessage, ex);
            }
        }

        /// <summary>
        /// Remove user avatar
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Updated user information</returns>
        public async Task<UserDto?> RemoveUserAvatarAsync(int id)
        {
            try
            {
                _logger?.LogDebug("Removing avatar for user {UserId}", id);

                var response = await _httpClient.DeleteAsync($"/api/v1/user/{id}/avatar");

                if (response.IsSuccessStatusCode)
                {
                    var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
                    _logger?.LogInformation("Avatar removed successfully for user {UserId}", id);
                    return user;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"Failed to remove user avatar: {response.StatusCode} - {errorContent}";

                _logger?.LogError("Remove avatar API request failed: {ErrorMessage}", errorMessage);
                throw new HttpRequestException(errorMessage);
            }
            catch (HttpRequestException)
            {
                throw; // Re-throw HTTP exceptions as-is
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error removing user avatar {id}: {ex.Message}";
                _logger?.LogError(ex, "Unexpected error removing avatar for user {UserId}", id);
                throw new Exception(errorMessage, ex);
            }
        }

        #endregion

        #region Password Management

        /// <summary>
        /// Change user password
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="changePasswordDto">Password change data</param>
        /// <returns>Success status</returns>
        public async Task<bool> ChangePasswordAsync(int id, ChangePasswordDto changePasswordDto)
        {
            try
            {
                if (changePasswordDto == null)
                    throw new ArgumentNullException(nameof(changePasswordDto));

                _logger?.LogDebug("Changing password for user {UserId}", id);

                var json = JsonSerializer.Serialize(changePasswordDto, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/api/v1/user/{id}/change-password", content);

                var success = response.IsSuccessStatusCode;

                if (success)
                {
                    _logger?.LogInformation("Password changed successfully for user {UserId}", id);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogWarning("Password change failed for user {UserId}: {StatusCode} - {ErrorContent}",
                        id, response.StatusCode, errorContent);
                }

                return success;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error changing password for user {id}: {ex.Message}";
                _logger?.LogError(ex, "Unexpected error changing password for user {UserId}", id);
                throw new Exception(errorMessage, ex);
            }
        }

        /// <summary>
        /// Reset user password (admin action)
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        public async Task<bool> ResetPasswordAsync(int id)
        {
            return await PerformUserActionAsync(id, "reset-password", "resetting password for");
        }

        #endregion

        #region Email Verification

        /// <summary>
        /// Send email verification
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success status</returns>
        public async Task<bool> SendEmailVerificationAsync(int id)
        {
            return await PerformUserActionAsync(id, "send-verification", "sending email verification for");
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Perform a user action (activate, deactivate, lock, unlock, etc.)
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="action">Action name</param>
        /// <param name="actionDescription">Action description for logging</param>
        /// <returns>Success status</returns>
        private async Task<bool> PerformUserActionAsync(int id, string action, string actionDescription)
        {
            try
            {
                _logger?.LogDebug("Performing action '{Action}' for user {UserId}", action, id);

                var response = await _httpClient.PostAsync($"/api/v1/user/{id}/{action}", null);

                var success = response.IsSuccessStatusCode;

                if (success)
                {
                    _logger?.LogInformation("Successfully {ActionDescription} user {UserId}", actionDescription, id);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger?.LogWarning("Failed {ActionDescription} user {UserId}: {StatusCode} - {ErrorContent}",
                        actionDescription, id, response.StatusCode, errorContent);
                }

                return success;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error {actionDescription} user {id}: {ex.Message}";
                _logger?.LogError(ex, "Unexpected error {ActionDescription} user {UserId}", actionDescription, id);
                throw new Exception(errorMessage, ex);
            }
        }

        #endregion

        #region API Versioning Support

        /// <summary>
        /// Get users using API version 2.0 (if available)
        /// </summary>
        /// <param name="pageNumber">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="search">Search term</param>
        /// <returns>Paginated users</returns>
        public async Task<PaginatedResult<UserDto>> GetUsersV2Async(int pageNumber = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                _logger?.LogDebug("Getting paginated users using API v2.0: page {PageNumber}, size {PageSize}, search '{Search}'",
                    pageNumber, pageSize, search);

                // Build query string for v2.0 API
                var queryParams = new List<string>
                {
                    $"pageNumber={pageNumber}",
                    $"pageSize={pageSize}"
                };

                if (!string.IsNullOrWhiteSpace(search))
                {
                    queryParams.Add($"search={Uri.EscapeDataString(search)}");
                }

                var query = $"/api/v2/user?{string.Join("&", queryParams)}";

                var response = await _httpClient.GetAsync(query);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaginatedResult<UserDto>>(_jsonOptions);
                    return result ?? PaginatedResult<UserDto>.Empty(pageNumber, pageSize);
                }

                // Fallback to v1.0 if v2.0 is not available
                _logger?.LogDebug("API v2.0 not available, falling back to v1.0");
                return await GetUsersAsync(pageNumber, pageSize, search);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                // API v2.0 not found, fallback to v1.0
                _logger?.LogDebug("API v2.0 endpoint not found, falling back to v1.0");
                return await GetUsersAsync(pageNumber, pageSize, search);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error using API v2.0, falling back to v1.0");
                return await GetUsersAsync(pageNumber, pageSize, search);
            }
        }

        #endregion
    }
}