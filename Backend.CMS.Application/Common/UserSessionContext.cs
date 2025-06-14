using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using System.Text.Json.Serialization;

namespace Backend.CMS.Application.Common
{
    public class UserSessionContext
    {
        public User? CurrentUser { get; set; }

        // Serializable user data for caching
        public int? UserId { get; set; }
        public string? Email { get; set; }
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public UserRole? Role { get; set; }
        public FileEntity? Avatar { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public string? Timezone { get; set; }
        public string? Language { get; set; }

        // Computed properties
        [JsonIgnore]
        public string FullName => CurrentUser?.FullName ??
            (!string.IsNullOrEmpty(FirstName) || !string.IsNullOrEmpty(LastName)
                ? $"{FirstName} {LastName}".Trim()
                : string.Empty);

        [JsonIgnore]
        public bool IsAuthenticated => CurrentUser != null || UserId.HasValue;

        // User preferences and settings (serializable)
        public Dictionary<string, object> Preferences { get; set; } = new();

        // Role checks
        [JsonIgnore]
        public bool IsAdmin => (CurrentUser?.Role ?? Role) == UserRole.Admin;

        [JsonIgnore]
        public bool IsDev => (CurrentUser?.Role ?? Role) == UserRole.Dev;

        [JsonIgnore]
        public bool IsCustomer => (CurrentUser?.Role ?? Role) == UserRole.Customer;

        [JsonIgnore]
        public bool IsAdminOrDev => IsAdmin || IsDev;

        // Related entities (only loaded when CurrentUser is available)
        [JsonIgnore]
        public List<Address> Addresses => CurrentUser?.Addresses?.Where(a => !a.IsDeleted).ToList() ?? new List<Address>();

        [JsonIgnore]
        public List<ContactDetails> ContactDetails => CurrentUser?.ContactDetails?.Where(c => !c.IsDeleted).ToList() ?? new List<ContactDetails>();

        // Primary contact info
        [JsonIgnore]
        public Address? PrimaryAddress => Addresses.FirstOrDefault(a => a.IsDefault) ?? Addresses.FirstOrDefault();

        [JsonIgnore]
        public ContactDetails? PrimaryContactDetails => ContactDetails.FirstOrDefault(c => c.IsDefault) ?? ContactDetails.FirstOrDefault();

        // Session metadata (always available and serializable)
        public DateTime SessionStartTime { get; set; } = DateTime.UtcNow;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? SessionId { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public string? RequestId { get; set; }
        public string? CorrelationId { get; set; }

        // Authorization context (serializable)
        public List<string> Permissions { get; set; } = new();
        public Dictionary<string, object> Claims { get; set; } = new();

        // Audit information
        [JsonIgnore]
        public string AuditUserName => FullName.IsNullOrEmpty() ? "System" : FullName;

        [JsonIgnore]
        public string AuditUserId => (CurrentUser?.Id ?? UserId)?.ToString() ?? "Unknown";

        // Helper methods
        public bool HasPermission(string permission)
        {
            return Permissions.Contains(permission) || IsAdminOrDev;
        }

        public bool HasRole(params UserRole[] roles)
        {
            var userRole = CurrentUser?.Role ?? Role;
            return userRole.HasValue && roles.Contains(userRole.Value);
        }

        public bool HasClaim(string claimType, string? claimValue = null)
        {
            if (!Claims.ContainsKey(claimType))
                return false;

            if (claimValue == null)
                return true;

            return Claims[claimType]?.ToString() == claimValue;
        }

        public T GetPreference<T>(string key, T defaultValue = default!)
        {
            // Try CurrentUser preferences first (if available)
            if (CurrentUser?.Preferences != null && CurrentUser.Preferences.ContainsKey(key))
            {
                try
                {
                    var value = CurrentUser.Preferences[key];
                    return ConvertPreferenceValue<T>(value, defaultValue);
                }
                catch
                {
                    // Fall through to cached preferences
                }
            }

            // Fall back to cached preferences
            if (Preferences.ContainsKey(key))
            {
                try
                {
                    var value = Preferences[key];
                    return ConvertPreferenceValue<T>(value, defaultValue);
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        public void SetPreference<T>(string key, T value)
        {
            // Update both CurrentUser and cached preferences
            if (CurrentUser?.Preferences != null)
            {
                if (value != null)
                    CurrentUser.Preferences[key] = value;
                else
                    CurrentUser.Preferences.Remove(key);
            }

            // Update cached preferences
            if (value != null)
                Preferences[key] = value;
            else
                Preferences.Remove(key);
        }

        public void UpdateLastActivity()
        {
            LastActivity = DateTime.UtcNow;
        }

        public bool IsSessionExpired(TimeSpan sessionTimeout)
        {
            return DateTime.UtcNow - LastActivity > sessionTimeout;
        }

        // Sync user data from CurrentUser to serializable properties
        public void SyncFromCurrentUser()
        {
            if (CurrentUser != null)
            {
                UserId = CurrentUser.Id;
                Email = CurrentUser.Email;
                Username = CurrentUser.Username;
                FirstName = CurrentUser.FirstName;
                LastName = CurrentUser.LastName;
                Role = CurrentUser.Role;
                Avatar = CurrentUser.AvatarFile;
                IsActive = CurrentUser.IsActive;
                IsLocked = CurrentUser.IsLocked;
                Timezone = CurrentUser.Timezone;
                Language = CurrentUser.Language;

                // Sync preferences
                if (CurrentUser.Preferences != null)
                {
                    Preferences = new Dictionary<string, object>(CurrentUser.Preferences);
                }
            }
        }

        // Create session from authenticated user
        public static UserSessionContext CreateFromUser(User user, string? ipAddress = null, string? userAgent = null)
        {
            var session = new UserSessionContext
            {
                CurrentUser = user,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                SessionId = Guid.NewGuid().ToString(),
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = user.Id.ToString(),
                    ["email"] = user.Email,
                    ["role"] = user.Role.ToString(),
                    ["firstName"] = user.FirstName,
                    ["lastName"] = user.LastName
                }
            };

            // Sync serializable data
            session.SyncFromCurrentUser();

            return session;
        }

        // Create session from cached data (without CurrentUser)
        public static UserSessionContext CreateFromCachedData(
            int userId,
            string email,
            string username,
            string firstName,
            string lastName,
            UserRole role,
            Dictionary<string, object> preferences,
            List<string> permissions,
            string sessionId)
        {
            return new UserSessionContext
            {
                CurrentUser = null, // Will be loaded later if needed
                UserId = userId,
                Email = email,
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                Role = role,
                Preferences = preferences ?? new Dictionary<string, object>(),
                Permissions = permissions ?? new List<string>(),
                SessionId = sessionId,
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = userId.ToString(),
                    ["email"] = email,
                    ["role"] = role.ToString(),
                    ["firstName"] = firstName,
                    ["lastName"] = lastName
                }
            };
        }

        private T ConvertPreferenceValue<T>(object value, T defaultValue)
        {
            if (value is T directValue)
                return directValue;

            if (value == null)
                return defaultValue;

            var targetType = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var convertedValue = Convert.ChangeType(value, underlyingType);
            return (T)convertedValue;
        }
    }

    // Extension method for string null/empty checks
    public static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string? value)
        {
            return string.IsNullOrEmpty(value);
        }
    }
}