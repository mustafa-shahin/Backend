

namespace Backend.CMS.Interfaces.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
        Task SendEmailAsync(string[] to, string subject, string body, bool isHtml = true);
        Task SendTemplatedEmailAsync(string to, string templateName, object model);
    }
}
