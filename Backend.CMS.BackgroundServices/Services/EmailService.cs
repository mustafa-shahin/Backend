using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Mail;
using System.Net;
using System.Text;

namespace Backend.CMS.BackgroundServices.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
        Task SendEmailAsync(string[] to, string subject, string body, bool isHtml = true);
        Task SendTemplatedEmailAsync(string to, string templateName, object model);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            await SendEmailAsync(new[] { to }, subject, body, isHtml);
        }

        public async Task SendEmailAsync(string[] to, string subject, string body, bool isHtml = true)
        {
            try
            {
                var smtpHost = _configuration["Email:Smtp:Host"];

                // Parse port manually instead of using GetValue
                var smtpPortString = _configuration["Email:Smtp:Port"];
                var smtpPort = !string.IsNullOrEmpty(smtpPortString) && int.TryParse(smtpPortString, out var port) ? port : 587;

                var smtpUsername = _configuration["Email:Smtp:Username"];
                var smtpPassword = _configuration["Email:Smtp:Password"];
                var fromEmail = _configuration["Email:FromEmail"];
                var fromName = _configuration["Email:FromName"];

                if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(fromEmail))
                {
                    _logger.LogWarning("Email configuration is incomplete. Skipping email send.");
                    return;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true
                };

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };

                foreach (var recipient in to)
                {
                    message.To.Add(recipient);
                }

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent successfully to {Recipients}", string.Join(", ", to));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recipients}", string.Join(", ", to));
                throw;
            }
        }

        public async Task SendTemplatedEmailAsync(string to, string templateName, object model)
        {
            try
            {
                var template = await LoadEmailTemplateAsync(templateName);
                var body = ProcessTemplate(template, model);
                var subject = ExtractSubjectFromTemplate(template);

                await SendEmailAsync(to, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send templated email {TemplateName} to {Recipient}", templateName, to);
                throw;
            }
        }

        private async Task<string> LoadEmailTemplateAsync(string templateName)
        {
            var templatePath = Path.Combine("EmailTemplates", $"{templateName}.html");

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Email template not found: {templatePath}");
            }

            return await File.ReadAllTextAsync(templatePath);
        }

        private string ProcessTemplate(string template, object model)
        {
            // Simple template processing - in production, consider using a proper template engine
            var properties = model.GetType().GetProperties();
            var result = template;

            foreach (var prop in properties)
            {
                var value = prop.GetValue(model)?.ToString() ?? "";
                result = result.Replace($"{{{{{prop.Name}}}}}", value);
            }

            return result;
        }

        private string ExtractSubjectFromTemplate(string template)
        {
            // Extract subject from template (assumes first line contains subject)
            var lines = template.Split('\n');
            var subjectLine = lines.FirstOrDefault(l => l.StartsWith("Subject:"));

            return subjectLine?.Substring(8).Trim() ?? "Notification";
        }
    }
}