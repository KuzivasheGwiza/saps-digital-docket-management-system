using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SAPS.DDMS.API.Services
{
    public interface ISmsService
    {
        Task<bool> SendSmsAsync(int? userId, string? recipientNumber, string message, string purpose);
        Task<bool> SendOtpAsync(User user, string otpCode);
    }

    public class SmsService : ISmsService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SmsService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public SmsService(
            ApplicationDbContext db,
            ILogger<SmsService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<bool> SendSmsAsync(int? userId, string? recipientNumber, string message, string purpose)
        {
            var hasNumber = !string.IsNullOrWhiteSpace(recipientNumber);
            var mode = _configuration["SmsGateway:Mode"] ?? "LocalQueue";
            var sms = new SmsMessage
            {
                FK_UserID = userId,
                RecipientNumber = recipientNumber?.Trim() ?? string.Empty,
                Message = message,
                Purpose = purpose,
                Status = hasNumber ? "Queued - Local Approval" : "Not Queued - Missing Number",
                ProviderReference = hasNumber ? $"SMS-{Guid.NewGuid():N}"[..18] : null
            };

            await _db.SmsMessages.AddAsync(sms);
            await _db.SaveChangesAsync();

            if (hasNumber && mode.Equals("HttpGateway", StringComparison.OrdinalIgnoreCase))
                await TrySendViaGatewayAsync(sms);

            if (userId.HasValue)
            {
                await _db.Notifications.AddAsync(new Notification
                {
                    FK_UserID = userId.Value,
                    Title = purpose,
                    Message = BuildNotificationMessage(sms.Status),
                    PriorityLevel = "Medium",
                    IsSMSSent = sms.SentAt.HasValue,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("SMS {Purpose} processed for user {UserId}. Status={Status}", purpose, userId, sms.Status);
            return sms.SentAt.HasValue || sms.Status.StartsWith("Queued", StringComparison.OrdinalIgnoreCase);
        }

        public Task<bool> SendOtpAsync(User user, string otpCode)
        {
            var message = $"SAPS DDMS one-time PIN: {otpCode}. It expires shortly. Do not share this code.";
            return SendSmsAsync(user.UserID, user.PhoneNumber, message, "Login OTP");
        }

        private async Task TrySendViaGatewayAsync(SmsMessage sms)
        {
            var apiUrl = _configuration["SmsGateway:ApiUrl"];
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                sms.Status = "Queued - Gateway Not Configured";
                return;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var apiKey = _configuration["SmsGateway:ApiKey"];
                if (!string.IsNullOrWhiteSpace(apiKey))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await client.PostAsJsonAsync(apiUrl, new
                {
                    to = sms.RecipientNumber,
                    message = sms.Message,
                    senderId = _configuration["SmsGateway:SenderId"] ?? "SAPS-DDMS",
                    reference = sms.ProviderReference
                });

                if (response.IsSuccessStatusCode)
                {
                    sms.Status = "Sent - Gateway";
                    sms.SentAt = DateTime.UtcNow;
                }
                else
                {
                    sms.Status = $"Queued - Gateway Error {(int)response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                sms.Status = "Queued - Gateway Exception";
                _logger.LogWarning(ex, "SMS gateway failed for SmsMessageID {SmsMessageID}", sms.SmsMessageID);
            }
        }

        private static string BuildNotificationMessage(string status)
        {
            if (status.StartsWith("Sent", StringComparison.OrdinalIgnoreCase))
                return "SMS sent through the configured gateway.";
            if (status.Contains("Missing Number", StringComparison.OrdinalIgnoreCase))
                return "SMS could not be queued because no phone number is saved.";
            return "SMS queued for communications officer review and dispatch.";
        }
    }
}
