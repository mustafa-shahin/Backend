using Backend.CMS.Domain.Entities;
namespace Backend.CMS.Application.DTOs
{
    public class SocialLoginDto
    {
        public string Provider { get; set; } = string.Empty; // "Google" or "Facebook"
        public string AccessToken { get; set; } = string.Empty;
        public string? IdToken { get; set; } // For Google
    }

    public class SocialAuthCallbackDto
    {
        public string Code { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string? ErrorDescription { get; set; }
    }

    public class SocialUserInfoDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public FileEntity? Picture { get; set; }
        public bool EmailVerified { get; set; }
        public string Provider { get; set; } = string.Empty;
    }

    public class LinkSocialAccountDto
    {
        public string Provider { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
    }

    public class UnlinkSocialAccountDto
    {
        public string Provider { get; set; } = string.Empty;
    }
}
