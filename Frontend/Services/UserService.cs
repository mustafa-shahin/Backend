using Backend.CMS.Application.DTOs;
using Frontend.Interface;
using System.Net.Http.Json;
using System.Text.Json;
namespace Frontend.Services
{
    public class UserService : IUserService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public UserService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<UserDto?> GetCurrentUserAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/auth/me");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting current user: {ex.Message}", ex);
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

        public async Task<bool> ChangePasswordAsync(ChangePasswordDto changePasswordDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/user/change-password", changePasswordDto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error changing password: {ex.Message}", ex);
            }
        }
    }
}
