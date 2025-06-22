using Backend.CMS.Domain.Entities;
using Backend.CMS.Domain.Enums;
using System.Text.Json.Serialization;

namespace Backend.CMS.Application.Common
{
    public class UserSessionContext
    {
        private const int MAX_PERMISSIONS_COUNT = 1000;
        private const int MAX_CLAIMS_COUNT = 100;
        private const int MAX_PREFERENCES_COUNT = 500;

        // User data (may not be available in cached sessions)
        [JsonIgnore]
        public User? CurrentUser { get; set; }

        // Serializable user data for caching
        public int? UserId { get; set; }
        public string? Email { get; set; }
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public UserRole? Role { get; set; }
        public FileEntity? Picture { get; set; }
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

        // User preferences and settings 
        private Dictionary<string, object> _preferences = new();
        public Dictionary<string, object> Preferences
        {
            get => _preferences;
            set => _preferences = ValidateAndLimitDictionary(value, MAX_PREFERENCES_COUNT, nameof(Preferences));
        }

        // Role checks
        [JsonIgnore]
        public bool IsAdmin => (CurrentUser?.Role ?? Role) == UserRole.Admin;

        [JsonIgnore]
        public bool IsDev => (CurrentUser?.Role ?? Role) == UserRole.Dev;

        [JsonIgnore]
        public bool IsCustomer => (CurrentUser?.Role ?? Role) == UserRole.Customer;

        [JsonIgnore]
        public bool IsAdminOrDev => IsAdmin || IsDev;

        // Related entities 
        [JsonIgnore]
        public List<Address> Addresses => CurrentUser?.Addresses?.Where(a => !a.IsDeleted).ToList() ?? new List<Address>();

        [JsonIgnore]
        public List<ContactDetails> ContactDetails => CurrentUser?.ContactDetails?.Where(c => !c.IsDeleted).ToList() ?? new List<ContactDetails>();

        // Primary contact info
        [JsonIgnore]
        public Address? PrimaryAddress => Addresses.FirstOrDefault(a => a.IsDefault) ?? Addresses.FirstOrDefault();

        [JsonIgnore]
        public ContactDetails? PrimaryContactDetails => ContactDetails.FirstOrDefault(c => c.IsDefault) ?? ContactDetails.FirstOrDefault();

        // Session metadata 
        public DateTime SessionStartTime { get; set; } = DateTime.UtcNow;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? SessionId { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public string? RequestId { get; set; }
        public string? CorrelationId { get; set; }

        // Authorization context 
        private List<string> _permissions = new();
        public List<string> Permissions
        {
            get => _permissions;
            set => _permissions = ValidateAndLimitList(value, MAX_PERMISSIONS_COUNT, nameof(Permissions));
        }

        private Dictionary<string, object> _claims = new();
        public Dictionary<string, object> Claims
        {
            get => _claims;
            set => _claims = ValidateAndLimitDictionary(value, MAX_CLAIMS_COUNT, nameof(Claims));
        }

        // Audit information
        [JsonIgnore]
        public string AuditUserName => FullName.IsNullOrEmpty() ? "System" : FullName;

        [JsonIgnore]
        public string AuditUserId => (CurrentUser?.Id ?? UserId)?.ToString() ?? "Unknown";

        // Helper methods
        public bool HasPermission(string permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
                return false;

            return Permissions.Contains(permission) || IsAdminOrDev;
        }

        public bool HasRole(params UserRole[] roles)
        {
            var userRole = CurrentUser?.Role ?? Role;
            return userRole.HasValue && roles.Contains(userRole.Value);
        }

        public bool HasClaim(string claimType, string? claimValue = null)
        {
            if (string.IsNullOrWhiteSpace(claimType))
                return false;

            if (!Claims.ContainsKey(claimType))
                return false;

            if (claimValue == null)
                return true;

            return Claims[claimType]?.ToString() == claimValue;
        }

        public void UpdateLastActivity()
        {
            LastActivity = DateTime.UtcNow;
        }

        public bool IsSessionExpired(TimeSpan sessionTimeout)
        {
            return DateTime.UtcNow - LastActivity > sessionTimeout;
        }

        public TimeSpan GetSessionAge()
        {
            return DateTime.UtcNow - SessionStartTime;
        }

        public TimeSpan GetIdleTime()
        {
            return DateTime.UtcNow - LastActivity;
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
                Picture = CurrentUser.Picture;
                IsActive = CurrentUser.IsActive;
                IsLocked = CurrentUser.IsLocked;
            }
        }

        // Preference management with validation
        public T? GetPreference<T>(string key, T? defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(key) || !Preferences.ContainsKey(key))
                return defaultValue;

            try
            {
                var value = Preferences[key];
                if (value is T directValue)
                    return directValue;

                if (value == null)
                    return defaultValue;

                var targetType = typeof(T);
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                var convertedValue = Convert.ChangeType(value, underlyingType);
                return (T)convertedValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public void SetPreference<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Preference key cannot be null or empty", nameof(key));

            if (Preferences.Count >= MAX_PREFERENCES_COUNT && !Preferences.ContainsKey(key))
                throw new InvalidOperationException($"Maximum number of preferences ({MAX_PREFERENCES_COUNT}) exceeded");

            if (value != null)
            {
                Preferences[key] = value;
            }
            else
            {
                Preferences.Remove(key);
            }
        }

