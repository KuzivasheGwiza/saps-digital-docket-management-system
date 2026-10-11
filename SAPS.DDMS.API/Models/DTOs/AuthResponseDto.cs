namespace SAPS.DDMS.API.Models.DTOs
{
    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public int SessionTimeoutMinutes { get; set; }
        public UserResponseDto? User { get; set; }
        public bool RequiresOtp { get; set; }
        public string? PendingPersonnelNumber { get; set; }
        public DateTime? OtpExpiresAt { get; set; }
        public string? DeliveryHint { get; set; }
    }
}
