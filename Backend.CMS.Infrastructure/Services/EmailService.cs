using Backend.CMS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Backend.CMS.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            _smtpUsername = _configuration["EmailSettings:SmtpUsername"] ?? "";
            _smtpPassword = _configuration["EmailSettings:SmtpPassword"] ?? "";
            _fromEmail = _configuration["EmailSettings:FromEmail"] ?? "";
            _fromName = _configuration["EmailSettings:FromName"] ?? "Backend CMS";
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string resetUrl)
        {
            var subject = "Password Reset Request";
            var body = GeneratePasswordResetEmailTemplate(resetToken, resetUrl);
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendEmailVerificationAsync(string email, string verificationToken, string verificationUrl)
        {
            var subject = "Verify Your Email Address";
            var body = GenerateEmailVerificationTemplate(verificationToken, verificationUrl);
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendWelcomeEmailAsync(string email, string firstName, string temporaryPassword)
        {
            var subject = "Welcome to Backend CMS";
            var body = GenerateWelcomeEmailTemplate(firstName, temporaryPassword);
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendAccountLockedEmailAsync(string email, string firstName)
        {
            var subject = "Account Security Alert - Account Locked";
            var body = GenerateAccountLockedEmailTemplate(firstName);
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendPasswordChangedEmailAsync(string email, string firstName)
        {
            var subject = "Password Changed Successfully";
            var body = GeneratePasswordChangedEmailTemplate(firstName);
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> Send2FAEnabledEmailAsync(string email, string firstName)
        {
            var subject = "Two-Factor Authentication Enabled";
            var body = Generate2FAEnabledEmailTemplate(firstName);
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                mailMessage.To.Add(to);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully to {Email}", to);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", to);
                return false;
            }
        }

        private string GeneratePasswordResetEmailTemplate(string resetToken, string resetUrl)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>Password Reset</title>
                </head>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2c3e50;'>Password Reset Request</h2>
                        <p>You have requested to reset your password. Click the link below to reset your password:</p>
                        <div style='margin: 30px 0;'>
                            <a href='{resetUrl}?token={resetToken}' 
                               style='background-color: #3498db; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; display: inline-block;'>
                                Reset Password
                            </a>
                        </div>
                        <p>This link will expire in 24 hours for security reasons.</p>
                        <p>If you did not request this password reset, please ignore this email.</p>
                        <hr style='margin: 30px 0; border: none; border-top: 1px solid #eee;'>
                        <p style='font-size: 12px; color: #666;'>
                            This is an automated message. Please do not reply to this email.
                        </p>
                    </div>
                </body>
                </html>";
        }

        private string GenerateEmailVerificationTemplate(string verificationToken, string verificationUrl)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>Email Verification</title>
                </head>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2c3e50;'>Verify Your Email Address</h2>
                        <p>Thank you for registering! Please verify your email address by clicking the link below:</p>
                        <div style='margin: 30px 0;'>
                            <a href='{verificationUrl}?token={verificationToken}' 
                               style='background-color: #27ae60; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; display: inline-block;'>
                                Verify Email
                            </a>
                        </div>
                        <p>This link will expire in 7 days.</p>
                        <hr style='margin: 30px 0; border: none; border-top: 1px solid #eee;'>
                        <p style='font-size: 12px; color: #666;'>
                            This is an automated message. Please do not reply to this email.
                        </p>
                    </div>
                </body>
                </html>";
        }

        private string GenerateWelcomeEmailTemplate(string firstName, string temporaryPassword)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>Welcome</title>
                </head>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2c3e50;'>Welcome to Backend CMS, {firstName}!</h2>
                        <p>Your account has been created successfully. Here are your login details:</p>
                        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 4px; margin: 20px 0;'>
                            <p><strong>Temporary Password:</strong> {temporaryPassword}</p>
                        </div>
                        <p><strong>Important:</strong> Please change your password after your first login for security reasons.</p>
                        <hr style='margin: 30px 0; border: none; border-top: 1px solid #eee;'>
                        <p style='font-size: 12px; color: #666;'>
                            This is an automated message. Please do not reply to this email.
                        </p>
                    </div>
                </body>
                </html>";
        }

        private string GenerateAccountLockedEmailTemplate(string firstName)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>Account Locked</title>
                </head>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #e74c3c;'>Account Security Alert</h2>
                        <p>Hello {firstName},</p>
                        <p>Your account has been temporarily locked due to multiple failed login attempts.</p>
                        <p>If this was you, please wait 30 minutes before attempting to log in again.</p>
                        <p>If you did not attempt to log in, please contact support immediately.</p>
                        <hr style='margin: 30px 0; border: none; border-top: 1px solid #eee;'>
                        <p style='font-size: 12px; color: #666;'>
                            This is an automated security message. Please do not reply to this email.
                        </p>
                    </div>
                </body>
                </html>";
        }

        private string GeneratePasswordChangedEmailTemplate(string firstName)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>Password Changed</title>
                </head>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #27ae60;'>Password Changed Successfully</h2>
                        <p>Hello {firstName},</p>
                        <p>Your password has been changed successfully.</p>
                        <p>If you did not make this change, please contact support immediately.</p>
                        <hr style='margin: 30px 0; border: none; border-top: 1px solid #eee;'>
                        <p style='font-size: 12px; color: #666;'>
                            This is an automated security message. Please do not reply to this email.
                        </p>
                    </div>
                </body>
                </html>";
        }

        private string Generate2FAEnabledEmailTemplate(string firstName)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>2FA Enabled</title>
                </head>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #27ae60;'>Two-Factor Authentication Enabled</h2>
                        <p>Hello {firstName},</p>
                        <p>Two-factor authentication has been successfully enabled on your account.</p>
                        <p>Your account is now more secure. You will need your authenticator app to log in.</p>
                        <p>If you did not enable this feature, please contact support immediately.</p>
                        <hr style='margin: 30px 0; border: none; border-top: 1px solid #eee;'>
                        <p style='font-size: 12px; color: #666;'>
                            This is an automated security message. Please do not reply to this email.
                        </p>
                    </div>
                </body>
                </html>";
        }
    }
}