using System.ComponentModel.DataAnnotations;

namespace Frontend.Models
{
    /// <summary>
    /// Extended validation attributes for Blazor forms
    /// </summary>
    public class PageCreateValidationModel
    {
        [Required(ErrorMessage = "Page name is required")]
        [StringLength(255, ErrorMessage = "Page name cannot exceed 255 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Page title is required")]
        [StringLength(500, ErrorMessage = "Page title cannot exceed 500 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Page slug is required")]
        [StringLength(255, ErrorMessage = "Page slug cannot exceed 255 characters")]
        [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Slug can only contain lowercase letters, numbers, and hyphens")]
        public string Slug { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        [StringLength(255, ErrorMessage = "Meta title cannot exceed 255 characters")]
        public string? MetaTitle { get; set; }

        [StringLength(500, ErrorMessage = "Meta description cannot exceed 500 characters")]
        public string? MetaDescription { get; set; }

        [StringLength(500, ErrorMessage = "Meta keywords cannot exceed 500 characters")]
        public string? MetaKeywords { get; set; }

        public Backend.CMS.Domain.Enums.PageStatus Status { get; set; } = Backend.CMS.Domain.Enums.PageStatus.Draft;

        [Range(0, 999, ErrorMessage = "Priority must be between 0 and 999")]
        public int? Priority { get; set; }

        public bool RequiresLogin { get; set; }
        public bool AdminOnly { get; set; }
    }

    /// <summary>
    /// Custom validation attribute for slug format
    /// </summary>
    public class SlugValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is string slug && !string.IsNullOrEmpty(slug))
            {
                // Check if slug contains only valid characters
                if (!System.Text.RegularExpressions.Regex.IsMatch(slug, @"^[a-z0-9-]+$"))
                {
                    return new ValidationResult("Slug can only contain lowercase letters, numbers, and hyphens");
                }

                // Check if slug starts or ends with hyphen
                if (slug.StartsWith("-") || slug.EndsWith("-"))
                {
                    return new ValidationResult("Slug cannot start or end with a hyphen");
                }

                // Check for consecutive hyphens
                if (slug.Contains("--"))
                {
                    return new ValidationResult("Slug cannot contain consecutive hyphens");
                }
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Helper methods for form validation
    /// </summary>
    public static class ValidationHelpers
    {
        /// <summary>
        /// Converts a title to a URL-friendly slug
        /// </summary>
        public static string GenerateSlug(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            // Convert to lowercase and replace spaces with hyphens
            var slug = title.ToLowerInvariant()
                           .Replace(" ", "-")
                           .Replace("_", "-");

            // Remove invalid characters
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9-]", "");

            // Remove consecutive hyphens
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");

            // Remove leading/trailing hyphens
            slug = slug.Trim('-');

            return slug;
        }

        /// <summary>
        /// Validates if a string is a valid email format
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var emailAttribute = new EmailAddressAttribute();
                return emailAttribute.IsValid(email);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates password strength
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return (false, "Password is required");

            if (password.Length < 8)
                return (false, "Password must be at least 8 characters long");

            if (!password.Any(char.IsUpper))
                return (false, "Password must contain at least one uppercase letter");

            if (!password.Any(char.IsLower))
                return (false, "Password must contain at least one lowercase letter");

            if (!password.Any(char.IsDigit))
                return (false, "Password must contain at least one number");

            if (!password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c)))
                return (false, "Password must contain at least one special character");

            return (true, string.Empty);
        }
    }
}

