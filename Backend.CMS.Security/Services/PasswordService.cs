using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.CMS.Security.Services
{
    public class PasswordService : IPasswordService
    {
        private readonly ILogger<PasswordService> _logger;
        private readonly IConfiguration _configuration;

        public PasswordService(ILogger<PasswordService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            try
            {
                var workFactor = _configuration.GetValue<int>("Security:PasswordPolicy:WorkFactor", 12);
                return BCrypt.Net.BCrypt.HashPassword(password, workFactor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to hash password");
                throw;
            }
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify password");
                return false;
            }
        }

        public bool IsPasswordStrong(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            var minLength = _configuration.GetValue<int>("Security:PasswordPolicy:MinLength", 8);
            var maxLength = _configuration.GetValue<int>("Security:PasswordPolicy:MaxLength", 128);
            var requireUppercase = _configuration.GetValue<bool>("Security:PasswordPolicy:RequireUppercase", true);
            var requireLowercase = _configuration.GetValue<bool>("Security:PasswordPolicy:RequireLowercase", true);
            var requireDigit = _configuration.GetValue<bool>("Security:PasswordPolicy:RequireDigit", true);
            var requireSpecialChar = _configuration.GetValue<bool>("Security:PasswordPolicy:RequireSpecialCharacter", true);
            var minUniqueChars = _configuration.GetValue<int>("Security:PasswordPolicy:MinUniqueCharacters", 4);

            // Length check
            if (password.Length < minLength || password.Length > maxLength)
                return false;

            // Character requirements
            if (requireUppercase && !Regex.IsMatch(password, @"[A-Z]"))
                return false;

            if (requireLowercase && !Regex.IsMatch(password, @"[a-z]"))
                return false;

            if (requireDigit && !Regex.IsMatch(password, @"\d"))
                return false;

            if (requireSpecialChar && !Regex.IsMatch(password, @"[!@#$%^&*(),.?""{}|<>]"))
                return false;

            // Unique characters check
            if (password.Distinct().Count() < minUniqueChars)
                return false;

            // Common password patterns
            if (IsCommonPassword(password))
                return false;

            return true;
        }

        public string GenerateRandomPassword(int length = 12)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            
            var password = new StringBuilder();
            
            // Ensure at least one character from each required category
            password.Append(GetRandomChar("ABCDEFGHJKLMNOPQRSTUVWXYZ", random));
            password.Append(GetRandomChar("abcdefghijklmnopqrstuvwxyz", random));
            password.Append(GetRandomChar("0123456789", random));
            password.Append(GetRandomChar("!@#$%^&*", random));
            
            // Fill the rest randomly
            for (int i = 4; i < length; i++)
            {
                password.Append(validChars[random.Next(validChars.Length)]);
            }
            
            // Shuffle the password
            return new string(password.ToString().OrderBy(x => random.Next()).ToArray());
        }

        private char GetRandomChar(string chars, Random random)
        {
            return chars[random.Next(chars.Length)];
        }

        private bool IsCommonPassword(string password)
        {
            var commonPasswords = new[]
            {
                "password", "123456", "password123", "admin", "qwerty",
                "letmein", "welcome", "monkey", "1234567890"
            };

            return commonPasswords.Any(common => 
                password.Equals(common, StringComparison.OrdinalIgnoreCase) ||
                password.Contains(common, StringComparison.OrdinalIgnoreCase));
        }

        public PasswordStrengthResult AnalyzePasswordStrength(string password)
        {
            var result = new PasswordStrengthResult();

            if (string.IsNullOrEmpty(password))
            {
                result.Score = 0;
                result.Feedback.Add("Password is required");
                return result;
            }

            var score = 0;
            var feedback = new List<string>();

            // Length scoring
            if (password.Length >= 8) score += 1;
            else feedback.Add("Use at least 8 characters");

            if (password.Length >= 12) score += 1;
            if (password.Length >= 16) score += 1;

            // Character variety
            if (Regex.IsMatch(password, @"[a-z]")) score += 1;
            else feedback.Add("Add lowercase letters");

            if (Regex.IsMatch(password, @"[A-Z]")) score += 1;
            else feedback.Add("Add uppercase letters");

            if (Regex.IsMatch(password, @"\d")) score += 1;
            else feedback.Add("Add numbers");

            if (Regex.IsMatch(password, @"[!@#$%^&*(),.?""{}|<>]")) score += 1;
            else feedback.Add("Add special characters");

            // Complexity bonuses
            if (password.Distinct().Count() >= password.Length * 0.7) score += 1;
            if (!IsCommonPassword(password)) score += 1;

            result.Score = Math.Min(score, 5);
            result.Strength = result.Score switch
            {
                0 or 1 => PasswordStrength.VeryWeak,
                2 => PasswordStrength.Weak,
                3 => PasswordStrength.Fair,
                4 => PasswordStrength.Good,
                5 => PasswordStrength.Strong,
                _ => PasswordStrength.VeryWeak
            };
            result.Feedback = feedback;

            return result;
        }
    }

    public class PasswordStrengthResult
    {
        public int Score { get; set; }
        public PasswordStrength Strength { get; set; }
        public List<string> Feedback { get; set; } = [];
    }

    public enum PasswordStrength
    {
        VeryWeak,
        Weak,
        Fair,
        Good,
        Strong
    }
}