        public void RemovePreference(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Preferences.Remove(key);
            }
        }

        public void ClearPreferences()
        {
            Preferences.Clear();
        }

        // Permission management
        public void AddPermission(string permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
                return;

            if (!Permissions.Contains(permission))
            {
                if (Permissions.Count >= MAX_PERMISSIONS_COUNT)
                    throw new InvalidOperationException($"Maximum number of permissions ({MAX_PERMISSIONS_COUNT}) exceeded");

                Permissions.Add(permission);
            }
        }

        public void RemovePermission(string permission)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                Permissions.Remove(permission);
            }
        }

        public void SetPermissions(IEnumerable<string> permissions)
        {
            var validPermissions = permissions?.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList() ?? new List<string>();
            Permissions = ValidateAndLimitList(validPermissions, MAX_PERMISSIONS_COUNT, nameof(Permissions));
        }

        // Validation and consistency checks
        public bool IsValid()
        {
            // Basic validation
            if (IsAuthenticated && !UserId.HasValue)
                return false;

            if (IsAuthenticated && string.IsNullOrWhiteSpace(Email))
                return false;

            if (SessionStartTime > DateTime.UtcNow)
                return false;

            if (LastActivity < SessionStartTime)
                return false;

            return true;
        }

        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (IsAuthenticated && !UserId.HasValue)
                errors.Add("Authenticated session must have a valid user ID");

            if (IsAuthenticated && string.IsNullOrWhiteSpace(Email))
                errors.Add("Authenticated session must have a valid email");

            if (SessionStartTime > DateTime.UtcNow)
                errors.Add("Session start time cannot be in the future");

            if (LastActivity < SessionStartTime)
                errors.Add("Last activity cannot be before session start time");

            if (Permissions.Count > MAX_PERMISSIONS_COUNT)
                errors.Add($"Too many permissions ({Permissions.Count} > {MAX_PERMISSIONS_COUNT})");

            if (Claims.Count > MAX_CLAIMS_COUNT)
                errors.Add($"Too many claims ({Claims.Count} > {MAX_CLAIMS_COUNT})");

            if (Preferences.Count > MAX_PREFERENCES_COUNT)
                errors.Add($"Too many preferences ({Preferences.Count} > {MAX_PREFERENCES_COUNT})");

            return errors;
        }

        // Create session from authenticated user
        public static UserSessionContext CreateFromUser(User user, string? ipAddress = null, string? userAgent = null)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

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
                    ["firstName"] = user.FirstName ?? string.Empty,
                    ["lastName"] = user.LastName ?? string.Empty
                }
            };

            // Sync serializable data
            session.SyncFromCurrentUser();

            return session;
        }

        // Create session from cached data 
        public static UserSessionContext CreateFromCachedData(
            int userId,
            string email,
            string username,
            string firstName,
            string lastName,
            UserRole role,
            Dictionary<string, object>? preferences = null,
            List<string>? permissions = null,
            string? sessionId = null)
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
                SessionId = sessionId ?? Guid.NewGuid().ToString(),
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = userId.ToString(),
                    ["email"] = email,
                    ["role"] = role.ToString(),
                    ["firstName"] = firstName ?? string.Empty,
                    ["lastName"] = lastName ?? string.Empty
                }
            };
        }

        // Helper methods for validation
        private static List<T> ValidateAndLimitList<T>(IEnumerable<T>? items, int maxCount, string propertyName)
        {
            if (items == null)
                return new List<T>();

            var list = items.ToList();
            if (list.Count > maxCount)
                throw new ArgumentException($"{propertyName} cannot contain more than {maxCount} items", propertyName);

            return list;
        }

        private static Dictionary<string, object> ValidateAndLimitDictionary(Dictionary<string, object>? dictionary, int maxCount, string propertyName)
        {
            if (dictionary == null)
                return new Dictionary<string, object>();

            if (dictionary.Count > maxCount)
                throw new ArgumentException($"{propertyName} cannot contain more than {maxCount} items", propertyName);

            return new Dictionary<string, object>(dictionary);
        }

        // Override ToString for debugging
        public override string ToString()
        {
            var status = IsAuthenticated ? $"Authenticated as {Email}" : "Anonymous";
            var sessionAge = GetSessionAge();
            var idleTime = GetIdleTime();

            return $"UserSession: {status}, Age: {sessionAge:hh\\:mm\\:ss}, Idle: {idleTime:hh\\:mm\\:ss}, SessionId: {SessionId}";
        }
    }

    // Extension method for string null/empty checks
    public static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string? value)
        {
            return string.IsNullOrEmpty(value);
        }

        public static bool IsNullOrWhiteSpace(this string? value)
        {
            return string.IsNullOrWhiteSpace(value);
        }
    }
}