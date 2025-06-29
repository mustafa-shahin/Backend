using Backend.CMS.Application.DTOs;
using Frontend.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;

namespace Frontend.Services
{
    public class UsersService : IUsersService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public UsersService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<PagedResult<UserDto>> GetUsersAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                var query = $"/api/user?page={page}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(search))
                {
                    query += $"&search={Uri.EscapeDataString(search)}";
                }

                var response = await _httpClient.GetAsync(query);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PagedResult<UserDto>>(_jsonOptions);
                    return result ?? new PagedResult<UserDto>();
                }

                throw new HttpRequestException($"Failed to get users: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting users: {ex.Message}", ex);
            }
        }

        public async Task<UserDto?> GetUserByIdAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/user/{id}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw new HttpRequestException($"Failed to get user: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting user {id}: {ex.Message}", ex);
            }
        }

        public async Task<UserDto?> CreateUserAsync(CreateUserDto createUserDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/user", createUserDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create user: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating user: {ex.Message}", ex);
            }
        }

        public async Task<UserDto?> UpdateUserAsync(int id, UpdateUserDto updateUserDto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/user/{id}", updateUserDto, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update user: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating user {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/user/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting user {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> ActivateUserAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/user/{id}/activate", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error activating user {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeactivateUserAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/user/{id}/deactivate", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deactivating user {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> LockUserAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/user/{id}/lock", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error locking user {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> UnlockUserAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/user/{id}/unlock", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error unlocking user {id}: {ex.Message}", ex);
            }
        }

        public async Task<UserDto?> UpdateUserAvatarAsync(int id, int? avatarFileId)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/user/{id}/avatar",
                    new { AvatarFileId = avatarFileId }, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update user avatar: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating user avatar {id}: {ex.Message}", ex);
            }
        }

        public async Task<UserDto?> RemoveUserAvatarAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/user/{id}/avatar");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to remove user avatar: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error removing user avatar {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> ChangePasswordAsync(int id, ChangePasswordDto changePasswordDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"/api/user/{id}/change-password",
                    changePasswordDto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error changing password for user {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> ResetPasswordAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/user/{id}/reset-password", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error resetting password for user {id}: {ex.Message}", ex);
            }
        }

        public async Task<bool> SendEmailVerificationAsync(int id)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/user/{id}/send-verification", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error sending email verification for user {id}: {ex.Message}", ex);
            }
        }
    }
}