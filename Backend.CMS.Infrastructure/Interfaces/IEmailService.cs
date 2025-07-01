namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string resetUrl);
        Task<bool> SendEmailVerificationAsync(string email, string verificationToken, string verificationUrl);
        Task<bool> SendWelcomeEmailAsync(string email, string firstName, string temporaryPassword);
        Task<bool> SendAccountLockedEmailAsync(string email, string firstName);
        Task<bool> SendPasswordChangedEmailAsync(string email, string firstName);
        Task<bool> Send2FAEnabledEmailAsync(string email, string firstName);
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
    }
}