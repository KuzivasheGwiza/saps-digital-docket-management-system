using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SAPS.DDMS.API.Services
{
    public interface IEmailService
    {
        Task<bool> SendActivationEmailAsync(string email, string fullName, string activationLink);
        Task<bool> SendNotificationEmailAsync(string email, string subject, string message);
        Task<bool> SendPasswordResetEmailAsync(string email, string resetLink);
        Task<bool> SendPasswordResetOtpEmailAsync(string email, string fullName, string otpCode, int expiryMinutes);
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

        public async Task<bool> SendActivationEmailAsync(string email, string fullName, string activationLink)
        {
            try
            {
                var smtpServer = GetSetting("Email:SmtpServer", "SmtpSettings:Host");
                var smtpPort = GetSetting("Email:SmtpPort", "SmtpSettings:Port");
                var username = GetSetting("Email:Username", "SmtpSettings:Username");
                var password = GetSetting("Email:Password", "SmtpSettings:Password");
                var fromName = GetSetting("Email:FromName", "SmtpSettings:FromName") ?? "SAPS DDMS";
                var fromEmail = GetSetting("Email:FromEmail", "SmtpSettings:FromEmail") ?? username;

                if (string.IsNullOrWhiteSpace(smtpServer) ||
                    string.IsNullOrWhiteSpace(smtpPort) ||
                    string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(password) ||
                    string.IsNullOrWhiteSpace(fromEmail))
                {
                    _logger.LogError("SMTP settings are incomplete. Activation email was not sent to {Email}", email);
                    return false;
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(new MailboxAddress(fullName, email));
                message.Subject = "Activate Your SAPS DDMS Account";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; margin: 0; padding: 20px; }}
                            .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.1); }}
                            .header {{ background: #ffffff; color: #0b3a67; border-top: 6px solid #f5b51b; border-bottom: 1px solid #dbe5ef; padding: 30px; text-align: center; }}
                            .content {{ color: #243b53; padding: 30px; }}
                            .button {{ display: inline-block; padding: 12px 24px; background-color: #0b3a67; color: #ffffff !important; text-decoration: none; border-radius: 6px; margin: 20px 0; font-weight: bold; }}
                            .footer {{ background: #f8f9fa; padding: 20px; text-align: center; font-size: 12px; color: #666; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1 style='margin: 0;'>SAPS DDMS</h1>
                                <p style='margin: 10px 0 0; opacity: 0.9;'>Digital Docket Management System</p>
                            </div>
                            <div class='content'>
                                <h2>Welcome, {fullName}!</h2>
                                <p>Your account has been created in the SAPS Digital Docket Management System.</p>
                                <p>Please click the button below to activate your account and set your password:</p>
                                <div style='text-align: center;'>
                                    <a href='{activationLink}' class='button'>Activate Account</a>
                                </div>
                                <p>This link will expire in <strong>72 hours</strong>.</p>
                                <hr style='margin: 20px 0;'>
                                <p style='color: #666; font-size: 12px;'>If you did not request this, please ignore this email.</p>
                            </div>
                            <div class='footer'>
                                <p>&copy; 2026 SAPS Digital Docket Management System. All rights reserved.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                client.Timeout = 30000;
                await client.ConnectAsync(smtpServer, int.Parse(smtpPort), SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Activation email sent to {email}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send activation email to {email}");
                return false;
            }
        }

        public async Task<bool> SendNotificationEmailAsync(string email, string subject, string message)
        {
            try
            {
                var smtpServer = GetSetting("Email:SmtpServer", "SmtpSettings:Host");
                var smtpPort = GetSetting("Email:SmtpPort", "SmtpSettings:Port");
                var username = GetSetting("Email:Username", "SmtpSettings:Username");
                var password = GetSetting("Email:Password", "SmtpSettings:Password");
                var fromName = GetSetting("Email:FromName", "SmtpSettings:FromName") ?? "SAPS DDMS";
                var fromEmail = GetSetting("Email:FromEmail", "SmtpSettings:FromEmail") ?? username;

                if (string.IsNullOrWhiteSpace(smtpServer) ||
                    string.IsNullOrWhiteSpace(smtpPort) ||
                    string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(password) ||
                    string.IsNullOrWhiteSpace(fromEmail))
                {
                    _logger.LogError("SMTP settings are incomplete. Notification email was not sent to {Email}", email);
                    return false;
                }

                var mimeMessage = new MimeMessage();
                mimeMessage.From.Add(new MailboxAddress(fromName, fromEmail));
                mimeMessage.To.Add(new MailboxAddress("User", email));
                mimeMessage.Subject = subject;
                mimeMessage.Body = new TextPart("html") { Text = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px;'>
                        <h2 style='color: #1a472a;'>{subject}</h2>
                        <p>{message}</p>
                        <hr>
                        <p style='color: #666; font-size: 12px;'>SAPS Digital Docket Management System</p>
                    </div>" };

                using var client = new SmtpClient();
                client.Timeout = 30000;
                await client.ConnectAsync(smtpServer, int.Parse(smtpPort), SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(mimeMessage);
                await client.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send notification email to {email}");
                return false;
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string resetLink)
        {
            try
            {
                var smtpServer = GetSetting("Email:SmtpServer", "SmtpSettings:Host");
                var smtpPort = GetSetting("Email:SmtpPort", "SmtpSettings:Port");
                var username = GetSetting("Email:Username", "SmtpSettings:Username");
                var password = GetSetting("Email:Password", "SmtpSettings:Password");
                var fromName = GetSetting("Email:FromName", "SmtpSettings:FromName") ?? "SAPS DDMS";
                var fromEmail = GetSetting("Email:FromEmail", "SmtpSettings:FromEmail") ?? username;

                if (string.IsNullOrWhiteSpace(smtpServer) ||
                    string.IsNullOrWhiteSpace(smtpPort) ||
                    string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(password) ||
                    string.IsNullOrWhiteSpace(fromEmail))
                {
                    _logger.LogError("SMTP settings are incomplete. Password reset email was not sent to {Email}", email);
                    return false;
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(new MailboxAddress("User", email));
                message.Subject = "Reset Your SAPS DDMS Password";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 500px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #1a472a;'>Password Reset Request</h2>
                        <p>We received a request to reset your password. Click the button below to create a new password:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{resetLink}' style='background-color: #1a472a; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px;'>Reset Password</a>
                        </div>
                        <p>This link expires according to the administrator-configured password reset policy.</p>
                        <p>If you did not request this, please ignore this email.</p>
                    </div>";

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                client.Timeout = 30000;
                await client.ConnectAsync(smtpServer, int.Parse(smtpPort), SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send password reset email to {email}");
                return false;
            }
        }

        public async Task<bool> SendPasswordResetOtpEmailAsync(string email, string fullName, string otpCode, int expiryMinutes)
        {
            var safeName = System.Net.WebUtility.HtmlEncode(fullName);
            var safeOtp = System.Net.WebUtility.HtmlEncode(otpCode);
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 500px; margin: 0 auto; padding: 20px;'>
                    <h2 style='color: #1a472a;'>Password reset one-time PIN</h2>
                    <p>Hello {safeName},</p>
                    <p>Use this one-time PIN to reset your SAPS DDMS password:</p>
                    <p style='font-size: 30px; font-weight: bold; letter-spacing: 8px; text-align: center;'>{safeOtp}</p>
                    <p>This PIN expires in <strong>{expiryMinutes} minutes</strong> and can only be used once.</p>
                    <p>If you did not request a password reset, ignore this email and keep your password private.</p>
                </div>";

            return await SendNotificationEmailAsync(email, "Your SAPS DDMS password reset PIN", body);
        }

        private string? GetSetting(params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = _configuration[key];
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }
    }
}
